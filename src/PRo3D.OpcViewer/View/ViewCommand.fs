namespace PRo3D.OpcViewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.Opc
open Aardvark.Rendering
open Argu
open MBrace.FsPickler
open PRo3D.OpcViewer

[<AutoOpen>]
module ViewCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"

    let run (args : ParseResults<Args>) : int =

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x 
            | None ->
                printfn "[WARNING] no data directories specified"
                exit 0
                []

         // discover all layers in datadirs ...
        let layerInfos = LayerManagement.searchLayerDirs datadirs
        
        // load layers from disk ...
        let serializer = FsPickler.CreateBinarySerializer()
        let patchHierarchies = 
            layerInfos 
            |> Seq.toList 
            |> List.map (fun info -> PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths info.Path.FullName))
        let patches =
            patchHierarchies
            |> List.map (fun x -> match x.tree with | QTree.Node (n, _) -> n | QTree.Leaf n      -> n)

         // global bounding box over all patches
        let gbb = patches |> Seq.map (fun patch -> patch.info.GlobalBoundingBox) |> Box3d

        let createInitialCameraView (gbb : Box3d) =
            let globalSky = gbb.Center.Normalized
            let plane = Plane3d(globalSky, 0.0)
            let plane2global pos = gbb.Center + plane.GetPlaneSpaceTransform().TransformPos(pos)

            let d = gbb.Size.Length * 0.5
            let localLocation = V3d(d * 0.2, d * 0.1, d * 0.5)
            let localLookAt   = V3d.Zero

            let globalLocation = plane2global localLocation
            let globalLookAt   = plane2global localLookAt

            let cam = CameraView.lookAt globalLocation globalLookAt globalSky

            cam


        // create OpcScene ... 
        let scene =
            { 
                useCompressedTextures = true
                preTransform     = Trafo3d.Identity
                patchHierarchies = Seq.delay (fun _ -> layerInfos |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = 0.1
                far              = 10000.0
                speed            = 5.0
                lodDecider       = DefaultMetrics.mars2 
            }

        // ... and show it
        let initialCam = createInitialCameraView gbb
        OpcViewer.run scene initialCam
        
