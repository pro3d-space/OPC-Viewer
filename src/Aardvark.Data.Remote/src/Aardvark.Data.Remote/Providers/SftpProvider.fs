namespace Aardvark.Data.Remote.Providers

open System
open System.IO
open System.Threading.Tasks
open Renci.SshNet
open Aardvark.Data.Remote
open Aardvark.Data.Remote.Common

/// Provider for SFTP downloads
type SftpProvider() =
    
    interface IDataProvider with
        
        member _.CanHandle(dataRef: DataRef) =
            match dataRef with
            | SftpZip _ -> true
            | _ -> false
        
        member _.ResolveAsync config dataRef =
            task {
                match dataRef with
                | SftpZip uri ->
                    match config.SftpConfig with
                    | Some sftpConfig ->
                        try
                            let localPath = Path.Combine(config.BaseDirectory, "sftp", uri.Host, uri.AbsolutePath.TrimStart('/'))
                            
                            // Use standard download workflow
                            let sftpOperation attempt =
                                task {
                                    use sftpClient = new SftpClient(sftpConfig.Host, sftpConfig.Port, sftpConfig.User, sftpConfig.Pass)
                                    sftpClient.Connect()
                                    
                                    let remotePath = uri.AbsolutePath
                                    Logger.log config.Logger Logger.Info $"[SFTP] Downloading {remotePath} to {localPath}"
                                    
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
                                    Logger.log config.Logger Logger.Info $"[SFTP] Downloaded {fileInfo.Length} bytes to {localPath}"
                                    
                                    // Ensure final 100% is reported
                                    progressReporter 100.0
                                    sftpClient.Disconnect()
                                    
                                    return localPath
                                }
                            
                            let! result = Common.Download.executeWithRetry config localPath sftpOperation
                            
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

/// Module functions for the SftpProvider
module SftpProvider =
    let create, register = Common.Provider.createSingleton SftpProvider