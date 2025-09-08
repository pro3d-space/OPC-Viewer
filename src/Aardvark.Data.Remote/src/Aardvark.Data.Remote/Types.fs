namespace Aardvark.Data.Remote

open System
open System.IO

/// Represents different types of data references that can be resolved
type DataRef =
    /// Local directory that may or may not exist
    | LocalDir of path: string * exists: bool 
    /// Relative directory path (resolved relative to base directory)
    | RelativeDir of path: string
    /// Local zip file (absolute path)
    | LocalZip of path: string
    /// Relative zip file path
    | RelativeZip of path: string
    /// HTTP/HTTPS zip file
    | HttpZip of uri: Uri
    /// SFTP zip file  
    | SftpZip of uri: Uri
    /// Invalid data reference with error message
    | Invalid of reason: string

/// Result of attempting to resolve a DataRef to a local directory
type ResolveResult =
    /// Successfully resolved to a local directory
    | Resolved of directoryPath: string
    /// Failed to download from remote location
    | DownloadError of uri: Uri * error: exn
    /// SFTP configuration is missing
    | SftpConfigMissing of uri: Uri
    /// Invalid directory path or other error
    | InvalidPath of reason: string

/// Configuration for SFTP connections (extracted from FileZilla config)
type SftpConfig = {
    Host: string
    Port: int
    User: string  
    Pass: string
}


/// New configuration type for the config-based API
type FetchConfig = {
    /// Base directory for resolving relative paths and caching downloads
    baseDirectory: string
    /// SFTP configuration (optional)
    sftpConfig: SftpConfig option
    /// Path to SFTP configuration file (alternative to sftpConfig)
    sftpConfigFile: string option
    /// Maximum number of retry attempts for failed downloads
    maxRetries: int
    /// Timeout for download operations
    timeout: TimeSpan
    /// Progress callback for download operations (percent complete 0.0-100.0)
    progress: (float -> unit) option
    /// Force re-download even if cached file exists
    forceDownload: bool
    /// Optional logging callback for diagnostic messages
    logger: Logger.LogCallback option
}

/// C# interop class for FetchConfig
[<AllowNullLiteral>]
type FetchConfiguration() =
    /// Base directory for resolving relative paths and caching downloads
    member val BaseDirectory : string = Environment.CurrentDirectory with get, set
    /// SFTP configuration (optional)
    member val SftpConfig : SftpConfig option = None with get, set
    /// Path to SFTP configuration file (alternative to SftpConfig)
    member val SftpConfigFile : string = null with get, set
    /// Maximum number of retry attempts for failed downloads
    member val MaxRetries : int = 3 with get, set
    /// Timeout for download operations
    member val Timeout : TimeSpan = TimeSpan.FromMinutes(5.0) with get, set
    /// Progress callback for download operations (percent complete 0.0-100.0)
    member val Progress : System.Action<float> = null with get, set
    /// Force re-download even if cached file exists
    member val ForceDownload : bool = false with get, set
    /// Optional logging callback for diagnostic messages
    member val Logger : System.Action<string> = null with get, set
    
    /// Convert from F# FetchConfig record
    static member FromFSharp(config: FetchConfig) =
        let csharpConfig = FetchConfiguration()
        csharpConfig.BaseDirectory <- config.baseDirectory
        csharpConfig.SftpConfig <- config.sftpConfig
        csharpConfig.SftpConfigFile <- config.sftpConfigFile |> Option.toObj
        csharpConfig.MaxRetries <- config.maxRetries
        csharpConfig.Timeout <- config.timeout
        csharpConfig.Progress <- 
            match config.progress with
            | Some callback -> System.Action<float>(callback)
            | None -> null
        csharpConfig.ForceDownload <- config.forceDownload
        csharpConfig.Logger <- 
            match config.logger with
            | Some logCallback -> System.Action<string>(fun msg -> logCallback Logger.Info msg)
            | None -> null
        csharpConfig
    
    /// Convert to F# FetchConfig record
    member this.ToFSharp() : FetchConfig = {
        baseDirectory = this.BaseDirectory
        sftpConfig = this.SftpConfig
        sftpConfigFile = Option.ofObj this.SftpConfigFile
        maxRetries = this.MaxRetries
        timeout = this.Timeout
        progress = 
            match this.Progress with
            | null -> None
            | action -> Some action.Invoke
        forceDownload = this.ForceDownload
        logger = 
            match this.Logger with
            | null -> None
            | action -> Some (fun level msg -> action.Invoke msg)
    }

/// FetchConfig utilities
module FetchConfig =
    /// Placeholder function to keep module structure (deprecated - use Fetch.defaultConfig instead)
    [<System.Obsolete("Use Fetch.defaultConfig instead")>]
    let internal deprecated() = ()

/// Functional provider record to replace IDataProvider interface
type Provider = {
    /// Check if this provider can handle the given DataRef
    canHandle: DataRef -> bool
    /// Resolve a DataRef to a local directory path using the given configuration
    resolve: FetchConfig -> DataRef -> Async<ResolveResult>
}