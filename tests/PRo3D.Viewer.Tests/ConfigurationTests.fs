module ConfigurationTests

open Expecto
open PRo3D.Viewer
open PRo3D.Viewer.Configuration

let tests = 
    testList "Configuration" [
        
        testList "DiffConfig" [
            test "includes verbose flag when set" {
                let config = { 
                    Data = [||]
                    NoValue = None
                    Speed = None
                    Verbose = Some true
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                }
                Expect.equal config.Verbose (Some true) "Verbose should be set to true"
            }
            
            test "handles None verbose flag" {
                let config = { 
                    Data = [||]
                    NoValue = None
                    Speed = None
                    Verbose = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                }
                Expect.equal config.Verbose None "Verbose should be None"
            }
            
            test "handles all optional fields as None" {
                let config = { 
                    Data = [||]
                    NoValue = None
                    Speed = None
                    Verbose = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                }
                Expect.isTrue (config.NoValue.IsNone) "NoValue should be None"
                Expect.isTrue (config.Speed.IsNone) "Speed should be None"
                Expect.isTrue (config.Verbose.IsNone) "Verbose should be None"
                Expect.isTrue (config.Sftp.IsNone) "Sftp should be None"
                Expect.isTrue (config.BaseDir.IsNone) "BaseDir should be None"
                Expect.isTrue (config.BackgroundColor.IsNone) "BackgroundColor should be None"
                Expect.isTrue (config.Screenshots.IsNone) "Screenshots should be None"
            }
            
            test "handles data array correctly" {
                let testData = [|"path1"; "path2"|]
                let config = { 
                    Data = testData
                    NoValue = None
                    Speed = None
                    Verbose = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                }
                Expect.equal config.Data testData "Data array should match input"
                Expect.equal config.Data.Length 2 "Data array should have 2 elements"
            }
        ]
        
        testList "ViewConfig" [
            test "handles empty data array" {
                let config = {
                    Data = [||]
                    Speed = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = None
                }
                Expect.equal config.Data.Length 0 "Empty data array should have length 0"
            }
            
            test "handles screenshots configuration" {
                let screenshotPath = Some "/test/screenshots"
                let config = {
                    Data = [||]
                    Speed = None
                    Sftp = None
                    BaseDir = None
                    BackgroundColor = None
                    Screenshots = screenshotPath
                }
                Expect.equal config.Screenshots screenshotPath "Screenshots path should match"
            }
        ]
    ]