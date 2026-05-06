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

    let private rootPatch (h : PatchHierarchy) : Patch =
        match h.tree with
        | QTree.Node (n, _) -> n
        | QTree.Leaf n -> n

    let private rootBoundingBox (h : PatchHierarchy) : Box3d =
        (rootPatch h).info.GlobalBoundingBox

    /// Number of distinct texture layers on the root patch. The on-disk
    /// `Textures` list contains a (texture, weights) pair per layer, so the
    /// effective layer count is `length / 2` — this matches the modulo the
    /// geospatial loader uses internally for `LegacyId` lookups.
    let textureLayerCount (h : PatchHierarchy) : int =
        let textures = (rootPatch h).info.Textures
        max 1 (List.length textures / 2)

    /// For diagnostics: print the texture-list ordering so callers can map
    /// `LegacyId i` to the actual filename.
    let logTextureLayers (h : PatchHierarchy) =
        let textures = (rootPatch h).info.Textures
        Log.line "[OpcLoading] root patch has %d Textures entries:" (List.length textures)
        textures
        |> List.iteri (fun i t ->
            Log.line "  [%d] %s" i t.fileName)

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

    /// Default texture id used by the multi-texturing pipeline when nothing
    /// more specific is selected. `LegacyId 0` picks the first texture layer.
    let private defaultSecondaryTextureId : TextureId =
        { texture = TextureReference.LegacyId 0
          channel = ChannelReference.ChannelWithIndex 0 }

    /// Build the scene graph for one patch hierarchy, including the multi-texturing
    /// hooks from `SecondaryTexture` so the UI can toggle a secondary layer on/off.
    /// The `SecondaryTextureId` attribute is applied unconditionally so that the
    /// AG-getter `SecondaryTexture.getSecondary` can resolve it; whether the
    /// secondary layer is actually shown is decided by the `UseSecondary` uniform
    /// in the fragment shader.
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
        |> SecondaryTexture.Sg.applySecondaryTextureId
                (AVal.constant (Some defaultSecondaryTextureId))

    /// Wraps an `ISg` in an `AttributeParameters` applicator that selects
    /// the primary texture by its `LegacyId` index. Pass `None` to fall
    /// back to the loader's default.
    let withPrimaryTextureIndex (textureIndex : aval<Option<int>>) (sg : ISg) : ISg =
        let attribs =
            textureIndex |> AVal.map (fun idx ->
                let selected =
                    idx |> Option.map (fun i ->
                        { texture = TextureReference.LegacyId i
                          channel = ChannelReference.ChannelWithIndex 0 })
                { AttributeParameters.defaultParams with selectedTexture = selected })
        Sg.AttributeParameters attribs sg
