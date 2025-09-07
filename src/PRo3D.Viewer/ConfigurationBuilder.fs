namespace PRo3D.Viewer

open Argu
open Aardvark.Base
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Project
open PRo3D.Viewer.Shared.PathUtils
open System.IO

/// Builders for constructing type-safe configurations from various sources
module ConfigurationBuilder =
    
    /// Build ViewConfig from command-line arguments
    let fromViewArgs (args: ParseResults<ViewCommand.Args>) : ViewConfig =
        // Process OBJ files
        let objFiles = 
            args.GetResult(ViewCommand.Args.ObjFiles, defaultValue = [])
            |> List.map (fun path -> 
                { Path = path; Type = Some DataType.Obj; Transform = None }: DataEntry)
        
        // Process data directories
        let dataDirs = 
            args.GetResults ViewCommand.Args.DataDirs
            |> List.concat  // Flatten the list of lists
            |> List.map (fun path -> 
                { Path = path; Type = Some DataType.Opc; Transform = None }: DataEntry)
        
        // Combine into unified array
        let data = dataDirs @ objFiles |> Array.ofList
        
        {
            Data = data
            Speed = args.TryGetResult ViewCommand.Args.Speed
            Sftp = args.TryGetResult ViewCommand.Args.Sftp
            BaseDir = args.TryGetResult ViewCommand.Args.BaseDir
            BackgroundColor = args.TryGetResult ViewCommand.Args.BackgroundColor
            Screenshots = None  // CLI args don't have screenshots field yet - will be added later
            ForceDownload = if args.Contains ViewCommand.Args.ForceDownload then Some true else None
            Verbose = if args.Contains ViewCommand.Args.Verbose then Some true else None
        }
    
    /// Build ViewConfig from parsed JSON project
    let fromViewProject (projectDir: string) (project: ViewProject) : ViewConfig =
        // Handle unified data array
        let data =
            match project.Data with
            | Some dataEntries when dataEntries.Length > 0 ->
                // Process unified data array
                dataEntries
                |> Array.map (fun entry ->
                    // Resolve path
                    let resolvedPath = resolveProjectPath projectDir entry.Path
                    
                    // Determine type (use specified type or infer)
                    let dataType =
                        match entry.Type with
                        | Some t -> Some t
                        | None -> Some (ProjectFile.inferDataType entry.Path)
                    
                    // Create DataEntry with resolved path
                    { Path = resolvedPath; Type = dataType; Transform = entry.Transform }: DataEntry
                )
            | _ ->
                // No data provided
                [||]
        
        let (baseDir, sftp, screenshots) = resolveConfigPaths projectDir project.BaseDir project.Sftp project.Screenshots
        
        {
            Data = data
            Speed = project.Speed
            Sftp = sftp
            BaseDir = baseDir
            BackgroundColor = project.BackgroundColor
            Screenshots = screenshots
            ForceDownload = project.ForceDownload
            Verbose = project.Verbose
        }
    
    /// Build DiffConfig from command-line arguments
    let fromDiffArgs (args: ParseResults<DiffCommand.Args>) : DiffConfig =
        {
            Data = args.GetResults DiffCommand.Args.DataDirs |> List.concat |> Array.ofList
            NoValue = args.TryGetResult DiffCommand.Args.NoValue
            Speed = args.TryGetResult DiffCommand.Args.Speed
            Verbose = if args.Contains DiffCommand.Args.Verbose then Some true else None
            Sftp = args.TryGetResult DiffCommand.Args.Sftp
            BaseDir = args.TryGetResult DiffCommand.Args.BaseDir
            BackgroundColor = args.TryGetResult DiffCommand.Args.BackgroundColor
            Screenshots = None  // CLI args don't have screenshots field yet - will be added later
            ForceDownload = if args.Contains DiffCommand.Args.ForceDownload then Some true else None
        }
    
    /// Build DiffConfig from parsed JSON project
    let fromDiffProject (projectDir: string) (project: DiffProject) : DiffConfig =
        // Resolve paths relative to project file directory
        let resolvePath = resolveProjectPath projectDir
        
        let data =
            project.Data
            |> Option.map (Array.map resolvePath)
            |> Option.defaultValue [||]
        
        let (baseDir, sftp, screenshots) = resolveConfigPaths projectDir project.BaseDir project.Sftp project.Screenshots
        
        {
            Data = data
            NoValue = project.NoValue
            Speed = project.Speed
            Verbose = project.Verbose
            Sftp = sftp
            BaseDir = baseDir
            BackgroundColor = project.BackgroundColor
            Screenshots = screenshots
            ForceDownload = project.ForceDownload
        }
    
    /// Build ExportConfig from command-line arguments
    let fromExportArgs (args: ParseResults<ExportCommand.Args>) : ExportConfig =
        // Get the data directory and convert to Data array
        let data = 
            match args.TryGetResult ExportCommand.Args.DataDir with
            | Some dir -> 
                [| { Path = dir; Type = Some DataType.Opc; Transform = None } |]
            | None -> [||]
        
        // Get the export format
        let format = 
            args.GetResult(ExportCommand.Args.Format, ExportCommand.ExportFormat.Pts)
        
        {
            Data = data
            Format = 
                match format with
                | ExportCommand.ExportFormat.Pts -> ExportFormat.Pts
                | ExportCommand.ExportFormat.Ply -> ExportFormat.Ply
            OutFile = args.TryGetResult ExportCommand.Args.Out
            Sftp = None  // Not available in current CLI args
            BaseDir = None  // Not available in current CLI args
            ForceDownload = None  // Not available in current CLI args
            Verbose = None  // Not available in current CLI args
        }
    
    /// Build ExportConfig from parsed JSON project
    let fromExportProject (projectDir: string) (project: ExportProject) : ExportConfig =
        // Resolve paths relative to project file directory
        let resolvePath = resolveProjectPath projectDir
        
        let data =
            project.Data
            |> Option.map (Array.map (fun entry ->
                { entry with Path = resolvePath entry.Path }
            ))
            |> Option.defaultValue [||]
        
        // Parse format string to ExportFormat
        let format = 
            match project.Format with
            | Some "pts" -> ExportFormat.Pts
            | Some "ply" -> ExportFormat.Ply
            | _ -> ExportFormat.Pts  // Default to PTS
        
        // Resolve configuration paths using common helper
        let (baseDir, sftp, _) = resolveConfigPaths projectDir project.BaseDir project.Sftp None
        
        {
            Data = data
            Format = format
            OutFile = project.Out |> Option.map resolvePath
            Sftp = sftp
            BaseDir = baseDir
            ForceDownload = project.ForceDownload
            Verbose = project.Verbose
        }