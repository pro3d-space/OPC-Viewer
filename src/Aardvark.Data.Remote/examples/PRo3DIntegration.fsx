// Example showing how Aardvark.Data.Remote integrates with PRo3D.Viewer
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open System.IO
open Aardvark.Data.Remote

printfn "=== PRo3D.Viewer Integration Examples ==="

// Example 1: How PRo3D.Viewer uses the library internally
printfn "\n1. PRo3D.Viewer Internal Usage:"

// This mimics the compatibility wrapper in PRo3D.Viewer's Data.fs
let resolveDataPath (basedir: string) (sftp: SftpConfig option) (forceDownload: bool) (logger: Logger.LogCallback option) (dataRef: DataRef) =
    let config = { 
        Fetch.defaultConfig with 
            baseDirectory = basedir
            sftpConfig = sftp
            forceDownload = forceDownload
            logger = logger
            progress = Some (fun percent -> 
                printf "\r%.2f%%" percent
                if percent >= 100.0 then printfn "" else System.Console.Out.Flush()
            )
    }
    
    let result = Resolver.resolve config dataRef
    
    // Convert to PRo3D's expected result format
    match result with
    | Resolved path -> Some path
    | _ -> None

// Test the compatibility wrapper
let testDataRef = Parser.parse "pro3d-test"
let resolvedPath = resolveDataPath (Path.GetTempPath()) None false None testDataRef
match resolvedPath with
| Some path -> printfn "✓ PRo3D compatibility wrapper resolved: %s" path
| None -> printfn "✗ PRo3D compatibility wrapper failed"

// Example 2: Direct usage in PRo3D commands
printfn "\n2. Direct Usage in PRo3D Commands:"

// This shows how PRo3D commands can use the library directly
let viewCommandConfig = {
    Fetch.defaultConfig with
        baseDirectory = "./data"
        progress = Some (fun percent -> printfn "Loading dataset: %.1f%%" percent)
        logger = Some (fun level msg -> printfn "[%A] %s" level msg)
        maxRetries = 3
}

let viewResult = Fetch.resolveWith viewCommandConfig "view-command-test"
match viewResult with
| Resolved path -> printfn "✓ View command resolved dataset: %s" path
| InvalidPath reason -> printfn "✗ View command failed: %s" reason
| _ -> printfn "✗ View command had other result"

// Example 3: List command with remote data support
printfn "\n3. List Command with Remote Data:"

let listCommandConfig = {
    Fetch.defaultConfig with
        baseDirectory = "./cache"
        forceDownload = false  // Use cache for listing
        logger = Some (fun level msg -> printfn "[LIST] %s" msg)
}

let remoteDataSources = [
    "local-dataset-1"
    "local-dataset-2"
    "remote-dataset" // Would be HTTP/SFTP in real usage
]

async {
    let! listResults = Fetch.resolveManyWith listCommandConfig remoteDataSources
    
    printfn "Available datasets:"
    for i, result in List.indexed listResults do
        let source = remoteDataSources.[i]
        match result with
        | Resolved path -> 
            printfn "  [%d] %s -> %s" i source path
            // In real PRo3D, this would scan for .opc files
        | InvalidPath reason -> 
            printfn "  [%d] %s -> ERROR: %s" i source reason
        | _ -> 
            printfn "  [%d] %s -> Other error" i source
            
} |> Async.RunSynchronously

// Example 4: Export command with remote sources
printfn "\n4. Export Command with Remote Sources:"

let exportConfig = {
    Fetch.defaultConfig with
        baseDirectory = "./export-cache"
        progress = Some (fun percent -> 
            if percent % 10.0 = 0.0 then 
                printfn "Export preparation: %.0f%%" percent
        )
}

let exportSource = "export-test-dataset"
let exportResult = Fetch.resolveWith exportConfig exportSource
match exportResult with
| Resolved path -> 
    printfn "✓ Export source resolved: %s" path
    printfn "  (Would now export OPC data to specified format)"
| InvalidPath reason -> 
    printfn "✗ Export failed to resolve source: %s" reason
| _ -> 
    printfn "✗ Export had other resolution error"

// Example 5: SFTP Configuration for Mars Data
printfn "\n5. Mars Data SFTP Configuration:"

