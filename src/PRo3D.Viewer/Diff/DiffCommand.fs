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
        //let w2p = ground.GetWorldToPlane()

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
        let pointsOther = Utils.getPoints true hierarchyOther
        let gbb = Box3d(pointsMain)
        sw.Restart()

        let signedSkyDist (intersectWith : ITriangleSet) (pGlobal : V3d) : float option =
            let up = Ray3d(pGlobal, sky)
            let hitUp = intersectWith.IntersectRay(&up)
            if hitUp.HasIntersection then
                Some hitUp.T
            else
                let down = Ray3d(pGlobal, -sky)
                let hitDown = intersectWith.IntersectRay(&down)
                if hitDown.HasIntersection then
                    Some -hitDown.T
                else
                    None

        // pre-process global ranges and absolute distances
        let rangeT0       = Range1d.Invalid  // sky dist    : signed t range
        let rangeT1       = Range1d.Invalid  // sky dist    : signed t range
        let rangeNearest0 = Range1d.Invalid  // nearest dist: absolute distance range
        let rangeNearest1 = Range1d.Invalid  // nearest dist: absolute distance range

        // distance ranges: main -> other
        for pGlobal in pointsMain do

            // compute closest point distance
            do
                let x = triangleTreeOther.GetClosestPoint(&pGlobal)
                let d = sqrt x.DistanceSquared
                rangeNearest0.ExtendBy(d)

            // compute sky intersection distance
            do
                match signedSkyDist triangleTreeOther pGlobal with
                | Some t ->
                    //countHits <- countHits + 1
                    //let g = ray.Intersect(ground)
                    //let p = g + sky * t
                    //let p' = w2p.TransformPos p
                    //rangeDist.ExtendBy(abs t)
                    rangeT0.ExtendBy(t)
                | None ->
                    ()

            ()

        // distance ranges: other -> main
        for pGlobal in pointsOther do

            // compute closest point distance
            do
                let x = triangleTreeMain.GetClosestPoint(&pGlobal)
                rangeNearest1.ExtendBy(sqrt x.DistanceSquared)

            // compute sky intersection distance
            do
                match signedSkyDist triangleTreeMain pGlobal with
                | Some t -> rangeT1.ExtendBy(t)
                | None -> ()

            ()

        sw.Stop()
        printfn "computing distances ... %A" sw.Elapsed

        
        let max0 = max (abs rangeT0.Min) (abs rangeT0.Max)
        let max1 = max (abs rangeT1.Min) (abs rangeT1.Max)

        printfn "range T0       : %A (abs max is %f)" rangeT0 max0
        printfn "range T1       : %A (abs max is %f)" rangeT1 max1
        printfn "range nearest0 : %A" rangeNearest0
        printfn "range nearest1 : %A" rangeNearest1

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
        
        let getColor (distMode : DistanceComputationMode) (toggleMode : DiffToggleMode) (p : V3d) : C3b =

            match distMode, toggleMode with

            | DistanceComputationMode.Nearest, DiffToggleMode.First ->
                let x = triangleTreeOther.GetClosestPoint(&p)
                let d = sqrt x.DistanceSquared
                let w = System.Math.Pow(d / rangeNearest0.Max, 0.25) |> float32
                C3b(C3f.Green * (1.0f - w) + C3f.Red * w)
                
            | DistanceComputationMode.Nearest, DiffToggleMode.Second ->
                let x = triangleTreeMain.GetClosestPoint(&p)
                let d = sqrt x.DistanceSquared
                let w = System.Math.Pow(d / rangeNearest1.Max, 0.25) |> float32
                C3b(C3f.Green * (1.0f - w) + C3f.Red * w)

            | DistanceComputationMode.Sky, DiffToggleMode.First ->
                match signedSkyDist triangleTreeOther p with
                | Some t ->
                    let w = float32(t / max0)
                    let c =
                        if w < 0.0f then
                            let w = -w
                            C3b(C3f.Blue * w + C3f.White * (1.0f - w))
                        else
                            C3b(C3f.Red * w + C3f.White * (1.0f - w))
                    c
                | None ->
                    C3b.GreenYellow

            | DistanceComputationMode.Sky, DiffToggleMode.Second ->
                match signedSkyDist triangleTreeMain p with
                | Some t ->
                    let w = float32(t / max1)
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

        // parse background color if provided
        let backgroundColor = parseBackgroundColor config.BackgroundColor

        // ... and show it using the unified viewer
        let viewerConfig : ViewerConfig = {
            mode = ViewerMode.DiffMode {
                env = env
                initialToggleMode = Shared.DiffToggleMode.First
            }
            scene = scene
            sky = sky
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
