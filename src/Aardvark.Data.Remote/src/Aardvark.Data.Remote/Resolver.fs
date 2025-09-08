namespace Aardvark.Data.Remote

open System
open System.Threading.Tasks

/// Main resolver for DataRef instances using functional providers
module Resolver =
    
    /// Resolve a DataRef using FetchConfig (new functional API)
    let resolveAsync (config: FetchConfig) (dataRef: DataRef) : Async<ResolveResult> =
        async {
            match dataRef with
            | Invalid reason -> 
                return InvalidPath reason
                
            | _ ->
                // Find a provider that can handle this DataRef
                match ProviderRegistry.findProvider dataRef with
                | Some provider ->
                    let! result = provider.resolve config dataRef
                    
                    // If result is a zip file, we need to extract it
                    match result with
                    | Resolved path when Zip.isZipFile path ->
                        // Extract zip file and return extracted directory
                        match Zip.extract path config.forceDownload config.logger with
                        | Ok extractedPath -> return Resolved extractedPath
                        | Error errorMessage -> return InvalidPath $"Failed to extract zip file {path}: {errorMessage}"
                        
                    | otherResult -> 
                        return otherResult
                        
                | None ->
                    return InvalidPath $"No provider found for DataRef: {Parser.describe dataRef}"
        }
    
    /// Resolve a DataRef to a local directory path (synchronous)
    let resolve (config: FetchConfig) (dataRef: DataRef) : ResolveResult =
        resolveAsync config dataRef |> Async.RunSynchronously
    
    /// Resolve multiple DataRefs in parallel
    let resolveMany (config: FetchConfig) (dataRefs: DataRef list) : Async<ResolveResult list> =
        async {
            let! results = 
                dataRefs
                |> List.map (resolveAsync config)
                |> Async.Parallel
            return Array.toList results
        }
