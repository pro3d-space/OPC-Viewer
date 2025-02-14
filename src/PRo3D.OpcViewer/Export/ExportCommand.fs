namespace PRo3D.OpcViewer

open Argu
open System.IO

[<AutoOpen>]
module ExportCommand =

    type ExportFormat = Pts | Ply

    type Args =
        | [<MainCommand>] DataDir of datadir : string
        | [<Mandatory>] Format of ExportFormat
        | [<Mandatory>] Out of outfile : string

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | DataDir _ -> "specify data directory"
                | Format  _ -> "specify export format"
                | Out _     -> "specify export file name"

    let run (args : ParseResults<Args>) : int =

        let datadir = args.GetResult Args.DataDir
        let format  = args.GetResult Args.Format
        
        let mutable outfile = args.GetResult Args.Out

        // discover all layers in datadirs ...
        let layers =
            Utils.searchLayerDir datadir
            |> List.sortBy (fun x -> x.Path.FullName)

        let ensureExtension (ext : string) (path : string) : string =
            if (not (Path.HasExtension(outfile))) || Path.GetExtension(outfile) <> ext then
                sprintf "%s.pts" (Path.GetFileNameWithoutExtension(path))
            else
                path

        match format with

        | ExportFormat.Pts ->
            outfile <- ensureExtension ".pts" outfile

            let mutable totalPointCount = 0
            use f = new StreamWriter(outfile)

            for layer in layers do
                let ps = layer.GetPoints true
                totalPointCount <- totalPointCount + ps.Length
                sprintf "%d" ps.Length |> f.WriteLine
                for p in ps do sprintf "%f %f %f" p.X p.Y p.Z |> f.WriteLine

            printfn "wrote %d points to %s" totalPointCount outfile

        | ExportFormat.Ply ->
            outfile <- ensureExtension ".ply" outfile
            failwith "TODO 33be375b-3bec-41ae-bd0e-c7ad04106d15."

        0