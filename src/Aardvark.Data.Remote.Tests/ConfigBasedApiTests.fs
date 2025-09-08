namespace Aardvark.Data.Remote.Tests

open System
open System.IO
open System.Threading.Tasks
open Expecto
open Aardvark.Data.Remote

module ConfigBasedApiTests =
    
    [<Tests>]
    let fetchConfigTests =
        testList "FetchConfig Tests" [
            
            testCase "FetchConfig has correct default values" <| fun _ ->
                let config = Fetch.defaultConfig
                Expect.equal config.baseDirectory Environment.CurrentDirectory "Should default to current directory"
                Expect.equal config.maxRetries 3 "Should default to 3 retries"
                Expect.equal config.timeout (TimeSpan.FromMinutes 5.0) "Should default to 5 minute timeout"
                Expect.isNone config.sftpConfig "Should default to no SFTP config"
                Expect.isNone config.sftpConfigFile "Should default to no SFTP config file"
                Expect.isNone config.progress "Should default to no progress callback"
                Expect.equal config.forceDownload false "Should default to false for force download"
                Expect.isNone config.logger "Should default to no logger"
            
            testCase "Can create custom FetchConfig using record syntax" <| fun _ ->
                let customConfig = { 
                    Fetch.defaultConfig with 
                        baseDirectory = "./custom-cache"
                        maxRetries = 5
                        timeout = TimeSpan.FromMinutes 10.0
                        forceDownload = true
                }
                Expect.equal customConfig.baseDirectory "./custom-cache" "Should use custom base directory"
                Expect.equal customConfig.maxRetries 5 "Should use custom retry count"
                Expect.equal customConfig.timeout (TimeSpan.FromMinutes 10.0) "Should use custom timeout"
                Expect.equal customConfig.forceDownload true "Should use custom force download"
        ]
    
    [<Tests>]
    let fsharpApiTests =
        testList "F# API Tests" [
            
            testCase "Fetch.resolve with string should work with defaults" <| fun _ ->
                // This test will initially fail - the new API doesn't exist yet
                let result = Fetch.resolve "/tmp/test-directory"
                match result with
                | Resolved path -> 
                    Expect.isTrue (Directory.Exists path) "Should create and resolve to existing directory"
                | _ -> Tests.failtest "Expected Resolved result"
            
            testCase "Fetch.resolveWith should use custom config" <| fun _ ->
                let customConfig = { 
                    Fetch.defaultConfig with 
                        baseDirectory = "./test-cache"
                        forceDownload = true 
                }
                let result = Fetch.resolveWith customConfig "relative/path"
                match result with
                | Resolved path ->
                    Expect.stringContains path "test-cache" "Should resolve relative to custom base directory"
                | _ -> Tests.failtest "Expected Resolved result"
            
            testCase "Fetch.resolveAsync should return F# Async" <| fun _ ->
                // Test that the F# API returns Async<ResolveResult>, not Task<ResolveResult>
                async {
                    let! result = Fetch.resolveAsync "/tmp/test-async"
                    match result with
                    | Resolved path -> 
                        Expect.isTrue (Directory.Exists path) "Should create and resolve directory"
                    | _ -> Tests.failtest "Expected Resolved result"
                } |> Async.RunSynchronously
            
            testCase "Fetch.resolveAsyncWith should use custom config with Async" <| fun _ ->
                let customConfig = { 
                    Fetch.defaultConfig with 
                        baseDirectory = "./async-cache"
                }
                async {
                    let! result = Fetch.resolveAsyncWith customConfig "async/test/path"
                    match result with
                    | Resolved path ->
                        Expect.stringContains path "async-cache" "Should use custom cache directory"
                    | _ -> Tests.failtest "Expected Resolved result"
                } |> Async.RunSynchronously
            
            testCase "Fetch.resolveMany should resolve multiple URLs in parallel" <| fun _ ->
                let urls = [
                    "/tmp/test1"
                    "/tmp/test2"
                    "/tmp/test3"
                ]
                async {
                    let! results = Fetch.resolveMany urls
                    Expect.equal results.Length 3 "Should return result for each URL"
                    results |> List.iter (fun result ->
                        match result with
                        | Resolved _ -> () // Expected
                        | _ -> Tests.failtest "All results should be Resolved"
                    )
                } |> Async.RunSynchronously
            
            testCase "Fetch.resolveManyWith should use custom config for all URLs" <| fun _ ->
                let customConfig = { 
                    Fetch.defaultConfig with 
                        baseDirectory = "./batch-cache"
                }
                let urls = ["batch1"; "batch2"]
                async {
                    let! results = Fetch.resolveManyWith customConfig urls
                    Expect.equal results.Length 2 "Should return result for each URL"
                    results |> List.iter (fun result ->
                        match result with
                        | Resolved path ->
                            Expect.stringContains path "batch-cache" "Should use custom cache directory"
                        | _ -> Tests.failtest "Expected Resolved result"
                    )
                } |> Async.RunSynchronously
        ]
    
    [<Tests>]
    let csharpApiTests =
        testList "C# API Tests" [
            
            testCase "Fetch.Resolve with URL only should use defaults" <| fun _ ->
                // Test C# API - this will initially fail
                let result = Fetch.Resolve("/tmp/csharp-test")
                match result with
                | Resolved path -> 
                    Expect.isTrue (Directory.Exists path) "Should create and resolve directory"
                | _ -> Tests.failtest "Expected Resolved result"
            
            testCase "Fetch.Resolve with URL and config should use custom config" <| fun _ ->
                let config = FetchConfiguration(
                    BaseDirectory = "./csharp-cache",
                    MaxRetries = 5,
                    ForceDownload = true
                )
                let result = Fetch.ResolveWith "csharp-custom" config
                match result with
                | Resolved path ->
                    Expect.stringContains path "csharp-cache" "Should use custom cache directory"
                | _ -> Tests.failtest "Expected Resolved result"
            
            testCase "Fetch.ResolveAsync should return Task" <| fun _ ->
                // Test that C# API returns Task<ResolveResult>
                let task = Fetch.ResolveAsync("/tmp/csharp-async")
                let result = task |> Async.AwaitTask |> Async.RunSynchronously
                match result with
                | Resolved path -> 
                    Expect.isTrue (Directory.Exists path) "Should create and resolve directory"
                | _ -> Tests.failtest "Expected Resolved result"
            
            testCase "Fetch.ResolveAsync with config should use custom config" <| fun _ ->
                let config = FetchConfiguration(
                    BaseDirectory = "./csharp-async-cache",
                    Timeout = TimeSpan.FromMinutes(10)
                )
                let task = Fetch.ResolveAsyncWith "csharp-async-custom" config
                let result = task |> Async.AwaitTask |> Async.RunSynchronously
                match result with
                | Resolved path ->
                    Expect.stringContains path "csharp-async-cache" "Should use custom cache directory"
                | _ -> Tests.failtest "Expected Resolved result"
            
            testCase "Fetch.ResolveMany should handle multiple URLs" <| fun _ ->
                let urls = ["/tmp/csharp1"; "/tmp/csharp2"]
                let task = Fetch.ResolveMany(urls)
                let results = task |> Async.AwaitTask |> Async.RunSynchronously
                Expect.equal results.Length 2 "Should return result for each URL"
            
            testCase "FetchConfiguration C# class should have correct default values" <| fun _ ->
                let config = FetchConfiguration()
                Expect.equal config.BaseDirectory Environment.CurrentDirectory "Should default to current directory"
                Expect.equal config.MaxRetries 3 "Should default to 3 retries"
                Expect.equal config.Timeout (TimeSpan.FromMinutes 5.0) "Should default to 5 minute timeout"
                Expect.equal config.SftpConfig None "Should default to None SFTP config"
                Expect.isNull config.SftpConfigFile "Should default to null SFTP config file"
                Expect.isNull config.Progress "Should default to null progress callback"
                Expect.equal config.ForceDownload false "Should default to false for force download"
                Expect.isNull config.Logger "Should default to null logger"
        ]
    
    [<Tests>]
    let configInteropTests =
        testList "Config Interop Tests" [
            
            testCase "F# config should be convertible to C# config" <| fun _ ->
                let fsharpConfig = { 
                    Fetch.defaultConfig with 
                        baseDirectory = "./interop-test"
                        maxRetries = 7
                }
                let csharpConfig = FetchConfiguration.FromFSharp(fsharpConfig)
                Expect.equal csharpConfig.BaseDirectory "./interop-test" "Should preserve base directory"
                Expect.equal csharpConfig.MaxRetries 7 "Should preserve max retries"
            
            testCase "C# config should be convertible to F# config" <| fun _ ->
                let csharpConfig = FetchConfiguration(
                    BaseDirectory = "./csharp-interop",
                    MaxRetries = 9,
                    ForceDownload = true
                )
                let fsharpConfig = csharpConfig.ToFSharp()
                Expect.equal fsharpConfig.baseDirectory "./csharp-interop" "Should preserve base directory"
                Expect.equal fsharpConfig.maxRetries 9 "Should preserve max retries"
                Expect.equal fsharpConfig.forceDownload true "Should preserve force download"
        ]
    
    [<Tests>]
    let errorHandlingTests =
        testList "Error Handling Tests" [
            
            testCase "Invalid URL should return InvalidPath result" <| fun _ ->
                let result = Fetch.resolve "invalid://not-a-real-protocol/file.zip"
                match result with
                | InvalidPath reason -> 
                    Expect.stringContains reason "invalid" "Should mention invalid protocol"
                | _ -> Tests.failtest "Expected InvalidPath result"
            
            testCase "Missing SFTP config should return SftpConfigMissing" <| fun _ ->
                let result = Fetch.resolve "sftp://example.com/file.zip"
                match result with
                | SftpConfigMissing uri ->
                    Expect.equal uri.Host "example.com" "Should preserve host information"
                | _ -> Tests.failtest "Expected SftpConfigMissing result"
            
        ]