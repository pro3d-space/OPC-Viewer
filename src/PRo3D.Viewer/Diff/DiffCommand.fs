namespace PRo3D.Viewer

open Argu
open Aardvark.Base
open Aardvark.GeoSpatial.Opc
open System.Collections.Generic
open Uncodium.Geometry.TriangleSet
open System.Diagnostics
open System.IO
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Diff
open PRo3D.Viewer.Shared
open PRo3D.Viewer.Shared.CommandUtils
open Aardvark.Data.Remote

[<AutoOpen>]
module DiffCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | [<Unique                      >] NoValue of float
        | [<Unique                      >] Speed   of float
        | [<Unique;AltCommandLine("-v") >] Verbose
        | [<AltCommandLine("-s")        >] Sftp of string
        | [<AltCommandLine("-b")        >] BaseDir of string
        | [<CustomCommandLine("--background-color"); AltCommandLine("--bg")>] BackgroundColor of string
        | [<CustomCommandLine("--force-download"); AltCommandLine("-f")>] ForceDownload
        | [<CustomCommandLine("--embree")  >] UseEmbree

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "Two datasets to compare."
                | NoValue  _ -> "value used if no difference can be computed. Optional. Default is nan."
                | Speed    _ -> "optional camera controller speed"
                | Verbose    -> "print more detailed info."
                | Sftp     _ -> "optional SFTP server config file (FileZilla format)"
                | BaseDir  _ -> "optional base directory for relative paths (default is ./data)"
                | BackgroundColor _ -> "optional background color (hex: #RGB/#RRGGBB, named: black/white/red/etc, RGB: r,g,b)"
                | ForceDownload -> "force re-download of remote data even if cached"
                | UseEmbree -> "use Embree backend for triangle intersection (Windows only)"

    let execute (config : DiffConfig) : int =

        let novalue = config.NoValue |> Option.defaultValue nan

        let verbose = config.Verbose |> Option.defaultValue false

        let sftpServerConfig = parseSftpConfig config.Sftp

        let basedir = resolveBaseDirectory config.BaseDir

        let datadirs = config.Data |> Array.toList

        let dataRefs = datadirs |> List.map Data.getDataRefFromString

        let forceDownload = config.ForceDownload |> Option.defaultValue false
        
        // Create logger from verbose flag
        let logger = 
            match config.Verbose |> Option.defaultValue false with
            | true -> Some (Logger.console Logger.Info)
            | false -> None
            
        let resolvedResults = resolveDataPaths basedir sftpServerConfig forceDownload logger dataRefs

        // Check for empty data directories first
        if datadirs.IsEmpty then
            0
        else
        
        match handleResolveResults resolvedResults with
        | None -> 1
        | Some datadirs ->

        let layersByDataDir = datadirs |> List.map Data.searchLayerDir

        // Check that we have exactly 2 data directories
        if datadirs.Length <> 2 then
            printfn "[ERROR] Please specify exactly 2 datasets to compare."
            printfn "[ERROR] You specified %d data directories" datadirs.Length
            1
        // Check that each directory contains at least one layer
        elif layersByDataDir |> List.exists List.isEmpty then
            printfn "[ERROR] One or more data directories contain no OPC layers."
            for (datadir, layers) in List.zip datadirs layersByDataDir do
                let (Data.DataDir path) = datadir
                if layers.IsEmpty then
                    printfn "[ERROR]   No layers found in: %s" path
            1
        else

        // Take the first layer from each directory for comparison
        let layerMain = layersByDataDir.[0].[0]
        let layerOther = layersByDataDir.[1].[0]
        
        // Log what we're comparing if verbose
        if verbose then
            printfn "Found layers:"
            for (i, (datadir, layers)) in List.zip datadirs layersByDataDir |> List.indexed do
                let (Data.DataDir path) = datadir
                printfn "  Directory %d (%s): %d layers" i path layers.Length
                for layer in layers do
                    printfn "    - %s" layer.Path.FullName
            printfn ""
            printfn "Computing difference between:"
            printfn "  Main:  %s" layerMain.Path.FullName  
            printfn "  Other: %s" layerOther.Path.FullName
            printfn "novalue: %f" novalue

        let hierarchyMain = layerMain.LoadPatchHierarchy ()
        let hierarchyOther = layerOther.LoadPatchHierarchy ()

        let sky = Utils.getSky hierarchyMain
        let ground = Plane3d(sky, V3d.Zero)
        let w2p = ground.GetWorldToPlane()

        let trianglesMainWithNaN = Utils.getTriangles false hierarchyMain
        let trianglesMain = trianglesMainWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let trianglesOtherWithNaN = Utils.getTriangles false hierarchyOther
        let trianglesOther = trianglesOtherWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        ///////////////////////////////////////////////////////////////////////
        // build TriangleSet3d for both datasets
        // (for fast intersection and closest point queries)
        ///////////////////////////////////////////////////////////////////////
        printfn "building triangle sets ........."
        let sw = Stopwatch.StartNew()
        let backend = if config.UseEmbree |> Option.defaultValue false then Backend.Embree else Backend.Default
        let options = Options(Backend = backend)
        let triangleTreeMain  = TriangleSet.Build(trianglesMain, options = options)
        let triangleTreeOther = TriangleSet.Build(trianglesOther, options = options)
        sw.Stop()
        printfn "building triangle sets ......... %A" sw.Elapsed

        let pointsMain = Utils.getPoints true hierarchyMain
        let gbb = Box3d(pointsMain)
        let mutable i = 0
        let mutable countHits = 0
        
        sw.Restart()
        let rangeDist = Range1d.Invalid
        let rangeT = Range1d.Invalid
        let mutable qs = List.empty<(V3d*V3d*float)>

        let rangeNearest = Range1d.Invalid
        for pGlobal in pointsMain do

            do
                let x = triangleTreeOther.GetClosestPoint(&pGlobal)
                let d = sqrt x.DistanceSquared
                rangeNearest.ExtendBy(d)

            let ray = Ray3d(pGlobal, sky)
            let hit = triangleTreeOther.IntersectRay(&ray)
            let x = if hit.HasIntersection then Some(abs hit.T, hit.T) else None
            
            i <- i + 1

            match x with
            | Some (dist, t) ->
                countHits <- countHits + 1
                let g = ray.Intersect(ground)
                let p = g + sky * t
                let p' = w2p.TransformPos p
                
                qs <- (pGlobal, p', t) :: qs
                rangeDist.ExtendBy(dist)
                rangeT.ExtendBy(t)
                ()
            | None ->
                ()
        sw.Stop()
        printfn "computing distances ... %A" sw.Elapsed

        printfn "%d hits / %d points" countHits pointsMain.Length
        printfn "range dist    : %A" rangeDist
        printfn "range T       : %A" rangeT
        printfn "range nearest : %A" rangeNearest

        let p2c = Dictionary<V3d,C3b>()

        //if false then // debug
        //    let outfile = @"E:\qs.pts"

        //    let max = max (abs rangeT.Min) (abs rangeT.Max)

        //    use f = new StreamWriter(outfile)
        //    for (pGlobal,p,t) in qs do
        //        let w = float32(t / max)
        //        let c =
        //            if w < 0.0f then
        //                let w = -w
        //                C3b(C3f.Blue * w + C3f.White * (1.0f - w))
        //            else
        //                C3b(C3f.Red * w + C3f.White * (1.0f - w))

        //        p2c[pGlobal] <- c

        //        sprintf "%f %f %f %i %i %i" p.X p.Y p.Z c.R c.G c.B |> f.WriteLine

        //    printfn "exported diff point cloud to %s" outfile

        //    use fHisto = new StreamWriter(outfile + ".csv")
        //    let histo = qs |> Seq.groupBy (fun (_,_,t) -> int(t*1000.0)) |> Seq.map (fun (key, xs) -> (key, xs |> Seq.length)) |> Seq.sortBy (fun (k,_) -> k) |> Seq.toList
        //    for (k,count) in histo do
        //        let line = sprintf "%i,%i" k count
        //        //printfn "%s" line
        //        fHisto.WriteLine line

        //    ()

        
 

        // create OpcScene ...
        // Use robust camera calculation to handle degenerate triangles/outliers
        let outlierPercentile = config.CameraOutlierPercentile |> Option.defaultValue 2.5
        let initialCam = Utils.createInitialCameraViewRobust pointsMain outlierPercentile
        let speed = config.Speed |> Option.defaultValue (initialCam.Far / 64.0)
        let scene =
            { 
                useCompressedTextures = true
                preTransform     = Trafo3d.Identity
                patchHierarchies = Seq.delay (fun _ -> [layerMain; layerOther] |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = initialCam.Near
                far              = initialCam.Far
                speed            = speed
                lodDecider       = LodDecider.lodDeciderMars Trafo3d.Identity //DefaultMetrics.mars2 
            }

        // ... and show it
        
        let max = max (abs rangeT.Min) (abs rangeT.Max)
        let getColor (mode : DistanceComputationMode) (p : V3d) : C3b =

            match mode with
            | DistanceComputationMode.Nearest ->
                let x = triangleTreeOther.GetClosestPoint(&p)
                let d = sqrt x.DistanceSquared
                let w = System.Math.Pow(System.Math.Min(rangeNearest.Max, d) / rangeNearest.Max, 0.25) |> float32
                C3b(C3f.White * (1.0f - w) + C3f.Red * w)

            | DistanceComputationMode.Sky -> 
                match (false, C3b.White) (*p2c.TryGetValue(p)*) with
                | (true, c) when false -> 
                    //printfn "haha %A" c
                    c
                | _ ->

                    let ray = Ray3d(p, sky)
                    let hit = triangleTreeOther.IntersectRay(&ray)
                    let x = if hit.HasIntersection then Some(abs hit.T, hit.T) else None
                    match x with
                    | Some (dist, t) ->
                        //printfn "%A" t
                        let w = float32(t / max)
                        //let w = System.Math.Pow(float w0, 0.5) |> float32
                        let c =
                            if w < 0.0f then
                                let w = -w
                                C3b(C3f.Blue * w + C3f.White * (1.0f - w))
                            else
                                C3b(C3f.Red * w + C3f.White * (1.0f - w))
                        c
                    | None ->
                        C3b.GreenYellow

        let env = {
            Label0 = Path.GetFileName layerMain.Path.FullName
            Label1 = Path.GetFileName layerOther.Path.FullName
            Tree0 = triangleTreeMain
            Tree1 = triangleTreeOther
            GetColor = getColor
            Sky = sky
            }

        // Parse background color if provided
        let backgroundColor = parseBackgroundColor config.BackgroundColor

        // ... and show it using the unified viewer
        let viewerConfig : ViewerConfig = {
            mode = ViewerMode.DiffMode {
                env = env
                initialToggleMode = Shared.DiffToggleMode.First
            }
            scene = scene
            initialCameraView = initialCam.CameraView
            customKeyHandlers = Map.empty
            customMouseHandler = None
            enableTextOverlay = true
            textOverlayFunc = None
            backgroundColor = backgroundColor
            screenshotDirectory = config.Screenshots
            version = config.Version
        }
        
        UnifiedViewer.run viewerConfig |> ignore
        0

    let run (version: string) (args : ParseResults<Args>) (globalScreenshots: string option) : int =
        // Build DiffConfig from command-line arguments
        let config : DiffConfig = {
            Data =
                match args.TryGetResult Args.DataDirs with
                | Some dataDirs -> dataDirs |> Array.ofList
                | None ->
                    printfn "[WARNING] no data directories specified"
                    [||]
            NoValue = args.TryGetResult Args.NoValue
            Speed = args.TryGetResult Args.Speed
            Verbose = if args.Contains Args.Verbose then Some true else None
            Sftp = args.TryGetResult Args.Sftp
            BaseDir = args.TryGetResult Args.BaseDir
            BackgroundColor = args.TryGetResult Args.BackgroundColor
            Screenshots = globalScreenshots
            ForceDownload = if args.Contains Args.ForceDownload then Some true else None
            UseEmbree = if args.Contains Args.UseEmbree then Some true else None
            CameraOutlierPercentile = None  // Not supported in CLI, use project files
            Version = version
        }
        execute config
