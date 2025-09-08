namespace PRo3D.Viewer

open Argu
open Aardvark.Data.Remote
open PRo3D.Viewer.Configuration

[<AutoOpen>]
module ListCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | [<Unique;AltCommandLine("-s") >] Stats

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"
                | Stats     -> "show detail info"

    let execute (config: ListConfig) : int =
        // Check if any data sources are provided
        if config.Data.Length = 0 then
            printfn "[WARNING] no data directories specified"
            0
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
        let baseDir = config.BaseDir |> Option.defaultValue "./tmp/data"
        let forceDownload = config.ForceDownload |> Option.defaultValue false
        
        // Log configuration if verbose
        match logger with
        | Some log ->
            log Logger.LogLevel.Info "List configuration:"
            log Logger.LogLevel.Info (sprintf "  Data sources: %d" config.Data.Length)
            log Logger.LogLevel.Info (sprintf "  Stats: %A" config.Stats)
            log Logger.LogLevel.Info (sprintf "  Base directory: %s" baseDir)
            log Logger.LogLevel.Info (sprintf "  Force download: %b" forceDownload)
            match sftpConfig with
            | Some sftp -> log Logger.LogLevel.Info (sprintf "  SFTP: %s@%s:%d" sftp.User sftp.Host sftp.Port)
            | None -> ()
        | None -> ()
        
        // Parse data references from config
        let dataRefs = 
            config.Data 
            |> Array.map getDataRefFromString
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
            // Search for layers in resolved directories (use existing logic)
            let layerInfos =
                Data.searchLayerDirs dataDirs
                |> List.sortBy (fun x -> x.Path.FullName)

            let showStats = config.Stats |> Option.defaultValue false

            for info in layerInfos do
                printfn "%s" info.Path.FullName
                if showStats then Utils.printLayerInfo info

            0

    let run (args : ParseResults<Args>) : int =
        // Simple conversion without ConfigurationBuilder dependency
        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x |> List.toArray
            | None -> [||]
        
        let stats = if args.Contains Args.Stats then Some true else None
        
        let config = {
            Data = datadirs
            Stats = stats
            Sftp = None
            BaseDir = None
            ForceDownload = None
            Verbose = None
        }
        
        execute config