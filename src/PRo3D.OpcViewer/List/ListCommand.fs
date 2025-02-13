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
            if showDetails then LayerUtils.printLayerInfo info

        0