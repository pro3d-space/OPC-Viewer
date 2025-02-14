namespace PRo3D.OpcViewer

open Argu

[<AutoOpen>]
module InfoCommand =

    type Args =
        | [<MainCommand>] DataDir of data_dir: string

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDir _ -> "specify data directory"

    let run (args : ParseResults<Args>) : int =

        let datadir = 
            match args.TryGetResult Args.DataDir with 
            | Some x -> x 
            | None ->
                printfn "[WARNING] no data directory specified"
                exit 0

        // discover all layers in datadirs ...
        let layerInfos =
            Utils.searchLayerDir datadir
            |> List.sortBy (fun x -> x.Path.FullName)

        for info in layerInfos do
            Utils.printLayerInfo info

        0