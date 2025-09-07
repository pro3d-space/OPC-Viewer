module ProjectFileTests

open Expecto
open System.IO
open PRo3D.Viewer.Project
open PRo3D.Viewer.Configuration

let tests =
    testList "ProjectFile" [
        
        testList "File-based Parsing" [
            test "loads diff project from file" {
                let tempDir = Path.GetTempPath()
                let tempFile = Path.Combine(tempDir, "test-diff-" + System.Guid.NewGuid().ToString() + ".json")
                
                try
                    let json = """{
                        "command": "diff",
                        "data": ["path1", "path2"],
                        "verbose": true
                    }"""
                    
                    File.WriteAllText(tempFile, json)
                    
                    match ProjectFile.load tempFile with
                    | ProjectConfig.DiffConfig project ->
                        Expect.equal project.Data (Some [|"path1"; "path2"|]) "Data should match JSON array"
                        Expect.equal project.Verbose (Some true) "Verbose should be true"
                        Expect.equal project.Command "diff" "Command should be diff"
                    | _ -> failtest "Should parse as DiffConfig"
                    
                finally
                    if File.Exists tempFile then
                        File.Delete tempFile
            }
            
            test "loads diff project with all fields" {
                let tempDir = Path.GetTempPath()
                let tempFile = Path.Combine(tempDir, "test-diff-full-" + System.Guid.NewGuid().ToString() + ".json")
                
                try
                    let json = """{
                        "command": "diff",
                        "data": ["dataset1", "dataset2"],
                        "noValue": 999.0,
                        "speed": 2.5,
                        "verbose": true,
                        "sftp": "/path/to/config",
                        "baseDir": "/base",
                        "backgroundColor": "#FF0000",
                        "screenshots": "/screenshots"
                    }"""
                    
                    File.WriteAllText(tempFile, json)
                    
                    match ProjectFile.load tempFile with
                    | ProjectConfig.DiffConfig project ->
                        Expect.equal project.Data (Some [|"dataset1"; "dataset2"|]) "Data should match"
                        Expect.equal project.NoValue (Some 999.0) "NoValue should match"
                        Expect.equal project.Speed (Some 2.5) "Speed should match"
                        Expect.equal project.Verbose (Some true) "Verbose should match"
                        Expect.equal project.Sftp (Some "/path/to/config") "Sftp should match"
                        Expect.equal project.BaseDir (Some "/base") "BaseDir should match"
                        Expect.equal project.BackgroundColor (Some "#FF0000") "BackgroundColor should match"
                        Expect.equal project.Screenshots (Some "/screenshots") "Screenshots should match"
                    | _ -> failtest "Should parse as DiffConfig"
                    
                finally
                    if File.Exists tempFile then
                        File.Delete tempFile
            }
            
            test "handles missing file gracefully" {
                let nonExistentFile = "/path/does/not/exist.json"
                
                match ProjectFile.load nonExistentFile with
                | ProjectConfig.InvalidConfig error ->
                    Expect.stringContains error "Project file not found" "Should contain file not found error"
                | _ -> failtest "Should return InvalidConfig for missing file"
            }
            
            test "handles invalid JSON gracefully" {
                let tempDir = Path.GetTempPath()
                let tempFile = Path.Combine(tempDir, "test-invalid-" + System.Guid.NewGuid().ToString() + ".json")
                
                try
                    let invalidJson = """{ "command": "diff", invalid }"""
                    File.WriteAllText(tempFile, invalidJson)
                    
                    match ProjectFile.load tempFile with
                    | ProjectConfig.InvalidConfig error ->
                        Expect.stringContains error "Failed to load project file" "Should contain error message"
                    | _ -> failtest "Should return InvalidConfig for invalid JSON"
                    
                finally
                    if File.Exists tempFile then
                        File.Delete tempFile
            }
        ]
        
        testList "Configuration Building" [
            test "builds DiffConfig from project with path resolution" {
                let tempDir = Path.GetTempPath()
                let projectDir = Path.Combine(tempDir, "test-project-" + System.Guid.NewGuid().ToString())
                Directory.CreateDirectory(projectDir) |> ignore
                
                try
                    let project : DiffProject = {
                        Command = "diff"
                        Data = Some [|"./relative/path1"; "/absolute/path2"|]
                        NoValue = Some 1.5
                        Speed = Some 3.0
                        Verbose = Some true
                        Sftp = Some "./config.sftp"
                        BaseDir = Some "./base"
                        BackgroundColor = Some "blue"
                        Screenshots = Some "./screenshots"
                        ForceDownload = Some true
                    }
                    
                    let config = PRo3D.Viewer.ConfigurationBuilder.fromDiffProject projectDir project
                    
                    // Check that relative paths are resolved
                    Expect.stringContains config.Data[0] projectDir "First path should be resolved relative to project dir"
                    Expect.equal config.Data[1] "/absolute/path2" "Absolute path should remain unchanged"
                    
                    // Check other fields
                    Expect.equal config.NoValue (Some 1.5) "NoValue should match"
                    Expect.equal config.Speed (Some 3.0) "Speed should match" 
                    Expect.equal config.Verbose (Some true) "Verbose should match"
                    Expect.isTrue config.Sftp.IsSome "Sftp should be resolved"
                    Expect.isTrue config.BaseDir.IsSome "BaseDir should be resolved"
                    Expect.equal config.BackgroundColor (Some "blue") "BackgroundColor should match"
                    Expect.isTrue config.Screenshots.IsSome "Screenshots should be resolved"
                    
                finally
                    if Directory.Exists projectDir then
                        Directory.Delete(projectDir, true)
            }
        ]
    ]