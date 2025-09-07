namespace Aardvark.Data.Remote

open System
open System.Threading.Tasks

/// Fluent API builder for DataRef resolution
type DataRefBuilder(dataRef: DataRef) =
    
    let mutable config = ResolverConfig.Default
    
    /// Set the base directory for resolving relative paths and caching
    member this.WithBaseDirectory(baseDir: string) =
        config <- { config with BaseDirectory = baseDir }
        this
    
    /// Set SFTP configuration
    member this.WithSftpConfig(sftpConfig: SftpConfig) =
        config <- { config with SftpConfig = Some sftpConfig }
        this
    
    /// Set SFTP configuration from FileZilla config file
    member this.WithSftpConfigFile(filePath: string) =
        match FileZillaConfig.tryParseFile filePath with
        | Some sftpConfig -> 
            config <- { config with SftpConfig = Some sftpConfig }
            this
        | None -> 
            this // Silently ignore invalid config files
    
    /// Set maximum retry attempts
    member this.WithMaxRetries(maxRetries: int) =
        config <- { config with MaxRetries = maxRetries }
        this
    
    /// Set timeout for operations
    member this.WithTimeout(timeout: TimeSpan) =
        config <- { config with Timeout = timeout }
        this
    
    /// Set progress callback (simple version)
    member this.WithProgress(progressCallback: float -> unit) =
        config <- { config with ProgressCallback = Some progressCallback }
        this
    
    /// Set detailed progress callback
    member this.WithDetailedProgress(progressCallback: Progress.DetailedProgressCallback) =
        let simpleCallback = Progress.toSimpleCallback progressCallback
        config <- { config with ProgressCallback = Some simpleCallback }
        this
    
    /// Remove progress callback
    member this.WithoutProgress() =
        config <- { config with ProgressCallback = None }
        this
    
    /// Use console progress reporting
    member this.WithConsoleProgress() =
        let consoleCallback = Progress.toSimpleCallback Progress.console
        config <- { config with ProgressCallback = Some consoleCallback }
        this
    
    /// Set logging callback
    member this.WithLogger(logger: Logger.LogCallback) =
        config <- { config with Logger = Some logger }
        this
    
    /// Enable verbose console logging
    member this.WithVerbose(verbose: bool) =
        let logger = if verbose 
                     then Some (Logger.console Logger.Info)
                     else None
        config <- { config with Logger = logger }
        this
    
    /// Enable debug-level console logging
    member this.WithDebugLogging() =
        config <- { config with Logger = Some (Logger.console Logger.Debug) }
        this
    
    /// Get the current configuration
    member _.GetConfig() = config
    
    /// Resolve the DataRef synchronously
    member _.Resolve() : ResolveResult =
        // Initialize providers if not already done
        Resolver.initializeDefaultProviders()
        Resolver.resolve config dataRef
    
    /// Resolve the DataRef asynchronously
    member _.ResolveAsync() : Task<ResolveResult> =
        // Initialize providers if not already done
        Resolver.initializeDefaultProviders()
        Resolver.resolveAsync config dataRef

