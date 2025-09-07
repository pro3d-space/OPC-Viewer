// Advanced usage examples for Aardvark.Data.Remote
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open System.Threading.Tasks
open Aardvark.Data.Remote

printfn "=== Aardvark.Data.Remote Advanced Usage Examples ==="

// Example 1: Progress reporting
printfn "\n1. Progress reporting with different callbacks:"

// Simple progress callback
let simpleProgress percent =
    printf "\rSimple progress: %.1f%%    " percent
    Console.Out.Flush()

// Detailed progress callback
let detailedProgress (info: Progress.ProgressInfo) =
    let dataRefDesc = 
        info.DataRef 
        |> Option.map Parser.describe 
        |> Option.defaultValue "Unknown"
    printf "\r[%s] %s: %.1f%%    " dataRefDesc info.Operation info.Percentage
    Console.Out.Flush()

// Create a test directory for demonstration
let testDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aardvark-test")
System.IO.Directory.CreateDirectory(testDir) |> ignore
printfn "  Created test directory: %s" testDir

let progressTest = 
    Fetch
        .From("test-relative-path")
        .WithBaseDirectory(testDir)
        .WithProgress(simpleProgress)
        .Resolve()

printfn "\n  Progress test result: %A" progressTest

// Example 2: SFTP configuration
printfn "\n2. SFTP configuration examples:"

// Manual SFTP config
let sftpConfig = {
    Host = "example.com"
    Port = 22
    User = "testuser"
    Pass = "testpass"
}

let sftpBuilder = 
    Fetch
        .From("sftp://example.com/dataset.zip")
        .WithSftpConfig(sftpConfig)
        .WithConsoleProgress()

printfn "  SFTP config: %A" (sftpBuilder.GetConfig().SftpConfig)

// Example 3: Batch processing
printfn "\n3. Batch processing multiple data references:"

let batchInputs = [
    "/tmp"
    "relative/path/1"
    "relative/path/2" 
    "http://httpbin.org/status/200"  // This will fail but demonstrates error handling
    "invalid://bad.url"
]

let batchTask = Fetch.ResolveMany batchInputs
// Note: In a real scenario, you'd await this properly
printfn "  Batch processing task created for %d inputs" batchInputs.Length

// Example 4: Custom provider implementation
printfn "\n4. Custom provider implementation:"

// Example custom provider that handles a fictional protocol
type ExampleProvider() =
    interface IDataProvider with
        member _.CanHandle(dataRef: DataRef) =
            match dataRef with
            | Invalid reason when reason.StartsWith("example://") -> true
            | _ -> false
            
        member _.ResolveAsync(config: ResolverConfig) (dataRef: DataRef) =
            task {
                match dataRef with
                | Invalid reason when reason.StartsWith("example://") ->
                    // Simulate some processing
                    do! Task.Delay(100)
                    let path = System.IO.Path.Combine(config.BaseDirectory, "example-data")
                    System.IO.Directory.CreateDirectory(path) |> ignore
                    return Resolved path
                | _ ->
                    return InvalidPath "ExampleProvider cannot handle this DataRef"
            }

// Register the custom provider
let customProvider = ExampleProvider() :> IDataProvider
ProviderRegistry.register customProvider

// Test the custom provider
let customResult = Parser.parse "example://custom-data"
printfn "  Custom DataRef: %A" customResult
printfn "  Custom provider can handle: %b" (customProvider.CanHandle customResult)

// Example 5: Utility functions and inspection
printfn "\n5. Utility functions and inspection:"

let testDataRefs = [
    Parser.parse "/absolute/path"
    Parser.parse "relative/path"
    Parser.parse "/path/to/file.zip"
    Parser.parse "http://example.com/data.zip"
    Parser.parse "sftp://server.com/data.zip"
    Parser.parse "invalid-input"
]

for dataRef in testDataRefs do
    printfn "  DataRef: %s" (Parser.describe dataRef)
    printfn "    Valid: %b" (Parser.isValid dataRef)
    
    // Check which providers can handle this DataRef
    let providers = ProviderRegistry.getProviders()
    let capableProviders = 
        providers 
        |> List.filter (fun p -> p.CanHandle dataRef)
        |> List.length
    printfn "    Capable providers: %d/%d" capableProviders providers.Length

// Example 6: Configuration inspection
printfn "\n6. Configuration inspection:"

let complexBuilder = 
    Fetch
        .From("test/data")
        .WithBaseDirectory("/custom/base")
        .WithMaxRetries(5)
        .WithTimeout(TimeSpan.FromMinutes(5.0))
        .WithProgress(fun _ -> ())

let complexConfig = complexBuilder.GetConfig()
printfn "  Configuration details:"
printfn "    Base Directory: %s" complexConfig.BaseDirectory
printfn "    Max Retries: %d" complexConfig.MaxRetries
printfn "    Timeout: %A" complexConfig.Timeout
printfn "    Has Progress Callback: %b" complexConfig.ProgressCallback.IsSome
printfn "    Has SFTP Config: %b" complexConfig.SftpConfig.IsSome

// Cleanup
System.IO.Directory.Delete(testDir, true)
printfn "\n  Cleaned up test directory"

printfn "\n=== Advanced examples completed ==="