namespace PRo3D.OpcViewer

open Argu
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.Rendering

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

        let mainLayer = 
            let xs = LayerUtils.searchLayerDir maindir
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
            LayerUtils.searchLayerDirs otherdirs
            |> List.filter (fun x -> x.Path.FullName <> mainLayer.Path.FullName)

        let otherLayer = match otherLayers |> List.first with | Some x -> x | None -> failwith "no other layers"
            
        if verbose then
            printfn "computing difference between main layer and other layer(s)"
            printfn "layers : %s (main)" mainLayer.Path.FullName
            for x in otherLayers do
                printfn "         %s (other)" x.Path.FullName
            printfn "novalue: %f" novalue
            printfn "verbose: true"

        // ... todo/wip

        let mainRoot = mainLayer.LoadPatchHierarchy ()
        let mainRootPatch = match mainRoot.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        let mainGbb = mainRootPatch.info.GlobalBoundingBox
        let mainGlobalSky = mainGbb.Center.Normalized

        let mainTriangles =
            
            let sw = System.Diagnostics.Stopwatch.StartNew()

            //let mutable psAll = List.empty<V3f>
            let mutable triangles = List.empty<Triangle3d>
            let mutable totalTriangleCount = 0
            let leafs = LayerUtils.traverse mainRoot.tree false
            for n in leafs do
                // printfn "%A" n
                let ig, _ = Patch.load mainRoot.opcPaths ViewerModality.XYZ n.info

                totalTriangleCount <- totalTriangleCount + ig.FaceCount

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

                let l2g = n.info.Local2Global
                let tp (p : V3f) : V3d = l2g.TransformPos (V3d(p))
                let transform (t : Triangle3f) : Triangle3d = Triangle3d(tp t.P0, tp t.P1, tp t.P2)

                for i in 0 .. 3 ..  ia.Length - 3 do
                    let t = Triangle3f(ps[ia[i]], ps[ia[i + 1]], ps[ia[i + 2]]) // local space
                    let r = Ray3f.Invalid
                    let (isHit, x) = t.Intersects(r)
                    let isNan = t.P0.IsNaN || t.P1.IsNaN || t.P2.IsNaN
                    match isNan with
                    | true -> ()
                    | false -> triangles <- transform t :: triangles

                //psAll <- ps |> List.ofArray |> List.append psAll
                ()
            
            sw.Stop()
            printfn "%A" sw.Elapsed
            printfn "dismissed %s triangles (because NaN)" ((totalTriangleCount - triangles.Length).ToString("N0"))
            printfn "there are %s triangles left" (triangles.Length.ToString("N0"))

            triangles

        
        let otherRoot = otherLayer.LoadPatchHierarchy ()
        //let otherRoot = mainRoot
        do
            let sky = mainGlobalSky
            let leafs = LayerUtils.traverse otherRoot.tree false
            for n in leafs do
                let ig, _ = Patch.load otherRoot.opcPaths ViewerModality.XYZ n.info

                let l2g = n.info.Local2Global
                let tp (p : V3f) : V3d = l2g.TransformPos (V3d(p))

                let ps = 
                    match ig.IndexedAttributes[DefaultSemantic.Positions] with
                    | (:? array<V3f> as v) when not (isNull v) -> v
                    | _ -> failwith ""

                let ps = ps |> Array.filter (fun p -> not p.IsNaN)

                for pLocal in ps do
                    let p = tp pLocal
                    let mutable isValid = false
                    let mutable nearestAbs = infinity
                    let mutable nearest = 0.0
                    let ray = Ray3d(p - sky * 65536.0, sky)
                    for t in mainTriangles do
                        let (isHit, dist) = t.Intersects(ray)
                        if isHit then
                            isValid <- true
                            let distAbs = System.Math.Abs(dist)
                            if distAbs < nearestAbs then
                                nearestAbs <-distAbs
                                nearest <- dist

                    if isValid then
                        printfn "%A %A" p nearest
            

        //printfn "count positions: %d" psAll.Length

        //psAll <- psAll |> List.filter (fun p -> not p.IsNaN)
        
        //printfn "count positions: %d" psAll.Length
        

        //let patch = match mainRoot.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n

        //let ig, _ = Patch.load mainRoot.opcPaths ViewerModality.XYZ patch.info
        //let positions = 
        //    match ig.IndexedAttributes[DefaultSemantic.Positions] with
        //    | (:? array<V3f> as v) when not (isNull v) -> v
        //    | _ -> failwith "[Queries] Patch has no V3f[] positions"

        //mainLayer.PrintInfo ()

        0