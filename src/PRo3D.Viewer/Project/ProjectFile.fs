namespace PRo3D.Viewer.Project

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Aardvark.Base

/// Data type for unified data entries
type DataType = 
    | Opc 
    | Obj

/// Unified data entry with type and transform
type DataEntry = {
    Path: string
    Type: DataType option  // None means auto-infer
    Transform: M44d option
}


/// View command project configuration - strongly typed public API
type ViewProject = {
    Command: string
    Data: DataEntry array option        // Unified data array
    Speed: float option
    Sftp: string option
    BaseDir: string option
    BackgroundColor: string option
    Screenshots: string option
}

/// Diff command project configuration - strongly typed public API
type DiffProject = {
    Command: string
    Data: string array option  // Unified data array (diff only supports strings, no transforms)
    NoValue: float option
    Speed: float option
    Verbose: bool option
    Sftp: string option
    BaseDir: string option
    BackgroundColor: string option
    Screenshots: string option
}

/// List command project configuration - strongly typed public API
type ListProject = {
    Command: string
    Data: string array option  // Unified data array
    Stats: bool option
}

/// Export command project configuration - strongly typed public API
type ExportProject = {
    Command: string
    DataDir: string option
    Format: string option  // "pts" or "ply"
    Out: string option
}

/// Discriminated union for all project types
type ProjectConfig = 
    | ViewConfig of ViewProject
    | DiffConfig of DiffProject
    | ListConfig of ListProject
    | ExportConfig of ExportProject
    | InvalidConfig of string


