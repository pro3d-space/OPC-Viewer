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

[<AutoOpen>]
module DiffCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | [<Unique                      >] NoValue of float
        | [<Unique                      >] Speed   of float
        | [<Unique;AltCommandLine("-v") >] Verbose
        | [<AltCommandLine("-s")        >] Sftp of string
        | [<AltCommandLine("-b")        >] BaseDir of string

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "Two datasets to compare."
                | NoValue  _ -> "value used if no difference can be computed. Optional. Default is nan."
                | Speed    _ -> "optional camera controller speed"
                | Verbose    -> "print more detailed info."
                | Sftp     _ -> "optional SFTP server config file (FileZilla format)"
                | BaseDir  _ -> "optional base directory for relative paths (default is ./data)"

    let run (args : ParseResults<Args>) : int =

        let novalue = args.GetResult(Args.NoValue, defaultValue = nan)

        let verbose = args.Contains(Args.Verbose)

        let sftpServerConfig = args.TryGetResult(Args.Sftp) |> Option.map Sftp.parseFileZillaConfigFile

        let basedir =
            match args.TryGetResult(Args.BaseDir) with
            | Some s -> s
            | None -> System.IO.Path.Combine(System.Environment.CurrentDirectory, "data")

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x
            | None ->
                printfn "[WARNING] no data directories specified"
                exit 0
                []

        let dataRefs = datadirs |> List.map Data.getDataRefFromString

        let resolve = Data.resolveDataPath basedir sftpServerConfig
        let resolvedResults = dataRefs |> List.map resolve

        let datadirs = resolvedResults |> List.map (fun x ->
            match x with
            | ResolveDataPathResult.Ok ok -> ok
            | ResolveDataPathResult.MissingSftpConfig uri ->
                printfn "Use --sftp|-s do specify SFTP config for %A" uri
                exit 1
            | ResolveDataPathResult.DownloadError (uri, e) ->
                printfn "%A: %A" uri e
                exit 1
            | ResolveDataPathResult.InvalidDataDir s ->
                printfn "invalid data dir: %A" s
                exit 1
            )

        let layers = datadirs |> List.collect Data.searchLayerDir

        if layers.Length <> 2 then
            printfn "[ERROR] Please specify exactly 2 datasets to compare."
            printfn "[ERROR] You specified:"
            for layer in layers do
                printfn "[ERROR]   %s" layer.Path.FullName
            exit 1

        let layerMain = layers[0]
        let layerOther = layers[1]
            
        if verbose then
            printfn "computing difference between"
            printfn "  %s" layerMain.Path.FullName
            printfn "  %s" layerOther.Path.FullName
            printfn "novalue: %f" novalue
            printfn "verbose: true"

        let hierarchyMain = layerMain.LoadPatchHierarchy ()
        let hierarchyOther = layerOther.LoadPatchHierarchy ()

        let sky = Utils.getSky hierarchyMain
        let ground = Plane3d(sky, V3d.Zero)
        let w2p = ground.GetWorldToPlane()

        let trianglesOtherWithNaN = Utils.getTriangles false hierarchyOther
        let trianglesOther = trianglesOtherWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let sw = Stopwatch.StartNew()
        
        //let foo = trianglesOther |> Array.filter (fun t ->
        //    let e0 = t.Edge01.Length
        //    let e1 = t.Edge12.Length
        //    let e2 = t.Edge20.Length
        //    e0 < e1 * 2.0 && e0 < e2 * 2.0
        //    )
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
                //printfn "%s" line
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