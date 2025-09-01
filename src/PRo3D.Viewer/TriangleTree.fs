namespace PRo3D.Viewer

open Aardvark.Base


type TriangleTree =
    | Inner of left : TriangleTree * right : TriangleTree * bounds : Box3d
    | Leaf of triangles : Triangle3d[] * bounds : Box3d
    | EmptyLeaf

module TriangleTree =

    /// Splits triangle t at plane intersecting the dim-axis at position x, where the dim-axis is the plane normal.
    /// Returns parts which are left and right of x as a two triangle arrays, which each can hold 0, 1, or 2 triangles.
    let private splitTriangle (t : Triangle3d) (dim : int) (splitAt : double) (eps : double) (lbb : Box3d) (rbb : Box3d) : (Triangle3d[] * Triangle3d[]) =

        // signed dist to split plane for P0, P1 and P2
        let d0 = t.P0[dim] - splitAt
        let d1 = t.P1[dim] - splitAt
        let d2 = t.P2[dim] - splitAt

        // location with respect to split plane for P0, P1, P2
        // -1 .. left
        //  0 .. inside split plane (within eps)
        // +1 .. right
        let l0 = if d0 > eps then 1 elif d0 < -eps then -1 else 0
        let l1 = if d1 > eps then 1 elif d1 < -eps then -1 else 0
        let l2 = if d2 > eps then 1 elif d2 < -eps then -1 else 0
            
        // result helpers
        let completeTriangleIsLeft  () = ([|t|], Array.empty)
        let completeTriangleIsRight () = (Array.empty, [|t|])

        /// p is inside split plane, a and b are on opposite sides
        let oneVertexInsideSplitPlane (p : V3d) (a : V3d) (b : V3d) =
            let x = a[dim]
            let p' = a + (b - a) * ((splitAt - x)/(b[dim]-x))

            do
                let delta = p'[dim] - splitAt
                if abs delta > 1e-10 then 
                    printfn "[WARNING] Split plane precision issue: delta = %f" delta

            let aSide = [|Triangle3d(a, p', p)|]
            let bSide = [|Triangle3d(p', b, p)|]
            if x < splitAt then
                (aSide, bSide)
            else
                (bSide, aSide)

        /// p is on one side of split plane, and a and b are on the opposite side
        let fullSplit (p : V3d) (a : V3d) (b : V3d) =
            let x = p[dim]
            let s = splitAt - x
            let a' = p + (a - p) * (s/(a[dim]-x))
            if abs(a'[dim] - splitAt) > 1e-10 then 
                printfn "[WARNING] Split plane precision issue at a': delta = %f" (a'[dim] - splitAt)
            let b' = p + (b - p) * (s/(b[dim]-x))
            if abs(b'[dim] - splitAt) > 1e-10 then 
                printfn "[WARNING] Split plane precision issue at b': delta = %f" (b'[dim] - splitAt)
            let pSide = [|Triangle3d(p, a', b')|]
            let otherSide = [|Triangle3d(a', a,  b'); Triangle3d(b', a,  b)|]
            if x < splitAt then
                if not (lbb.Contains(pSide[0])) then 
                    printfn "[WARNING] Triangle outside expected bounding box (left)"
                if not (rbb.Contains(otherSide[0])) then 
                    printfn "[WARNING] Triangle outside expected bounding box (right)"
                if not (rbb.Contains(otherSide[1])) then 
                    printfn "[WARNING] Triangle outside expected bounding box (right)"
                (pSide, otherSide)
            else
                if not (lbb.Contains(otherSide[0])) then 
                    printfn "[WARNING] Triangle outside expected bounding box (left)"
                if not (lbb.Contains(otherSide[1])) then 
                    printfn "[WARNING] Triangle outside expected bounding box (left)"
                if not (rbb.Contains(pSide[0])) then 
                    printfn "[WARNING] Triangle outside expected bounding box (right)"
                (otherSide, pSide)

        if l0 < 1 && l1 < 1 && l2 < 1 then

            completeTriangleIsLeft ()

        elif l0 > -1 && l1 > -1 && l2 > -1 then

            completeTriangleIsRight ()

        else

            // info: points keep original winding in all function calls below
            match l0, l1, l2 with
            |  0, -1, +1 -> oneVertexInsideSplitPlane t.P0 t.P1 t.P2
            |  0, +1, -1 -> oneVertexInsideSplitPlane t.P0 t.P1 t.P2

            | -1,  0, +1 -> oneVertexInsideSplitPlane t.P1 t.P2 t.P0
            | +1,  0, -1 -> oneVertexInsideSplitPlane t.P1 t.P2 t.P0

            | -1, +1,  0 -> oneVertexInsideSplitPlane t.P2 t.P0 t.P1
            | +1, -1,  0 -> oneVertexInsideSplitPlane t.P2 t.P0 t.P1

            | -1, +1, +1 -> fullSplit t.P0 t.P1 t.P2
            | +1, -1, -1 -> fullSplit t.P0 t.P1 t.P2

            | +1, -1, +1 -> fullSplit t.P1 t.P2 t.P0
            | -1, +1, -1 -> fullSplit t.P1 t.P2 t.P0

            | +1, +1, -1 -> fullSplit t.P2 t.P0 t.P1
            | -1, -1, +1 -> fullSplit t.P2 t.P0 t.P1

            | _          -> failwith (sprintf "l0=%d, l1=%d, l2=%d. TODO 7559394f-d84f-4e54-95dc-62d941cba625." l0 l1 l2)


    let private getBoundingBoxOfTriangles (triangles : Triangle3d[]) : Box3d =
        let bb = Box3d.Invalid
        let ps = triangles.AsCastSpan<Triangle3d, V3d>()
        for i in 0 .. ps.Length-1 do bb.ExtendBy(ps[i])
        bb

    let mutable tsNextProgress = System.DateTime.Now
    let rec private build' (triangles : Triangle3d[]) (bb : Box3d) (progressRange : Range1d) : TriangleTree =
        
        if System.DateTime.Now > tsNextProgress then
            printfn "[PROGRESS] %10.8f" progressRange.Min
            tsNextProgress <- System.DateTime.Now.AddSeconds(1.0)

        //printfn "[BEGIN] %d" triangles.Length

        let mutable recomputeBounds = false
        let count = triangles.Length
        let triangles = triangles |> Array.filter (fun x -> not x.IsDegenerated)
        if count <> triangles.Length then
            recomputeBounds <- true
        //    printfn "removed %d degenerated triangles; %d remaining" (count - triangles.Length) triangles.Length
        //    //System.Diagnostics.Debugger.Break()
        
        let count = triangles.Length
        let triangles = triangles |> Array.distinct
        if count <> triangles.Length then
            recomputeBounds <- true
        //    printfn "removed %d duplicates; %d remaining" (count - triangles.Length) triangles.Length
        //    //System.Diagnostics.Debugger.Break()
        
        let count = triangles.Length
        let bb = if recomputeBounds then getBoundingBoxOfTriangles triangles else bb

        
        let normalDeviation =
            if count < 512 then
                let normals = triangles |> Array.map (fun t -> t.Normal)
                let avgNormal = normals |> Array.average
                let xs = normals |> Array.map (fun n -> n.Dot(avgNormal))
                let x = xs |> Array.average
                //printfn "[NORMAL] %10.8f" x
                x
            else
                0.0


        if count < 32 || normalDeviation > 0.95 (*|| (count < 128 && bb.Size[bb.MajorDim] > bb.Volume)*) then
        
            //if bb.Size[bb.MajorDim] > bb.Volume then printfn "**************   %d" count

            if count > 0 then
                Leaf(triangles, bb)
            else
                EmptyLeaf

        else
            
            let eps = 0.00001

            let splitDim = bb.MajorDim
            let splitAt = (bb.Min[splitDim] + bb.Max[splitDim]) * 0.5

            let (lbb, rbb) = bb.SplitDim(splitDim)
            if abs(lbb.Max[splitDim] - splitAt) > 1e-10 then 
                printfn "[WARNING] Left bounding box precision issue: delta = %f" (lbb.Max[splitDim] - splitAt)
            if abs(rbb.Min[splitDim] - splitAt) > 1e-10 then 
                printfn "[WARNING] Right bounding box precision issue: delta = %f" (rbb.Min[splitDim] - splitAt)

            let split = triangles |> Array.map (fun t -> splitTriangle t splitDim splitAt eps lbb rbb)
            let lts = split |> Array.collect fst
            let rts = split |> Array.collect snd

            let countAfterSplit = lts.Length + rts.Length
            let countAdditionalTriangles = countAfterSplit - count
            
            if countAfterSplit > count * 3 then
                printfn "[WARNING] Excessive triangle splitting: %10d -> %10d | %10d      %10d delta" count lts.Length rts.Length countAdditionalTriangles
                // Continue with degraded performance instead of crashing

            //if count > 100000 then
            //    printfn "[SPLIT] %10d -> %10d | %10d      %10d delta" count lts.Length rts.Length countAdditionalTriangles

            if lts.Length = 0 || rts.Length = 0 then
                //printfn "[ZERO ] %10d -> %10d | %10d      %10d delta" count lts.Length rts.Length countAdditionalTriangles
                Leaf(triangles, bb)
            else

                
                let progressRangeSplitAt = progressRange.Lerp(float(lts.Length) / float(countAfterSplit))
                let progressRangeLeft  = progressRange.SplitLeft(progressRangeSplitAt)
                let progressRangeRight = progressRange.SplitRight(progressRangeSplitAt)
                

                let lbb = getBoundingBoxOfTriangles lts
                let l = //build' lts lbb progressRangeLeft
                    if lbb.Volume > 0.01 then
                        build' lts lbb progressRangeLeft
                    else
                        if lts.Length > 16384 then printfn "L %d" lts.Length
                        Leaf(lts, lbb)

                let rbb = getBoundingBoxOfTriangles rts
                let r = //build' rts rbb progressRangeRight
                    if rbb.Volume > 0.01 then 
                        build' rts rbb progressRangeRight
                    else
                        if rts.Length > 16384 then printfn "R %d" lts.Length
                        Leaf(rts, rbb)
            
                //printfn "INNER %10d %10d %A %A" lts.Length rts.Length (lbb.Size.Round(5)) (rbb.Size.Round(5))

                Inner(l, r, bb)


    let rec build (triangles : Triangle3d[]) : TriangleTree =
        let result = build' triangles (getBoundingBoxOfTriangles triangles) Range1d.Unit
        printfn "[PROGRESS] %10.8f" 1.0
        result

    /// Returns absolute dist and t for nearest hit on ray (with respect to ray.Origin).
    let rec getNearestIntersection (tree : TriangleTree) (ray : Ray3d) : (float * float) option =
        
        match tree with

        | Inner (treeL, treeR, bounds) ->
            
            match bounds.Intersects(ray) with
            | (false, _) -> None
            | (true, _) ->
                let l = getNearestIntersection treeL ray
                let r = getNearestIntersection treeR ray

                match l, r with
                | None        , None         -> None
                | Some _      , None         -> l
                | None        , Some _       -> r
                | Some (dL, _), Some (dR, _) -> if dL < dR then l else r


        | Leaf (triangles, bounds)  ->
            match bounds.Intersects(ray) with
            | (true, _) ->
                let mutable bestDist = infinity
                let mutable bestT = nan
                for triangle in triangles do
                    let (isHit, t) = triangle.Intersects(ray)
                    if (isHit) then
                        let dist = abs t
                        if dist < bestDist then
                            bestDist <- dist
                            bestT <- t
            
                let result =
                    if isInfinity bestDist then
                        None 
                    else 
                        Some (bestDist, bestT)

                result
            | (false, _) -> None

        | EmptyLeaf -> None

