namespace PRo3D.OpcViewer

open Argu
open Aardvark.Base
open Aardvark.GeoSpatial.Opc
open System.Collections.Generic
open System.Diagnostics
open System.IO
open Aardvark.Opc
open Aardvark.Opc.DiffRendering



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

        let trianglesMainWithNaN = Utils.getTriangles false hierarchyMain
        let trianglesMain = trianglesMainWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let trianglesOtherWithNaN = Utils.getTriangles false hierarchyOther
        let trianglesOther = trianglesOtherWithNaN |> List.filter Utils.isValidTriangle |> Array.ofList

        let sw = Stopwatch.StartNew()
        
        //do
        //    let groups =
        //        trianglesOther
        //        |> Seq.collect (fun t -> [(t.P0, t); (t.P1, t); (t.P2, t)])
        //        |> Seq.groupBy fst
        //        |> Seq.map (fun (k,v) -> (k, v |> Seq.map snd |> Seq.toList))
        //        |> Seq.sortByDescending (fun (k, v) -> v.Length)
        //        |> Seq.toList

        //    printfn "groups: %d" groups.Length
        //    for (k, ts) in groups do
        //        printfn "  group (%d entries) with key %A" ts.Length (k.Round(3))
        //        for t in ts do
        //            printfn "    %A | %A | %A" (t.P0.Round(3)) (t.P1.Round(3)) (t.P2.Round(3))

        //    exit 1
        //    ()

        let triangleTreeMain  = TriangleTree.build trianglesMain
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

        if false then // debug
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
                patchHierarchies = Seq.delay (fun _ -> [layerMain; layerOther] |> Seq.map (fun info -> info.Path.FullName))
                boundingBox      = gbb
                near             = initialCam.Near
                far              = initialCam.Far
                speed            = speed
                lodDecider       = LodDecider.lodDeciderMars Trafo3d.Identity //DefaultMetrics.mars2 
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

        DiffViewer.run scene initialCam.CameraView computeColor triangleTreeMain triangleTreeOther

        //0