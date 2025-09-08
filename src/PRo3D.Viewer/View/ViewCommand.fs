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

        // get root patch from each hierarchy
        let patches =
            patchHierarchies
            |> List.map (fun x -> match x.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n)

         // global bounding box - compute from available data
        let gbb = 
            match patches.Length, objBounds with
            | 0, None ->
                // No data at all - shouldn't happen due to earlier validation
                printfn "[WARNING] No geometry found, using default bounding box"
                Box3d(V3d(-10,-10,-10), V3d(10,10,10))
            | 0, Some objBox ->
                // OBJ only
                printfn "[INFO] Using bounding box from OBJ files: %A" objBox
                objBox
            | _, None ->
                // OPC only (existing behavior)
                printfn "[INFO] Using bounding box from OPC patches"
                patches |> Seq.map (fun patch -> patch.info.GlobalBoundingBox) |> Box3d
            | _, Some objBox ->
                // Both OPC and OBJ - combine bounds
                let opcBox = patches |> Seq.map (fun patch -> patch.info.GlobalBoundingBox) |> Box3d
                let combinedBox = Box3d [opcBox; objBox]
                printfn "[INFO] Combining OPC and OBJ bounding boxes: %A" combinedBox
                combinedBox

        // create OpcScene ...
        let initialCam = Utils.createInitialCameraView gbb
        let speed = config.Speed |> Option.defaultValue (initialCam.Far / 64.0)
        
        // Determine OPC transformation - use first non-identity transform or identity
        let opcTransform = 
            let dataTransforms = opcEntries |> Array.map (fun e -> e.Transform)
            if dataTransforms.Length > 0 then
                // Find first non-None transform, or use identity
                match dataTransforms |> Array.tryFind Option.isSome with
                | Some (Some m) -> 
                    printfn "[OPC] Applying transformation to OPC data"
                    Trafo3d(m, m.Inverse)
                | _ -> Trafo3d.Identity
            else
                Trafo3d.Identity
        
        let opcScene =
            { 
                useCompressedTextures = true
                preTransform     = opcTransform
                patchHierarchies = Seq.delay (fun _ -> layerInfos |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = initialCam.Near
                far              = initialCam.Far
                speed            = speed
                lodDecider       = DefaultMetrics.mars2 
            }

        // Parse background color if provided
        let backgroundColor = parseBackgroundColor config.BackgroundColor

        // ... and show it using the unified viewer
        let viewerConfig : ViewerConfig = {
            mode = ViewerMode.ViewMode {
                objSceneGraphs = objScene
                enablePicking = true
            }
            scene = opcScene
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
            Version = version
        }
        execute config
