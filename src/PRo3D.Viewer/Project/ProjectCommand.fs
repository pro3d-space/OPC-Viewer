namespace PRo3D.Viewer

open Argu
open System
open System.IO
open PRo3D.Viewer.Project
open PRo3D.Viewer.Configuration

[<AutoOpen>]
module ProjectCommand =

    type Args =
        | [<MainCommand>] ProjectFile of project_file: string
        | [<CustomCommandLine("--background-color"); AltCommandLine("--bg")>] BackgroundColor of string

        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | ProjectFile _ -> "Path to JSON project file"
                | BackgroundColor _ -> "optional background color (hex: #RGB/#RRGGBB, named: black/white/red/etc, RGB: r,g,b)"

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
            
            // Override background color and screenshots if provided via CLI arguments
            let config = 
                let configWithBg = 
                    match args.TryGetResult(Args.BackgroundColor) with
                    | Some cliColor -> { baseConfig with BackgroundColor = Some cliColor }
                    | None -> baseConfig
                
                match globalScreenshots with
                | Some cliScreenshots -> { configWithBg with Screenshots = Some cliScreenshots }
                | None -> configWithBg
            
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
            
            // Override background color and screenshots if provided via CLI arguments
            let config = 
                let configWithBg = 
                    match args.TryGetResult(Args.BackgroundColor) with
                    | Some cliColor -> { baseConfig with BackgroundColor = Some cliColor }
                    | None -> baseConfig
                
                match globalScreenshots with
                | Some cliScreenshots -> { configWithBg with Screenshots = Some cliScreenshots }
                | None -> configWithBg
            
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
            // Build args from project for Export command
            let parser = ArgumentParser.Create<ExportCommand.Args>()
            try
                let args = ResizeArray<string>()
                
                match exportProject.DataDir with
                | Some dir -> args.Add(dir)
                | None -> ()
                
                match exportProject.Format with
                | Some "pts" -> 
                    args.Add("--format")
                    args.Add("Pts")
                | Some "ply" -> 
                    args.Add("--format")
                    args.Add("Ply")
                | _ -> ()
                
                match exportProject.Out with
                | Some out -> 
                    args.Add("--out")
                    args.Add(out)
                | None -> ()
                
                let parsedArgs = parser.Parse(args.ToArray(), ignoreUnrecognized = false)
                ExportCommand.run parsedArgs
            with
            | ex ->
                printfn "[ERROR] Failed to execute export command: %s" ex.Message
                1