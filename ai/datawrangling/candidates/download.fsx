#!/usr/bin/env dotnet fsi

#r "nuget: System.Text.Json"
#r "nuget: SSH.NET"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Types.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Parser.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Provider.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Common.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Cache.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Zip.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/FileZillaConfig.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Progress.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Providers/LocalProvider.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Providers/HttpProvider.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Providers/SftpProvider.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Resolver.fs"
#load "../../../src/Aardvark.Data.Remote/src/Aardvark.Data.Remote/Fetch.fs"

open System
open System.IO
open System.Text.Json
open Aardvark.Data.Remote

// Configuration
let listJsonPath = "list.json"
let cacheDir = "cache"
let sftpConfigPath = @"W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml"

// JSON types for parsing list.json
type OpcPair = {
    sol: int
    regular: string
    regularPath: string
    ai: string
    aiPath: string
    notes: string option
}

type CandidatesList = {
    description: string
    sourceFiles: string array
    totalPairs: int
    pairs: OpcPair array
}

// Load the candidates list
let loadCandidatesList () =
    let json = File.ReadAllText listJsonPath
    let options = JsonSerializerOptions()
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    JsonSerializer.Deserialize<CandidatesList>(json, options)

// Download/cache a single SFTP file using Fetch API (with built-in caching)
let downloadFile (sftpPath: string) =
    let result = 
        sftpPath
        |> Fetch.from
        |> Fetch.configure
        |> Fetch.withBaseDirectory cacheDir
        |> Fetch.withSftpConfigFile sftpConfigPath
        |> Fetch.withConsoleProgress
        |> Fetch.withMaxRetries 3
        |> Fetch.resolve
    
    match result with
    | Resolved localPath -> 
        printfn "✓ Available: %s" (Path.GetRelativePath(cacheDir, localPath))
        true
    | InvalidPath reason ->
        printfn "✗ Invalid: %s - %s" sftpPath reason
        false
    | DownloadError (uri, error) ->
        printfn "✗ Error: %s - %s" sftpPath error.Message
        false
    | SftpConfigMissing uri ->
        printfn "✗ Config missing: %s" sftpPath
        false

// Process all files in the candidates list
let downloadAllFiles () =
    let candidates = loadCandidatesList ()
    
    printfn "=== OPC SFTP Download Script ==="
    printfn "Total pairs to process: %d" candidates.totalPairs
    printfn "Cache directory: %s" (Path.GetFullPath cacheDir)
    printfn "SFTP config: %s" sftpConfigPath
    printfn ""
    printfn "Note: Files cached in SFTP path structure (cache/sftp/server/path/file.zip)"
    printfn "Fetch API handles caching - files are downloaded once and reused automatically"
    printfn ""
    
    // Collect all SFTP paths
    let allPaths = [
        for pair in candidates.pairs do
            yield pair.regularPath
            yield pair.aiPath
    ]
    
    printfn "Processing %d files..." allPaths.Length
    printfn ""
    
    let mutable successCount = 0
    let mutable errorCount = 0
    
    for sftpPath in allPaths do
        if downloadFile sftpPath then
            successCount <- successCount + 1
        else
            errorCount <- errorCount + 1
    
    printfn ""
    printfn "=== Download Summary ==="
    printfn "Successful: %d" successCount
    printfn "Errors: %d" errorCount
    printfn "Total: %d" (successCount + errorCount)
    
    if errorCount = 0 then
        printfn ""
        printfn "Cache directory size: %s" 
            (let dirInfo = DirectoryInfo(cacheDir)
             if dirInfo.Exists then
                 let totalSize = dirInfo.GetFiles("*", SearchOption.AllDirectories) 
                               |> Array.sumBy (fun f -> f.Length)
                 sprintf "%.2f GB" (float totalSize / 1024.0 / 1024.0 / 1024.0)
             else "0 bytes")

// Main execution
try
    if not (File.Exists listJsonPath) then
        failwithf "Could not find %s" listJsonPath
        
    if not (File.Exists sftpConfigPath) then
        failwithf "Could not find SFTP config at %s" sftpConfigPath
    
    downloadAllFiles ()
    printfn "Script completed successfully!"
    
with
| ex -> 
    printfn "Script failed: %s" ex.Message
    exit 1