﻿open Argu
open PRo3D.OpcViewer
open PRo3D.OpcViewer.Data
open System.IO

[<EntryPoint>]
let main argv =

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
