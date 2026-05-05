namespace PRo3D.Viewer.SimpleGui

open System.IO

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Configurations
open Aardvark.GeoSpatial.Opc.Load

open FSharp.Data.Adaptive
open MBrace.FsPickler


module OpcLoading =

    let private serializer = FsPickler.CreateBinarySerializer()

    /// Recursively find every directory that owns a `patches/patchhierarchy.xml`
    /// (i.e. the base path expected by `PatchHierarchy.load`).
    let findHierarchyBasePaths (rootDir : string) : list<string> =
        if not (Directory.Exists rootDir) then []
        else
            let root = DirectoryInfo(rootDir)
            root.EnumerateDirectories("patches", SearchOption.AllDirectories)
            |> Seq.filter (fun d -> File.Exists(Path.Combine(d.FullName, "patchhierarchy.xml")))
            |> Seq.choose (fun d -> if isNull d.Parent then None else Some d.Parent.FullName)
            |> Seq.distinct
            |> List.ofSeq

    let private rootBoundingBox (h : PatchHierarchy) : Box3d =
        let rootPatch =
            match h.tree with
            | QTree.Node (n, _) -> n
            | QTree.Leaf n -> n
        rootPatch.info.GlobalBoundingBox

    /// Loads each patch hierarchy from disk and combines the root-node
    /// bounding boxes (i.e. the *lowest-quality* coverage of every OPC).
    /// Returns (loadedHierarchies, combinedBox).
    let loadHierarchies (basePaths : list<string>) : list<PatchHierarchy * string> * Box3d =
        let hierarchies =
            basePaths
            |> List.map (fun bp ->
                let h = PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths bp)
                h, bp)
        let combined =
            hierarchies
            |> Seq.map (fst >> rootBoundingBox)
            |> Box3d
        hierarchies, combined

    /// Build the scene graph for one patch hierarchy, including the multi-texturing
    /// hooks from `SecondaryTexture` so the UI can toggle a secondary layer on/off.
    /// The `SecondaryTextureId` attribute is applied unconditionally so that the
    /// AG-getter `SecondaryTexture.getSecondary` can resolve it; whether the
    /// secondary layer is actually shown is decided by the `UseSecondary` uniform
    /// in the fragment shader.
    /// (The installed `Aardvark.GeoSpatial.Opc 5.11.2` exposes the legacy int-based
    /// API; the newer `TextureId`/`TextureReference` types are not in this version.)
    let buildHierarchySg
            (signature     : IFramebufferSignature)
            (runner        : Load.Runner)
            (asyncLoading  : bool)
            (basePath      : string)
            (h             : PatchHierarchy) : ISg =
        let tree = PatchLod.toRoseTree h.tree
        let paths = OpcPaths basePath
        let loader = Aardvark.Data.PixImagePfim.Loader
        Sg.patchLodMultiTexturingWithLoader
            signature runner basePath
            DefaultMetrics.mars2
            SecondaryTexture.getSecondary
            false false
            ViewerModality.XYZ
            PatchLod.CoordinatesMapping.Local
            asyncLoading
            tree
            (Some (SecondaryTexture.textures paths))
            (Some (SecondaryTexture.vertexAttributes paths))
            loader
        |> SecondaryTexture.Sg.applySecondaryTextureId (AVal.constant 0)
