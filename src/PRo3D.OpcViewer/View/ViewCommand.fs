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
        let layerInfos = LayerUtils.searchLayerDirs datadirs
        
        // load patch hierarchies ...
        let patchHierarchies = 
            layerInfos
            |> Seq.toList 
            |> List.map LayerUtils.loadPatchHierarchy

        // get root patch from each hierarchy
        let patches =
            patchHierarchies
            |> List.map (fun x -> match x.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n)

         // global bounding box over all patches
        let gbb = patches |> Seq.map (fun patch -> patch.info.GlobalBoundingBox) |> Box3d

        let createInitialCameraView (gbb : Box3d) =
            let globalSky = gbb.Center.Normalized
            let plane = Plane3d(globalSky, 0.0)
            let plane2global pos = gbb.Center + plane.GetPlaneSpaceTransform().TransformPos(pos)

            let d = gbb.Size.Length
            let localLocation = V3d(d * 0.1, d * 0.05, d * 0.25)
            let localLookAt   = V3d.Zero

            let globalLocation = plane2global localLocation
            let globalLookAt   = plane2global localLookAt

            let cam = CameraView.lookAt globalLocation globalLookAt globalSky

            let far = d * 1.5
            let near = far / 1024.0

            (cam, near, far)

        // create OpcScene ...
        let (initialCam, near, far) = createInitialCameraView gbb
        let speed = args.GetResult(Speed, defaultValue = far / 64.0)
        let scene =
            { 
                useCompressedTextures = true
                preTransform     = Trafo3d.Identity
                patchHierarchies = Seq.delay (fun _ -> layerInfos |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = near
                far              = far
                speed            = speed
                lodDecider       = DefaultMetrics.mars2 
            }

        // ... and show it
        
        OpcViewer.run scene initialCam
        
