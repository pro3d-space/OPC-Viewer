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

        let layerMain = mainLayer.LoadPatchHierarchy ()
        let layerOther = otherLayer.LoadPatchHierarchy ()

        let sky = LayerUtils.getSky layerMain

        let trianglesMainWithNaN = LayerUtils.getTriangles false layerMain
        let trianglesMain = trianglesMainWithNaN |> List.filter LayerUtils.isValidTriangle

        

        do
            let nodes = LayerUtils.traverse layerOther.tree false
            for node in nodes do

                let ig, _ = Patch.load layerOther.opcPaths ViewerModality.XYZ node.info

                let l2g = node.info.Local2Global
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
                    let ray = Ray3d(p, sky)
                    for t in trianglesMain do
                        let (isHit, dist) = t.Intersects(ray)
                        if isHit then
                            isValid <- true
                            let distAbs = System.Math.Abs(dist)
                            if distAbs < nearestAbs then
                                nearestAbs <-distAbs
                                nearest <- dist

                    if isValid then
                        printfn "%-64s %A" (sprintf "%A" p) nearest
            

        0