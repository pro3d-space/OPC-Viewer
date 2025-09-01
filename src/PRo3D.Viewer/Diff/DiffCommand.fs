namespace PRo3D.Viewer

open Argu
open Aardvark.Base
open Aardvark.GeoSpatial.Opc
open System.Collections.Generic
open System.Diagnostics
open System.IO
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Diff
open PRo3D.Viewer.Shared

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

    let execute (config : DiffConfig) : int =

        let novalue = config.NoValue |> Option.defaultValue nan

        let verbose = config.Verbose |> Option.defaultValue false

        let sftpServerConfig = config.Sftp |> Option.map Sftp.parseFileZillaConfigFile

        let basedir =
            match config.BaseDir with
            | Some s -> s
            | None -> System.IO.Path.Combine(System.Environment.CurrentDirectory, "data")

        let datadirs = config.Data |> Array.toList

        let dataRefs = datadirs |> List.map Data.getDataRefFromString

        let resolve = Data.resolveDataPath basedir sftpServerConfig
        let resolvedResults = dataRefs |> List.map resolve

        // Check for empty data directories first
        if datadirs.IsEmpty then
            0
        else
        
        let datadirResults = 
            resolvedResults |> List.map (fun x ->
                match x with
                | ResolveDataPathResult.Ok ok -> Some ok
                | ResolveDataPathResult.MissingSftpConfig uri ->
                    printfn "Use --sftp|-s do specify SFTP config for %A" uri
                    None
                | ResolveDataPathResult.DownloadError (uri, e) ->
                    printfn "%A: %A" uri e
                    None
                | ResolveDataPathResult.InvalidDataDir s ->
                    printfn "invalid data dir: %A" s
                    None
            )
        
        if datadirResults |> List.exists Option.isNone then
            1
        else
        
        let datadirs = datadirResults |> List.choose id

        let layers = datadirs |> List.collect Data.searchLayerDir

        if layers.Length <> 2 then
            printfn "[ERROR] Please specify exactly 2 datasets to compare."
            printfn "[ERROR] You specified:"
            for layer in layers do
                printfn "[ERROR]   %s" layer.Path.FullName
            1
        else

        let layerMain = layers[0]
        let layerOther = layers[1]
            
        if verbose then
            printfn "computing difference between"
            printfn "  %s" layerMain.Path.FullName
            printfn "  %s" layerOther.Path.FullName
            printfn "novalue: %f" novalue
            printfn "verbose: true"

        let hierarchyMain = layerMain.LoadPatchHierarchy ()
        let hierarchyOther = layerOther.LoadPatchHierarchy ()

        let sky = Utils.getSky hierarchyMain
        let ground = Plane3d(sky, V3d.Zero)
        let w2p = ground.GetWorldToPlane()

        let trianglesMainWithNaN = Utils.getTriangles false hierarchyMain
        let trianglesMain = trianglesMainWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let trianglesOtherWithNaN = Utils.getTriangles false hierarchyOther
        let trianglesOther = trianglesOtherWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let sw = Stopwatch.StartNew()
        
        //do
        //    let groups =
        //        trianglesOther
        //        |> Seq.collect (fun t -> [(t.P0, t); (t.P1, t); (t.P2, t)])
        //        |> Seq.groupBy fst
        //        |> Seq.map (fun (k,v) -> (k, v |> Seq.map snd |> Seq.toList))
        //        |> Seq.sortByDescending (fun (k, v) -> v.Length)
        //        |> Seq.toList

        //    printfn "groups: %d" groups.Length
        //    for (k, ts) in groups do
        //        printfn "  group (%d entries) with key %A" ts.Length (k.Round(3))
        //        for t in ts do
        //            printfn "    %A | %A | %A" (t.P0.Round(3)) (t.P1.Round(3)) (t.P2.Round(3))

        //    exit 1
        //    ()

        let triangleTreeMain  = TriangleTree.build trianglesMain
        let triangleTreeOther = TriangleTree.build trianglesOther
        sw.Stop()
        printfn "building tree ......... %A" sw.Elapsed

        let pointsMain = Utils.getPoints true hierarchyMain
        let gbb = Box3d(pointsMain)
        let mutable i = 0
        let mutable countHits = 0
        
        sw.Restart()
        let rangeDist = Range1d.Invalid
        let rangeT = Range1d.Invalid
        let mutable qs = List.empty<(V3d*V3d*float)>
        for pGlobal in pointsMain do

            let ray = Ray3d(pGlobal, sky)
            let x = TriangleTree.getNearestIntersection triangleTreeOther ray
            
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
                //if i % 1000 = 0 then
                //    printfn "[%10d/%d][%d hits] hit dist %16.3f %16.3f" i pointsMain.Length countHits dist t
                ()
            | None ->
                //if i % 1000 = 0 then
                //    printfn "[%10d/%d][%d hits] no hit" i pointsMain.Length countHits
                ()
        sw.Stop()
        printfn "computing distances ... %A" sw.Elapsed

        printfn "%d hits / %d points" countHits pointsMain.Length
        printfn "range dist: %A" rangeDist
        printfn "range T   : %A" rangeT

        let p2c = Dictionary<V3d,C3b>()

        if false then // debug
            let outfile = @"E:\qs.pts"

            let max = max (abs rangeT.Min) (abs rangeT.Max)

            use f = new StreamWriter(outfile)
            for (pGlobal,p,t) in qs do
                let w = float32(t / max)
                let c =
                    if w < 0.0f then
                        let w = -w
                        C3b(C3f.Blue * w + C3f.White * (1.0f - w))
                    else
                        C3b(C3f.Red * w + C3f.White * (1.0f - w))

                p2c[pGlobal] <- c

                sprintf "%f %f %f %i %i %i" p.X p.Y p.Z c.R c.G c.B |> f.WriteLine

            printfn "exported diff point cloud to %s" outfile

            use fHisto = new StreamWriter(outfile + ".csv")
            let histo = qs |> Seq.groupBy (fun (_,_,t) -> int(t*1000.0)) |> Seq.map (fun (key, xs) -> (key, xs |> Seq.length)) |> Seq.sortBy (fun (k,_) -> k) |> Seq.toList
            for (k,count) in histo do
                let line = sprintf "%i,%i" k count
                //printfn "%s" line
                fHisto.WriteLine line

            ()

        
 

        // create OpcScene ...
        let initialCam = Utils.createInitialCameraView gbb
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
            | DistanceComputationMode.Nearest -> C3b.Red
            | _ -> 
                match (false, C3b.White) (*p2c.TryGetValue(p)*) with
                | (true, c) when false -> 
                    //printfn "haha %A" c
                    c
                | _ ->

                    let ray = Ray3d(p, sky)
                    let x = TriangleTree.getNearestIntersection triangleTreeOther ray
                    match x with
                    | Some (dist, t) ->
                        //printfn "%A" t
                        let w = float32(t / max)
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
        let backgroundColor = 
            match config.BackgroundColor with
            | Some colorStr ->
                match Utils.parseBackgroundColor colorStr with
                | Result.Ok color -> color
                | Result.Error msg ->
                    printfn "[WARNING] Invalid background color '%s': %s. Using default black." colorStr msg
                    C4f.Black
            | None -> C4f.Black

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
        }
        
        UnifiedViewer.run viewerConfig |> ignore
        0

    let run (args : ParseResults<Args>) (globalScreenshots: string option) : int =
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
        }
        execute config
