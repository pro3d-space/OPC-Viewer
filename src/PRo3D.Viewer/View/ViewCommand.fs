namespace PRo3D.Viewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Argu
open PRo3D.Viewer
open PRo3D.Viewer.Data
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Shared
open PRo3D.Viewer.Shared.CommandUtils
open Aardvark.Data.Remote
open Aardvark.SceneGraph

open PRo3D.Viewer.Ribbon

[<AutoOpen>]
module ViewCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | Speed of float
        | [<AltCommandLine("-s") >] Sftp of string
        | [<AltCommandLine("-b") >] BaseDir of string
        | [<CustomCommandLine("--obj"); AltCommandLine("-o")>] ObjFiles of string list
        | [<CustomCommandLine("--background-color"); AltCommandLine("--bg")>] BackgroundColor of string
        | [<CustomCommandLine("--force-download"); AltCommandLine("-f")>] ForceDownload
        | [<Unique;AltCommandLine("-v") >] Verbose

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"
                | Speed    _ -> "optional camera controller speed"
                | Sftp     _ -> "optional SFTP server config file (FileZilla format)"
                | BaseDir  _ -> "optional base directory for relative paths (default is ./data)"
                | ObjFiles _ -> "optional OBJ files to load alongside OPC data"
                | BackgroundColor _ -> "optional background color (hex: #RGB/#RRGGBB, named: black/white/red/etc, RGB: r,g,b)"
                | ForceDownload -> "force re-download of remote data even if cached"
                | Verbose -> "print more detailed info."

    let execute (config : ViewConfig) : int =

        // Separate OBJ and OPC entries from unified data array
        let objEntries = config.Data |> Array.filter (fun e -> e.Type = Some DataType.Obj)
        let opcEntries = config.Data |> Array.filter (fun e -> e.Type = Some DataType.Opc)
        
        // process OBJ files first to determine if we need data directories
        let objFiles = objEntries |> Array.map (fun e -> e.Path) |> Array.toList
        let objTransforms = objEntries |> Array.map (fun e -> e.Transform) |> Array.toList
        printfn "[OBJ] Processing %d OBJ files..." objFiles.Length
        let validObjFiles = 
            objFiles 
            |> List.filter (fun path ->
                if System.IO.File.Exists path then 
                    printfn "[OBJ] Found OBJ file: %s" path
                    System.Console.Out.Flush()
                    true
                else 
                    printfn "[OBJ WARNING] OBJ file not found: %s" path
                    System.Console.Out.Flush()
                    false
            )

        printfn "[OBJ] Loaded %d valid OBJ files" validObjFiles.Length
        System.Console.Out.Flush()

        // compute bounds from OBJ files
        let objBounds = 
            if validObjFiles.Length > 0 then
                printfn "[OBJ] Computing bounds from OBJ files..."
                validObjFiles 
                |> List.choose Data.Wavefront.getObjFileBounds
                |> function
                    | [] -> 
                        printfn "[OBJ WARNING] Could not compute bounds from any OBJ files"
                        None
                    | boxes -> 
                        let combinedBox = Box3d boxes
                        printfn "[OBJ] Combined bounds: %A" combinedBox
                        Some combinedBox
            else
                None

        // load OBJ scene graphs for rendering
        let objScene = 
            if validObjFiles.Length > 0 then
                printfn "[OBJ] Loading OBJ models for rendering..."
                // Zip files with their transforms (or None if not enough transforms)
                let filesWithTransforms = 
                    List.zip validObjFiles 
                        (objTransforms @ List.replicate validObjFiles.Length None |> List.take validObjFiles.Length)
                
                filesWithTransforms
                |> List.map (fun (file, transform) ->
                    try
                        printfn "[OBJ] Loading model: %s" file
                        if transform.IsSome then
                            printfn "[OBJ] Applying transformation to: %s" file
                        let sg = Data.Wavefront.loadObjFileWithTransform file transform
                        printfn "[OBJ] Successfully loaded: %s" file
                        Some sg
                    with ex ->
                        printfn "[OBJ WARNING] Could not load model %s: %s" file ex.Message
                        None
                )
                |> List.choose id  // Filter out None values
            else
                []

        printfn "[OBJ] Loaded %d models for rendering" objScene.Length

        // handle data directories - only required if no valid OBJ files
        let datadirs = 
            if opcEntries.Length > 0 then
                opcEntries |> Array.map (fun e -> e.Path) |> Array.toList
            else
            if validObjFiles.Length = 0 then
                printfn "[ERROR] no data directories or OBJ files specified"
                []
            else
                printfn "[INFO] No data directories specified, loading OBJ files only"
                []

        // Early return if no data directories and no valid OBJ files
        if datadirs.IsEmpty && validObjFiles.IsEmpty then
            1
        else

        let dataRefs = datadirs |> List.map Data.getDataRefFromString

        let hasErrors = 
            dataRefs |> List.exists (fun x ->
                match x with
                | LocalDir(path, false) ->
                    printfn "[ERROR] directory does not exist: %s" path
                    true
                | Invalid path ->
                    printfn "[ERROR] invalid location: %s" path
                    true
                | _ -> false
            )
        
        if hasErrors then
            1
        else

        let basedir = resolveBaseDirectory config.BaseDir

        let sftpServerConfig = parseSftpConfig config.Sftp

        let forceDownload = config.ForceDownload |> Option.defaultValue false
        
        // Create logger from verbose flag
        let logger = 
            match config.Verbose |> Option.defaultValue false with
            | true -> Some (Logger.console Logger.Info)
            | false -> None
            
        let resolvedResults = resolveDataPaths basedir sftpServerConfig forceDownload logger dataRefs

        match handleResolveResults resolvedResults with
        | None -> 1
        | Some datadirs ->
            
        // discover all layers in datadirs (only if we have data directories) ...
        let layerInfos = 
            if datadirs.Length > 0 then
                Data.searchLayerDirs datadirs
            else
                []
        
        for x in layerInfos do
            printfn "found layer data in %s" x.Path.FullName

        // load patch hierarchies (only if we have layers) ...
        let patchHierarchies = 
            if layerInfos.Length > 0 then
                layerInfos
                |> Seq.toList 
                |> List.map Utils.loadPatchHierarchy
            else
                []

        // Parse a single .trafo file (JSON: [forwardMat, inverseMat]) -> Trafo3d option
        let parseTrafoFile (trafoFile : string) : Trafo3d option =
            try
                let json = System.IO.File.ReadAllText trafoFile
                use doc  = System.Text.Json.JsonDocument.Parse json
                let arr  = doc.RootElement
                let parseMat (el : System.Text.Json.JsonElement) =
                    let rows = el.EnumerateArray() |> Seq.toArray
                    M44d(
                        rows.[0].[0].GetDouble(), rows.[0].[1].GetDouble(), rows.[0].[2].GetDouble(), rows.[0].[3].GetDouble(),
                        rows.[1].[0].GetDouble(), rows.[1].[1].GetDouble(), rows.[1].[2].GetDouble(), rows.[1].[3].GetDouble(),
                        rows.[2].[0].GetDouble(), rows.[2].[1].GetDouble(), rows.[2].[2].GetDouble(), rows.[2].[3].GetDouble(),
                        rows.[3].[0].GetDouble(), rows.[3].[1].GetDouble(), rows.[3].[2].GetDouble(), rows.[3].[3].GetDouble()
                    )
                let forward = parseMat arr.[0]
                let inverse = parseMat arr.[1]
                printfn "[TRAFO] loaded .trafo file: %s" trafoFile
                Some (Trafo3d(forward, inverse))
            with ex ->
                printfn "[TRAFO] failed to load .trafo file: %s" ex.Message
                None

        // Try to find and load a .trafo file alongside the OPC dataset
        let loadTrafoFile (opcPath : string) : Trafo3d option =
            let dir =
                if System.IO.Directory.Exists opcPath then opcPath
                else System.IO.Path.GetDirectoryName opcPath
            System.IO.Directory.GetFiles(dir, "*.trafo")
            |> Array.tryHead
            |> Option.bind parseTrafoFile
        // Load a trafo per OPC entry — one trafo file per dataset folder
        let opcTrafos : Trafo3d[] =
            opcEntries |> Array.map (fun e ->
                match e.Transform with
                | Some m -> Trafo3d(m, m.Inverse)
                | None   ->
                    match loadTrafoFile e.Path with
                    | Some t -> t
                    | None   -> Trafo3d.Identity
            )

        // get root patch from each hierarchy
        let patches =
            patchHierarchies
            |> List.map (fun x -> match x.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n)

        // Pair each layerInfo with the trafo of the opcEntry whose path contains it
        let layerInfosWithTrafos : (LayerInfo * Trafo3d) list =
            layerInfos |> List.map (fun li ->
                let trafo =
                    opcEntries
                    |> Array.tryFindIndex (fun e ->
                        li.Path.FullName.StartsWith(
                            System.IO.Path.GetFullPath(e.Path),
                            System.StringComparison.OrdinalIgnoreCase))
                    |> Option.map (fun i -> opcTrafos.[i])
                    |> Option.defaultValue Trafo3d.Identity
                li, trafo
            )

        let gbb =
            match patches.Length, objBounds with
            | 0, None ->
                Box3d(V3d(-10,-10,-10), V3d(10,10,10))
            | 0, Some objBox ->
                objBox
            | _, _ ->
                let opcBox =
                    List.zip patches (layerInfosWithTrafos |> List.map snd)
                    |> List.map (fun (patch, trafo) ->
                        patch.info.GlobalBoundingBox.Transformed(trafo.Forward))
                    |> Box3d
                match objBounds with
                | Some objBox -> Box3d [opcBox; objBox]
                | None        -> opcBox

        printfn "[DEBUG] final gbb = %A" gbb
        printfn "[DEBUG] gbb.Center = %A" gbb.Center
        printfn "[DEBUG] gbb.Size = %A" gbb.Size

        // create OpcScene ...
        let initialCam =
            match config.CameraOutlierPercentile with
            | Some outlierPercentile when not patches.IsEmpty ->
                // Use robust camera calculation with individual points
                printfn "[INFO] Using robust camera calculation with %g%% outlier trimming" outlierPercentile
                let allPoints =
                    patchHierarchies
                    |> Seq.collect (Utils.getPoints true)
                    |> List.ofSeq
                Utils.createInitialCameraViewRobust allPoints outlierPercentile
            | _ ->
                // Use standard patch-based bounding box calculation
                Utils.createInitialCameraView gbb
        let speed = config.Speed |> Option.defaultValue (initialCam.Far / 64.0)

        let opcScene =
            { 
                useCompressedTextures = true
                preTransform     = Trafo3d.Identity
                patchHierarchies = Seq.delay (fun _ ->
                    layerInfosWithTrafos |> Seq.map (fun (info, _) -> info.Path.FullName))
                boundingBox      = gbb
                near             = initialCam.Near
                far              = initialCam.Far
                speed            = speed
                lodDecider       = DefaultMetrics.mars2 
            }

        // Parse background color if provided
        let backgroundColor = parseBackgroundColor config.BackgroundColor

        let ribbonState =
            match config.Ribbon with
            | None -> RibbonState.defaultState
            | Some ribbonCfg ->
                let halfWidth = ribbonCfg.HalfWidth |> Option.defaultValue 2.0
                match PRo3D.Viewer.Ribbon.GeoJson.tryParseAllLineStrings ribbonCfg.GeoJson with
                | Result.Error err ->
                    printfn "[RIBBON ERROR] %s" err
                    RibbonState.defaultState
                | Result.Ok polylines ->
                    // Apply a name-matched .trafo next to the GeoJSON (lines.geojson -> lines.trafo),
                    // analogous to the OPC trafo import. Absent file -> identity (leave as-is).
                    let polylineTrafo =
                        let candidate = System.IO.Path.ChangeExtension(ribbonCfg.GeoJson, ".trafo")
                        if System.IO.File.Exists candidate then
                            parseTrafoFile candidate |> Option.defaultValue Trafo3d.Identity
                        else
                            Trafo3d.Identity
                    let polylines =
                        if polylineTrafo = Trafo3d.Identity then polylines
                        else
                            polylines
                            |> Array.map (fun pl ->
                                { pl with points = pl.points |> Array.map polylineTrafo.Forward.TransformPos })
                    let autoPlane =
                        match DnsAlgorithms.computeDnSPlane V3d.YAxis polylines with
                        | Some plane ->
                            printfn "[DnS] auto-fitted at startup: normal %A  centre %A  radius %.3f"
                                plane.plane.Normal plane.centerOfMass plane.size
                            Some plane
                        | None -> None
                    { RibbonState.defaultState with
                        allPolylines  = polylines
                        selectedIndex = 0
                        halfWidth     = halfWidth
                        showPolyline  = true
                        dnSPlane      = autoPlane }

        let patchTrafos =
            layerInfosWithTrafos |> List.map snd

        // ... and show it using the unified viewer
        let viewerConfig : ViewerConfig = {
            mode = ViewerMode.ViewMode {
                objSceneGraphs = objScene
                enablePicking = true
                initialRibbonState = ribbonState
                patchTrafos    = patchTrafos
            }
            scene = opcScene
            sky = match List.first patchHierarchies with | Some x -> Utils.getSky x | None -> V3d.ZAxis
            initialCameraView = initialCam.CameraView
            customKeyHandlers = Map.empty
            customMouseHandler = None
            enableTextOverlay = false
            textOverlayFunc = None
            backgroundColor = backgroundColor
            screenshotDirectory = config.Screenshots
            version = config.Version
        }
        
        UnifiedViewer.run viewerConfig |> ignore
        0

    let run (version: string) (args : ParseResults<Args>) (globalScreenshots: string option) : int =
        // Build config directly here to avoid circular dependency
        let objFiles = 
            args.GetResult(Args.ObjFiles, defaultValue = [])
            |> List.map (fun path -> 
                { Path = path; Type = Some Configuration.DataType.Obj; Transform = None }: Configuration.DataEntry)
        
        let dataDirs = 
            args.GetResults Args.DataDirs
            |> List.concat  // Flatten the list of lists
            |> List.map (fun path -> 
                { Path = path; Type = Some Configuration.DataType.Opc; Transform = None }: Configuration.DataEntry)
        
        let data = (dataDirs @ objFiles) |> Array.ofList
        
        let config : ViewConfig = {
            Data = data
            Speed = args.TryGetResult Args.Speed
            Sftp = args.TryGetResult Args.Sftp
            BaseDir = args.TryGetResult Args.BaseDir
            BackgroundColor = args.TryGetResult Args.BackgroundColor
            Screenshots = globalScreenshots
            ForceDownload = if args.Contains Args.ForceDownload then Some true else None
            Verbose = if args.Contains Args.Verbose then Some true else None
            CameraOutlierPercentile = None  // Not supported in CLI, use project files
            Version = version
            Ribbon = None
        }
        execute config