/// Unified API module for DataRef resolution
/// Provides both idiomatic F# pipeline and C# builder patterns
module Fetch =
    
    /// Parse a string into a DataRef
    let from (input: string) : DataRef =
        Parser.parse input
    
    /// Create a configuration with the given DataRef
    let private withConfig (dataRef: DataRef) : DataRef * ResolverConfig =
        (dataRef, ResolverConfig.Default)
    
    /// Set the base directory for resolving relative paths and caching
    let withBaseDirectory (baseDir: string) (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with BaseDirectory = baseDir })
    
    /// Set SFTP configuration
    let withSftpConfig (sftpConfig: SftpConfig) (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with SftpConfig = Some sftpConfig })
    
    /// Set SFTP configuration from FileZilla config file
    let withSftpConfigFile (filePath: string) (dataRef: DataRef, config: ResolverConfig) =
        match FileZillaConfig.tryParseFile filePath with
        | Some sftpConfig -> 
            (dataRef, { config with SftpConfig = Some sftpConfig })
        | None -> 
            (dataRef, config) // Silently ignore invalid config files
    
    /// Set maximum retry attempts
    let withMaxRetries (maxRetries: int) (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with MaxRetries = maxRetries })
    
    /// Set timeout for operations
    let withTimeout (timeout: TimeSpan) (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with Timeout = timeout })
    
    /// Set progress callback (simple version)
    let withProgress (progressCallback: float -> unit) (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with ProgressCallback = Some progressCallback })
    
    /// Set detailed progress callback
    let withDetailedProgress (progressCallback: Progress.DetailedProgressCallback) (dataRef: DataRef, config: ResolverConfig) =
        let simpleCallback = Progress.toSimpleCallback progressCallback
        (dataRef, { config with ProgressCallback = Some simpleCallback })
    
    /// Remove progress callback
    let withoutProgress (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with ProgressCallback = None })
    
    /// Use console progress reporting
    let withConsoleProgress (dataRef: DataRef, config: ResolverConfig) =
        let consoleCallback = Progress.toSimpleCallback Progress.console
        (dataRef, { config with ProgressCallback = Some consoleCallback })
    
    /// Set logging callback
    let withLogger (logger: Logger.LogCallback) (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with Logger = Some logger })
    
    /// Enable verbose console logging
    let withVerbose (verbose: bool) (dataRef: DataRef, config: ResolverConfig) =
        let logger = if verbose 
                     then Some (Logger.console Logger.Info)
                     else None
        (dataRef, { config with Logger = logger })
    
    /// Enable debug-level console logging
    let withDebugLogging (dataRef: DataRef, config: ResolverConfig) =
        (dataRef, { config with Logger = Some (Logger.console Logger.Debug) })
    
    /// Resolve the DataRef synchronously
    let resolve (dataRef: DataRef, config: ResolverConfig) : ResolveResult =
        // Initialize providers if not already done
        Resolver.initializeDefaultProviders()
        Resolver.resolve config dataRef
    
    /// Resolve the DataRef asynchronously
    let resolveAsync (dataRef: DataRef, config: ResolverConfig) : Task<ResolveResult> =
        // Initialize providers if not already done
        Resolver.initializeDefaultProviders()
        Resolver.resolveAsync config dataRef
    
    // Convenience functions that work directly with DataRef (no config tuple)
    
    /// Resolve a DataRef with default configuration
    let resolveDefault (dataRef: DataRef) : ResolveResult =
        (dataRef, ResolverConfig.Default) |> resolve
    
    /// Resolve a DataRef asynchronously with default configuration  
    let resolveDefaultAsync (dataRef: DataRef) : Task<ResolveResult> =
        (dataRef, ResolverConfig.Default) |> resolveAsync
    
    /// Start a pipeline with configuration
    let configure (dataRef: DataRef) : DataRef * ResolverConfig =
        withConfig dataRef
    
    // Quick resolution functions that work directly with strings
    
    /// Quick resolve a string with default configuration
    let quickResolve (input: string) : ResolveResult =
        input |> from |> resolveDefault
    
    /// Quick async resolve a string with default configuration
    let quickResolveAsync (input: string) : Task<ResolveResult> =
        input |> from |> resolveDefaultAsync
    
    /// Resolve multiple inputs in parallel
    let resolveMany (inputs: string list) : Task<(string * ResolveResult) list> =
        task {
            // Initialize providers
            Resolver.initializeDefaultProviders()
            
            let dataRefs = inputs |> List.map (fun input -> (input, from input))
            let tasks = dataRefs |> List.map (fun (input, dataRef) -> 
                task {
                    let! result = resolveDefaultAsync dataRef
                    return (input, result)
                })
            
            let! results = Task.WhenAll(tasks)
            return results |> Array.toList
        }
    
    // ============================================================
    // Builder-style API for C# interop (PascalCase)
    // ============================================================
    
    /// Start building a DataRef resolution from a string (Builder style)
    let From (input: string) : DataRefBuilder =
        let dataRef = Parser.parse input
        DataRefBuilder(dataRef)
    
    /// Start building a DataRef resolution from a parsed DataRef (Builder style)
    let FromParsed (dataRef: DataRef) : DataRefBuilder =
        DataRefBuilder(dataRef)
    
    /// Quick resolve with default configuration (Builder style)
    let Resolve (input: string) : ResolveResult =
        From(input).Resolve()
    
    /// Quick async resolve with default configuration (Builder style)
    let ResolveAsync (input: string) : Task<ResolveResult> =
        From(input).ResolveAsync()
    
    /// Resolve multiple inputs in parallel (Builder style)
    let ResolveMany (inputs: string list) : Task<(string * ResolveResult) list> =
        task {
            // Initialize providers
            Resolver.initializeDefaultProviders()
            
            let dataRefs = inputs |> List.map (fun input -> (input, Parser.parse input))
            let tasks = dataRefs |> List.map (fun (input, dataRef) -> 
                task {
                    let! result = Resolver.resolveAsync ResolverConfig.Default dataRef
                    return (input, result)
                })
            
            let! results = Task.WhenAll(tasks)
            return results |> Array.toList
        }