namespace Aardvark.Data.Remote.Providers

open System
open System.IO
open Renci.SshNet
open Aardvark.Data.Remote
open Aardvark.Data.Remote.Common

/// Functional SFTP provider module
module SftpProvider =
    
    /// Check if this provider can handle the given DataRef
    let canHandle dataRef =
        match dataRef with
        | SftpZip _ -> true
        | _ -> false
    
    /// Resolve a DataRef using the SFTP provider
    let resolve (config: FetchConfig) dataRef =
        async {
                match dataRef with
                | SftpZip uri ->
                    match config.sftpConfig with
                    | Some sftpConfig ->
                        try
                            let localPath = Path.Combine(config.baseDirectory, "sftp", uri.Host, uri.AbsolutePath.TrimStart('/'))
                            
                            // Use standard download workflow
                            let sftpOperation attempt =
                                async {
                                    use sftpClient = new SftpClient(sftpConfig.Host, sftpConfig.Port, sftpConfig.User, sftpConfig.Pass)
                                    sftpClient.Connect()
                                    
                                    let remotePath = uri.AbsolutePath
                                    Logger.log config.logger Logger.Info $"[SFTP] Downloading {remotePath} to {localPath}"
                                    
                                    let progressReporter = Common.createRateLimitedReporter config 1000
                                    progressReporter 0.0
                                    
                                    // Get file size for progress calculation
                                    let attrs = sftpClient.GetAttributes(remotePath)
                                    let totalBytes = attrs.Size
                                    
                                    use fileStream = new FileStream(localPath, FileMode.Create)
                                    
                                    // Download with progress callback
                                    sftpClient.DownloadFile(remotePath, fileStream, fun bytesDownloaded ->
                                        let percent = float bytesDownloaded * 100.0 / float totalBytes
                                        progressReporter percent
                                    )
                                    
                                    fileStream.Flush()
                                    
                                    let fileInfo = FileInfo(localPath)
                                    Logger.log config.logger Logger.Info $"[SFTP] Downloaded {fileInfo.Length} bytes to {localPath}"
                                    
                                    // Ensure final 100% is reported
                                    progressReporter 100.0
                                    sftpClient.Disconnect()
                                    
                                    return localPath
                                }
                            
                            let sftpTask attempt = sftpOperation attempt |> Async.StartAsTask
                            let! result = Common.Download.executeWithRetry config localPath sftpTask |> Async.AwaitTask
                            
                            match result with
                            | Ok path -> return Resolved path
                            | Error ex -> return DownloadError (uri, ex)
                                
                        with ex ->
                            return DownloadError (uri, ex)
                            
                    | None ->
                        return SftpConfigMissing uri
                        
                | _ ->
                    return InvalidPath "SftpProvider cannot handle this DataRef type"
        }
    
    /// Provider record instance
    let provider : Provider = {
        canHandle = canHandle
        resolve = resolve
    }