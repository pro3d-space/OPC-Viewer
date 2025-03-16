namespace PRo3D.OpcViewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.Opc
open Argu
open PRo3D.OpcViewer
open PRo3D.OpcViewer.Data

[<AutoOpen>]
module ViewCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | Speed of float
        | [<AltCommandLine("-s") >] Sftp of string
        | [<AltCommandLine("-b") >] BaseDir of string

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"
                | Speed    _ -> "optional camera controller speed"
                | Sftp     _ -> "optional SFTP server config file (FileZilla format)"
                | BaseDir  _ -> "optional base directory for relative paths (default is ./data)"

    let run (args : ParseResults<Args>) : int =

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x
            | None ->
                printfn "[ERROR] no data directories specified"
                exit 0
                []

        let dataRefs = datadirs |> List.map Data.getDataRefFromString

        for x in dataRefs do
            match x with
            | AbsoluteDirRef(path, false) ->
                printfn "[ERROR] directory does not exist: %s" path
                exit 1
            | InvalidDataRef path ->
                printfn "[ERROR] invalid location: %s" path
                exit 1
            | _ -> ()

        let basedir =
            match args.TryGetResult(Args.BaseDir) with
            | Some s -> s
            | None -> System.IO.Path.Combine(System.Environment.CurrentDirectory, "data")

        let sftpServerConfig = args.TryGetResult(Args.Sftp) |> Option.map Sftp.parseFileZillaConfig

        let resolve = Data.resolveDataPath basedir sftpServerConfig
        let resolvedResults = dataRefs |> List.map resolve

        let datadirs = resolvedResults |> List.map (fun x ->
            match x with
            | ResolveDataPathResult.Ok ok -> ok
            | ResolveDataPathResult.MissingSftpConfig uri ->
                printfn "Use --sftp|-s do specify SFTP config for %A" uri
                exit 1
            | ResolveDataPathResult.DownloadError (uri, e) ->
                printfn "%A: %A" uri e
                exit 1
            | ResolveDataPathResult.InvalidDataDir s ->
                printfn "invalid data dir: %A" s
                exit 1
            )
            
        // discover all layers in datadirs ...
        let layerInfos = Data.searchLayerDirs datadirs
        
        // load patch hierarchies ...
        let patchHierarchies = 
            layerInfos
            |> Seq.toList 
            |> List.map Utils.loadPatchHierarchy

        // get root patch from each hierarchy
        let patches =
            patchHierarchies
            |> List.map (fun x -> match x.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n)

         // global bounding box over all patches
        let gbb = patches |> Seq.map (fun patch -> patch.info.GlobalBoundingBox) |> Box3d

        // create OpcScene ...
        let initialCam = Utils.createInitialCameraView gbb
        let speed = args.GetResult(Speed, defaultValue = initialCam.Far / 64.0)
        let scene =
            { 
                useCompressedTextures = true
                preTransform     = Trafo3d.Identity
                patchHierarchies = Seq.delay (fun _ -> layerInfos |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = initialCam.Near
                far              = initialCam.Far
                speed            = speed
                lodDecider       = DefaultMetrics.mars2 
            }

        // ... and show it
        
        OpcViewer.run scene initialCam.CameraView
        
