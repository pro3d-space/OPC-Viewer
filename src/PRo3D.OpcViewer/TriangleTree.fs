namespace PRo3D.OpcViewer

open Aardvark.Base


type TriangleTree =
    | Inner of boundsLeft  : Box3d * boundsRight : Box3d * left : TriangleTree * right : TriangleTree
    | Leaf of triangles : Triangle3d[]
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
                if delta <> 0.0 then System.Diagnostics.Debugger.Break()                        // PARANOID

            let aSide = [|Triangle3d(a, p', p)|]
            let bSide = [|Triangle3d(p', b, p)|]
            if x < splitAt then
                if not (lbb.Contains(aSide[0])) then System.Diagnostics.Debugger.Break()        // PARANOID
                if not (rbb.Contains(bSide[0])) then System.Diagnostics.Debugger.Break()        // PARANOID
                (aSide, bSide)
            else
                if not (lbb.Contains(bSide[0])) then System.Diagnostics.Debugger.Break()        // PARANOID
                if not (rbb.Contains(aSide[0])) then System.Diagnostics.Debugger.Break()        // PARANOID
                (bSide, aSide)

        /// p is on one side of split plane, and a and b are on the opposite side
        let fullSplit (p : V3d) (a : V3d) (b : V3d) =
            let x = p[dim]
            let s = splitAt - x
            let a' = p + (a - p) * (s/(a[dim]-x))
            if a'[dim] - splitAt <> 0.0 then System.Diagnostics.Debugger.Break()                // PARANOID
            let b' = p + (b - p) * (s/(b[dim]-x))
            if b'[dim] - splitAt <> 0.0 then System.Diagnostics.Debugger.Break()                // PARANOID
            let pSide = [|Triangle3d(p, a', b')|]
            let otherSide = [|Triangle3d(a', a,  b'); Triangle3d(b', a,  b)|]
            if x < splitAt then
                if not (lbb.Contains(pSide[0])) then System.Diagnostics.Debugger.Break()        // PARANOID
                if not (rbb.Contains(otherSide[0])) then System.Diagnostics.Debugger.Break()    // PARANOID
                if not (rbb.Contains(otherSide[1])) then System.Diagnostics.Debugger.Break()    // PARANOID
                (pSide, otherSide)
            else
                if not (lbb.Contains(otherSide[0])) then System.Diagnostics.Debugger.Break()    // PARANOID
                if not (lbb.Contains(otherSide[1])) then System.Diagnostics.Debugger.Break()    // PARANOID
                if not (rbb.Contains(pSide[0])) then System.Diagnostics.Debugger.Break()        // PARANOID
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

    let rec private build' (triangles : Triangle3d[]) (bb : Box3d) : TriangleTree =
        
        //printfn "[BEGIN] %d" triangles.Length

        let count = triangles.Length
        let triangles = triangles |> Array.filter (fun x -> not x.IsDegenerated)
        //if count <> triangles.Length then
        //    printfn "removed %d degenerated triangles; %d remaining" (count - triangles.Length) triangles.Length
        //    //System.Diagnostics.Debugger.Break()
        
        let count = triangles.Length
        let triangles = triangles |> Array.distinct
        //if count <> triangles.Length then
        //    printfn "removed %d duplicates; %d remaining" (count - triangles.Length) triangles.Length
        //    //System.Diagnostics.Debugger.Break()
        
        let count = triangles.Length

        if count < 32 then
        
            //printfn "LEAF  %10d" count
            Leaf(triangles)

        else
            
            let eps = 0.0000000001                                                                          // NEW IMPLEMENTATION

            let splitDim =
                //printfn "count = %d" count
                //for i = 0 to 2 do
                [0..2]
                |> List.map (fun i ->
                    let splitAt = (bb.Min[i] + bb.Max[i]) * 0.5
                    let (lbb, rbb) = bb.SplitDim(i)   
                    let split = triangles |> Array.map (fun t -> splitTriangle t i splitAt eps lbb rbb)
                    let lts = split |> Array.collect fst
                    let rts = split |> Array.collect snd

                    let countAfterSplit = lts.Length + rts.Length
                    let countAdditionalTriangles = countAfterSplit - count
                    //printfn "dim=%d delta triangles = %d" i countAdditionalTriangles
                    (i, countAdditionalTriangles)
                    )
                |> List.minBy (fun (_, delta) -> delta)
                |> fst

            //let majorDim = bb.MajorDim                                                                      // NEW IMPLEMENTATION
            let splitAt = (bb.Min[splitDim] + bb.Max[splitDim]) * 0.5                                       // NEW IMPLEMENTATION

            let (lbb, rbb) = bb.SplitDim(splitDim)                                                       // OLD IMPLEMENTATION
            if lbb.Max[splitDim] <> splitAt then System.Diagnostics.Debugger.Break()                        // PARANOID
            if rbb.Min[splitDim] <> splitAt then System.Diagnostics.Debugger.Break()                        // PARANOID
            //let lts = triangles |> Array.filter(lbb.Intersects) // triangles intersecting left box        // OLD IMPLEMENTATION
            //let rts = triangles |> Array.filter(rbb.Intersects) // triangles intersecting right box       // OLD IMPLEMENTATION

            let split = triangles |> Array.map (fun t -> splitTriangle t splitDim splitAt eps lbb rbb)      // NEW IMPLEMENTATION
            let lts = split |> Array.collect fst                                                            // NEW IMPLEMENTATION
            let rts = split |> Array.collect snd                                                            // NEW IMPLEMENTATION

            let countAfterSplit = lts.Length + rts.Length
            let countAdditionalTriangles = countAfterSplit - count
            
            if count > 10000 then
                printfn "[SPLIT] %10d -> %10d | %10d      %10d delta" count lts.Length rts.Length countAdditionalTriangles

            do                                                                                              // PARANOID
                let ltsOutside = lts |> Array.filter (fun t -> not (lbb.Contains(t)))                       // PARANOID
                let rtsOutside = rts |> Array.filter (fun t -> not (rbb.Contains(t)))                       // PARANOID
                if ltsOutside.Length > 0 then
                    let t = ltsOutside[0]
                    let debug0 = lbb.Distance(t.P0)
                    let debug1 = lbb.Distance(t.P1)
                    let debug2 = lbb.Distance(t.P2)
                    System.Diagnostics.Debugger.Break() 
                    failwith "ba70f09f-f332-41a5-a485-d35d94ccbf7c"
                if rtsOutside.Length > 0 then
                    let t = rtsOutside[0]
                    let debug0 = rbb.Distance(t.P0)
                    let debug1 = rbb.Distance(t.P1)
                    let debug2 = rbb.Distance(t.P2)
                    System.Diagnostics.Debugger.Break()  
                    failwith "181e03af-df38-433c-b81f-7993423b8bd2"
                ()

            //printfn "SPLIT  %10d %10d" lts.Length rts.Length
            //if lts.Length = 593 && rts.Length = 0 then System.Diagnostics.Debugger.Break()

            let lbb = getBoundingBoxOfTriangles lts
            let l = 
                if lbb.Volume > 0.1 then
                    build' lts lbb
                else
                    if lts.Length > 10000 then printfn "L %d" lts.Length
                    Leaf(lts)

            let rbb = getBoundingBoxOfTriangles rts
            let r =
                if rbb.Volume > 0.1 then 
                    build' rts rbb
                else
                    if rts.Length > 10000 then printfn "R %d" lts.Length
                    Leaf(rts)
            
            //printfn "INNER %10d %10d %A %A" lts.Length rts.Length (lbb.Size.Round(5)) (rbb.Size.Round(5))
            //if lts.Length = 67 && rts.Length = 120 then System.Diagnostics.Debugger.Break()
            Inner(lbb, rbb, l, r)


    let rec build (triangles : Triangle3d[]) : TriangleTree =
        build' triangles (getBoundingBoxOfTriangles triangles)

    /// Returns absolute dist and t for nearest hit on ray (with respect to ray.Origin).
    let rec getNearestIntersection (tree : TriangleTree) (ray : Ray3d) : (float * float) option =
        
        match tree with

        | Inner (boxL, boxR, treeL, treeR) ->
            
            let (hitL, tL) = boxL.Intersects(ray)
            let (hitR, tR) = boxR.Intersects(ray)

            let l = if hitL then getNearestIntersection treeL ray else None
            let r = if hitR then getNearestIntersection treeR ray else None

            match l, r with
            | None        , None         -> None
            | Some _      , None         -> l
            | None        , Some _       -> r
            | Some (dL, _), Some (dR, _) -> if dL < dR then l else r


        | Leaf triangles  ->
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

        | EmptyLeaf -> None

