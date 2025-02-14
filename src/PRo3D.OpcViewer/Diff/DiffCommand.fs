namespace PRo3D.OpcViewer

open Argu
open Aardvark.Base
open System.Diagnostics

type TriangleTree =
    | Inner of leftBox: Box3d * rightBox: Box3d * left: TriangleTree * right: TriangleTree
    | Leaf of triangles: Triangle3d list

module TriangleTree =

    let private getBoundingBoxOfTriangles (triangles : Triangle3d list) : Box3d =
        Box3d(triangles |> List.collect (fun x -> [ x.P0; x.P1; x.P2 ]))

    let rec build (triangles : Triangle3d list) : TriangleTree =
        let count = triangles.Length

        if count < 32 then

            Leaf(triangles)

        else

            let bb = getBoundingBoxOfTriangles triangles
            let (lbb, rbb) = bb.SplitMajorDimension()
            let lts = triangles |> List.filter(lbb.Intersects) // triangles intersecting left box
            let rts = triangles |> List.filter(rbb.Intersects) // triangles intersecting right box

            if (lts.Length < count || rts.Length < count) then

                let lbb = getBoundingBoxOfTriangles lts
                let rbb = getBoundingBoxOfTriangles rts
                let l =
                    if lts.Length < count then
                        build lts 
                    else
                        if count > 128 then printfn "L %d" lts.Length
                        Leaf(lts)

                let r =
                    if rts.Length < count then
                        build rts
                    else
                       if count > 128 then printfn "R %d" rts.Length
                       Leaf(rts)

                Inner(lbb, rbb, l, r)

            else
                if count > 128 then printfn "  %d" count
                Leaf(triangles)

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

        

[<AutoOpen>]
module DiffCommand =

    type Args =
        | [<Mandatory                   >] Main    of dir: string
        | [<Mandatory                   >] Other   of dirs : string list
        | [<Unique                      >] NoValue of float
        | [<Unique;AltCommandLine("-v") >] Verbose

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Main    _ -> "directory containing the single main layer."
                | Other   _ -> "one or more directories containing layers to compare to the main layer."
                | NoValue _ -> "value used if no difference can be computed. Optional. Default is nan."
                | Verbose   -> "print more detailed info."

    let run (args : ParseResults<Args>) : int =

        let maindir = 
            match args.TryGetResult Args.Main with 
            | Some x -> x 
            | None -> printfn "[ERROR] No main layer specified."; exit 1

        let otherdirs =
            match args.TryGetResult Args.Other with 
            | Some x -> x
            | None -> printfn "[ERROR] No other layer(s) specified to compare the main layer with."; exit 1

        let novalue = args.GetResult(Args.NoValue, defaultValue = nan)

        let verbose = args.Contains(Args.Verbose)

        let layerMain = 
            let xs = Utils.searchLayerDir maindir
            match xs with
            | [ x ] -> x
            | _     ->
                printfn "[ERROR] Please specify exactly one main layer."
                printfn "[ERROR] I found %d layers in \"--main %s\":" xs.Length maindir
                let mutable i = 1
                for x in xs do
                    printfn "[ERROR] %4d. %s" i x.Path.FullName
                    i <- i + 1
                exit 1

        let otherLayers =
            Utils.searchLayerDirs otherdirs
            |> List.filter (fun x -> x.Path.FullName <> layerMain.Path.FullName)

        let layerOther = match otherLayers |> List.first with | Some x -> x | None -> failwith "no other layers"
            
        if verbose then
            printfn "computing difference between main layer and other layer(s)"
            printfn "layers : %s (main)" layerMain.Path.FullName
            for x in otherLayers do
                printfn "         %s (other)" x.Path.FullName
            printfn "novalue: %f" novalue
            printfn "verbose: true"

        // ... todo/wip

        let hierarchyMain = layerMain.LoadPatchHierarchy ()
        let hierarchyOther = layerOther.LoadPatchHierarchy ()

        let sky = Utils.getSky hierarchyMain

        let trianglesOtherWithNaN = Utils.getTriangles false hierarchyOther
        let trianglesOther = trianglesOtherWithNaN |> List.filter Utils.isValidTriangle

        let sw = Stopwatch.StartNew()
        let triangleTreeOther = TriangleTree.build trianglesOther
        sw.Stop()
        printfn "building tree ......... %A" sw.Elapsed

        let pointsMain = Utils.getPoints true hierarchyMain
        let mutable i = 0
        let mutable countHits = 0
        
        sw.Restart()
        for p in pointsMain do

            let ray = Ray3d(p, sky)
            let x = TriangleTree.getNearestIntersection triangleTreeOther ray
            
            i <- i + 1

            match x with
            | Some (dist, t) ->
                countHits <- countHits + 1
                //if i % 1000 = 0 then
                //    printfn "[%10d/%d][%d hits] hit dist %16.3f %16.3f" i pointsMain.Length countHits dist t
            | None ->
                //if i % 1000 = 0 then
                //    printfn "[%10d/%d][%d hits] no hit" i pointsMain.Length countHits
                ()
        sw.Stop()
        printfn "computing distances ... %A" sw.Elapsed

        printfn "%d hits / %d points" countHits pointsMain.Length 

        //do
        //    let nodes = Utils.traverse hierarchyOther.tree false
        //    for node in nodes do

        //        let ig, _ = Patch.load hierarchyOther.opcPaths ViewerModality.XYZ node.info

        //        let l2g = node.info.Local2Global
        //        let tp (p : V3f) : V3d = l2g.TransformPos (V3d(p))

        //        let ps =
        //            match ig.IndexedAttributes[DefaultSemantic.Positions] with
        //            | (:? array<V3f> as v) when not (isNull v) -> v
        //            | _ -> failwith ""

        //        let ps = ps |> Array.filter (fun p -> not p.IsNaN)

        //        for pLocal in ps do
        //            let p = tp pLocal
        //            let mutable isValid = false
        //            let mutable nearestAbs = infinity
        //            let mutable nearest = 0.0
        //            let ray = Ray3d(p, sky)
        //            for t in trianglesMain do
        //                let (isHit, dist) = t.Intersects(ray)
        //                if isHit then
        //                    isValid <- true
        //                    let distAbs = System.Math.Abs(dist)
        //                    if distAbs < nearestAbs then
        //                        nearestAbs <-distAbs
        //                        nearest <- dist

        //            if isValid then
        //                printfn "%-64s %A" (sprintf "%A" p) nearest
            

        0