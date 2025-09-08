// Advanced usage examples for Aardvark.Data.Remote
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open System.IO
open System.Threading.Tasks
open Aardvark.Data.Remote

printfn "=== Aardvark.Data.Remote Advanced Usage Examples ==="

// Example 1: SFTP Configuration
printfn "\n1. SFTP Configuration (with mock config):"
let sftpConfig = {
    Host = "sftp.example.com"
    Port = 22
    User = "testuser" 
    Pass = "testpass"
}

let sftpFetchConfig = {
    Fetch.defaultConfig with
        sftpConfig = Some sftpConfig
        baseDirectory = Path.GetTempPath()
}

// Note: This will fail because the SFTP server doesn't exist
let sftpResult = Fetch.resolveWith sftpFetchConfig "sftp://sftp.example.com/test.zip"
match sftpResult with
| SftpConfigMissing uri -> printfn "✗ Expected - SFTP config missing for: %A" uri
| DownloadError (uri, ex) -> printfn "✓ Expected - SFTP connection failed: %s" ex.Message
| InvalidPath reason -> printfn "✓ Expected - Invalid SFTP path: %s" reason
| _ -> printfn "Unexpected SFTP result"

// Example 2: FileZilla Configuration File Support
printfn "\n2. FileZilla Configuration File:"
let filezillaConfig = {
    Fetch.defaultConfig with
        sftpConfigFile = Some "/path/to/filezilla.xml"  // Non-existent file
}

let filezillaResult = Fetch.resolveWith filezillaConfig "sftp://server.com/data.zip"
match filezillaResult with
| SftpConfigMissing _ -> printfn "✓ Expected - No valid SFTP config found"
| InvalidPath reason -> printfn "✓ Expected - Invalid config: %s" reason
| _ -> printfn "Unexpected FileZilla result"

// Example 3: Complex Progress Reporting
printfn "\n3. Advanced Progress Reporting:"
let mutable progressHistory = []

let advancedProgressConfig = {
    Fetch.defaultConfig with
        baseDirectory = Path.GetTempPath()
        progress = Some (fun percent ->
            progressHistory <- percent :: progressHistory
            printf "\r[Advanced] Progress: %.1f%%" percent
            if percent >= 100.0 then printfn ""
        )
        logger = Some (fun level msg ->
            printfn "[%A] %s" level msg
        )
        maxRetries = 10
        timeout = TimeSpan.FromSeconds(30.0)
        forceDownload = true
}

let advancedResult = Fetch.resolveWith advancedProgressConfig "advanced-test"
match advancedResult with
| Resolved path -> 
    printfn "✓ Advanced resolution successful: %s" path
    printfn "  Progress history: %A" (List.rev progressHistory)
| _ -> printfn "Advanced resolution had other result"

// Example 4: High-Performance Batch Processing
printfn "\n4. High-Performance Batch Processing:"
let largeBatch = [
    for i in 1..10 do
        yield $"batch-item-{i}"
]

let performanceConfig = {
    Fetch.defaultConfig with
        baseDirectory = Path.GetTempPath()
        maxRetries = 1  // Fast fail for demo
        timeout = TimeSpan.FromSeconds(5.0)
}

let stopwatch = System.Diagnostics.Stopwatch.StartNew()
async {
    let! batchResults = Fetch.resolveManyWith performanceConfig largeBatch
    stopwatch.Stop()
    
    let successCount = batchResults |> List.filter (function | Resolved _ -> true | _ -> false) |> List.length
    let errorCount = batchResults.Length - successCount
    
    printfn "✓ Batch processing completed in %dms" stopwatch.ElapsedMilliseconds
    printfn "  Success: %d, Errors: %d, Total: %d" successCount errorCount batchResults.Length
    printfn "  Average per item: %.1fms" (float stopwatch.ElapsedMilliseconds / float batchResults.Length)
    
} |> Async.RunSynchronously

// Example 5: Mixed Protocol Batch (with expected failures)
printfn "\n5. Mixed Protocol Batch (with expected failures):"
let mixedUrls = [
    "local-test-1"  // Should succeed
    "local-test-2"  // Should succeed
    "http://fake-url.com/data.zip"  // Should fail
    "sftp://fake-sftp.com/data.zip"  // Should fail - no SFTP config
    "invalid://bad-protocol/data.zip"  // Should fail - bad protocol
]

async {
    let! mixedResults = Fetch.resolveMany mixedUrls
    
    printfn "Mixed protocol results:"
    for i, result in List.indexed mixedResults do
        let url = mixedUrls.[i]
        match result with
        | Resolved path -> printfn "  ✓ %s -> %s" url path
        | InvalidPath reason -> printfn "  ✗ %s -> Invalid: %s" url reason
        | DownloadError (uri, ex) -> printfn "  ✗ %s -> Download error: %s" url ex.Message
        | SftpConfigMissing uri -> printfn "  ✗ %s -> SFTP config missing" url
        
} |> Async.RunSynchronously

// Example 6: Direct Resolver Usage (Low-level API)
printfn "\n6. Direct Resolver Usage:"
let dataRef = Parser.parse "resolver-test"
let resolverResult = Resolver.resolve performanceConfig dataRef
match resolverResult with
| Resolved path -> printfn "✓ Direct resolver success: %s" path
| _ -> printfn "Direct resolver had other result"

// Example 7: Async Concurrent Operations
printfn "\n7. Concurrent Async Operations:"
let concurrentTasks = [
    async { 
        let! result = Fetch.resolveAsync "concurrent-1"
        return ("concurrent-1", result)
    }
    async { 
        let! result = Fetch.resolveAsyncWith performanceConfig "concurrent-2"
        return ("concurrent-2", result)
    }
    async { 
        let! results = Fetch.resolveMany ["concurrent-batch-1"; "concurrent-batch-2"]
        return ("batch", match results with | [r1; r2] -> r1 | _ -> InvalidPath "batch error")
    }
]

async {
    let! concurrentResults = concurrentTasks |> Async.Parallel
    
    printfn "Concurrent operation results:"
    for (name, result) in concurrentResults do
        match result with
        | Resolved path -> printfn "  ✓ %s -> %s" name path
        | InvalidPath reason -> printfn "  ✗ %s -> %s" name reason
        | _ -> printfn "  ~ %s -> Other result" name
        
} |> Async.RunSynchronously

// Example 8: C# Interop Demonstration  
printfn "\n8. C# Interop with FetchConfiguration:"
let csharpConfig = FetchConfiguration()
csharpConfig.BaseDirectory <- Path.GetTempPath()
csharpConfig.MaxRetries <- 2
csharpConfig.ForceDownload <- true

// Convert to F# config and use
let fsharpConfig = csharpConfig.ToFSharp()
let csharpInteropResult = Fetch.resolveWith fsharpConfig "csharp-interop-test"
match csharpInteropResult with
| Resolved path -> printfn "✓ C# interop success: %s" path
| _ -> printfn "C# interop had other result"

// Demonstrate Task-based C# API
let csharpTaskResult = Fetch.ResolveAsync("csharp-task-test") |> Async.AwaitTask |> Async.RunSynchronously
match csharpTaskResult with
| Resolved path -> printfn "✓ C# Task API success: %s" path
| _ -> printfn "C# Task API had other result"

printfn "\n=== Advanced Usage Examples Complete ==="