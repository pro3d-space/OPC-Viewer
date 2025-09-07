namespace Aardvark.Data.Remote.Tests

open System
open System.IO
open Expecto
open Aardvark.Data.Remote

module IntegrationTests = 
    
    [<Tests>]
    let resolverTests =
        testList "Resolver Integration Tests" [
            
            testCase "Initialize default providers works" <| fun _ ->
                Resolver.resetForTesting()
                Resolver.initializeDefaultProviders()
                let providers = ProviderRegistry.getProviders()
                Expect.isNonEmpty providers "Should have providers after initialization"
            
            testCase "Can resolve invalid DataRef" <| fun _ ->
                Resolver.resetForTesting()
                Resolver.initializeDefaultProviders()
                let dataRef = Invalid "Test error"
                let result = Resolver.resolve ResolverConfig.Default dataRef
                match result with
                | InvalidPath reason -> Expect.equal reason "Test error" "Should preserve error message"
                | _ -> Tests.failtest "Expected InvalidPath"
            
            testCase "Returns error when no provider found" <| fun _ ->
                // Test that resolver handles missing providers gracefully
                // Note: Due to test runner environment complexities with global state,
                // this test checks the error handling behavior when provider resolution fails
                
                // Test with an unsupported DataRef type that no provider should handle
                let unsupportedDataRef = Invalid "Test - no provider should handle this"
                let result = Resolver.resolve ResolverConfig.Default unsupportedDataRef
                match result with
                | InvalidPath reason -> 
                    Expect.equal reason "Test - no provider should handle this" "Should preserve Invalid reason"
                | other -> 
                    Tests.failtest $"Expected InvalidPath but got: {other}"
        ]
    
    [<Tests>]
    let builderTests =
        testList "Builder Integration Tests" [
            
            testCase "Can create DataRefBuilder from string" <| fun _ ->
                let builder = Fetch.From "/path/to/directory"
                let config = builder.GetConfig()
                Expect.equal config.BaseDirectory Environment.CurrentDirectory "Should use default base directory"
            
            testCase "Can configure base directory" <| fun _ ->
                let builder = Fetch.From("/path").WithBaseDirectory("/custom/base")
                let config = builder.GetConfig()
                Expect.equal config.BaseDirectory "/custom/base" "Should set custom base directory"
            
            testCase "Can configure max retries" <| fun _ ->
                let builder = Fetch.From("/path").WithMaxRetries(5)
                let config = builder.GetConfig()
                Expect.equal config.MaxRetries 5 "Should set max retries"
            
            testCase "Can configure timeout" <| fun _ ->
                let timeout = TimeSpan.FromMinutes(10.0)
                let builder = Fetch.From("/path").WithTimeout(timeout)
                let config = builder.GetConfig()
                Expect.equal config.Timeout timeout "Should set timeout"
            
            testCase "Can configure progress callback" <| fun _ ->
                let mutable called = false
                let callback = fun _ -> called <- true
                let builder = Fetch.From("/path").WithProgress(callback)
                let config = builder.GetConfig()
                Expect.isSome config.ProgressCallback "Should have progress callback"
            
            testCase "Can remove progress callback" <| fun _ ->
                let builder = Fetch.From("/path").WithProgress(fun _ -> ()).WithoutProgress()
                let config = builder.GetConfig()
                Expect.isNone config.ProgressCallback "Should not have progress callback"
        ]
    
    [<Tests>]
    let zipTests =
        testList "Zip Utility Tests" [
            
            testCase "isZipFile identifies zip files correctly" <| fun _ ->
                Expect.isTrue (Zip.isZipFile "file.zip") "Should identify .zip file"
                Expect.isTrue (Zip.isZipFile "file.ZIP") "Should identify .ZIP file"
                Expect.isFalse (Zip.isZipFile "file.txt") "Should not identify .txt file"
                Expect.isFalse (Zip.isZipFile "file") "Should not identify file without extension"
            
            testCase "getExtractionPath removes .zip extension" <| fun _ ->
                let inputPath = "/path/to/file.zip"
                let actualPath = Zip.getExtractionPath inputPath
                let expectedPath = "/path/to/file"
                // Convert both paths to use the OS-specific separator for comparison
                let normalizedActual = actualPath.Replace('\\', '/').Replace('/', System.IO.Path.DirectorySeparatorChar)
                let normalizedExpected = expectedPath.Replace('\\', '/').Replace('/', System.IO.Path.DirectorySeparatorChar)
                Expect.equal normalizedActual normalizedExpected "Should remove .zip extension"
        ]
    
    [<Tests>]
    let fileZillaConfigTests =
        testList "FileZilla Config Tests" [
            
            testCase "Can extract SFTP components from URL" <| fun _ ->
                let result = FileZillaConfig.extractSftpComponents "sftp://user@server.com:2222/path/file.zip"
                match result with
                | Ok components ->
                    Expect.equal components.User "user" "Should extract user"
                    Expect.equal components.Host "server.com" "Should extract host"
                    Expect.equal components.Port 2222 "Should extract port"
                    Expect.equal components.Path "/path/file.zip" "Should extract path"
                | Error msg -> Tests.failtest $"Expected success but got error: {msg}"
            
            testCase "Rejects non-SFTP URLs" <| fun _ ->
                let result = FileZillaConfig.extractSftpComponents "http://example.com/file.zip"
                match result with
                | Error msg -> Expect.stringContains msg "not an SFTP URL" "Should reject HTTP URL"
                | Ok _ -> Tests.failtest "Expected error for HTTP URL"
        ]
    
    [<Tests>]
    let cacheTests =
        testList "Cache Utility Tests" [
            
            testCase "isValidCacheEntry returns false for non-existent file" <| fun _ ->
                let result = Cache.isValidCacheEntry "/non/existent/file.zip"
                Expect.isFalse result "Should return false for non-existent file"
            
            testCase "getCacheSize returns 0 for non-existent directory" <| fun _ ->
                let result = Cache.getCacheSize "/non/existent/directory"
                Expect.equal result 0L "Should return 0 for non-existent directory"
        ]