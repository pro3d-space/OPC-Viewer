namespace PRo3D.OpcViewer

open Argu

[<AutoOpen>]
module ListCommand =

    type Args =
        | [<MainCommand>] DataDirs of data_dir: string list

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDirs _ -> "specify data directories"


    let run (args : ParseResults<Args>) : int =

        let datadirs = 
            match args.TryGetResult Args.DataDirs with 
            | Some x -> x 
            | None ->
                printfn "[WARNING] no data directories specified"
                exit 0
                []

        // discover all layers in datadirs ...
        let layerInfos = LayerManagement.searchLayerDirs datadirs

        // print ...
        for x in layerInfos do printfn "[layer] %s" x.Path.FullName

        0