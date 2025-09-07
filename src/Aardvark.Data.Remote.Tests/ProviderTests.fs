namespace Aardvark.Data.Remote.Tests

open System
open System.IO
open Expecto
open Aardvark.Data.Remote
open Aardvark.Data.Remote.Providers

module ProviderTests = 
    
    [<Tests>]
    let providerRegistryTests =
        testSequenced <| testList "ProviderRegistry Tests" [
            
            testCase "Can register and retrieve providers" <| fun _ ->
                Resolver.resetForTesting()
                let initialCount = ProviderRegistry.getProviders().Length
                let provider = LocalProvider.create()
                ProviderRegistry.register provider
                
                let providers = ProviderRegistry.getProviders()
                Expect.hasLength providers (initialCount + 1) "Should have one more provider after registration"
            
            testCase "Can find provider for DataRef" <| fun _ ->
                Resolver.resetForTesting()
                let provider = LocalProvider.create()
                ProviderRegistry.register provider
                
                let dataRef = LocalDir("/path", true)
                let found = ProviderRegistry.findProvider dataRef
                Expect.isSome found "Should find provider for LocalDir"
            
            testCase "Returns None when no provider found" <| fun _ ->
                Resolver.resetForTesting()
                
                let dataRef = LocalDir("/path", true)
                let found = ProviderRegistry.findProvider dataRef
                Expect.isNone found "Should not find provider when none registered"
            
            testCase "Can clear all providers" <| fun _ ->
                Resolver.resetForTesting()
                // Verify clear works by registering a provider and then clearing
                let provider = LocalProvider.create()
                ProviderRegistry.register provider
                let countAfterAdd = ProviderRegistry.getProviders().Length
                Expect.isTrue (countAfterAdd > 0) "Should have providers after adding"
                
                ProviderRegistry.clear()
                let providers = ProviderRegistry.getProviders()
                Expect.hasLength providers 0 "Should have no providers after clear"
        ]
    
    [<Tests>]
    let localProviderTests =
        testList "LocalProvider Tests" [
            
            testCase "Can handle local directory DataRef" <| fun _ ->
                let provider = LocalProvider.create()
                let dataRef = LocalDir("/path", true)
                let canHandle = provider.CanHandle dataRef
                Expect.isTrue canHandle "Should handle LocalDir"
            
            testCase "Can handle relative directory DataRef" <| fun _ ->
                let provider = LocalProvider.create()
                let dataRef = RelativeDir("path")
                let canHandle = provider.CanHandle dataRef
                Expect.isTrue canHandle "Should handle RelativeDir"
            
            testCase "Can handle local zip DataRef" <| fun _ ->
                let provider = LocalProvider.create()
                let dataRef = LocalZip("/path/file.zip")
                let canHandle = provider.CanHandle dataRef
                Expect.isTrue canHandle "Should handle LocalZip"
            
            testCase "Cannot handle HTTP DataRef" <| fun _ ->
                let provider = LocalProvider.create()
                let dataRef = HttpZip(Uri("http://example.com/file.zip"))
                let canHandle = provider.CanHandle dataRef
                Expect.isFalse canHandle "Should not handle HttpZip"
        ]
    
    [<Tests>]
    let httpProviderTests =
        testList "HttpProvider Tests" [
            
            testCase "Can handle HTTP DataRef" <| fun _ ->
                let provider = HttpProvider.create()
                let dataRef = HttpZip(Uri("http://example.com/file.zip"))
                let canHandle = provider.CanHandle dataRef
                Expect.isTrue canHandle "Should handle HttpZip"
            
            testCase "Cannot handle local directory DataRef" <| fun _ ->
                let provider = HttpProvider.create()
                let dataRef = LocalDir("/path", true)
                let canHandle = provider.CanHandle dataRef
                Expect.isFalse canHandle "Should not handle LocalDir"
        ]
    
    [<Tests>]
    let sftpProviderTests =
        testList "SftpProvider Tests" [
            
            testCase "Can handle SFTP DataRef" <| fun _ ->
                let provider = SftpProvider.create()
                let dataRef = SftpZip(Uri("sftp://user@server.com/file.zip"))
                let canHandle = provider.CanHandle dataRef
                Expect.isTrue canHandle "Should handle SftpZip"
            
            testCase "Cannot handle local directory DataRef" <| fun _ ->
                let provider = SftpProvider.create()
                let dataRef = LocalDir("/path", true)
                let canHandle = provider.CanHandle dataRef
                Expect.isFalse canHandle "Should not handle LocalDir"
        ]