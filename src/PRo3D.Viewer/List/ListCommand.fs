namespace PRo3D.Viewer

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
            | None -> []
                
        if datadirs.IsEmpty then
            printfn "[WARNING] no data directories specified"
            0
        else
            let showStats = args.Contains(Args.Stats)

            // discover all layers in datadirs ...
            let layerInfos =
                Data.searchLayerDirs datadirs
                |> List.sortBy (fun x -> x.Path.FullName)

            for info in layerInfos do
                printfn "%s" info.Path.FullName
                if showStats then Utils.printLayerInfo info

            0