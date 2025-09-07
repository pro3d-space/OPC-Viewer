namespace Aardvark.Data.Remote

open System
open System.IO

/// Parses strings into DataRef instances
module Parser =
    
    /// Parse a string into a DataRef
    let parse (input: string) : DataRef =
        
        let handleAbsolutePath (path: string) : DataRef =
            if Directory.Exists path then
                LocalDir (path, true)
            else
                if Path.HasExtension path && Path.GetExtension(path).ToLowerInvariant() = ".zip" then
                    LocalZip path
                else
                    LocalDir (path, false)

        let handleRelativePath (path: string) : DataRef =
            if Path.HasExtension path && Path.GetExtension(path).ToLowerInvariant() = ".zip" then
                RelativeZip path
            else
                RelativeDir path

        let isZipUri (uri: Uri) : bool =
            let path = uri.AbsolutePath
            Path.HasExtension path && Path.GetExtension(path).ToLowerInvariant() = ".zip"

        if String.IsNullOrWhiteSpace(input) then
            Invalid "Input string cannot be null or empty"
        else
            try
                let uri = Uri(input)
                match uri.Scheme.ToLowerInvariant() with
                | "http" | "https" -> 
                    if isZipUri uri then 
                        HttpZip uri 
                    else 
                        Invalid $"HTTP/HTTPS URLs must point to .zip files: {input}"
                | "sftp" -> 
                    if isZipUri uri then 
                        SftpZip uri 
                    else 
                        Invalid $"SFTP URLs must point to .zip files: {input}"
                | "file" -> 
                    handleAbsolutePath uri.LocalPath
                | _ -> 
                    Invalid $"Unsupported URI scheme: {uri.Scheme}"
            with
            | :? UriFormatException ->
                // Not a valid URI, treat as file path
                if Path.IsPathRooted(input) then
                    handleAbsolutePath input
                elif Path.GetInvalidPathChars() |> Array.exists (fun c -> input.Contains(c)) then
                    Invalid $"Path contains invalid characters: {input}"
                else
                    handleRelativePath input
            | ex ->
                Invalid $"Failed to parse input: {ex.Message}"

    /// Try to parse a string into a DataRef, returning Result type
    let tryParse (input: string) : Result<DataRef, string> =
        match parse input with
        | Invalid reason -> Error reason
        | validRef -> Ok validRef

    /// Check if a DataRef is valid (not Invalid case)
    let isValid (dataRef: DataRef) : bool =
        match dataRef with
        | Invalid _ -> false
        | _ -> true

    /// Get a human-readable description of a DataRef
    let describe (dataRef: DataRef) : string =
        match dataRef with
        | LocalDir (path, true) -> $"Local directory: {path} (exists)"
        | LocalDir (path, false) -> $"Local directory: {path} (will be created)"
        | RelativeDir path -> $"Relative directory: {path}"
        | LocalZip path -> $"Local zip file: {path}"
        | RelativeZip path -> $"Relative zip file: {path}"
        | HttpZip uri -> $"HTTP zip file: {uri}"
        | SftpZip uri -> $"SFTP zip file: {uri}"
        | Invalid reason -> $"Invalid: {reason}"