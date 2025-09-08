// Basic usage examples for Aardvark.Data.Remote
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open System.IO
open Aardvark.Data.Remote

printfn "=== Aardvark.Data.Remote Basic Usage Examples ==="

// Example 1: Simple resolution with default configuration
printfn "\n1. Simple resolution:"
let result1 = Fetch.resolve "test-local-dir"
match result1 with
| Resolved path -> printfn "✓ Resolved to: %s" path
| InvalidPath reason -> printfn "✗ Error: %s" reason
| _ -> printfn "✗ Other result type"

// Example 2: Resolution with custom configuration
printfn "\n2. Resolution with custom configuration:"
let customConfig = {
    Fetch.defaultConfig with
        baseDirectory = Path.GetTempPath()
        maxRetries = 5
        forceDownload = true
}

let result2 = Fetch.resolveWith customConfig "custom-test-dir"
match result2 with
| Resolved path -> printfn "✓ Resolved with custom config to: %s" path
| InvalidPath reason -> printfn "✗ Error: %s" reason
| _ -> printfn "✗ Other result type"

// Example 3: Async resolution
printfn "\n3. Async resolution:"
async {
    let! result = Fetch.resolveAsync "async-test-dir"
    match result with
    | Resolved path -> printfn "✓ Async resolved to: %s" path
    | InvalidPath reason -> printfn "✗ Async error: %s" reason
    | _ -> printfn "✗ Other async result type"
} |> Async.RunSynchronously

// Example 4: Async resolution with custom config
printfn "\n4. Async resolution with custom config:"
let configWithProgress = {
    Fetch.defaultConfig with
        baseDirectory = Path.GetTempPath()
        progress = Some (fun percent -> printf "\rProgress: %.1f%%" percent)
        logger = Some (fun level msg -> printfn "\n[%A] %s" level msg)
}

async {
    let! result = Fetch.resolveAsyncWith configWithProgress "async-custom-test-dir"
    match result with
    | Resolved path -> printfn "\n✓ Async with config resolved to: %s" path
    | InvalidPath reason -> printfn "\n✗ Async config error: %s" reason
    | _ -> printfn "\n✗ Other async config result type"
} |> Async.RunSynchronously

// Example 5: Batch processing
printfn "\n5. Batch processing:"
let urls = [
    "batch-test-1"
    "batch-test-2" 
    "batch-test-3"
]

async {
    let! results = Fetch.resolveMany urls
    
    printfn "Batch results:"
    for i, result in List.indexed results do
        match result with
        | Resolved path -> printfn "  [%d] ✓ %s -> %s" i urls.[i] path
        | InvalidPath reason -> printfn "  [%d] ✗ %s -> Error: %s" i urls.[i] reason
        | _ -> printfn "  [%d] ✗ %s -> Other result" i urls.[i]
} |> Async.RunSynchronously

// Example 6: Batch processing with custom config
printfn "\n6. Batch processing with custom config:"
async {
    let! results = Fetch.resolveManyWith customConfig urls
    
    printfn "Batch results with custom config:"
    for i, result in List.indexed results do
        match result with
        | Resolved path -> printfn "  [%d] ✓ %s -> %s" i urls.[i] path
        | InvalidPath reason -> printfn "  [%d] ✗ %s -> Error: %s" i urls.[i] reason
        | _ -> printfn "  [%d] ✗ %s -> Other result" i urls.[i]
} |> Async.RunSynchronously

// Example 7: Parsing and validation
printfn "\n7. Parsing different URL types:"
let testUrls = [
    "/absolute/path"
    "relative/path"
    "http://example.com/data.zip"
    "sftp://server.com/data.zip"
    "invalid://bad-scheme"
    ""
]

for url in testUrls do
    let parsed = Parser.parse url
    let isValid = Parser.isValid parsed
    let description = Parser.describe parsed
    printfn "  %s -> Valid: %b, Type: %s" url isValid description

// Example 8: Error handling patterns
printfn "\n8. Error handling examples:"
let testInvalidUrl = "invalid://not-supported"
let result8 = Fetch.resolve testInvalidUrl
match result8 with
| Resolved path -> 
    printfn "Unexpected success: %s" path
| InvalidPath reason ->
    printfn "✓ Properly caught invalid URL: %s" reason
| DownloadError (uri, ex) ->
    printfn "Download error for %A: %s" uri ex.Message  
| SftpConfigMissing uri ->
    printfn "SFTP config missing for %A" uri

printfn "\n=== Basic Usage Examples Complete ==="