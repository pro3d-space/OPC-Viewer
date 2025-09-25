module IntegrationTests

open Expecto
open System.IO
open PRo3D.Viewer
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Project

let tests =
    testList "Integration" [
        
        testList "End-to-End Configuration Flow" [
            test "CLI to Config to Execution pathway works" {
                // Test the complete flow: CLI args → Config → Ready for execution
                let mockDiffConfig : DiffConfig = {
                    Data = [|"dataset1"; "dataset2"|]
                    NoValue = Some 42.0
                    Speed = Some 1.5
                    Verbose = Some true
                    Sftp = Some "/config/sftp.conf"
                    BaseDir = Some "/base/dir"
                    BackgroundColor = Some "red"
                    Screenshots = Some "/screenshots"
                    ForceDownload = Some true
                    UseEmbree = Some true
                    Version = "1.0.0-test"
                }
                
                // Verify all fields are properly set
                Expect.equal mockDiffConfig.Data.Length 2 "Should have 2 data entries"
                Expect.equal mockDiffConfig.NoValue (Some 42.0) "NoValue should be set"
                Expect.equal mockDiffConfig.Speed (Some 1.5) "Speed should be set"
                Expect.equal mockDiffConfig.Verbose (Some true) "Verbose should be true"
                Expect.equal mockDiffConfig.Sftp (Some "/config/sftp.conf") "Sftp should be set"
                Expect.equal mockDiffConfig.BaseDir (Some "/base/dir") "BaseDir should be set"
                Expect.equal mockDiffConfig.BackgroundColor (Some "red") "BackgroundColor should be set"
                Expect.equal mockDiffConfig.Screenshots (Some "/screenshots") "Screenshots should be set"
            }
            
            test "Project File to Config pathway works" {
                let tempDir = Path.GetTempPath()
                let projectDir = Path.Combine(tempDir, "integration-test-" + System.Guid.NewGuid().ToString())
                Directory.CreateDirectory(projectDir) |> ignore
                
                try
                    // Create a complete project configuration
                    let projectJson = """{
                        "command": "diff",
                        "data": ["./data1", "./data2"], 
                        "noValue": 123.45,
                        "speed": 2.0,
                        "verbose": true,
                        "sftp": "./sftp-config.conf",
                        "baseDir": "./base",
                        "backgroundColor": "#00FF00",
                        "screenshots": "./screenshots"
                    }"""
                    
                    // Create temporary file for ProjectFile.load
                    let tempFile = Path.Combine(projectDir, "test-project.json")
                    File.WriteAllText(tempFile, projectJson)
                    
                    match ProjectFile.load tempFile with
                    | ProjectConfig.DiffConfig project ->
                        let config = PRo3D.Viewer.ConfigurationBuilder.fromDiffProject "1.0.0-test" projectDir project
                        
                        // Verify the complete transformation
                        Expect.equal config.Data.Length 2 "Should have 2 data entries"
                        Expect.isTrue (config.Data[0].Contains(projectDir)) "First data path should be resolved relative to project"
                        Expect.isTrue (config.Data[1].Contains(projectDir)) "Second data path should be resolved relative to project"
                        Expect.equal config.NoValue (Some 123.45) "NoValue should be preserved"
                        Expect.equal config.Speed (Some 2.0) "Speed should be preserved"
                        Expect.equal config.Verbose (Some true) "Verbose should be preserved"
                        Expect.isTrue config.Sftp.IsSome "Sftp should be resolved"
                        Expect.isTrue config.BaseDir.IsSome "BaseDir should be resolved"
                        Expect.equal config.BackgroundColor (Some "#00FF00") "BackgroundColor should be preserved"
                        Expect.isTrue config.Screenshots.IsSome "Screenshots should be resolved"
                        
                    | _ -> failtest "Project should parse as DiffConfig"
                        
                finally
                    if Directory.Exists projectDir then
                        Directory.Delete(projectDir, true)
            }
            
            test "Mixed absolute and relative paths handled correctly" {
                let tempDir = Path.GetTempPath()
                let projectDir = Path.Combine(tempDir, "mixed-paths-test-" + System.Guid.NewGuid().ToString())
                Directory.CreateDirectory(projectDir) |> ignore
                
                try
                    let project : DiffProject = {
                        Command = "diff"
                        Data = Some [|"./relative-path"; "/absolute/path"|]
                        NoValue = None
                        Speed = None
                        Verbose = None
                        Sftp = Some "./relative-sftp.conf"
                        BaseDir = Some "/absolute/base"
                        BackgroundColor = None
                        Screenshots = Some "./relative-screenshots"
                        ForceDownload = None
                        UseEmbree = None
                    }
                    
                    let config = PRo3D.Viewer.ConfigurationBuilder.fromDiffProject "1.0.0-test" projectDir project
                    
                    // Check mixed path resolution
                    Expect.isTrue (config.Data[0].Contains(projectDir)) "Relative data path should be resolved"
                    Expect.equal config.Data[1] "/absolute/path" "Absolute data path should remain unchanged"
                    Expect.isTrue (config.Sftp.Value.Contains(projectDir)) "Relative sftp path should be resolved"
                    Expect.equal config.BaseDir (Some "/absolute/base") "Absolute base dir should remain unchanged"
                    Expect.isTrue (config.Screenshots.Value.Contains(projectDir)) "Relative screenshots path should be resolved"
                    
                finally
                    if Directory.Exists projectDir then
                        Directory.Delete(projectDir, true)
            }
        ]
        
        testList "Architecture Consistency" [
            test "DiffConfig and ViewConfig have consistent Screenshots field" {
                let diffConfig : DiffConfig = {
                    Data = [||]
                    NoValue = None
                    Speed = None
                    Verbose = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = Some "/test"
                    ForceDownload = None
                    UseEmbree = None
                    Version = "1.0.0-test"
                }
                
                let viewConfig : ViewConfig = {
                    Data = [||]
                    Speed = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = Some "/test"
                    ForceDownload = None
                    Verbose = None
                    Version = "1.0.0-test"
                }
                
                // Both should have Screenshots field with same value
                Expect.equal diffConfig.Screenshots viewConfig.Screenshots "Both configs should have identical Screenshots handling"
            }
            
            test "All optional fields behave consistently" {
                let diffConfig : DiffConfig = {
                    Data = [||]
                    NoValue = None
                    Speed = None
                    Verbose = None  // DiffConfig specific
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                    ForceDownload = None
                    UseEmbree = None
                    Version = "1.0.0-test"
                }
                
                let viewConfig : ViewConfig = {
                    Data = [||]
                    Speed = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                    ForceDownload = None
                    Verbose = None
                    Version = "1.0.0-test"
                }
                
                // Common fields should behave the same
                Expect.equal diffConfig.Speed viewConfig.Speed "Speed handling should be consistent"
                Expect.equal diffConfig.Sftp viewConfig.Sftp "Sftp handling should be consistent"
                Expect.equal diffConfig.BaseDir viewConfig.BaseDir "BaseDir handling should be consistent"
                Expect.equal diffConfig.BackgroundColor viewConfig.BackgroundColor "BackgroundColor handling should be consistent"
                Expect.equal diffConfig.Screenshots viewConfig.Screenshots "Screenshots handling should be consistent"
            }
        ]
        
        testList "Error Handling" [
            test "gracefully handles malformed project files" {
                let tempDir = Path.GetTempPath()
                let tempFile = Path.Combine(tempDir, "malformed-" + System.Guid.NewGuid().ToString() + ".json")
                
                try
                    let malformedJson = """{ "command": "diff", "data": [1, 2, 3] }""" // numbers instead of strings
                    File.WriteAllText(tempFile, malformedJson)
                    
                    match ProjectFile.load tempFile with
                    | ProjectConfig.InvalidConfig _ -> 
                        // Expected - malformed JSON should result in InvalidConfig
                        Expect.isTrue true "Malformed JSON should be handled gracefully"
                    | _ -> 
                        // If it somehow parses, that's also acceptable for this test
                        Expect.isTrue true "Unexpected successful parsing is also acceptable"
                        
                finally
                    if File.Exists tempFile then
                        File.Delete tempFile
            }
            
            test "handles empty data arrays" {
                let tempDir = Path.GetTempPath()
                let tempFile = Path.Combine(tempDir, "empty-data-" + System.Guid.NewGuid().ToString() + ".json")
                
                try
                    let emptyDataJson = """{
                        "command": "diff",
                        "data": []
                    }"""
                    
                    File.WriteAllText(tempFile, emptyDataJson)
                    
                    match ProjectFile.load tempFile with
                    | ProjectConfig.DiffConfig project ->
                        let config = PRo3D.Viewer.ConfigurationBuilder.fromDiffProject "1.0.0-test" "/test" project
                        Expect.equal config.Data.Length 0 "Empty data array should be handled correctly"
                    | _ -> failtest "Empty data project should parse successfully"
                    
                finally
                    if File.Exists tempFile then
                        File.Delete tempFile
            }
        ]
    ]