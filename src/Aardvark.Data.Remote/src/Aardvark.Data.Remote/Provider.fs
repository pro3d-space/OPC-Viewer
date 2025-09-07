namespace Aardvark.Data.Remote

open System.Threading.Tasks

/// Interface for data providers that can resolve DataRef instances
type IDataProvider =
    /// Check if this provider can handle the given DataRef
    abstract member CanHandle: dataRef: DataRef -> bool
    
    /// Asynchronously resolve a DataRef to a local directory path
    abstract member ResolveAsync: config: ResolverConfig -> dataRef: DataRef -> Task<ResolveResult>

/// Registry for managing data providers
module ProviderRegistry =
    
    let mutable private providers: IDataProvider list = []
    
    /// Register a new data provider
    let register (provider: IDataProvider) =
        providers <- provider :: providers
    
    /// Get all registered providers
    let getProviders() = providers |> List.rev
    
    /// Find the first provider that can handle the given DataRef
    let findProvider (dataRef: DataRef) : IDataProvider option =
        providers |> List.tryFind (fun p -> p.CanHandle dataRef)
    
    /// Clear all registered providers (mainly for testing)
    let clear() =
        providers <- []