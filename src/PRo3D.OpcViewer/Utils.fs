namespace PRo3D.OpcViewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.Rendering
open MBrace.FsPickler
open System.IO
open System.Runtime.CompilerServices

type LayerInfo = {
    Path: DirectoryInfo
    PatchHierarchyFile: FileInfo
}

type PatchHierarchyStats = {
    CountLeafNodes : int
    CountInnerNodes : int
    CountVertices : int
    CountFaces : int
    MinLevel : int
    MaxLevel : int
    IndexedAttributes : Symbol[]
    SingleAttributes : Symbol[]
}

type CameraViewAndNearFar = {
    CameraView : CameraView
    Near : float
    Far : float
}

[<Extension>]
type Box3dExtensions() =

    [<Extension>]
    static member SplitX(b : Box3d, x : double) : Box3d * Box3d =
        let a = Box3d(b.Min, V3d(x, b.Max.Y, b.Max.Z))
        let b = Box3d(V3d(x, b.Min.Y, b.Min.Z), b.Max)
        (a, b)

    [<Extension>]
    static member SplitX(b : Box3d) : Box3d * Box3d =
        b.SplitX(b.Center.X)

    [<Extension>]
    static member SplitY(b : Box3d, y : double) : Box3d * Box3d =
        let a = Box3d(b.Min, V3d(b.Max.X, y, b.Max.Z))
        let b = Box3d(V3d(b.Min.X, y, b.Min.Z), b.Max)
        (a, b)
        
    [<Extension>]
    static member SplitY(b : Box3d) : Box3d * Box3d =
        b.SplitY(b.Center.Y)

    [<Extension>]
    static member SplitZ(b : Box3d, z : double) : Box3d * Box3d =
        let a = Box3d(b.Min, V3d(b.Max.X, b.Max.Y, z))
        let b = Box3d(V3d(b.Min.X, b.Min.Y, z), b.Max)
        (a, b)

    [<Extension>]
    static member SplitZ(b : Box3d) : Box3d * Box3d =
        b.SplitZ(b.Center.Z)

    [<Extension>]
    static member SplitDim(b : Box3d, dim : int) : Box3d * Box3d =
        match dim with
        | 0 -> b.SplitX()
        | 1 -> b.SplitY()
        | 2 -> b.SplitZ()
        | _ -> sprintf "Invalid dimension %d." dim |> failwith

    [<Extension>]
    static member SplitMajorDimension(b : Box3d) : Box3d * Box3d =
        b.SplitDim(b.MajorDim)

