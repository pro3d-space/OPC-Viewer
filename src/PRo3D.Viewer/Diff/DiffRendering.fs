namespace PRo3D.Viewer.Diff

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.GeoSpatial.Opc.PatchLod
open Aardvark.Rendering
open FSharp.Data.Adaptive
open PRo3D.Viewer.Shared 

module LodDecider =

    let lodDeciderMars 
           (preTrafo    : Trafo3d) (self        : AdaptiveToken) (viewTrafo   : aval<Trafo3d>) (_projection : aval<Trafo3d>) (p : Aardvark.GeoSpatial.Opc.PatchLod.RenderPatch) 
           (lodParams   : aval<LodParameters>) (isActive    : aval<bool>) =

           let lodParams = lodParams.GetValue self
           //let isActive = isActive.GetValue self

           let campPos = (viewTrafo.GetValue self).Backward.C3.XYZ
           let bb      = p.info.GlobalBoundingBox.Transformed(lodParams.trafo * preTrafo ) //* preTrafo)
           let closest = bb.GetClosestPointOn(campPos)
           let dist    = (closest - campPos).Length

           //// super agressive to prune out far away stuff, too aggressive !!!
           //if not isActive || (campPos - bb.Center).Length > p.info.GlobalBoundingBox.Size.[p.info.GlobalBoundingBox.Size.MajorDim] * 1.5 
           //    then false
           //else

           let unitPxSize = lodParams.frustum.right / (float lodParams.size.X / 2.0)
           let px = (lodParams.frustum.near * p.triangleSize) / dist // (pow dist 1.2) // (added pow 1.2 here... discuss)

               //    Log.warn "%f to %f - avgSize: %f" px (unitPxSize * lodParams.factor) p.triangleSize
           px > unitPxSize * (exp lodParams.factor)
 

module DiffRendering =

    let createSceneGraphSimple (signature : IFramebufferSignature) (uploadThreadpool : Load.Runner) (basePath : string) (h : PatchHierarchy) =
        let t = PatchLod.toRoseTree h.tree
         
         
        Sg.patchLod signature uploadThreadpool basePath DefaultMetrics.mars2 
                    false false ViewerModality.XYZ PatchLod.CoordinatesMapping.Local 
                    true t
                    

    // define you scope type here to 
    type PatchScope = PatchScope of int

    module DefaultSemantic =
        [<Literal>]
        let Distances = "Distances"
        let DistancesSym = Sym.ofString Distances

    let createSceneGraphCustom
        (signature : IFramebufferSignature)
        (uploadThreadpool : Load.Runner)
        (h : PatchHierarchy)
        (distanceMode : aval<DistanceComputationMode>)
        (toggleMode : aval<DiffToggleMode>)
        (getColor : ComputeDistanceColor) =
           
        // use this anonymous scope extraction for patchNodes for potentially expensive computations, needed later in the getter functions
        let context (n : PatchNode) (s : Ag.Scope) =
            PatchScope(0) :> obj

        let uniforms = 
            // chance to add uniforms for rendering (available in shader as uniform parameters with given semantics)
            Map.ofList [
                //"FootprintModelViewProj", fun scope (patch : RenderPatch) -> 
                //    let viewTrafo,_ = scope |> unbox<aval<M44d> * obj>
                //    let r = AVal.map2 (fun viewTrafo (model : Trafo3d) -> viewTrafo * model.Forward) viewTrafo patch.trafo 
                //    r :> IAdaptiveValue
            ]

        let getTextures (paths : OpcPaths) (scope : obj) (patch : RenderPatch) : Map<string, aval<ITexture>> =
            let scope = scope :?> PatchScope
            // add textures here (set of textures is fixed), an example is here: https://github.com/aardvark-platform/aardvark.geospatial/blob/40bbfcd2a886043ec366dbc39ee057b80db17d3d/src/Aardvark.GeoSpatial.Opc/MultiTexturing.fs#L37
            Map.empty
                
        let distanceComputationEnabled : aval<bool> = cval true

        let computeDistancesForPatch (paths : OpcPaths) (patch : RenderPatch) =
            let buffer : aval<IBuffer> =
                adaptive {
                    let! enabled = distanceComputationEnabled
                    let! distanceMode = distanceMode
                    let! toggleMode = toggleMode

                    if enabled then
                        let (g, elapsedIndex), createTime =
                            timed (fun () ->
                                Patch.load paths patch.modality patch.info
                            )
                        //let idx = g.IndexArray |> unbox<int[]>
                        let positions = g.IndexedAttributes[DefaultSemantic.Positions] |> unbox<V3f[]>
                        let distances =
                            positions |> Array.map (fun pLocal ->
                                //let pLocal = positions[idx]
                                let c =
                                    match pLocal.AnyNaN with
                                    | true ->
                                        C3b.Yellow
                                    | false ->
                                        let p = V3d(pLocal) |> patch.info.Local2Global.TransformPos
                                        getColor distanceMode toggleMode p

                                V3f(C3f.FromC3b(c))
                            )
                        return ArrayBuffer(distances) :> IBuffer
                    else
                        return failwith "don't know" :> IBuffer
                }
            BufferView(buffer, typeof<V3f>)

        let getVertexAttributes (paths : OpcPaths) (scope : obj) (patch : RenderPatch) : Map<Symbol, BufferView> =
            let scope = scope :?> PatchScope
            let vertexData = patch.info.Positions
            Map.ofList [
                DefaultSemantic.DistancesSym, computeDistancesForPatch paths patch   
            ]

        PatchNode(
            signature, 
            uploadThreadpool, 
            h.opcPaths.Opc_DirAbsPath, 
            LodDecider.lodDeciderMars Trafo3d.Identity, //DefaultMetrics.mars2, 
            false, 
            true, 
            ViewerModality.XYZ, 
            PatchLod.CoordinatesMapping.Local, 
            true, 
            context, 
            uniforms,
            PatchLod.toRoseTree h.tree,
            Some (getTextures h.opcPaths), 
            Some (getVertexAttributes h.opcPaths), 
            Aardvark.Data.PixImagePfim.Loader
        )

