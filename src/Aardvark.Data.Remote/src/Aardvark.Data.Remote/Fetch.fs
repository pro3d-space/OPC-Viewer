namespace Aardvark.Data.Remote

open System
open System.Threading.Tasks

/// Clean functional API for DataRef resolution
module Fetch =
    
    /// Default configuration for fetch operations
    let defaultConfig = {
        baseDirectory = Environment.CurrentDirectory
        sftpConfig = None
        sftpConfigFile = None
        maxRetries = 3
        timeout = TimeSpan.FromMinutes(5.0)
        progress = None
        forceDownload = false
        logger = None
    }
    
    // ============================================================
    // F# API - Idiomatic functional design with Async workflows
    // ============================================================
    
    /// <summary>Resolves a URL or path to a local directory using default configuration.</summary>
    /// <param name="input">URL or file path to resolve (local, HTTP/HTTPS, or SFTP)</param>
    /// <returns>ResolveResult indicating success with local path or specific error type</returns>
    /// <example>
    /// <code>
    /// let result = Fetch.resolve "http://example.com/data.zip"
    /// match result with
    /// | Resolved path -> printfn "Data available at: %s" path
    /// | InvalidPath reason -> printfn "Error: %s" reason
    /// </code>
    /// </example>
    let resolve (input: string) : ResolveResult =
        let dataRef = Parser.parse input
        Resolver.resolve defaultConfig dataRef
    
    /// <summary>Resolves a URL or path to a local directory using custom configuration.</summary>
    /// <param name="config">Custom FetchConfig with specific settings</param>
    /// <param name="input">URL or file path to resolve</param>
    /// <returns>ResolveResult indicating success with local path or specific error type</returns>
    /// <example>
    /// <code>
    /// let config = { Fetch.defaultConfig with baseDirectory = "/cache" }
    /// let result = Fetch.resolveWith config "http://example.com/data.zip"
    /// </code>
    /// </example>
    let resolveWith (config: FetchConfig) (input: string) : ResolveResult =
        let dataRef = Parser.parse input
        Resolver.resolve config dataRef
    
    /// <summary>Asynchronously resolves a URL or path using default configuration.</summary>
    /// <param name="input">URL or file path to resolve</param>
    /// <returns>Async workflow yielding ResolveResult</returns>
    /// <example>
    /// <code>
    /// async {
    ///     let! result = Fetch.resolveAsync "http://example.com/data.zip"
    ///     match result with
    ///     | Resolved path -> printfn "Downloaded to: %s" path
    ///     | _ -> printfn "Failed to resolve"
    /// }
    /// </code>
    /// </example>
    let resolveAsync (input: string) : Async<ResolveResult> =
        let dataRef = Parser.parse input
        Resolver.resolveAsync defaultConfig dataRef
    
    /// <summary>Asynchronously resolves a URL or path using custom configuration.</summary>
    /// <param name="config">Custom FetchConfig with specific settings</param>
    /// <param name="input">URL or file path to resolve</param>
    /// <returns>Async workflow yielding ResolveResult</returns>
    /// <example>
    /// <code>
    /// let config = { Fetch.defaultConfig with maxRetries = 10 }
    /// async {
    ///     let! result = Fetch.resolveAsyncWith config "sftp://server.com/data.zip"
    ///     // Handle result...
    /// }
    /// </code>
    /// </example>
    let resolveAsyncWith (config: FetchConfig) (input: string) : Async<ResolveResult> =
        let dataRef = Parser.parse input
        Resolver.resolveAsync config dataRef
    
    /// <summary>Resolves multiple URLs or paths in parallel using default configuration.</summary>
    /// <param name="inputs">List of URLs or file paths to resolve</param>
    /// <returns>Async workflow yielding list of ResolveResult in same order as inputs</returns>
    /// <example>
    /// <code>
    /// let urls = ["local/path"; "http://example.com/data1.zip"; "http://example.com/data2.zip"]
    /// async {
    ///     let! results = Fetch.resolveMany urls
    ///     // Process results...
    /// }
    /// </code>
    /// </example>
    let resolveMany (inputs: string list) : Async<ResolveResult list> =
        let dataRefs = inputs |> List.map Parser.parse
        Resolver.resolveMany defaultConfig dataRefs
    
    /// <summary>Resolves multiple URLs or paths in parallel using custom configuration.</summary>
    /// <param name="config">Custom FetchConfig with specific settings</param>
    /// <param name="inputs">List of URLs or file paths to resolve</param>
    /// <returns>Async workflow yielding list of ResolveResult in same order as inputs</returns>
    /// <example>
    /// <code>
    /// let config = { Fetch.defaultConfig with baseDirectory = "/batch-cache" }
    /// let urls = ["data1.zip"; "data2.zip"; "data3.zip"]
    /// async {
    ///     let! results = Fetch.resolveManyWith config urls
    ///     // All downloads will use /batch-cache as base directory
    /// }
    /// </code>
    /// </example>
    let resolveManyWith (config: FetchConfig) (inputs: string list) : Async<ResolveResult list> =
        let dataRefs = inputs |> List.map Parser.parse
        Resolver.resolveMany config dataRefs
    
    // ============================================================
    // C# API - Modern config-based design with Task workflows
    // ============================================================
    
    /// Resolve a URL with default configuration (C# API)
    let Resolve (input: string) : ResolveResult =
        resolve input
    
    /// Resolve a URL with custom configuration (C# API)
    let ResolveWith (input: string) (config: FetchConfiguration) : ResolveResult =
        let fsharpConfig = config.ToFSharp()
        resolveWith fsharpConfig input
    
    /// Resolve a URL asynchronously with default configuration (C# API)
    let ResolveAsync (input: string) : Task<ResolveResult> =
        resolveAsync input |> Async.StartAsTask
    
    /// Resolve a URL asynchronously with custom configuration (C# API)
    let ResolveAsyncWith (input: string) (config: FetchConfiguration) : Task<ResolveResult> =
        let fsharpConfig = config.ToFSharp()
        resolveAsyncWith fsharpConfig input |> Async.StartAsTask
    
    /// Resolve multiple URLs in parallel with default configuration (C# API)
    let ResolveMany (inputs: string list) : Task<ResolveResult list> =
        resolveMany inputs |> Async.StartAsTask
    
    /// Resolve multiple URLs in parallel with custom configuration (C# API)
    let ResolveManyWith (inputs: string list) (config: FetchConfiguration) : Task<ResolveResult list> =
        let fsharpConfig = config.ToFSharp()
        resolveManyWith fsharpConfig inputs |> Async.StartAsTask