namespace PRo3D.Viewer

open Argu
open System.IO
open Aardvark.Base
open Aardvark.Data.Remote
open PRo3D.Viewer.Configuration

[<AutoOpen>]
module ExportCommand =

    type ExportFormat = Pts | Ply

    type Args =
        | [<MainCommand>] DataDir of datadir : string
        | [<Mandatory>] Format of ExportFormat
        | [<Mandatory>] Out of outfile : string

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDir _ -> "specify data directory"
                | Format  _ -> "specify export format"
                | Out _     -> "specify export file name"

    let execute (config: ExportConfig) : int =
        // Check if any data sources are provided
        if config.Data.Length = 0 then
            printfn "[ERROR] No data sources provided for export"
            1
        else
        
        // Setup verbose logging if requested
        let logger = 
            if config.Verbose = Some true then
                Some (fun level msg -> 
                    let prefix = 
                        match level with
                        | Logger.LogLevel.Debug -> "[DEBUG]"
                        | Logger.LogLevel.Info -> "[INFO]"
                        | Logger.LogLevel.Warning -> "[WARN]"
                        | Logger.LogLevel.Error -> "[ERROR]"
                    printfn "%s %s" prefix msg)
            else
                None
        
        // Load SFTP configuration if provided
        let sftpConfig = 
            config.Sftp
            |> Option.bind FileZillaConfig.tryParseFile
        
        // Setup base directory
        let baseDir = config.BaseDir |> Option.defaultValue "./data"
        let forceDownload = config.ForceDownload |> Option.defaultValue false
        
        // Log configuration if verbose
        match logger with
        | Some log ->
            log Logger.LogLevel.Info "Export configuration:"
            log Logger.LogLevel.Info (sprintf "  Data sources: %d" config.Data.Length)
            log Logger.LogLevel.Info (sprintf "  Format: %A" config.Format)
            log Logger.LogLevel.Info (sprintf "  Output: %s" (config.OutFile |> Option.defaultValue "(stdout)"))
            log Logger.LogLevel.Info (sprintf "  Base directory: %s" baseDir)
            log Logger.LogLevel.Info (sprintf "  Force download: %b" forceDownload)
            match sftpConfig with
            | Some sftp -> log Logger.LogLevel.Info (sprintf "  SFTP: %s@%s:%d" sftp.User sftp.Host sftp.Port)
            | None -> ()
        | None -> ()
        
        // Parse data references from config
        let dataRefs = 
            config.Data 
            |> Array.map (fun entry -> getDataRefFromString entry.Path)
            |> Array.toList
        
        // Log data references if verbose
        match logger with
        | Some log ->
            log Logger.LogLevel.Info "Resolving data references:"
            dataRefs |> List.iter (fun ref -> log Logger.LogLevel.Info (sprintf "  %A" ref))
        | None -> ()
        
        // Resolve all data paths (download if necessary)
        let resolvedResults = 
            dataRefs |> List.map (resolveDataPath baseDir sftpConfig forceDownload logger)
        
        // Handle resolve results
        let handleResolveResults (results: ResolveDataPathResult list) =
            let datadirResults = 
                results |> List.map (fun x ->
                    match x with
                    | ResolveDataPathResult.Ok ok -> Some ok
                    | ResolveDataPathResult.MissingSftpConfig uri ->
                        printfn "Use --sftp|-s to specify SFTP config for %A" uri
                        None
                    | ResolveDataPathResult.DownloadError (uri, e) ->
                        printfn "%A: %A" uri e
                        None
                    | ResolveDataPathResult.InvalidDataDir s ->
                        printfn "invalid data dir: %A" s
                        None
                )
            
            if datadirResults |> List.exists Option.isNone then
                None
            else
                Some (datadirResults |> List.choose id)
        
        match handleResolveResults resolvedResults with
        | None -> 
            printfn "[ERROR] Failed to resolve one or more data paths"
            1
        | Some dataDirs ->
            // Search for layers in resolved directories
            let layers = searchLayerDirs dataDirs |> List.sortBy (fun x -> x.Path.FullName)
            
            if layers.IsEmpty then
                printfn "[ERROR] No OPC layers found in resolved data paths"
                1
            else
            
            match logger with
            | Some log -> log Logger.LogLevel.Info (sprintf "Found %d layers" layers.Length)
            | None -> ()
            
            // Get output file name or use default
            let mutable outfile = config.OutFile |> Option.defaultValue "export.pts"
            
            // Ensure correct extension
            let ensureExtension (ext : string) (path : string) : string =
                if (not (Path.HasExtension(path))) || Path.GetExtension(path) <> ext then
                    sprintf "%s%s" (Path.GetFileNameWithoutExtension(path)) ext
                else
                    path
            
            // Export based on format
            match config.Format with
            | Configuration.ExportFormat.Pts ->
                outfile <- ensureExtension ".pts" outfile
                
                match logger with
                | Some log -> log Logger.LogLevel.Info (sprintf "Exporting to PTS format: %s" outfile)
                | None -> ()
                
                let mutable totalPointCount = 0
                use f = new StreamWriter(outfile)
                
                for layer in layers do
                    let ps = layer.GetPoints true
                    totalPointCount <- totalPointCount + ps.Length
                    sprintf "%d" ps.Length |> f.WriteLine
                    for p in ps do sprintf "%f %f %f" p.X p.Y p.Z |> f.WriteLine
                
                printfn "wrote %d points to %s" totalPointCount outfile
            
            | Configuration.ExportFormat.Ply ->
                outfile <- ensureExtension ".ply" outfile
                
                match logger with
                | Some log -> log Logger.LogLevel.Info (sprintf "Exporting to PLY format: %s" outfile)
                | None -> ()
                
                let mutable totalPointCount = 0
                let mutable totalTriangleCount = 0
                let points = ResizeArray<V3d>()
                let triangles = ResizeArray<Triangle3d>()
                
                // Collect all points and triangles from all layers
                for layer in layers do
                    let layerPoints = layer.GetPoints true
                    let layerTriangles = layer.GetTriangles(true)
                    
                    // Add points
                    for p in layerPoints do
                        points.Add(p)
                    
                    // Add triangles (storing actual triangle vertices)
                    for t in layerTriangles do
                        triangles.Add(t)
                    
                    totalPointCount <- totalPointCount + layerPoints.Length
                    totalTriangleCount <- totalTriangleCount + layerTriangles.Length
                
                // Write PLY file with triangles as separate vertices
                use f = new StreamWriter(outfile)
                
                // PLY header - write triangles as individual vertices (3 per triangle)
                f.WriteLine("ply")
                f.WriteLine("format ascii 1.0")
                f.WriteLine(sprintf "element vertex %d" (totalTriangleCount * 3))
                f.WriteLine("property float x")
                f.WriteLine("property float y")
                f.WriteLine("property float z")
                f.WriteLine(sprintf "element face %d" totalTriangleCount)
                f.WriteLine("property list uchar int vertex_indices")
                f.WriteLine("end_header")
                
                // Write vertices from triangles
                for t in triangles do
                    f.WriteLine(sprintf "%f %f %f" t.P0.X t.P0.Y t.P0.Z)
                    f.WriteLine(sprintf "%f %f %f" t.P1.X t.P1.Y t.P1.Z)
                    f.WriteLine(sprintf "%f %f %f" t.P2.X t.P2.Y t.P2.Z)
                
                // Write faces with correct indices
                let mutable idx = 0
                for _ in triangles do
                    f.WriteLine(sprintf "3 %d %d %d" idx (idx + 1) (idx + 2))
                    idx <- idx + 3
                
                printfn "wrote %d triangles (%d vertices) to %s" totalTriangleCount (totalTriangleCount * 3) outfile
            
            0

    let run (args : ParseResults<Args>) : int =

        let datadir = args.GetResult Args.DataDir |> DataDir.ofString
        let format  = args.GetResult Args.Format
        
        let mutable outfile = args.GetResult Args.Out

        // discover all layers in datadirs ...
        let layers =
            Data.searchLayerDir datadir
            |> List.sortBy (fun x -> x.Path.FullName)

        let ensureExtension (ext : string) (path : string) : string =
            if (not (Path.HasExtension(outfile))) || Path.GetExtension(outfile) <> ext then
                sprintf "%s.pts" (Path.GetFileNameWithoutExtension(path))
            else
                path

        match format with

        | ExportFormat.Pts ->
            outfile <- ensureExtension ".pts" outfile

            let mutable totalPointCount = 0
            use f = new StreamWriter(outfile)

            for layer in layers do
                let ps = layer.GetPoints true
                totalPointCount <- totalPointCount + ps.Length
                sprintf "%d" ps.Length |> f.WriteLine
                for p in ps do sprintf "%f %f %f" p.X p.Y p.Z |> f.WriteLine

            printfn "wrote %d points to %s" totalPointCount outfile

        | ExportFormat.Ply ->
            outfile <- ensureExtension ".ply" outfile
            
            let mutable totalPointCount = 0
            let mutable totalTriangleCount = 0
            let points = ResizeArray<V3d>()
            let triangles = ResizeArray<Triangle3d>()
            
            // Collect all points and triangles from all layers
            for layer in layers do
                let layerPoints = layer.GetPoints true
                let layerTriangles = layer.GetTriangles(true)
                
                // Add points
                for p in layerPoints do
                    points.Add(p)
                
                // Add triangles (storing actual triangle vertices)
                for t in layerTriangles do
                    triangles.Add(t)
                
                totalPointCount <- totalPointCount + layerPoints.Length
                totalTriangleCount <- totalTriangleCount + layerTriangles.Length
            
            // Write PLY file with triangles as separate vertices
            use f = new StreamWriter(outfile)
            
            // PLY header - write triangles as individual vertices (3 per triangle)
            f.WriteLine("ply")
            f.WriteLine("format ascii 1.0")
            f.WriteLine(sprintf "element vertex %d" (totalTriangleCount * 3))
            f.WriteLine("property float x")
            f.WriteLine("property float y")
            f.WriteLine("property float z")
            f.WriteLine(sprintf "element face %d" totalTriangleCount)
            f.WriteLine("property list uchar int vertex_indices")
            f.WriteLine("end_header")
            
            // Write vertices from triangles
            for t in triangles do
                f.WriteLine(sprintf "%f %f %f" t.P0.X t.P0.Y t.P0.Z)
                f.WriteLine(sprintf "%f %f %f" t.P1.X t.P1.Y t.P1.Z)
                f.WriteLine(sprintf "%f %f %f" t.P2.X t.P2.Y t.P2.Z)
            
            // Write faces with correct indices
            let mutable idx = 0
            for _ in triangles do
                f.WriteLine(sprintf "3 %d %d %d" idx (idx + 1) (idx + 2))
                idx <- idx + 3
            
            printfn "wrote %d triangles (%d vertices) to %s" totalTriangleCount (totalTriangleCount * 3) outfile

        0