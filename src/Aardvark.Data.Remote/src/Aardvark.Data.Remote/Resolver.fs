namespace Aardvark.Data.Remote

open System
open System.Threading.Tasks
open Aardvark.Data.Remote.Providers

/// Main resolver for DataRef instances
module Resolver =
    
    let mutable private initialized = false
    
    /// Initialize the default providers
    let initializeDefaultProviders() =
        if not initialized then
            // Clear any existing providers
            ProviderRegistry.clear()
            
            // Register providers in order of preference
            LocalProvider.register() |> ignore
            HttpProvider.register() |> ignore
            SftpProvider.register() |> ignore
            
            initialized <- true
    
    /// Reset initialization state (for testing)
    let resetForTesting() =
        initialized <- false
        ProviderRegistry.clear()
    
    /// Asynchronously resolve a DataRef to a local directory path
    let resolveAsync (config: ResolverConfig) (dataRef: DataRef) : Task<ResolveResult> =
        task {
            match dataRef with
            | Invalid reason -> 
                return InvalidPath reason
                
            | _ ->
                // Find a provider that can handle this DataRef
                match ProviderRegistry.findProvider dataRef with
                | Some provider ->
                    let! result = provider.ResolveAsync config dataRef
                    
                    // If result is a zip file, we need to extract it
                    match result with
                    | Resolved path when Zip.isZipFile path ->
                        // Extract zip file and return extracted directory
                        // Use force extraction when force download is enabled
                        match Zip.extract path config.ForceDownload config.Logger with
                        | Ok extractedPath -> return Resolved extractedPath
                        | Error errorMessage -> return InvalidPath $"Failed to extract zip file {path}: {errorMessage}"
                        
                    | otherResult -> 
                        return otherResult
                        
                | None ->
                    return InvalidPath $"No provider found for DataRef: {Parser.describe dataRef}"
        }
    
    /// Resolve a DataRef to a local directory path (synchronous)
    let resolve (config: ResolverConfig) (dataRef: DataRef) : ResolveResult =
        resolveAsync config dataRef |> Async.AwaitTask |> Async.RunSynchronously
    
    /// Resolve multiple DataRefs in parallel
    let resolveMany (config: ResolverConfig) (dataRefs: DataRef list) : Task<(DataRef * ResolveResult) list> =
        task {
            let tasks = dataRefs |> List.map (fun dataRef -> 
                task {
                    let! result = resolveAsync config dataRef
                    return (dataRef, result)
                })
            
            let! results = Task.WhenAll(tasks)
            return results |> Array.toList
        }
    
    /// Create a resolver function with a specific configuration
    let createResolver (config: ResolverConfig) = 
        resolve config
    
    /// Create an async resolver function with a specific configuration
    let createAsyncResolver (config: ResolverConfig) = 
        resolveAsync config