module Utils =

    let private (+/) path1 path2 = Path.Combine(path1, path2)
    let private serializer = FsPickler.CreateBinarySerializer()

    /// Loads patch hierarchy for given layer info from disk.
    let loadPatchHierarchy (info : LayerInfo) : PatchHierarchy =
        PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths info.Path.FullName)

    /// Enumerates all (recursive) subdirs of given dirs that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDirs (dirs : seq<string>) : LayerInfo list =
        dirs 
        |> Seq.map (fun s -> DirectoryInfo(s))
        |> Seq.filter (fun d -> d.Exists)
        |> Seq.collect (fun d -> 
            d.EnumerateDirectories("patches", SearchOption.AllDirectories)
            )
        |> Seq.filter (fun d -> File.Exists(d.FullName +/ "patchhierarchy.xml"))
        |> Seq.map (fun d -> { Path = d.Parent; PatchHierarchyFile = FileInfo(d.FullName +/ "patchhierarchy.xml") })
        |> List.ofSeq

    /// Enumerates all (recursive) subdirs of given dir that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDir (dir : string) : LayerInfo list =
        searchLayerDirs [dir]

    /// Enumerates all nodes of a QTree in depth-first order.
    let rec traverse (root : QTree<'a>) (includeInner : bool) : 'a seq = seq {
        match root with
        | QTree.Node (n, xs) ->
            if includeInner then yield n
            for x in xs do yield! traverse x includeInner
        | QTree.Leaf n -> yield n
        }
    
    /// Compiles stats for given PatchHierarchy.
    let getPatchHierarchyStats (patchHierarchy : PatchHierarchy) : PatchHierarchyStats =

        let mutable countLeafNodes = 0
        let mutable countInnerNodes = 0
        let mutable countVertices = 0
        let mutable countFaces = 0
        let mutable minLevel = System.Int32.MaxValue
        let mutable maxLevel = System.Int32.MinValue

        let updateLevel i =
            if i < minLevel then minLevel <- i
            if i > maxLevel then maxLevel <- i

        let opcPaths = patchHierarchy.opcPaths
        let rec traverse (n : QTree<Patch>) : unit =
            match n with
            | QTree.Node (n, xs) ->
                updateLevel n.level
                countInnerNodes <- countInnerNodes + 1
                for x in xs do traverse x
            | QTree.Leaf n ->
                updateLevel n.level
                countLeafNodes <- countLeafNodes + 1
                let ig, _ = Patch.load opcPaths  ViewerModality.XYZ n.info
                countVertices <- countVertices + ig.VertexCount
                countFaces <- countFaces + ig.FaceCount
            

        traverse patchHierarchy.tree
        
        let patch = match patchHierarchy.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        let ig, _ = Patch.load patchHierarchy.opcPaths ViewerModality.XYZ patch.info

        {
            CountLeafNodes = countLeafNodes
            CountInnerNodes = countInnerNodes
            CountVertices = countVertices
            CountFaces = countFaces
            MinLevel = minLevel
            MaxLevel = maxLevel
            IndexedAttributes = if ig.IndexedAttributes <> null then ig.IndexedAttributes.Keys |> Array.ofSeq else Array.empty
            SingleAttributes = if ig.SingleAttributes <> null then ig.SingleAttributes.Keys |> Array.ofSeq else Array.empty
        }

    /// Returns true if triangle contains at least one vertex which is NaN.
    let isInvalidTriangle (triangle : Triangle3d) : bool = triangle.P0.IsNaN || triangle.P1.IsNaN || triangle.P2.IsNaN
    
    /// Returns true if triangle does not contain NaN vertices.
    let isValidTriangle (triangle : Triangle3d) : bool = not (isInvalidTriangle triangle)

    /// Returns true if triangle contains at least one vertex which is NaN.
    let isInvalidTriangleF (triangle : Triangle3f) : bool = triangle.P0.IsNaN || triangle.P1.IsNaN || triangle.P2.IsNaN

    /// Returns true if triangle does not contain NaN vertices.
    let isValidTriangleF (triangle : Triangle3f) : bool = not (isInvalidTriangleF triangle)

    /// Gets sky vector for given patch hierarchy.
    /// Warning: Implementation is dubious. 
    /// While the sky direction seems approximately right, this is most probably not the correct or exact vector.
    let getSky (patchHierarchy : PatchHierarchy) : V3d =
        let rootPatch = match patchHierarchy.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        let gbb = rootPatch.info.GlobalBoundingBox
        let sky = gbb.Center.Normalized
        sky

    /// Gets root patch of given patch hierarchy.
    let getRootPatch (patchHierarchy : PatchHierarchy) : Patch =
        match patchHierarchy.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n

    /// Prints a short summary of given LayerInfo.
    /// E.g. attributes and various counts for nodes, vertices and faces.
    let printLayerInfo (info : LayerInfo) : unit =
        printfn "%s" info.Path.FullName

        let f (i : int) = i.ToString("N0")

        let stats = info |> loadPatchHierarchy |> getPatchHierarchyStats
        let countNodes = stats.CountInnerNodes + stats.CountLeafNodes
        if stats.IndexedAttributes.Length > 0 then
            let s = String.concat ", " (stats.IndexedAttributes |> Array.map(fun x -> x.ToString()))
            printfn "    %s" s
        if stats.SingleAttributes.Length > 0 then
            let s = String.concat ", " (stats.SingleAttributes |> Array.map(fun x -> x.ToString()))
            printfn "    %s" s

        printfn "    %s nodes (%s leafs, %s inner, levels %d..%d)" (f(countNodes)) (f(stats.CountLeafNodes)) (f(stats.CountInnerNodes)) stats.MinLevel stats.MaxLevel
        printfn "    vertices %12s" (f(stats.CountVertices))
        printfn "    faces    %12s" (f(stats.CountFaces))

    /// Returns all points of given patch.
    /// Coordinates are in world space.
    let getPointsPatch (excludeNaN : bool) (hierarchy : PatchHierarchy) (node : Patch) : V3d list =
        let ig, _ = Patch.load hierarchy.opcPaths ViewerModality.XYZ node.info
        let l2g = node.info.Local2Global
        let ps0 = 
            match ig.IndexedAttributes[DefaultSemantic.Positions] with
            | (:? array<V3f> as v) when not (isNull v) -> v
            | _ -> failwith ""

        let ps1 = if excludeNaN then ps0 |> Seq.filter (fun p -> not p.IsNaN) else ps0
        let ps = ps1 |> Seq.map (fun p -> l2g.TransformPos (V3d(p))) |> List.ofSeq
        ps

    /// Returns all points of the PatchHierarchy's most detailed level.
    /// Coordinates are in world space.
    let getPoints (excludeNaN : bool) (hierarchy : PatchHierarchy) : V3d list =
        traverse hierarchy.tree false 
        |> Seq.collect (getPointsPatch excludeNaN hierarchy)
        |> List.ofSeq
      
    /// Returns all triangles of given patch.
    /// Coordinates are in world space.
    let getTrianglesPatch (excludeNaN : bool) (hierarchy : PatchHierarchy) (node : Patch) : Triangle3d[] =
        
        let ig, _ = Patch.load hierarchy.opcPaths ViewerModality.XYZ node.info

        let ia = 
            if ig.IsIndexed then
                match ig.IndexArray with
                | :? array<int> as idx -> idx
                | _ -> failwith "[Queries] Patch index geometry has no int[] index"
            else
                failwith "[Queries] Patch index geometry is not indexed."

        let ps = 
            match ig.IndexedAttributes[DefaultSemantic.Positions] with
            | (:? array<V3f> as v) when not (isNull v) -> v
            | _ -> failwith ""

        let l2g = node.info.Local2Global
        let tp (p : V3f) : V3d = l2g.TransformPos (V3d(p))
        //let transform (t : Triangle3f) : Triangle3d = Triangle3d(tp t.P0, tp t.P1, tp t.P2)

        let mutable triangles = Array.zeroCreate<Triangle3d> (ia.Length / 3)
        let mutable j = 0
        for i in 0 .. triangles.Length-1 do
            triangles[i].P0 <- tp ps[ia[j]]; j <- j + 1
            triangles[i].P1 <- tp ps[ia[j]]; j <- j + 1
            triangles[i].P2 <- tp ps[ia[j]]; j <- j + 1
            ()

        if excludeNaN then
            triangles <- triangles |> Array.filter isValidTriangle

        triangles

    /// Returns all triangles of the PatchHierarchy's most detailed level.
    /// Coordinates are in world space.
    let getTriangles (excludeNaN : bool) (hierarchy : PatchHierarchy)  : Triangle3d list =
        traverse hierarchy.tree false 
        |> Seq.collect (getTrianglesPatch excludeNaN hierarchy)
        |> List.ofSeq

    let createInitialCameraView (gbb : Box3d) : CameraViewAndNearFar =
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

        { CameraView = cam; Near = near;  Far = far }


type LayerInfo with

    member this.LoadPatchHierarchy () =
        this |> Utils.loadPatchHierarchy

    member this.GetPoints (excludeNaN : bool) =
        this |> Utils.loadPatchHierarchy |> Utils.getPoints excludeNaN

    member this.GetTriangles (excludeNaN : bool) =
        this |> Utils.loadPatchHierarchy |> Utils.getTriangles excludeNaN

