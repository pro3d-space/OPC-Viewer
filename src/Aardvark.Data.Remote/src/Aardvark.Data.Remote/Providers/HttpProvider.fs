namespace Aardvark.Data.Remote.Providers

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open Aardvark.Data.Remote
open Aardvark.Data.Remote.Common

/// Provider for HTTP/HTTPS downloads
type HttpProvider() =
    
    interface IDataProvider with
        
        member _.CanHandle(dataRef: DataRef) =
            match dataRef with
            | HttpZip _ -> true
            | _ -> false
        
        member _.ResolveAsync config dataRef =
            task {
                match dataRef with
                | HttpZip uri ->
                    try
                        // Validate path to prevent directory traversal
                        let relPath = uri.AbsolutePath.Substring(1).Replace("..", "").Replace("~", "")
                        let targetPath = Path.GetFullPath(Path.Combine(config.BaseDirectory, relPath))
                        
                        // Ensure target path is within base directory
                        if not (targetPath.StartsWith(Path.GetFullPath(config.BaseDirectory))) then
                            return InvalidPath $"Path traversal detected: {targetPath}"
                        else
                            // Use standard download workflow
                            let downloadOperation attempt =
                                task {
                                    use httpClient = new HttpClient(Timeout = config.Timeout)
                                    Logger.log config.Logger Logger.Info $"[HTTP] Downloading {uri} to {targetPath}"
                                    
                                    let progressReporter = Common.createRateLimitedReporter config 1000
                                    progressReporter 0.0
                                    
                                    // Use ResponseHeadersRead to start streaming immediately
                                    use! response = httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                                    response.EnsureSuccessStatusCode() |> ignore
                                    
                                    // Get total size if available
                                    let totalBytes = 
                                        if response.Content.Headers.ContentLength.HasValue then
                                            Some response.Content.Headers.ContentLength.Value
                                        else
                                            None
                                    
                                    // Stream to file with progress
                                    use! stream = response.Content.ReadAsStreamAsync()
                                    use fileStream = new FileStream(targetPath, FileMode.Create)
                                    
                                    let buffer = Array.zeroCreate<byte> 8192
                                    let mutable totalRead = 0L
                                    let mutable bytesRead = 0
                                    
                                    // Read stream in chunks
                                    bytesRead <- stream.Read(buffer, 0, buffer.Length)
                                    while bytesRead > 0 do
                                        fileStream.Write(buffer, 0, bytesRead)
                                        totalRead <- totalRead + int64 bytesRead
                                        
                                        // Report progress if we know total size
                                        match totalBytes with
                                        | Some total when total > 0L ->
                                            let percent = float totalRead * 100.0 / float total
                                            progressReporter percent
                                        | _ -> ()
                                        
                                        bytesRead <- stream.Read(buffer, 0, buffer.Length)
                                    
                                    fileStream.Flush()
                                    
                                    let fileInfo = FileInfo(targetPath)
                                    Logger.log config.Logger Logger.Info $"[HTTP] Downloaded {fileInfo.Length} bytes to {targetPath}"
                                    progressReporter 100.0
                                    
                                    return targetPath
                                }
                            
                            let! result = Common.Download.executeWithRetry config targetPath downloadOperation
                            
                            match result with
                            | Ok path -> return Resolved path
                            | Error ex -> return DownloadError (uri, ex)
                    with ex ->
                        return DownloadError (uri, ex)
                        
                | _ ->
                    return InvalidPath "HttpProvider cannot handle this DataRef type"
            }

/// Module functions for the HttpProvider
module HttpProvider =
    let create, register = Common.Provider.createSingleton HttpProvider