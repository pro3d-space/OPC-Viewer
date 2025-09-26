namespace PRo3D.Viewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.Rendering
open MBrace.FsPickler
open System
open System.IO
open System.Net.Http
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

    let private serializer = FsPickler.CreateBinarySerializer()

    let downloadFileAsync (uri: Uri) (destinationPath: string) =
        async {
            printfn "downloading %A" uri
            printfn "  to %s" destinationPath
            use httpClient = new HttpClient()
            let! response = httpClient.GetAsync(uri) |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsByteArrayAsync() |> Async.AwaitTask
            File.WriteAllBytes(destinationPath, content)
        }
        
    /// Loads patch hierarchy for given layer info from disk.
    let loadPatchHierarchy (info : LayerInfo) : PatchHierarchy =
        PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths info.Path.FullName)

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

    /// Gets global bounding box of given patch hierarchy.
    let getGlobalBoundingBox (patchHierarchy : PatchHierarchy) : Box3d =
        let rootPatch = match patchHierarchy.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        rootPatch.info.GlobalBoundingBox

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

        //let imax = triangles.Length
        //triangles <- [| for i in 1..2..imax - 1 do yield triangles.[i] |]

        if excludeNaN then
            triangles <- triangles |> Array.filter isValidTriangle

        triangles

    /// Returns all triangles of the PatchHierarchy's most detailed level.
    /// Coordinates are in world space.
    let getTriangles (excludeNaN : bool) (hierarchy : PatchHierarchy)  : Triangle3d list =
        traverse hierarchy.tree false 
        |> Seq.collect (getTrianglesPatch excludeNaN hierarchy)
        |> List.ofSeq

    /// Creates a robust bounding box by excluding outliers based on percentile trimming.
    /// For each axis, removes the specified percentile from both extremes.
    let createRobustBoundingBox (points : V3d list) (outlierPercentile : float) : Box3d =
        if points.IsEmpty then
            Box3d.Invalid
        else
            let count = List.length points
            let trimCount = int(float count * outlierPercentile / 100.0)
            let keepCount = count - (2 * trimCount)

            if keepCount <= 0 then
                // Fallback to all points if trimming would remove everything
                Box3d(points)
            else
                // Sort and trim each axis independently for better outlier handling
                let xValues = points |> List.map (fun p -> p.X) |> List.sort
                let yValues = points |> List.map (fun p -> p.Y) |> List.sort
                let zValues = points |> List.map (fun p -> p.Z) |> List.sort

                let xTrimmed = xValues |> List.skip trimCount |> List.take keepCount
                let yTrimmed = yValues |> List.skip trimCount |> List.take keepCount
                let zTrimmed = zValues |> List.skip trimCount |> List.take keepCount

                let minPoint = V3d(List.head xTrimmed, List.head yTrimmed, List.head zTrimmed)
                let maxPoint = V3d(List.last xTrimmed, List.last yTrimmed, List.last zTrimmed)

                Box3d(minPoint, maxPoint)

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

    /// Creates initial camera view using a robust bounding box that excludes outliers.
    /// This provides better camera positioning when the dataset contains degenerate triangles.
    let createInitialCameraViewRobust (points : V3d list) (outlierPercentile : float) : CameraViewAndNearFar =
        let robustBB = createRobustBoundingBox points outlierPercentile
        if robustBB.IsValid then
            createInitialCameraView robustBB
        else
            // Fallback to original method if robust calculation fails
            createInitialCameraView (Box3d(points))

    /// Parses a background color string into C4f.
    /// Supports hex colors (#RGB, #RRGGBB), named colors, and RGB values (r,g,b).
    let parseBackgroundColor (colorStr : string) : Result<C4f, string> =
        let trimmed = colorStr.Trim()
        
        if String.IsNullOrEmpty(trimmed) then
            Result.Error "Color string cannot be empty"
        else
        
        // Try hex colors first
        if trimmed.StartsWith("#") then
            let hex = trimmed.Substring(1)
            if hex.Length = 3 then
                // #RGB format - expand to #RRGGBB
                try
                    let r = System.Convert.ToInt32(hex.Substring(0,1) + hex.Substring(0,1), 16) |> float32
                    let g = System.Convert.ToInt32(hex.Substring(1,1) + hex.Substring(1,1), 16) |> float32  
                    let b = System.Convert.ToInt32(hex.Substring(2,1) + hex.Substring(2,1), 16) |> float32
                    Result.Ok (C4f(r / 255.0f, g / 255.0f, b / 255.0f, 1.0f))
                with
                | _ -> Result.Error (sprintf "Invalid hex color format: %s (expected #RGB)" trimmed)
            elif hex.Length = 6 then
                // #RRGGBB format
                try
                    let r = System.Convert.ToInt32(hex.Substring(0,2), 16) |> float32
                    let g = System.Convert.ToInt32(hex.Substring(2,2), 16) |> float32
                    let b = System.Convert.ToInt32(hex.Substring(4,2), 16) |> float32
                    Result.Ok (C4f(r / 255.0f, g / 255.0f, b / 255.0f, 1.0f))
                with
                | _ -> Result.Error (sprintf "Invalid hex color format: %s (expected #RRGGBB)" trimmed)
            else
                Result.Error (sprintf "Invalid hex color format: %s (expected #RGB or #RRGGBB)" trimmed)
        
        // Try RGB format (r,g,b)
        elif trimmed.Contains(",") then
            let parts = trimmed.Split(',') |> Array.map (fun s -> s.Trim())
            if parts.Length = 3 then
                try
                    let r = float32 parts.[0] / 255.0f
                    let g = float32 parts.[1] / 255.0f  
                    let b = float32 parts.[2] / 255.0f
                    if r >= 0.0f && r <= 1.0f && g >= 0.0f && g <= 1.0f && b >= 0.0f && b <= 1.0f then
                        Result.Ok (C4f(r, g, b, 1.0f))
                    else
                        Result.Error (sprintf "RGB values must be 0-255: %s" trimmed)
                with
                | _ -> Result.Error (sprintf "Invalid RGB format: %s (expected r,g,b)" trimmed)
            else
                Result.Error (sprintf "Invalid RGB format: %s (expected r,g,b)" trimmed)
        
        // Try named colors
        else
            match trimmed.ToLower() with
            | "black" -> Result.Ok C4f.Black
            | "white" -> Result.Ok C4f.White  
            | "red" -> Result.Ok C4f.Red
            | "green" -> Result.Ok C4f.Green
            | "blue" -> Result.Ok C4f.Blue
            | "yellow" -> Result.Ok C4f.Yellow
            | "cyan" -> Result.Ok C4f.Cyan
            | "magenta" -> Result.Ok C4f.Magenta
            | "gray" | "grey" -> Result.Ok (C4f(0.5f, 0.5f, 0.5f, 1.0f))
            | "darkgray" | "darkgrey" -> Result.Ok (C4f(0.25f, 0.25f, 0.25f, 1.0f))
            | "lightgray" | "lightgrey" -> Result.Ok (C4f(0.75f, 0.75f, 0.75f, 1.0f))
            | _ -> Result.Error (sprintf "Unknown color name: %s (supported: black, white, red, green, blue, yellow, cyan, magenta, gray, darkgray, lightgray)" trimmed)

type LayerInfo with

    member this.LoadPatchHierarchy () =
        this |> Utils.loadPatchHierarchy

    member this.GetPoints (excludeNaN : bool) =
        this |> Utils.loadPatchHierarchy |> Utils.getPoints excludeNaN

    member this.GetTriangles (excludeNaN : bool) =
        this |> Utils.loadPatchHierarchy |> Utils.getTriangles excludeNaN

