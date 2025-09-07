namespace Aardvark.Data.Remote.Tests

open Expecto
open Aardvark.Data.Remote

module DataRefTests = 
    
    [<Tests>]
    let parserTests =
        testList "Parser Tests" [
            
            testCase "Parse local directory path" <| fun _ ->
                let result = Parser.parse "/path/to/directory"
                match result with
                | LocalDir (path, _) -> Expect.equal path "/path/to/directory" "Path should match"
                | _ -> Tests.failtest "Expected LocalDir"
            
            testCase "Parse relative directory path" <| fun _ ->
                let result = Parser.parse "relative/path"
                match result with
                | RelativeDir path -> Expect.equal path "relative/path" "Path should match"
                | _ -> Tests.failtest "Expected RelativeDir"
            
            testCase "Parse local zip file" <| fun _ ->
                let result = Parser.parse "/path/to/file.zip"
                match result with
                | LocalZip path -> Expect.equal path "/path/to/file.zip" "Path should match"
                | _ -> Tests.failtest "Expected LocalZip"
            
            testCase "Parse relative zip file" <| fun _ ->
                let result = Parser.parse "relative/file.zip"
                match result with
                | RelativeZip path -> Expect.equal path "relative/file.zip" "Path should match"
                | _ -> Tests.failtest "Expected RelativeZip"
            
            testCase "Parse HTTP URL" <| fun _ ->
                let result = Parser.parse "http://example.com/data.zip"
                match result with
                | HttpZip uri -> Expect.equal (uri.ToString()) "http://example.com/data.zip" "URL should match"
                | _ -> Tests.failtest "Expected HttpZip"
            
            testCase "Parse HTTPS URL" <| fun _ ->
                let result = Parser.parse "https://example.com/data.zip"
                match result with
                | HttpZip uri -> Expect.equal (uri.ToString()) "https://example.com/data.zip" "URL should match"
                | _ -> Tests.failtest "Expected HttpZip"
            
            testCase "Parse SFTP URL" <| fun _ ->
                let result = Parser.parse "sftp://user@server.com/data.zip"
                match result with
                | SftpZip uri -> Expect.equal (uri.ToString()) "sftp://user@server.com/data.zip" "URL should match"
                | _ -> Tests.failtest "Expected SftpZip"
            
            testCase "Parse invalid HTTP URL (no .zip)" <| fun _ ->
                let result = Parser.parse "http://example.com/data"
                match result with
                | Invalid reason -> Expect.stringContains reason "must point to .zip files" "Should require .zip"
                | _ -> Tests.failtest "Expected Invalid"
            
            testCase "Parse invalid SFTP URL (no .zip)" <| fun _ ->
                let result = Parser.parse "sftp://server.com/data"
                match result with
                | Invalid reason -> Expect.stringContains reason "must point to .zip files" "Should require .zip"
                | _ -> Tests.failtest "Expected Invalid"
            
            testCase "Parse empty string" <| fun _ ->
                let result = Parser.parse ""
                match result with
                | Invalid reason -> Expect.stringContains reason "cannot be null or empty" "Should reject empty"
                | _ -> Tests.failtest "Expected Invalid"
            
            testCase "Parse null string" <| fun _ ->
                let result = Parser.parse null
                match result with
                | Invalid reason -> Expect.stringContains reason "cannot be null or empty" "Should reject null"
                | _ -> Tests.failtest "Expected Invalid"
        ]
    
    [<Tests>]
    let utilityTests =
        testList "Parser Utility Tests" [
            
            testCase "tryParse valid input returns Ok" <| fun _ ->
                let result = Parser.tryParse "/path/to/directory"
                match result with
                | Ok (LocalDir _) -> () // Expected
                | _ -> Tests.failtest "Expected Ok with LocalDir"
            
            testCase "tryParse invalid input returns Error" <| fun _ ->
                let result = Parser.tryParse ""
                match result with
                | Error reason -> Expect.stringContains reason "cannot be null or empty" "Should have error message"
                | _ -> Tests.failtest "Expected Error"
            
            testCase "isValid returns true for valid DataRef" <| fun _ ->
                let dataRef = LocalDir("/path", true)
                let result = Parser.isValid dataRef
                Expect.isTrue result "Should be valid"
            
            testCase "isValid returns false for Invalid DataRef" <| fun _ ->
                let dataRef = Invalid "Some error"
                let result = Parser.isValid dataRef
                Expect.isFalse result "Should be invalid"
            
            testCase "describe returns meaningful description" <| fun _ ->
                let dataRef = LocalDir("/path/to/data", true)
                let description = Parser.describe dataRef
                Expect.stringContains description "/path/to/data" "Should contain path"
                Expect.stringContains description "exists" "Should indicate existence"
        ]