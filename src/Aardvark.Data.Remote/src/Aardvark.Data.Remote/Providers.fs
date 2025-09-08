namespace Aardvark.Data.Remote

open Aardvark.Data.Remote.Providers

/// Functional provider registry - immutable list of all providers
module ProviderRegistry =
    
    /// All available providers in order of preference
    let all = [
        LocalProvider.provider
        HttpProvider.provider
        SftpProvider.provider
    ]
    
    /// Find the first provider that can handle the given DataRef
    let findProvider dataRef =
        all |> List.tryFind (fun provider -> provider.canHandle dataRef)
    
