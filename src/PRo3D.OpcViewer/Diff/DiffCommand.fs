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
            
        if verbose then
            printfn "computing difference between main layer and other layer(s)"
            printfn "layers : %s (main)" mainLayer.Path.FullName
            for x in otherLayers do
                printfn "         %s (other)" x.Path.FullName
            printfn "novalue: %f" novalue
            printfn "verbose: true"

        // ... todo/wip

        let mainRoot = mainLayer.LoadPatchHierarchy ()

        let patch = match mainRoot.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        let ig, _ = Patch.load mainRoot.opcPaths ViewerModality.XYZ patch.info
        let positions = 
            match ig.IndexedAttributes[DefaultSemantic.Positions] with
            | (:? array<V3f> as v) when not (isNull v) -> v
            | _ -> failwith "[Queries] Patch has no V3f[] positions"

        mainLayer.PrintInfo ()

        0