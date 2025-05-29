namespace PRo3D.OpcViewer

open Argu

[<AutoOpen>]
module ListCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list
        | [<Unique;AltCommandLine("-s") >] Stats

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"
                | Stats     -> "show detail info"

    let run (args : ParseResults<Args>) : int =

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x |> List.map DataDir.ofString
            | None ->
                printfn "[WARNING] no data directories specified"
                exit 0
                []

        let showStats = args.Contains(Args.Stats)

        // discover all layers in datadirs ...
        let layerInfos =
            Data.searchLayerDirs datadirs
            |> List.sortBy (fun x -> x.Path.FullName)

        for info in layerInfos do
            printfn "%s" info.Path.FullName
            if showStats then Utils.printLayerInfo info

        0