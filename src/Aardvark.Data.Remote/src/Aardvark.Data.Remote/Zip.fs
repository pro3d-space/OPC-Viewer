namespace Aardvark.Data.Remote

open System.IO
open System.IO.Compression

/// Utilities for handling zip file extraction
module Zip =
    
    /// Remove file extension from a path
    let private removeExtension (path: string) =
        if Path.HasExtension path then
            Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))
        else 
            path

    /// Extract zip to directory (name without .zip). If force=true, replace existing directory.
    let extract (zipPath: string) (forceExtract: bool) (logger: Logger.LogCallback option) : Result<string, string> =
        try
            if not (File.Exists zipPath) then
                Error $"Zip file not found: {zipPath}"
            else
                let fileInfo = FileInfo(zipPath)
                Logger.log logger Logger.Info $"[ZIP] Extracting {zipPath} (size: {fileInfo.Length} bytes)"
                
                let targetPath = removeExtension zipPath
                
                // Extract if target directory doesn't exist OR if force extraction is requested
                let shouldExtract = forceExtract || not (Directory.Exists targetPath)
                
                if shouldExtract then
                    // Remove existing directory if force extracting
                    if forceExtract && Directory.Exists targetPath then
                        Logger.log logger Logger.Info $"[ZIP] Force extraction: removing existing directory {targetPath}"
                        Directory.Delete(targetPath, true)
                    
                    Directory.CreateDirectory targetPath |> ignore
                    ZipFile.ExtractToDirectory(zipPath, targetPath)
                    
                    let extractedFiles = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories)
                    Logger.log logger Logger.Info $"[ZIP] Extracted {extractedFiles.Length} files to {targetPath}"
                else
                    Logger.log logger Logger.Debug $"[ZIP] Directory already exists: {targetPath}"
                
                Ok targetPath
        with
        | ex -> Error $"Failed to extract zip file {zipPath}: {ex.Message}"

    
    /// Check if a path is a zip file based on extension
    let isZipFile (path: string) : bool =
        Path.HasExtension path && Path.GetExtension(path).ToLowerInvariant() = ".zip"
    
    /// Get the expected extraction directory for a zip file
    let getExtractionPath (zipPath: string) : string =
        removeExtension zipPath

