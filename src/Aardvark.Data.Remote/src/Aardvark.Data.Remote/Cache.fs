namespace Aardvark.Data.Remote

open System
open System.IO

/// Caching utilities for downloaded files
module Cache =
    
    /// Cache entry information
    type CacheEntry = {
        FilePath: string
        CachedAt: DateTime
        Size: int64
        Uri: Uri option
    }
    
    /// Check if a cached file exists and is valid
    let isValidCacheEntry (filePath: string) : bool =
        File.Exists(filePath)
    
    /// Get cache information for a file
    let getCacheInfo (filePath: string) : CacheEntry option =
        try
            if File.Exists(filePath) then
                let fileInfo = FileInfo(filePath)
                Some {
                    FilePath = filePath
                    CachedAt = fileInfo.LastWriteTimeUtc
                    Size = fileInfo.Length
                    Uri = None
                }
            else
                None
        with
        | _ -> None
    
    /// Clean up cache directory by removing files older than specified age
    let cleanOldFiles (cacheDir: string) (maxAge: TimeSpan) : int =
        try
            if Directory.Exists(cacheDir) then
                let cutoff = DateTime.UtcNow - maxAge
                let files = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories)
                
                let mutable deletedCount = 0
                for filePath in files do
                    try
                        let fileInfo = FileInfo(filePath)
                        if fileInfo.LastWriteTimeUtc < cutoff then
                            File.Delete(filePath)
                            deletedCount <- deletedCount + 1
                    with
                    | _ -> () // Ignore individual file errors
                
                deletedCount
            else
                0
        with
        | _ -> 0
    
    /// Get total cache size in bytes
    let getCacheSize (cacheDir: string) : int64 =
        try
            if Directory.Exists(cacheDir) then
                let files = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories)
                files 
                |> Array.sumBy (fun path -> 
                    try (FileInfo(path)).Length
                    with | _ -> 0L)
            else
                0L
        with
        | _ -> 0L
    
    /// Clear entire cache directory
    let clearCache (cacheDir: string) : Result<unit, string> =
        try
            if Directory.Exists(cacheDir) then
                Directory.Delete(cacheDir, true)
            Ok ()
        with
        | ex -> Error $"Failed to clear cache: {ex.Message}"