namespace Aardvark.Data.Remote.Providers

open System.IO
open Aardvark.Data.Remote

/// Functional local provider module
module LocalProvider =
    
    /// Check if this provider can handle the given DataRef
    let canHandle dataRef =
        match dataRef with
        | LocalDir _ | RelativeDir _ | LocalZip _ | RelativeZip _ -> true
        | _ -> false
    
    /// Resolve a DataRef using the local provider
    let resolve (config: FetchConfig) dataRef =
        async {
            try
                match dataRef with
                | LocalDir (path, true) ->
                    return Resolved path
                    
                | LocalDir (path, false) ->
                    let info = Directory.CreateDirectory(path)
                    return Resolved info.FullName
                    
                | RelativeDir pathRel ->
                    let fullPath = Path.Combine(config.baseDirectory, pathRel)
                    let info = Directory.CreateDirectory(fullPath)
                    return Resolved info.FullName
                    
                | LocalZip path ->
                    if File.Exists(path) then
                        return Resolved path
                    else
                        return InvalidPath $"Zip file not found: {path}"
                        
                | RelativeZip pathRel ->
                    let fullPath = Path.Combine(config.baseDirectory, pathRel)
                    if File.Exists(fullPath) then
                        return Resolved fullPath
                    else
                        return InvalidPath $"Zip file not found: {fullPath}"
                        
                | _ ->
                    return InvalidPath "LocalProvider cannot handle this DataRef type"
                    
            with ex ->
                return InvalidPath $"LocalProvider error: {ex.Message}"
        }
    
    /// Provider record instance
    let provider : Provider = {
        canHandle = canHandle
        resolve = resolve
    }