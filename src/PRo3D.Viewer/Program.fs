open Argu
open PRo3D.Viewer
open PRo3D.Viewer.Project
open System.Linq
open System.Reflection

[<EntryPoint>]
let main argv =

    let PROGRAM_NAME = "PRo3D.Viewer"
    let VERSION      = 
        typeof<Usage.CliArguments>.Assembly
            .GetCustomAttributes()
            .OfType<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
        |> fun attr -> if isNull attr then "0.0.0" else attr.InformationalVersion

    
    //do
    //    // devel/debug - you can safely delete this block
    //    // (should not have been commited to the git repo in the first place)
    //    let filename = @"W:\Datasets\Pro3D\confidential\2025-08-12_RockFace_Orig.obj\RockFace_Orig.obj"
    //    let objModel : Aardvark.SceneGraph.ISg = Data.Wavefront.loadObjFile filename
    //    exit 0
    //    ()
    

    let parser = ArgumentParser.Create<CliArguments>(programName = PROGRAM_NAME)

    // Preprocess arguments to support JSON file shortcut
    let preprocessedArgv =
        match argv with
        | [||] -> argv  // No arguments, return as-is
        | _ when argv.[0].EndsWith(".json", System.StringComparison.OrdinalIgnoreCase) ->
            // First argument is a JSON file, prepend "project" command
            Array.append [|"project"|] argv
        | _ -> argv  // Not a JSON file, return as-is

    let arguments : ParseResults<CliArguments> =
        try
            parser.ParseCommandLine(inputs = preprocessedArgv, raiseOnUsage = true)
        with :? ArguParseException as e ->
            eprintfn "%s" e.Message
            exit 1
    
    // Check for dry-run flag first
    if arguments.Contains DryRun then
        // Output parsed arguments as JSON and exit
        let json = DryRunSerializer.serializeToJson arguments
        printfn "%s" json
        0
    else
        // Extract global screenshots argument
        let globalScreenshots = arguments.TryGetResult Screenshots
        
        // Normal command execution
        match arguments.GetAllResults() with
        | [Diff x]   -> DiffCommand.run VERSION x globalScreenshots
        | [Export x] -> ExportCommand.run x
        | [List x]   -> ListCommand.run x
        | [Project x] -> ProjectCommand.run VERSION x globalScreenshots
        | [View x]   -> ViewCommand.run VERSION x globalScreenshots
        | [Version]  -> printfn "%s" VERSION; exit 0
        | _          -> printfn "%s" (parser.PrintUsage()); exit 1
