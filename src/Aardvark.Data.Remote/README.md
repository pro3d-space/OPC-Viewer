# Aardvark.Data.Remote

A .NET library for resolving remote and local data references with support for HTTP/HTTPS, SFTP, and local file system access. Designed for use with Mars exploration datasets and other large scientific data collections.

## Features

- **Multiple Protocol Support**: Local file system, HTTP/HTTPS downloads, SFTP transfers
- **Automatic Zip Extraction**: Seamless handling of compressed datasets  
- **Smart Caching**: Local caching of remote files to avoid re-downloads
- **Download Integrity**: Lock files prevent corrupted downloads from blocking re-download
- **Progress Reporting**: Built-in progress callbacks for long-running operations
- **Functional API**: Clean F# functional design with aligned C# config-based API
- **Type Safety**: F# discriminated unions ensure compile-time correctness
- **No Initialization**: Pure functional design, no mutable state

## Quick Start

### F# API

```fsharp
open Aardvark.Data.Remote

// Simple usage with default configuration
let result = Fetch.resolve "/path/to/data"

// With custom configuration
let config = { 
    Fetch.defaultConfig with 
        baseDirectory = "/cache"
        progress = Some (printfn "Progress: %.1f%%")
}
let result = Fetch.resolveWith config "http://example.com/dataset.zip"

// Async workflow
async {
    let! result = Fetch.resolveAsync "http://example.com/dataset.zip"
    match result with
    | Resolved path -> printfn "Data available at: %s" path
    | InvalidPath reason -> printfn "Error: %s" reason
    | DownloadError (uri, ex) -> printfn "Download failed: %s" ex.Message
    | SftpConfigMissing uri -> printfn "SFTP config required for: %A" uri
} |> Async.RunSynchronously
```

### C# API

```csharp
using Aardvark.Data.Remote;

// Simple usage with default configuration
var result = Fetch.Resolve("http://example.com/dataset.zip");

// With custom configuration
var config = new FetchConfiguration
{
    BaseDirectory = "/cache",
    MaxRetries = 5,
    ForceDownload = true
};
var result = Fetch.ResolveWith("http://example.com/dataset.zip", config);

// Async with Task
var resultTask = Fetch.ResolveAsync("http://example.com/dataset.zip");
var result = await resultTask;
```

## Supported Data References

### Local File System
- **Absolute directories**: `/path/to/dataset`
- **Relative directories**: `relative/path`
- **Zip files**: `/path/to/dataset.zip` or `relative/dataset.zip`

### Remote Downloads  
- **HTTP/HTTPS**: `http://example.com/dataset.zip`
- **SFTP**: `sftp://user@server.com/dataset.zip`

### Invalid References
- Invalid paths are automatically detected and reported

## Configuration

### F# Configuration

```fsharp
let config = {
    Fetch.defaultConfig with
        baseDirectory = "/data/cache"           // Base directory for relative paths
        sftpConfig = Some sftpConfig           // SFTP connection details  
        maxRetries = 3                         // Retry attempts for downloads
        timeout = TimeSpan.FromMinutes(10.0)   // Operation timeout
        progress = Some progressFn             // Progress reporting
        forceDownload = false                  // Force re-download even if cached
        logger = Some loggerCallback          // Logging callback
}
```

### C# Configuration

```csharp
var config = new FetchConfiguration
{
    BaseDirectory = "/data/cache",
    SftpConfig = sftpConfig,
    MaxRetries = 3,
    Timeout = TimeSpan.FromMinutes(10),
    Progress = percent => Console.WriteLine($"Progress: {percent:F1}%"),
    ForceDownload = false,
    Logger = message => Console.WriteLine(message)
};
```

## API Reference

### F# Functions

| Function | Description |
|----------|-------------|
| `resolve : string -> ResolveResult` | Resolve URL with default config |
| `resolveWith : FetchConfig -> string -> ResolveResult` | Resolve URL with custom config |
| `resolveAsync : string -> Async<ResolveResult>` | Async resolve with default config |
| `resolveAsyncWith : FetchConfig -> string -> Async<ResolveResult>` | Async resolve with custom config |
| `resolveMany : string list -> Async<ResolveResult list>` | Resolve multiple URLs in parallel |
| `resolveManyWith : FetchConfig -> string list -> Async<ResolveResult list>` | Resolve multiple with custom config |

### C# Methods

