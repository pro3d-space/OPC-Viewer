// Basic usage examples for Aardvark.Data.Remote
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open Aardvark.Data.Remote

printfn "=== Aardvark.Data.Remote Basic Usage Examples ==="

// Example 1: Simple local directory
printfn "\n1. Local directory resolution:"
let localResult = Fetch.Resolve "/tmp"
match localResult with
| Resolved path -> printfn "✓ Local directory resolved to: %s" path
| InvalidPath reason -> printfn "✗ Error: %s" reason
| _ -> printfn "✗ Unexpected result"

// Example 2: Parsing and validation
printfn "\n2. Parsing and validation:"
let inputs = [
    "/valid/local/path"
    "relative/path"
    "http://example.com/data.zip"
    "sftp://server.com/data.zip"
    "invalid://bad-scheme"
    ""
]

for input in inputs do
    let parsed = Parser.parse input
    let isValid = Parser.isValid parsed
    let description = Parser.describe parsed
    printfn "  %s -> Valid: %b, %s" input isValid description

// Example 3: Configuration builder
printfn "\n3. Configuration with builder pattern:"
let configuredResult = 
    Fetch
        .From("relative/dataset")
        .WithBaseDirectory(System.IO.Directory.GetCurrentDirectory())
        .WithMaxRetries(5)
        .WithTimeout(TimeSpan.FromMinutes(2.0))
        .WithProgress(fun percent -> printf "\rProgress: %.1f%%" percent; Console.Out.Flush())

let config = configuredResult.GetConfig()
printfn "  Base directory: %s" config.BaseDirectory
printfn "  Max retries: %d" config.MaxRetries
printfn "  Timeout: %A" config.Timeout
printfn "  Has progress callback: %b" config.ProgressCallback.IsSome

// Example 4: Error handling patterns
printfn "\n4. Error handling patterns:"
let testCases = [
    ("Valid local", "/tmp")
    ("Invalid URL", "invalid://example.com/data.zip")
    ("Missing SFTP config", "sftp://server.com/data.zip")
    ("Non-zip HTTP", "http://example.com/data.txt")
]

for (description, input) in testCases do
    let result = Fetch.Resolve input
    printf "  %s: " description
    match result with
    | Resolved path -> printfn "SUCCESS -> %s" path
    | InvalidPath reason -> printfn "INVALID -> %s" reason
    | SftpConfigMissing uri -> printfn "SFTP CONFIG MISSING -> %A" uri
    | DownloadError (uri, ex) -> printfn "DOWNLOAD ERROR -> %s" ex.Message

// Example 5: Manual resolution with custom config
printfn "\n5. Manual resolution with custom configuration:"
let customConfig = {
    ResolverConfig.Default with
        BaseDirectory = "/custom/base"
        MaxRetries = 10
        Timeout = TimeSpan.FromSeconds(30.0)
}

let dataRef = Parser.parse "relative/test/path"
let manualResult = Resolver.resolve customConfig dataRef
match manualResult with
| Resolved path -> printfn "✓ Resolved to: %s" path
| InvalidPath reason -> printfn "✗ Failed: %s" reason
| _ -> printfn "✗ Other error"

printfn "\n=== Examples completed ==="