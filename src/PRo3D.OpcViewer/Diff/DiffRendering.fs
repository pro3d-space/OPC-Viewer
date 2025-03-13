namespace Aardvark.Opc

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.GeoSpatial.Opc.PatchLod
open Aardvark.Rendering
open FSharp.Data.Adaptive 
open PRo3D.OpcViewer

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

    let createSceneGraphCustom  (signature : IFramebufferSignature) (uploadThreadpool : Load.Runner) (basePath : string) (h : PatchHierarchy) (mode : aval<DistanceComputationMode>) (getColor : ComputeDistance) =
           
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

        let rnd = new RandomSystem()
        let computeDistancesForPatch (paths : OpcPaths) (patch : RenderPatch) =
            let buffer : aval<IBuffer> = 
                (distanceComputationEnabled, mode) 
                ||> AVal.map2 (fun enabled mode -> 
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
                                    match pLocal.IsNaN with
                                    | true -> C3b.Yellow
                                    | false -> let p = V3d(pLocal) |> patch.info.Local2Global.TransformPos
                                               getColor mode p
                                
                                V3f(C3f.FromC3b(c))
                                //rnd.UniformV3f()
                            )
                        ArrayBuffer(distances)
                    else
                        failwith "dont know"
                )
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
            DefaultMetrics.mars2, 
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

