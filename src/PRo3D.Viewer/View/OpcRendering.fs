namespace PRo3D.Viewer.View

open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Data.Opc
open Aardvark.SceneGraph.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.GeoSpatial.Opc.PatchLod
open FSharp.Data.Adaptive 
open Aardvark.GeoSpatial.Opc

module OpcRendering =

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


    type PatchInfoTable() = 
        // getting id's must be fast, resolving can be O(n)
        let c = ConcurrentDictionary<string, PatchFileInfo * int>()
        member x.GetId (p : PatchFileInfo) = 
            c.GetOrAdd(p.Name, fun _ -> 
                p, c.Count
            )

        member x.LookupLinear(id : int) : PatchFileInfo option =
            c |> Seq.tryPick (fun kvp -> 
                let (pfi, entryId) = kvp.Value
                if entryId = id then
                    Some pfi
                else 
                    None
            )

    let createSceneGraphCustom  (signature : IFramebufferSignature) (uploadThreadpool : Load.Runner) (mutableInfoTable : PatchInfoTable)  (basePath : string) (h : PatchHierarchy) =
           
        // use this anonymous scope extraction for patchNodes for potentially expensive computations, needed later in the getter functions
        let context (n : PatchNode) (s : Ag.Scope) =
            PatchScope(0) :> obj

        let uniforms = 
            // chance to add uniforms for rendering (available in shader as uniform parameters with given semantics)
            Map.ofList [
                "PatchId", (fun scope (patch : RenderPatch) -> 
                    mutableInfoTable.GetId patch.info |> snd |> AVal.constant :> IAdaptiveValue
                )
                //"FootprintModelViewProj", fun scope (patch : RenderPatch) -> 
                //    let viewTrafo,_ = scope |> unbox<aval<M44d> * obj>
                //    let r = AVal.map2 (fun viewTrafo (model : Trafo3d) -> viewTrafo * model.Forward) viewTrafo patch.trafo 
                //    r :> IAdaptiveValue
            ]

        let getTextures (paths : OpcPaths) (scope : obj) (patch : RenderPatch) : Map<string, aval<ITexture>> =
            let scope = scope :?> PatchScope
            // add textures here (set of textures is fixed), an example is here: https://github.com/aardvark-platform/aardvark.geospatial/blob/40bbfcd2a886043ec366dbc39ee057b80db17d3d/src/Aardvark.GeoSpatial.Opc/MultiTexturing.fs#L37
            Map.empty
                
        let getVertexAttributes (paths : OpcPaths) (scope : obj) (patch : RenderPatch) : Map<Symbol, BufferView> =
            let scope = scope :?> PatchScope
            // add textures here (set of textures is fixed), an example is here: https://github.com/aardvark-platform/aardvark.geospatial/blob/40bbfcd2a886043ec366dbc39ee057b80db17d3d/src/Aardvark.GeoSpatial.Opc/MultiTexturing.fs#L55
            Map.empty

        let sg = 
            PatchNode(
                signature, 
                uploadThreadpool, 
                h.opcPaths.Opc_DirAbsPath, 
                DefaultMetrics.mars2 , 
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
        sg

