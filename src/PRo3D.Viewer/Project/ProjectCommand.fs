namespace PRo3D.Viewer

open Argu
open System
open System.IO
open PRo3D.Viewer.Project
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Shared.ConfigurationUtils

[<AutoOpen>]
module ProjectCommand =

    type Args =
        | [<MainCommand>] ProjectFile of project_file: string
        | [<CustomCommandLine("--background-color"); AltCommandLine("--bg")>] BackgroundColor of string
        | [<CustomCommandLine("--force-download"); AltCommandLine("-f")>] ForceDownload

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | ProjectFile _ -> "Path to JSON project file"
                | BackgroundColor _ -> "optional background color (hex: #RGB/#RRGGBB, named: black/white/red/etc, RGB: r,g,b)"
                | ForceDownload -> "force re-download of remote data even if cached"

    // These conversion functions have been removed - use ConfigurationBuilder instead

    let run (args : ParseResults<Args>) (globalScreenshots: string option) : int =
        
        let projectFilePath = args.GetResult(Args.ProjectFile)
        
        // Get the directory containing the project file for path resolution
        let projectFileDir = Path.GetDirectoryName(Path.GetFullPath(projectFilePath))
        
        // Load the project file
        match ProjectFile.load projectFilePath with
        | ProjectConfig.InvalidConfig error ->
            printfn "[ERROR] %s" error
            1
            
        | ProjectConfig.ViewConfig viewProject ->
            // Build ViewConfig directly from the parsed project
            let baseConfig = ConfigurationBuilder.fromViewProject projectFileDir viewProject
            
            // Override background color, screenshots, and force download if provided via CLI arguments
            let forceDownloadOverride = if args.Contains Args.ForceDownload then Some true else None
            let config = applyViewConfigOverrides baseConfig (args.TryGetResult(Args.BackgroundColor)) globalScreenshots forceDownloadOverride
            
            let hasData = 
                config.Data.Length > 0
            
            if not hasData then
                printfn "[ERROR] View project must specify data entries"
                1
            else
                // Call the view command directly with the type-safe config
                try
                    ViewCommand.execute config
                with
                | ex ->
                    printfn "[ERROR] Failed to execute view command: %s" ex.Message
                    1
        
        | ProjectConfig.DiffConfig diffProject ->
            // Build DiffConfig directly from the parsed project
            let baseConfig = ConfigurationBuilder.fromDiffProject projectFileDir diffProject
            
            // Override background color, screenshots, and force download if provided via CLI arguments
            let forceDownloadOverride = if args.Contains Args.ForceDownload then Some true else None
            let config = applyDiffConfigOverrides baseConfig (args.TryGetResult(Args.BackgroundColor)) globalScreenshots forceDownloadOverride
            
            // Direct execution with clean configuration - no more args conversion!
            try
                DiffCommand.execute config
            with
            | ex ->
                printfn "[ERROR] Failed to execute diff command: %s" ex.Message
                1
        
        | ProjectConfig.ListConfig listProject ->
            // Build args from project for List command
            let parser = ArgumentParser.Create<ListCommand.Args>()
            try
                let args = ResizeArray<string>()
                
                match listProject.Data with
                | Some dirs -> args.AddRange(dirs)
                | None -> ()
                
                match listProject.Stats with
                | Some true -> args.Add("--stats")
                | _ -> ()
                
                let parsedArgs = parser.Parse(args.ToArray(), ignoreUnrecognized = false)
                ListCommand.run parsedArgs
            with
            | ex ->
                printfn "[ERROR] Failed to execute list command: %s" ex.Message
                1
        
        | ProjectConfig.ExportConfig exportProject ->
            // Build ExportConfig directly from the parsed project
            let baseConfig = ConfigurationBuilder.fromExportProject projectFileDir exportProject
            
            // Override background color, screenshots, and force download if provided via CLI arguments
            let forceDownloadOverride = if args.Contains Args.ForceDownload then Some true else None
            let config = applyExportConfigOverrides baseConfig (args.TryGetResult(Args.BackgroundColor)) globalScreenshots forceDownloadOverride
            
            // Direct execution with clean configuration
            try
                ExportCommand.execute config
            with
            | ex ->
                printfn "[ERROR] Failed to execute export command: %s" ex.Message
                1