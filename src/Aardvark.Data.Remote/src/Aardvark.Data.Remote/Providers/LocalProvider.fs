namespace Aardvark.Data.Remote.Providers

open System.IO
open System.Threading.Tasks
open Aardvark.Data.Remote

/// Provider for local directories and files
type LocalProvider() =
    
    interface IDataProvider with
        
        member _.CanHandle(dataRef: DataRef) =
            match dataRef with
            | LocalDir _ | RelativeDir _ | LocalZip _ | RelativeZip _ -> true
            | _ -> false
        
        member _.ResolveAsync config dataRef =
            task {
                try
                    match dataRef with
                    | LocalDir (path, true) ->
                        return Resolved path
                        
                    | LocalDir (path, false) ->
                        let info = Directory.CreateDirectory(path)
                        return Resolved info.FullName
                        
                    | RelativeDir pathRel ->
                        let fullPath = Path.Combine(config.BaseDirectory, pathRel)
                        let info = Directory.CreateDirectory(fullPath)
                        return Resolved info.FullName
                        
                    | LocalZip path ->
                        if File.Exists(path) then
                            return Resolved path
                        else
                            return InvalidPath $"Zip file not found: {path}"
                            
                    | RelativeZip pathRel ->
                        let fullPath = Path.Combine(config.BaseDirectory, pathRel)
                        if File.Exists(fullPath) then
                            return Resolved fullPath
                        else
                            return InvalidPath $"Zip file not found: {fullPath}"
                            
                    | _ ->
                        return InvalidPath "LocalProvider cannot handle this DataRef type"
                        
                with ex ->
                    return InvalidPath $"LocalProvider error: {ex.Message}"
            }

/// Module functions for the LocalProvider
module LocalProvider =
    let create, register = Common.Provider.createSingleton LocalProvider