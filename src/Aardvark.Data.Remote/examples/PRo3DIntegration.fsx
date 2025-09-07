// Example showing how to use Aardvark.Data.Remote in place of original PRo3D.Viewer Data module
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open Aardvark.Data.Remote

printfn "=== PRo3D.Viewer Integration Examples ==="

// This demonstrates how the new library replaces the original PRo3D.Viewer functionality

// Example 1: Original PRo3D.Viewer pattern vs New library pattern
printfn "\n1. Migration from original PRo3D.Viewer patterns:"

// OLD PRo3D.Viewer approach (conceptual - this is what the wrapper now does internally):
let simulateOldApproach (basedir: string) (input: string) =
    printfn "  OLD: Would call resolveDataPath with basedir=%s, input=%s" basedir input
    
    // This is essentially what the old code did:
    let dataRef = Parser.parse input  // Was: getDataRefFromString
    
    let config = { 
        ResolverConfig.Default with 
            BaseDirectory = basedir 
    }
    
    let result = Resolver.resolve config dataRef  // Was: resolveDataPath
    
    // Convert to old result format for compatibility
    match result with
    | Resolved path -> printfn "  OLD RESULT: Success -> %s" path
    | InvalidPath reason -> printfn "  OLD RESULT: Invalid -> %s" reason
    | DownloadError (uri, ex) -> printfn "  OLD RESULT: Download error -> %A, %s" uri ex.Message
    | SftpConfigMissing uri -> printfn "  OLD RESULT: SFTP config missing -> %A" uri

// NEW approach with enhanced features:
let newApproach (basedir: string) (input: string) =
    printfn "  NEW: Using builder pattern with enhanced features"
    
    let result = 
        Fetch
            .From(input)
            .WithBaseDirectory(basedir)
            .WithConsoleProgress()
            .WithMaxRetries(3)
            .WithTimeout(TimeSpan.FromMinutes(5.0))
            .Resolve()
    
    match result with
    | Resolved path -> printfn "  NEW RESULT: Success -> %s" path
    | InvalidPath reason -> printfn "  NEW RESULT: Invalid -> %s" reason
    | DownloadError (uri, ex) -> printfn "  NEW RESULT: Download error -> %A, %s" uri ex.Message
    | SftpConfigMissing uri -> printfn "  NEW RESULT: SFTP config missing -> %A" uri

// Test both approaches
let testInput = "relative/test/data"
let testBasedir = System.IO.Path.GetTempPath()

simulateOldApproach testBasedir testInput
newApproach testBasedir testInput

// Example 2: SFTP configuration migration
printfn "\n2. SFTP configuration migration:"

// OLD: Would use PRo3D.Viewer.Sftp.SftpServerConfig
type OldSftpConfig = {
    Host: string
    Port: int
    User: string
    Pass: string
}

let oldSftpConfig = {
    Host = "mars-data.nasa.gov"
    Port = 22
    User = "scientist"
    Pass = "secret123"
}

// NEW: Direct mapping to new SftpConfig
let newSftpConfig = {
    Host = oldSftpConfig.Host
    Port = oldSftpConfig.Port
    User = oldSftpConfig.User
    Pass = oldSftpConfig.Pass
}

printfn "  Converted SFTP config: %A" newSftpConfig

let sftpResult = 
    Fetch
        .From("sftp://mars-data.nasa.gov/dataset.zip")
        .WithSftpConfig(newSftpConfig)
        .WithBaseDirectory("/mars-data/cache")
        .Resolve()

printfn "  SFTP resolution result: %A" sftpResult

// Example 3: Batch processing for multiple datasets (common PRo3D.Viewer pattern)
printfn "\n3. Batch processing multiple datasets (PRo3D.Viewer style):"

let marsDatasets = [
    "/local/curiosity/sol_0001"
    "/local/curiosity/sol_0002" 
    "http://mars-data.nasa.gov/curiosity/sol_0003.zip"
    "sftp://secure.nasa.gov/perseverance/sol_0100.zip"
    "relative/datasets/opportunity"
]

// Process each dataset
for (i, dataset) in List.indexed marsDatasets do
    printfn "  Processing dataset %d: %s" (i + 1) dataset
    
    let result = 
        Fetch
            .From(dataset)
            .WithBaseDirectory("/mars-data")
            .WithProgress(fun p -> printf "    %.0f%% " p; Console.Out.Flush())
            .WithMaxRetries(5)  // Mars data can be unreliable!
            .Resolve()
    
    printf "\n"
    match result with
    | Resolved path -> 
        printfn "    ✓ Ready for processing: %s" path
        // In real PRo3D.Viewer, this would continue with:
        // - Loading OPC hierarchies
        // - Building scene graphs
        // - Setting up viewer
        
    | InvalidPath reason -> 
        printfn "    ✗ Skipping invalid dataset: %s" reason
        
    | DownloadError (uri, ex) -> 
        printfn "    ✗ Download failed: %s" ex.Message
        
    | SftpConfigMissing uri -> 
        printfn "    ✗ SFTP credentials needed for: %A" uri

// Example 4: Error patterns common in PRo3D.Viewer
printfn "\n4. Error handling patterns for scientific data workflows:"

let processDataset (input: string) =
    let result = 
        Fetch
            .From(input)
            .WithBaseDirectory("/scientific-data/cache")
            .WithTimeout(TimeSpan.FromHours(1.0))  // Large datasets!
            .WithProgress(fun p -> 
                if p % 25.0 = 0.0 then  // Log every 25%
                    printfn "      Processing %s: %.0f%% complete" input p
            )
            .Resolve()
    
    match result with
    | Resolved path ->
        // Simulate scientific data validation
        if System.IO.Directory.Exists(path) then
            let files = System.IO.Directory.GetFiles(path, "*.xml", System.IO.SearchOption.AllDirectories)
            if files.Length > 0 then
                printfn "    ✓ Valid dataset with %d metadata files" files.Length
                true
            else
                printfn "    ✗ Dataset missing required metadata files"
                false
        else
            printfn "    ✗ Resolved path does not exist: %s" path
            false
            
    | InvalidPath reason ->
        printfn "    ✗ Invalid dataset reference: %s" reason
        false
        
    | DownloadError (uri, ex) ->
        printfn "    ✗ Network error downloading %A: %s" uri ex.Message
        printfn "      Recommendation: Check network connection and retry later"
        false
        
    | SftpConfigMissing uri ->
        printfn "    ✗ SFTP authentication required for %A" uri
        printfn "      Recommendation: Configure SFTP credentials in settings"
        false

// Test error handling with various inputs
let testDatasets = [
    "/tmp"  // Should exist and be valid
    "nonexistent/path"  // Invalid path
    "http://httpbin.org/status/404"  // Will cause download error
    "sftp://test.com/data.zip"  // Missing SFTP config
]

for dataset in testDatasets do
    printfn "  Testing dataset: %s" dataset
    let success = processDataset dataset
    printfn "    Processing result: %s" (if success then "SUCCESS" else "FAILED")

printfn "\n5. Migration benefits:"
printfn "  ✓ Backward compatibility maintained"
printfn "  ✓ Enhanced error handling and reporting"
printfn "  ✓ Progress tracking for large downloads"
printfn "  ✓ Configurable timeouts and retries"
printfn "  ✓ Extensible provider system"
printfn "  ✓ Type-safe configuration"
printfn "  ✓ Comprehensive test coverage"

printfn "\n=== PRo3D.Viewer integration examples completed ==="