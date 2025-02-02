namespace PRo3D.OpcViewer

open Argu

[<AutoOpen>]
module ListCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | [<Unique;AltCommandLine("-d") >] Detail

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"
                | Detail     -> "show detail info"

    let run (args : ParseResults<Args>) : int =

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x 
            | None ->
                printfn "[WARNING] no data directories specified"
                exit 0
                []

        let showDetails = args.Contains(Args.Detail)

        // discover all layers in datadirs ...
        let layerInfos =
            LayerUtils.searchLayerDirs datadirs
            |> List.sortBy (fun x -> x.Path.FullName)

        for info in layerInfos do
            printfn "%s" info.Path.FullName

            if showDetails then
                let f (i : int) = i.ToString("N0")

                let stats = info |> LayerUtils.loadPatchHierarchy |> LayerUtils.getPatchHierarchyStats
                let countNodes = stats.CountInnerNodes + stats.CountLeafNodes
                if stats.IndexedAttributes.Length > 0 then
                    let s = String.concat ", " (stats.IndexedAttributes |> Array.map(fun x -> x.ToString()))
                    printfn "    %s" s
                if stats.SingleAttributes.Length > 0 then
                    let s = String.concat ", " (stats.SingleAttributes |> Array.map(fun x -> x.ToString()))
                    printfn "    %s" s

                printfn "    %12s nodes (%s leafs, %s inner)" (f(countNodes)) (f(stats.CountLeafNodes)) (f(stats.CountInnerNodes))
                printfn "    %12s vertices %12s faces" (f(stats.CountVertices)) (f(stats.CountFaces))

        0