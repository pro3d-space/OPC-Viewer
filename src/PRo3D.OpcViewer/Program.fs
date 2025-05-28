open Argu
open PRo3D.OpcViewer
open PRo3D.OpcViewer.Data
open System.IO

[<EntryPoint>]
let main argv =

    //do
    //    let json = File.ReadAllText(@"W:\Datasets\Pro3D\confidential\Vaughan-Boulders-2025\Vaughan-Boulders-2025.pro3d.json")
    //    let pro3d = Pro3DFile.parse json

    //    let skipPrefix = @"D:\M20\"
    //    let xs =
    //        pro3d.SurfaceModel.Surfaces.Flat
    //        |> Array.collect (fun x -> x.Surfaces.OpcPaths)
    //        |> Array.map (fun x ->
    //            if not (x.StartsWith(skipPrefix)) then printfn "  [ERROR] path %s does not start with %s" x skipPrefix
    //            x.Replace('\\', '/').Substring(skipPrefix.Length)
    //            )
    //        |> Array.sort
        
    //    for x in xs do
    //        printfn "%s" x

    //    System.Environment.Exit(0)

    let PROGRAM_NAME = "PRo3D.OpcViewer"
    let VERSION      = "1.0.1"

    let parser = ArgumentParser.Create<CliArguments>(programName = PROGRAM_NAME)

    let arguments : ParseResults<CliArguments> =
        try
            parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        with :? ArguParseException as e ->
            eprintfn "%s" e.Message
            exit 1
    
    match arguments.GetAllResults() with
    | [Diff x]   -> DiffCommand.run x
    | [Export x] -> ExportCommand.run x
    | [Info x]   -> InfoCommand.run x
    | [List x]   -> ListCommand.run x
    | [View x]   -> ViewCommand.run x
    | [Version]  -> printfn "%s" VERSION; exit 0
    | _          -> printfn "%s" (parser.PrintUsage()); exit 1
