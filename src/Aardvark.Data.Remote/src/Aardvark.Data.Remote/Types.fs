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

/// Configuration for the data resolver
type ResolverConfig = {
    /// Base directory for resolving relative paths and caching downloads
    BaseDirectory: string
    /// SFTP configuration (optional)
    SftpConfig: SftpConfig option
    /// Maximum number of retry attempts for failed downloads
    MaxRetries: int
    /// Timeout for download operations
    Timeout: TimeSpan
    /// Progress callback for download operations (percent complete 0.0-100.0)
    ProgressCallback: (float -> unit) option
    /// Force re-download even if cached file exists
    ForceDownload: bool
    /// Optional logging callback for diagnostic messages
    Logger: Logger.LogCallback option
}
    
/// Default resolver configuration
module ResolverConfig =
    let Default = {
        BaseDirectory = Environment.CurrentDirectory
        SftpConfig = None
        MaxRetries = 3
        Timeout = TimeSpan.FromMinutes(5.0)
        ProgressCallback = None
        ForceDownload = false
        Logger = None
    }