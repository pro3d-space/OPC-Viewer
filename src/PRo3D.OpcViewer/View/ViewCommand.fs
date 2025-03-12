namespace PRo3D.OpcViewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.Opc
open Aardvark.Rendering
open Argu
open PRo3D.OpcViewer

[<AutoOpen>]
module ViewCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | Speed of float

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"
                | Speed    _ -> "optional camera controller speed"

    let run (args : ParseResults<Args>) : int =

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x 
            | None ->
                printfn "[WARNING] no data directories specified"
                exit 0
                []

         // discover all layers in datadirs ...
        let layerInfos = Utils.searchLayerDirs datadirs
        
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
        