| Method | Description |
|--------|-------------|
| `Resolve(string url)` | Resolve URL with default config |
| `ResolveWith(string url, FetchConfiguration config)` | Resolve URL with custom config |
| `ResolveAsync(string url)` | Async resolve returning Task |
| `ResolveAsyncWith(string url, FetchConfiguration config)` | Async resolve with custom config |
| `ResolveMany(List<string> urls)` | Resolve multiple URLs in parallel |
| `ResolveManyWith(List<string> urls, FetchConfiguration config)` | Resolve multiple with custom config |

## SFTP Configuration

SFTP support requires connection details:

### F# SFTP Config

```fsharp
let sftpConfig = {
    Host = "sftp.example.com"
    Port = 22
    User = "username" 
    Pass = "password"
}

let config = { 
    Fetch.defaultConfig with 
        sftpConfig = Some sftpConfig 
}
let result = Fetch.resolveWith config "sftp://server.com/dataset.zip"
```

### C# SFTP Config

```csharp
var sftpConfig = new SftpConfig
{
    Host = "sftp.example.com",
    Port = 22,
    User = "username",
    Pass = "password"
};

var config = new FetchConfiguration { SftpConfig = sftpConfig };
var result = Fetch.ResolveWith("sftp://server.com/dataset.zip", config);
```

### FileZilla Configuration

The library can read SFTP settings from FileZilla configuration files:

```fsharp
let config = { 
    Fetch.defaultConfig with 
        sftpConfigFile = Some "/path/to/filezilla.xml"
}
let result = Fetch.resolveWith config "sftp://server.com/dataset.zip"
```

## Progress Reporting

### F# Progress

```fsharp
// Simple progress callback
let config = {
    Fetch.defaultConfig with
        progress = Some (fun percent -> printfn "%.1f%% complete" percent)
}

// With detailed logging
let config = {
    Fetch.defaultConfig with
        logger = Some (fun level msg -> printfn "[%A] %s" level msg)
}
```

### C# Progress

```csharp
// Simple progress callback
var config = new FetchConfiguration
{
    Progress = percent => Console.WriteLine($"{percent:F1}% complete")
};

// With logging
var config = new FetchConfiguration
{
    Logger = message => Console.WriteLine($"[INFO] {message}")
};
```

## Batch Processing

### F# Batch Processing

```fsharp
let urls = [
    "/local/dataset1"
    "http://example.com/dataset2.zip"  
    "sftp://server.com/dataset3.zip"
]

async {
    let! results = Fetch.resolveMany urls
    
    results |> List.iter (fun result ->
        match result with
        | Resolved path -> printfn "Success: %s" path
        | InvalidPath reason -> printfn "Error: %s" reason
        | _ -> ()
    )
} |> Async.RunSynchronously
```

### C# Batch Processing

```csharp
var urls = new List<string>
{
    "/local/dataset1",
    "http://example.com/dataset2.zip",
    "sftp://server.com/dataset3.zip"
};

var results = await Fetch.ResolveMany(urls);

foreach (var result in results)
{
    switch (result)
    {
        case var r when r.IsResolved:
            Console.WriteLine($"Success: {r.Path}");
            break;
        case var r when r.IsInvalidPath:
            Console.WriteLine($"Error: {r.Reason}");
            break;
    }
}
```

## Error Handling

The library uses discriminated unions for type-safe error handling:

```fsharp
match result with
| Resolved path -> 
    // Success: data available at local path
    processDataset path
    
| InvalidPath reason ->
    // Invalid data reference or parsing error
    log.Error("Invalid path: {reason}", reason)
    
| DownloadError (uri, exception) ->
    // Network or download error
    log.Error("Download failed from {uri}: {error}", uri, exception.Message)
    
| SftpConfigMissing uri ->
    // SFTP connection details not provided
    log.Warning("SFTP configuration required for {uri}", uri)
```

## Architecture

The library uses a pure functional design:

- **Immutable Configuration**: All config passed as immutable records/classes
- **Functional Providers**: Provider system uses pure functions, no interfaces
- **No Initialization**: No provider registration or initialization needed
- **No Mutable State**: Everything is immutable and thread-safe

## Integration with PRo3D.Viewer

This library was extracted from PRo3D.Viewer. The viewer uses a compatibility wrapper:

```fsharp
// PRo3D.Viewer usage
let config = { 
    Fetch.defaultConfig with 
        baseDirectory = basedir
        sftpConfig = sftp
        forceDownload = forceDownload
        logger = logger
}
let result = Resolver.resolve config dataRef
```

## Requirements

- .NET 8.0
- F# 8.0+
- Dependencies: SSH.NET (for SFTP), System.Text.Json

## License

This library was developed as part of the PRo3D.Viewer project for Mars exploration data visualization.