/// Module for JSON parsing and project loading
module ProjectFile =
    
    /// Infer data type from file path
    let inferDataType (path: string) : DataType =
        if path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) then
            Obj
        else
            Opc  // Default to OPC for directories and other files
    
    /// Determines the command type from JSON
    let private getCommandType (json: string) =
        try
            use doc = JsonDocument.Parse(json)
            match doc.RootElement.TryGetProperty("command") with
            | true, prop -> Some (prop.GetString())
            | false, _ -> None
        with
        | ex -> None

    /// Parses a view project from JSON with validation
    let private parseViewProject (json: string) =
        try
            // Validate JSON size to prevent memory attacks
            if json.Length > 1_000_000 then  // 1MB limit
                InvalidConfig "JSON file too large (max 1MB)"
            else
                // Parse JSON manually for better control
                use doc = JsonDocument.Parse(json)
                let root = doc.RootElement
                
                // Get command
                let command = 
                    match root.TryGetProperty("command") with
                    | true, prop -> prop.GetString()
                    | _ -> ""
                    
                if command <> "view" then
                    InvalidConfig "View project must have command='view'"
                else
                    // Parse unified data array
                    let data = 
                        match root.TryGetProperty("data") with
                        | true, prop when prop.ValueKind = JsonValueKind.Array ->
                            let items = 
                                prop.EnumerateArray() 
                                |> Seq.toArray
                                |> Array.map (fun item ->
                                    let path = item.GetProperty("path").GetString()
                                    let dataType = 
                                        match item.TryGetProperty("type") with
                                        | true, t when t.ValueKind = JsonValueKind.String -> 
                                            match t.GetString().ToLowerInvariant() with
                                            | "opc" -> Some Opc
                                            | "obj" -> Some Obj
                                            | _ -> None  // Invalid type, will infer
                                        | _ -> None  // No type specified, will infer
                                    let transform = 
                                        match item.TryGetProperty("transform") with
                                        | true, t when t.ValueKind <> JsonValueKind.Null -> 
                                            Some (M44d.Parse(t.GetString()))
                                        | _ -> None
                                    { Path = path; Type = dataType; Transform = transform }: DataEntry
                                )
                            Some items
                        | _ -> None
                    
                    // Parse other optional fields
                    let speed = 
                        match root.TryGetProperty("speed") with
                        | true, prop when prop.ValueKind = JsonValueKind.Number -> Some (prop.GetDouble())
                        | _ -> None
                        
                    let sftp = 
                        match root.TryGetProperty("sftp") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                        | _ -> None
                        
                    let baseDir = 
                        match root.TryGetProperty("baseDir") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                        | _ -> None
                        
                    let backgroundColor = 
                        match root.TryGetProperty("backgroundColor") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                        | _ -> None
                        
                    let screenshots = 
                        match root.TryGetProperty("screenshots") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                        | _ -> None
                    
                    // Validate paths
                    let hasDangerousPath = 
                        data
                        |> Option.defaultValue [||] 
                        |> Array.exists (fun d -> 
                            d.Path.Contains("~")
                        )
                        
                    if hasDangerousPath then
                        InvalidConfig "Paths contain potentially dangerous patterns"
                    else
                        // Create strongly-typed public ViewProject
                        let viewProject : ViewProject = {
                            Command = command
                            Data = data
                            Speed = speed
                            Sftp = sftp
                            BaseDir = baseDir
                            BackgroundColor = backgroundColor
                            Screenshots = screenshots
                        }
                        ViewConfig viewProject
        with
        | ex -> InvalidConfig (sprintf "Failed to parse view project: %s" ex.Message)
    
    /// Parses a diff project from JSON with validation
    let private parseDiffProject (json: string) =
        try
            // Validate JSON size
            if json.Length > 1_000_000 then  // 1MB limit
                InvalidConfig "JSON file too large (max 1MB)"
            else
                // Parse JSON manually for better control
                use doc = JsonDocument.Parse(json)
                let root = doc.RootElement
                
                // Get command
                let command = 
                    match root.TryGetProperty("command") with
                    | true, prop -> prop.GetString()
                    | _ -> ""
                    
                if command <> "diff" then
                    InvalidConfig "Diff project must have command='diff'"
                else
                    // Parse data array (required for diff)
                    let data = 
                        match root.TryGetProperty("data") with
                        | true, prop when prop.ValueKind = JsonValueKind.Array ->
                            let items = prop.EnumerateArray() |> Seq.toArray |> Array.map (fun item -> item.GetString())
                            Some items
                        | _ -> None
                    
                    // Validate that exactly 2 data entries are provided for diff
                    match data with
                    | Some dirs when dirs.Length = 2 -> 
                        // Validate paths
                        let hasDangerousPath = 
                            dirs |> Array.exists (fun d -> 
                                d.Contains("~")
                            )
                        if hasDangerousPath then
                            InvalidConfig "Data directory paths contain potentially dangerous patterns"
                        else
                            // Parse optional fields
                            let noValue = 
                                match root.TryGetProperty("noValue") with
                                | true, prop when prop.ValueKind = JsonValueKind.Number -> Some (prop.GetDouble())
                                | _ -> None
                                
                            let speed = 
                                match root.TryGetProperty("speed") with
                                | true, prop when prop.ValueKind = JsonValueKind.Number -> Some (prop.GetDouble())
                                | _ -> None
                                
                            let verbose = 
                                match root.TryGetProperty("verbose") with
                                | true, prop when prop.ValueKind = JsonValueKind.True -> Some true
                                | true, prop when prop.ValueKind = JsonValueKind.False -> Some false
                                | _ -> None
                                
                            let sftp = 
                                match root.TryGetProperty("sftp") with
                                | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                                | _ -> None
                                
                            let baseDir = 
                                match root.TryGetProperty("baseDir") with
                                | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                                | _ -> None
                                
                            let backgroundColor = 
                                match root.TryGetProperty("backgroundColor") with
                                | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                                | _ -> None
                                
                            let screenshots = 
                                match root.TryGetProperty("screenshots") with
                                | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                                | _ -> None
                            
                            // Create strongly-typed public DiffProject
                            let diffProject : DiffProject = {
                                Command = command
                                Data = data
                                NoValue = noValue
                                Speed = speed
                                Verbose = verbose
                                Sftp = sftp
                                BaseDir = baseDir
                                BackgroundColor = backgroundColor
                                Screenshots = screenshots
                            }
                            DiffConfig diffProject
                    | Some dirs -> InvalidConfig (sprintf "Diff command requires exactly 2 data entries, got %d" dirs.Length)
                    | None -> InvalidConfig "Diff command requires data with exactly 2 entries"
        with
        | ex -> InvalidConfig (sprintf "Failed to parse diff project: %s" ex.Message)
    
    /// Parses a list project from JSON with validation
    let private parseListProject (json: string) =
        try
            // Validate JSON size
            if json.Length > 1_000_000 then  // 1MB limit
                InvalidConfig "JSON file too large (max 1MB)"
            else
                // Parse JSON manually for better control
                use doc = JsonDocument.Parse(json)
                let root = doc.RootElement
                
                // Get command
                let command = 
                    match root.TryGetProperty("command") with
                    | true, prop -> prop.GetString()
                    | _ -> ""
                    
                if command <> "list" && command <> "ls" then
                    InvalidConfig "List project must have command='list' or command='ls'"
                else
                    // Parse data array
                    let data = 
                        match root.TryGetProperty("data") with
                        | true, prop when prop.ValueKind = JsonValueKind.Array ->
                            let items = prop.EnumerateArray() |> Seq.toArray |> Array.map (fun item -> item.GetString())
                            Some items
                        | _ -> None
                    
                    // Parse stats flag
                    let stats = 
                        match root.TryGetProperty("stats") with
                        | true, prop when prop.ValueKind = JsonValueKind.True -> Some true
                        | true, prop when prop.ValueKind = JsonValueKind.False -> Some false
                        | _ -> None
                    
                    // Create strongly-typed public ListProject
                    let listProject : ListProject = {
                        Command = "list"  // Normalize to "list"
                        Data = data
                        Stats = stats
                    }
                    ListConfig listProject
        with
        | ex -> InvalidConfig (sprintf "Failed to parse list project: %s" ex.Message)
    
    /// Parses an export project from JSON with validation
    let private parseExportProject (json: string) =
        try
            // Validate JSON size
            if json.Length > 1_000_000 then  // 1MB limit
                InvalidConfig "JSON file too large (max 1MB)"
            else
                // Parse JSON manually for better control
                use doc = JsonDocument.Parse(json)
                let root = doc.RootElement
                
                // Get command
                let command = 
                    match root.TryGetProperty("command") with
                    | true, prop -> prop.GetString()
                    | _ -> ""
                    
                if command <> "export" then
                    InvalidConfig "Export project must have command='export'"
                else
                    // Parse data directory
                    let dataDir = 
                        match root.TryGetProperty("dataDir") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                        | _ -> None
                    
                    // Parse format
                    let format = 
                        match root.TryGetProperty("format") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> 
                            let fmt = prop.GetString().ToLower()
                            if fmt = "pts" || fmt = "ply" then Some fmt
                            else None
                        | _ -> None
                    
                    // Parse output file
                    let out = 
                        match root.TryGetProperty("out") with
                        | true, prop when prop.ValueKind = JsonValueKind.String -> Some (prop.GetString())
                        | _ -> None
                    
                    // Validate required fields
                    match dataDir, format, out with
                    | Some _, Some _, Some _ ->
                        // Create strongly-typed public ExportProject
                        let exportProject : ExportProject = {
                            Command = command
                            DataDir = dataDir
                            Format = format
                            Out = out
                        }
                        ExportConfig exportProject
                    | _ -> InvalidConfig "Export project must specify dataDir, format, and out fields"
        with
        | ex -> InvalidConfig (sprintf "Failed to parse export project: %s" ex.Message)
    
    /// Loads and parses a project file
    let load (projectFilePath: string) =
        try
            if not (File.Exists projectFilePath) then
                InvalidConfig (sprintf "Project file not found: %s" projectFilePath)
            else
                let json = File.ReadAllText(projectFilePath)
                match getCommandType json with
                | Some "view" -> parseViewProject json
                | Some "diff" -> parseDiffProject json
                | Some "list" | Some "ls" -> parseListProject json
                | Some "export" -> parseExportProject json
                | Some cmd -> InvalidConfig (sprintf "Unknown command: %s" cmd)
                | None -> InvalidConfig "Project file must specify a 'command' field"
        with
        | ex -> InvalidConfig (sprintf "Failed to load project file: %s" ex.Message)
    
    /// Serializes a ViewProject to JSON string
    let serializeViewProject (project: ViewProject) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        
        writer.WriteStartObject()
        writer.WriteString("command", "view")
        
        // Write data array
        match project.Data with
        | Some data when data.Length > 0 ->
            writer.WritePropertyName("data")
            writer.WriteStartArray()
            for entry in data do
                writer.WriteStartObject()
                writer.WriteString("path", entry.Path)
                match entry.Type with
                | Some Opc -> writer.WriteString("type", "opc")
                | Some Obj -> writer.WriteString("type", "obj")
                | None -> ()  // Don't write type if it should be inferred
                match entry.Transform with
                | Some m -> writer.WriteString("transform", m.ToString())
                | None -> ()
                writer.WriteEndObject()
            writer.WriteEndArray()
        | _ -> ()
        
        // Write optional fields
        project.Speed |> Option.iter (fun s -> writer.WriteNumber("speed", s))
        project.Sftp |> Option.iter (fun s -> writer.WriteString("sftp", s))
        project.BaseDir |> Option.iter (fun s -> writer.WriteString("baseDir", s))
        project.BackgroundColor |> Option.iter (fun s -> writer.WriteString("backgroundColor", s))
        
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
    
    /// Serializes a DiffProject to JSON string
    let serializeDiffProject (project: DiffProject) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        
        writer.WriteStartObject()
        writer.WriteString("command", "diff")
        
        // Write data array if present
        match project.Data with
        | Some dirs when dirs.Length > 0 ->
            writer.WritePropertyName("data")
            writer.WriteStartArray()
            for dir in dirs do
                writer.WriteStringValue(dir)
            writer.WriteEndArray()
        | _ -> ()
        
        // Write optional fields
        project.NoValue |> Option.iter (fun n -> writer.WriteNumber("noValue", n))
        project.Speed |> Option.iter (fun s -> writer.WriteNumber("speed", s))
        project.Verbose |> Option.iter (fun v -> writer.WriteBoolean("verbose", v))
        project.Sftp |> Option.iter (fun s -> writer.WriteString("sftp", s))
        project.BaseDir |> Option.iter (fun s -> writer.WriteString("baseDir", s))
        project.BackgroundColor |> Option.iter (fun s -> writer.WriteString("backgroundColor", s))
        
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
    
    /// Serializes a ListProject to JSON string
    let serializeListProject (project: ListProject) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        
        writer.WriteStartObject()
        writer.WriteString("command", "list")
        
        // Write data array if present
        match project.Data with
        | Some dirs when dirs.Length > 0 ->
            writer.WritePropertyName("data")
            writer.WriteStartArray()
            for dir in dirs do
                writer.WriteStringValue(dir)
            writer.WriteEndArray()
        | _ -> ()
        
        // Write optional fields
        project.Stats |> Option.iter (fun s -> writer.WriteBoolean("stats", s))
        
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
    
    /// Serializes an ExportProject to JSON string
    let serializeExportProject (project: ExportProject) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        
        writer.WriteStartObject()
        writer.WriteString("command", "export")
        
        // Write required fields
        project.DataDir |> Option.iter (fun d -> writer.WriteString("dataDir", d))
        project.Format |> Option.iter (fun f -> writer.WriteString("format", f))
        project.Out |> Option.iter (fun o -> writer.WriteString("out", o))
        
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
    