// Example of how PRo3D would configure SFTP for Mars datasets
let marsDataConfig = {
    Host = "dig-sftp.joanneum.at" 
    Port = 2200
    User = "mastcam-z"
    Pass = "decoded-password-here"  // In real usage, decoded from FileZilla XML
}

let marsConfig = {
    Fetch.defaultConfig with
        baseDirectory = "./mars-data-cache"
        sftpConfig = Some marsDataConfig
        progress = Some (fun percent -> 
            printfn "Downloading Mars dataset: %.1f%%" percent
        )
        maxRetries = 5  // Mars data is precious, retry more
        timeout = TimeSpan.FromMinutes(30.0)  // Large datasets
}

// This would fail because SFTP server/credentials aren't real
let marsUrl = "sftp://dig-sftp.joanneum.at:2200/Mission/0300/0320/Job_0320_8341-034-rad/result/Job_0320_8341-034-rad_opc.zip"
let marsResult = Fetch.resolveWith marsConfig marsUrl
match marsResult with
| SftpConfigMissing uri -> printfn "✓ Expected - SFTP config validation working"
| DownloadError (uri, ex) -> printfn "✓ Expected - Mars SFTP connection failed: %s" ex.Message
| InvalidPath reason -> printfn "✓ Expected - Mars URL parsing: %s" reason
| Resolved path -> printfn "! Unexpected success (Mars SFTP actually connected): %s" path

// Example 6: FileZilla Configuration File Usage
printfn "\n6. FileZilla Configuration File Usage:"

let filezillaMarsConfig = {
    Fetch.defaultConfig with
        baseDirectory = "./mars-filezilla-cache"
        sftpConfigFile = Some "W:\\Datasets\\Pro3D\\confidential\\2025-02-24_AI-Mars-3D\\Mastcam-Z.xml"
        progress = Some (fun percent -> printfn "Mars FileZilla: %.1f%%" percent)
}

let filezillaMarsResult = Fetch.resolveWith filezillaMarsConfig marsUrl
match filezillaMarsResult with
| SftpConfigMissing _ -> printfn "✓ Expected - FileZilla config file not found"
| DownloadError (_, ex) -> printfn "✓ Expected - FileZilla Mars connection: %s" ex.Message
| InvalidPath reason -> printfn "✓ Expected - FileZilla config issue: %s" reason
| Resolved path -> printfn "! Unexpected - FileZilla Mars success: %s" path

// Example 7: Project File Integration
printfn "\n7. Project File Integration:"

// Shows how PRo3D project files would work with the new API
type ProjectData = {
    data: string list
    baseDirectory: string option
    sftp: string option  // Path to FileZilla config
}

let exampleProject = {
    data = [
        "local-dataset"
        "http://example.com/remote-dataset.zip"
        "sftp://server.com/mars-dataset.zip"
    ]
    baseDirectory = Some "./project-cache"
    sftp = Some "/path/to/filezilla.xml"
}

// Convert project config to FetchConfig
let projectConfig = {
    Fetch.defaultConfig with
        baseDirectory = exampleProject.baseDirectory |> Option.defaultValue Environment.CurrentDirectory
        sftpConfigFile = exampleProject.sftp
        progress = Some (fun percent -> printfn "Project loading: %.1f%%" percent)
}

async {
    let! projectResults = Fetch.resolveManyWith projectConfig exampleProject.data
    
    printfn "Project data resolution:"
    for i, result in List.indexed projectResults do
        let dataSource = exampleProject.data.[i]
        match result with
        | Resolved path -> printfn "  ✓ %s -> %s" dataSource path
        | InvalidPath reason -> printfn "  ✗ %s -> %s" dataSource reason
        | SftpConfigMissing _ -> printfn "  ✗ %s -> SFTP config needed" dataSource
        | DownloadError (_, ex) -> printfn "  ✗ %s -> Download failed: %s" dataSource ex.Message
        
} |> Async.RunSynchronously

printfn "\n=== PRo3D Integration Examples Complete ==="
printfn "\nNotes:"
printfn "- PRo3D.Viewer now uses this library internally via the compatibility wrapper"
printfn "- All remote data features are available in view, list, diff, export commands"
printfn "- Project files can specify data sources as URLs, with automatic resolution"
printfn "- SFTP support enables direct access to Mars exploration datasets"