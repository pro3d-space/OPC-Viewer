namespace Aardvark.Data.Remote

open System
open System.IO
open System.Threading.Tasks

/// Common helper functions to reduce code duplication
module Common =
    
    /// Report progress if callback is configured
    let reportProgress (config: ResolverConfig) (percent: float) =
        config.ProgressCallback |> Option.iter (fun cb -> cb percent)
    
    /// Create a rate-limited progress reporter that limits updates to specified interval
    let createRateLimitedReporter (config: ResolverConfig) (intervalMs: int) =
        let mutable lastReportTime = DateTime.MinValue
        fun (percent: float) ->
            let now = DateTime.UtcNow
            let elapsed = (now - lastReportTime).TotalMilliseconds
            // Always report 0% and 100%, or if interval elapsed
            if percent <= 0.0 || percent >= 100.0 || elapsed >= float intervalMs then
                lastReportTime <- now
                reportProgress config percent
    
    /// Execute an async operation with retry logic and exponential backoff
    let retryAsync (config: ResolverConfig) (operation: int -> Task<'T>) : Task<Result<'T, exn>> =
        task {
            let mutable lastException = None
            let mutable success = false
            let mutable result = Unchecked.defaultof<'T>
            
            for attempt = 1 to config.MaxRetries do
                if not success then
                    try
                        let! opResult = operation attempt
                        result <- opResult
                        success <- true
                    with ex ->
                        lastException <- Some ex
                        if attempt < config.MaxRetries then
                            // Exponential backoff: 2^(attempt-1) seconds
                            let delay = TimeSpan.FromSeconds(float (2.0 ** float (attempt - 1)))
                            do! Task.Delay(delay)
            
            if success then
                return Ok result
            else
                match lastException with
                | Some ex -> return Error ex
                | None -> return Error (Exception("Operation failed after all retries"))
        }
    
    /// Lock file utilities for download integrity
    module LockFile =
        
        /// Generate lock file path for a target file
        let getLockFilePath (targetPath: string) : string =
            targetPath + ".downloading"
        
        /// Create a lock file before starting download
        let create (targetPath: string) : unit =
            try
                let lockPath = getLockFilePath targetPath
                let lockDir = Path.GetDirectoryName(lockPath)
                if not (Directory.Exists(lockDir)) then
                    Directory.CreateDirectory(lockDir) |> ignore
                File.WriteAllText(lockPath, DateTime.UtcNow.ToString("O"))
            with
            | _ -> () // Silently ignore lock file creation failures
        
        /// Remove lock file after successful download
        let remove (targetPath: string) : unit =
            try
                let lockPath = getLockFilePath targetPath
                if File.Exists(lockPath) then
                    File.Delete(lockPath)
            with
            | _ -> () // Silently ignore lock file removal failures
        
        /// Check if target file has incomplete download (lock file exists)
        let isIncomplete (targetPath: string) : bool =
            try
                let lockPath = getLockFilePath targetPath
                File.Exists(lockPath)
            with
            | _ -> false // Assume complete if we can't check lock file
        
        /// Check if cached file is valid (exists and no lock file)
        let isValidCache (targetPath: string) : bool =
            File.Exists(targetPath) && not (isIncomplete targetPath)
        
        /// Execute a download operation with lock file management
        let withLockFile (targetPath: string) (downloadOperation: unit -> Task<Result<'T, exn>>) : Task<Result<'T, exn>> =
            task {
                try
                    // Create lock file before starting download
                    create targetPath
                    
                    // Execute the download operation
                    let! result = downloadOperation()
                    
                    match result with
                    | Ok value ->
                        // Remove lock file after successful download
                        remove targetPath
                        return Ok value
                    | Error _ ->
                        // Leave lock file in place to indicate incomplete download
                        return result
                with
                | ex ->
                    // Leave lock file in place to indicate incomplete download
                    return Error ex
            }
    
    /// Common download workflow patterns
    module Download =
        
        /// Ensure target directory exists
        let ensureDirectoryExists (targetPath: string) : unit =
            let targetDir = Path.GetDirectoryName(targetPath)
            if not (Directory.Exists(targetDir)) then
                Directory.CreateDirectory(targetDir) |> ignore
        
        /// Standard download workflow with cache validation, lock file management, and retry logic
        let executeWithRetry (config: ResolverConfig) (targetPath: string) (downloadOperation: int -> Task<string>) : Task<Result<string, exn>> =
            task {
                // Check if we should download (not cached, force download, or cache invalid)
                if config.ForceDownload || not (LockFile.isValidCache targetPath) then
                    // Ensure target directory exists
                    ensureDirectoryExists targetPath
                    
                    // Execute download with lock file management and retry logic
                    let downloadWithLock () = retryAsync config downloadOperation
                    let! result = LockFile.withLockFile targetPath downloadWithLock
                    return result
                else
                    // File is cached and valid
                    return Ok targetPath
            }

    /// Create a singleton provider with standard registration functions
    module Provider =
        
        /// Create singleton instance functions for a provider
        let createSingleton<'T when 'T :> IDataProvider> (provider: unit -> 'T) =
            let instance = lazy (provider() :> IDataProvider)
            
            let create() = instance.Value
            
            let register() =
                let p = create()
                ProviderRegistry.register p
                p
            
            (create, register)