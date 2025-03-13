namespace PRo3D.OpcViewer

open Argu
open Aardvark.Base
open Aardvark.GeoSpatial.Opc
open System.Collections.Generic
open System.Diagnostics
open System.IO
open Aardvark.Opc

type TriangleTree =
    | Inner of boundsLeft  : Box3d * boundsRight : Box3d * left : TriangleTree * right : TriangleTree
    | Leaf of triangles : Triangle3d[]

module TriangleTree =

    let private getBoundingBoxOfTriangles (triangles : Triangle3d[]) : Box3d =
        let bb = Box3d.Invalid
        let ps = triangles.AsCastSpan<Triangle3d, V3d>()
        for i in 0 .. ps.Length-1 do bb.ExtendBy(ps[i])
        bb

    let rec private build' (triangles : Triangle3d[]) (bb : Box3d) : TriangleTree =
        let count = triangles.Length

        if count < 32 then

            Leaf(triangles)

        else

            //let bb = getBoundingBoxOfTriangles triangles
            let (lbb, rbb) = bb.SplitMajorDimension()
            let lts = triangles |> Array.filter(lbb.Intersects) // triangles intersecting left box
            let rts = triangles |> Array.filter(rbb.Intersects) // triangles intersecting right box

            if (lts.Length < count || rts.Length < count) then

                let lbb = getBoundingBoxOfTriangles lts
                let rbb = getBoundingBoxOfTriangles rts

                let l =
                    if lts.Length < count then
                        build' lts lbb
                    else
                        if count > 128 then printfn "L %d" lts.Length
                        Leaf(lts)

                let r =
                    if rts.Length < count then
                        build' rts rbb
                    else
                       if count > 128 then printfn "R %d" rts.Length
                       Leaf(rts)

                Inner(lbb, rbb, l, r)

            else
                if count > 128 then printfn "  %d" count
                Leaf(triangles)

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

        

[<AutoOpen>]
module DiffCommand =

    type Args =
        | [<Mandatory                   >] Main    of dir: string
        | [<Mandatory                   >] Other   of dirs : string list
        | [<Unique                      >] NoValue of float
        | [<Unique                      >] Speed   of float
        | [<Unique;AltCommandLine("-v") >] Verbose

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Main    _ -> "directory containing the single main layer."
                | Other   _ -> "one or more directories containing layers to compare to the main layer."
                | NoValue _ -> "value used if no difference can be computed. Optional. Default is nan."
                | Speed   _ -> "optional camera controller speed"
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
        let ground = Plane3d(sky, V3d.Zero)
        let w2p = ground.GetWorldToPlane()

        let trianglesOtherWithNaN = Utils.getTriangles false hierarchyOther
        let trianglesOther = trianglesOtherWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let sw = Stopwatch.StartNew()
        //let bb = (Utils.getRootPatch hierarchyOther).info.GlobalBoundingBox
        let triangleTreeOther = TriangleTree.build trianglesOther
        sw.Stop()
        printfn "building tree ......... %A" sw.Elapsed

        let pointsMain = Utils.getPoints true hierarchyMain
        let gbb = Box3d(pointsMain)
        let mutable i = 0
        let mutable countHits = 0
        
        sw.Restart()
        let rangeDist = Range1d.Invalid
        let rangeT = Range1d.Invalid
        let mutable qs = List.empty<(V3d*V3d*float)>
        for pGlobal in pointsMain do

            let ray = Ray3d(pGlobal, sky)
            let x = TriangleTree.getNearestIntersection triangleTreeOther ray
            
            i <- i + 1

            match x with
            | Some (dist, t) ->
                countHits <- countHits + 1
                let g = ray.Intersect(ground)
                let p = g + sky * t
                let p' = w2p.TransformPos p
                

                qs <- (pGlobal, p', t) :: qs
                rangeDist.ExtendBy(dist)
                rangeT.ExtendBy(t)
                //if i % 1000 = 0 then
                //    printfn "[%10d/%d][%d hits] hit dist %16.3f %16.3f" i pointsMain.Length countHits dist t
                ()
            | None ->
                //if i % 1000 = 0 then
                //    printfn "[%10d/%d][%d hits] no hit" i pointsMain.Length countHits
                ()
        sw.Stop()
        printfn "computing distances ... %A" sw.Elapsed

        printfn "%d hits / %d points" countHits pointsMain.Length
        printfn "range dist: %A" rangeDist
        printfn "range T   : %A" rangeT

        let p2c = Dictionary<V3d,C3b>()

        do
            let outfile = @"E:\qs.pts"

            let max = max (abs rangeT.Min) (abs rangeT.Max)

            use f = new StreamWriter(outfile)
            for (pGlobal,p,t) in qs do
                let w = float32(t / max)
                let c =
                    if w < 0.0f then
                        let w = -w
                        C3b(C3f.Blue * w + C3f.White * (1.0f - w))
                    else
                        C3b(C3f.Red * w + C3f.White * (1.0f - w))

                p2c[pGlobal] <- c

                sprintf "%f %f %f %i %i %i" p.X p.Y p.Z c.R c.G c.B |> f.WriteLine

            printfn "exported diff point cloud to %s" outfile

            use fHisto = new StreamWriter(outfile + ".csv")
            let histo = qs |> Seq.groupBy (fun (_,_,t) -> int(t*1000.0)) |> Seq.map (fun (key, xs) -> (key, xs |> Seq.length)) |> Seq.sortBy (fun (k,_) -> k) |> Seq.toList
            for (k,count) in histo do
                let line = sprintf "%i,%i" k count
                printfn "%s" line
                fHisto.WriteLine line

            ()

        // create OpcScene ...
        let initialCam = Utils.createInitialCameraView gbb
        let speed = args.GetResult(Speed, defaultValue = initialCam.Far / 64.0)
        let scene =
            { 
                useCompressedTextures = true
                preTransform     = Trafo3d.Identity
                patchHierarchies = Seq.delay (fun _ -> [layerMain] |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = initialCam.Near
                far              = initialCam.Far
                speed            = speed
                lodDecider       = DefaultMetrics.mars2 
            }

        // ... and show it
        
        let max = max (abs rangeT.Min) (abs rangeT.Max)
        let computeColor (mode : DistanceComputationMode) (p : V3d) : C3b =

            match mode with
            | DistanceComputationMode.Nearest -> C3b.Red
            | _ -> 
                match (false, C3b.White) (*p2c.TryGetValue(p)*) with
                | (true, c) when false -> 
                    //printfn "haha %A" c
                    c
                | _ ->

                    let ray = Ray3d(p, sky)
                    let x = TriangleTree.getNearestIntersection triangleTreeOther ray
                    match x with
                    | Some (dist, t) ->
                        //printfn "%A" t
                        let w = float32(t / max)
                        let c =
                            if w < 0.0f then
                                let w = -w
                                C3b(C3f.Blue * w + C3f.White * (1.0f - w))
                            else
                                C3b(C3f.Red * w + C3f.White * (1.0f - w))
                        c
                    | None ->
                        C3b.GreenYellow

        DiffViewer.run scene initialCam.CameraView computeColor

        //0