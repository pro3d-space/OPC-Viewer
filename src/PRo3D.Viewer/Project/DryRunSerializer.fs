namespace PRo3D.Viewer.Project

open Argu
open PRo3D.Viewer
open PRo3D.Viewer.Usage
open PRo3D.Viewer.Configuration
open System.IO

/// Module for converting CLI arguments to JSON project format for dry-run
module DryRunSerializer =
    
    /// Converts View command arguments to ViewProject
    let viewArgsToProject (args: ParseResults<ViewCommand.Args>) (globalScreenshots: string option) : ViewProject =
        let objFiles = 
            args.GetResults ViewCommand.Args.ObjFiles
            |> List.concat  // Flatten the list of lists since GetResults returns a list of lists
        
        let dataDirs = 
            args.GetResults ViewCommand.Args.DataDirs
            |> List.concat  // Flatten the list of lists
        
        // Convert to unified data array
        let dataEntries = 
            let opcEntries = 
                dataDirs 
                |> List.map (fun path -> 
                    { Path = path; Type = Some DataType.Opc; Transform = None }: DataEntry)
            let objEntries = 
                objFiles
                |> List.map (fun path -> 
                    { Path = path; Type = Some DataType.Obj; Transform = None }: DataEntry)
            opcEntries @ objEntries |> Array.ofList
        
        {
            Command = "view"
            Data = if dataEntries.Length > 0 then Some dataEntries else None
            Speed = args.TryGetResult ViewCommand.Args.Speed
            Sftp = args.TryGetResult ViewCommand.Args.Sftp
            BaseDir = args.TryGetResult ViewCommand.Args.BaseDir
            BackgroundColor = args.TryGetResult ViewCommand.Args.BackgroundColor
            Screenshots = globalScreenshots
        }
    
    /// Converts Diff command arguments to DiffProject
    let diffArgsToProject (args: ParseResults<DiffCommand.Args>) (globalScreenshots: string option) : DiffProject =
        let data = 
            args.GetResults DiffCommand.Args.DataDirs
            |> List.concat
            |> Array.ofList
        
        {
            Command = "diff"
            Data = if data.Length > 0 then Some data else None
            NoValue = args.TryGetResult DiffCommand.Args.NoValue
            Speed = args.TryGetResult DiffCommand.Args.Speed
            Verbose = if args.Contains DiffCommand.Args.Verbose then Some true else None
            Sftp = args.TryGetResult DiffCommand.Args.Sftp
            BaseDir = args.TryGetResult DiffCommand.Args.BaseDir
            BackgroundColor = args.TryGetResult DiffCommand.Args.BackgroundColor
            Screenshots = globalScreenshots
        }
    
    /// Converts List command arguments to ListProject
    let listArgsToProject (args: ParseResults<ListCommand.Args>) : ListProject =
        let data = 
            args.GetResults ListCommand.Args.DataDirs
            |> List.concat
            |> Array.ofList
        
        {
            Command = "list"
            Data = if data.Length > 0 then Some data else None
            Stats = if args.Contains ListCommand.Args.Stats then Some true else None
        }
    
    /// Converts Export command arguments to ExportProject
    let exportArgsToProject (args: ParseResults<ExportCommand.Args>) : ExportProject =
        let format = 
            match args.TryGetResult ExportCommand.Args.Format with
            | Some ExportCommand.ExportFormat.Pts -> Some "pts"
            | Some ExportCommand.ExportFormat.Ply -> Some "ply"
            | None -> None
        
        {
            Command = "export"
            DataDir = args.TryGetResult ExportCommand.Args.DataDir
            Format = format
            Out = args.TryGetResult ExportCommand.Args.Out
        }
    
    /// Converts Project command arguments by loading and returning the JSON file content
    let projectArgsToJson (args: ParseResults<ProjectCommand.Args>) : string =
        let projectFilePath = args.GetResult(ProjectCommand.Args.ProjectFile)
        
        if File.Exists projectFilePath then
            // Read the existing JSON file and return it formatted
            let json = File.ReadAllText(projectFilePath)
            // Parse and re-serialize to ensure proper formatting
            try
                use doc = System.Text.Json.JsonDocument.Parse(json)
                use stream = new System.IO.MemoryStream()
                use writer = new System.Text.Json.Utf8JsonWriter(stream, System.Text.Json.JsonWriterOptions(Indented = true))
                doc.WriteTo(writer)
                writer.Flush()
                System.Text.Encoding.UTF8.GetString(stream.ToArray())
            with
            | _ -> json  // Return original if parsing fails
        else
            sprintf "{ \"error\": \"Project file not found: %s\" }" projectFilePath
    
    /// Main function to serialize any command to JSON
    let serializeToJson (arguments: ParseResults<CliArguments>) : string =
        // Extract global screenshots argument
        let globalScreenshots = arguments.TryGetResult Screenshots
        
        // Filter out DryRun and Screenshots from the results to find the actual command
        let results = arguments.GetAllResults() |> List.filter (function 
            | CliArguments.DryRun -> false 
            | CliArguments.Screenshots _ -> false 
            | _ -> true)
        
        match results with
        | [CliArguments.Diff x] -> 
            let project = diffArgsToProject x globalScreenshots
            ProjectFile.serializeDiffProject project
        | [CliArguments.Export x] -> 
            let project = exportArgsToProject x
            ProjectFile.serializeExportProject project
        | [CliArguments.List x] -> 
            let project = listArgsToProject x
            ProjectFile.serializeListProject project
        | [CliArguments.Project x] -> 
            projectArgsToJson x
        | [CliArguments.View x] -> 
            let project = viewArgsToProject x globalScreenshots
            ProjectFile.serializeViewProject project
        | [CliArguments.Version] -> 
            "{ \"command\": \"version\" }"
        | [] -> 
            "{ \"error\": \"No command specified\" }"
        | _ -> 
            "{ \"error\": \"Invalid command combination\" }"