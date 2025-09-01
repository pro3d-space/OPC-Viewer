namespace PRo3D.Viewer

open Argu
open Aardvark.Base
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Project
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
                    let resolvedPath = 
                        match entry.Path with
                        | path when path.StartsWith("http://") || path.StartsWith("https://") || path.StartsWith("sftp://") -> path
                        | path when Path.IsPathRooted(path) -> path
                        | path -> Path.GetFullPath(Path.Combine(projectDir, path))
                    
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
        
        let baseDir =
            project.BaseDir
            |> Option.map (fun bd ->
                if Path.IsPathRooted(bd) then bd
                else Path.GetFullPath(Path.Combine(projectDir, bd))
            )
        
        let sftp =
            project.Sftp
            |> Option.map (fun s ->
                if Path.IsPathRooted(s) then s
                else Path.GetFullPath(Path.Combine(projectDir, s))
            )
        
        let screenshots =
            project.Screenshots
            |> Option.map (fun s ->
                if Path.IsPathRooted(s) then s
                else Path.GetFullPath(Path.Combine(projectDir, s))
            )
        
        {
            Data = data
            Speed = project.Speed
            Sftp = sftp
            BaseDir = baseDir
            BackgroundColor = project.BackgroundColor
            Screenshots = screenshots
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
        }
    
    /// Build DiffConfig from parsed JSON project
    let fromDiffProject (projectDir: string) (project: DiffProject) : DiffConfig =
        // Resolve paths relative to project file directory
        let resolvePath (path: string) =
            match path with
            | p when p.StartsWith("http://") || p.StartsWith("https://") || p.StartsWith("sftp://") -> p
            | p when Path.IsPathRooted(p) -> p
            | p -> Path.GetFullPath(Path.Combine(projectDir, p))
        
        let data =
            project.Data
            |> Option.map (Array.map resolvePath)
            |> Option.defaultValue [||]
        
        let baseDir =
            project.BaseDir
            |> Option.map (fun bd ->
                if Path.IsPathRooted(bd) then bd
                else Path.GetFullPath(Path.Combine(projectDir, bd))
            )
        
        let sftp =
            project.Sftp
            |> Option.map (fun s ->
                if Path.IsPathRooted(s) then s
                else Path.GetFullPath(Path.Combine(projectDir, s))
            )
        
        let screenshots =
            project.Screenshots
            |> Option.map (fun s ->
                if Path.IsPathRooted(s) then s
                else Path.GetFullPath(Path.Combine(projectDir, s))
            )
        
        {
            Data = data
            NoValue = project.NoValue
            Speed = project.Speed
            Verbose = project.Verbose
            Sftp = sftp
            BaseDir = baseDir
            BackgroundColor = project.BackgroundColor
            Screenshots = screenshots
        }
    
    // Export and List commands don't have JSON project support yet
    // These are placeholders for future implementation