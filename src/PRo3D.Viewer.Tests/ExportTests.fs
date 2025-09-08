module ExportTests

open Expecto
open PRo3D.Viewer
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Project

let tests = 
    testList "Export" [
        
        testList "ExportConfig" [
            test "supports Data array instead of single DataDir" {
                // This test will fail because ExportConfig currently has DataDir: string
                // We want it to have Data: DataEntry array
                let testData : DataEntry array = [|
                    { Path = "/data/opc1"; Type = Some DataType.Opc; Transform = None }
                    { Path = "/data/opc2"; Type = Some DataType.Opc; Transform = None }
                |]
                
                let config = {
                    Data = testData  // This will fail - ExportConfig doesn't have Data field yet
                    Format = ExportFormat.Ply
                    OutFile = Some "output.ply"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None  // This will fail - field doesn't exist yet
                    Verbose = None        // This will fail - field doesn't exist yet
                }
                
                Expect.equal config.Data testData "Data array should contain all entries"
                Expect.equal config.Data.Length 2 "Should have 2 data entries"
            }
            
            test "handles SFTP remote data URLs" {
                let sftpUrl = "sftp://mastcam-z@dig-sftp.joanneum.at:2200/Mission/0610/8618/Job_0610_8618-110-rad-AI-Test"
                let testData : DataEntry array = [|
                    { Path = sftpUrl; Type = Some DataType.Opc; Transform = None }
                |]
                
                let config = {
                    Data = testData  // Will fail - no Data field
                    Format = ExportFormat.Pts
                    OutFile = Some "remote.pts"
                    Sftp = Some "/path/to/sftp.xml"
                    BaseDir = Some "./tmp/data"
                    ForceDownload = None  // Will fail - field doesn't exist
                    Verbose = None        // Will fail - field doesn't exist
                }
                
                Expect.equal config.Data.[0].Path sftpUrl "SFTP URL should be preserved"
                Expect.isSome config.Sftp "SFTP config should be set"
            }
            
            test "handles HTTP/HTTPS zip file URLs" {
                let httpUrl = "https://example.com/data/opc_data.zip"
                let testData : DataEntry array = [|
                    { Path = httpUrl; Type = Some DataType.Opc; Transform = None }
                |]
                
                let config = {
                    Data = testData  // Will fail - no Data field
                    Format = ExportFormat.Ply
                    OutFile = Some "http_data.ply"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = Some true  // Will fail - field doesn't exist
                    Verbose = None             // Will fail - field doesn't exist
                }
                
                Expect.equal config.Data.[0].Path httpUrl "HTTP URL should be preserved"
                Expect.equal config.ForceDownload (Some true) "ForceDownload should be set"
            }
            
            test "includes ForceDownload option" {
                let config = {
                    Data = [||]  // Will fail - no Data field
                    Format = ExportFormat.Pts
                    OutFile = None
                    Sftp = None
                    BaseDir = None
                    ForceDownload = Some true  // Will fail - field doesn't exist
                    Verbose = None             // Will fail - field doesn't exist
                }
                
                Expect.equal config.ForceDownload (Some true) "ForceDownload should be Some true"
            }
            
            test "includes Verbose option" {
                let config = {
                    Data = [||]  // Will fail - no Data field
                    Format = ExportFormat.Ply
                    OutFile = None
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None
                    Verbose = Some true  // Will fail - field doesn't exist
                }
                
                Expect.equal config.Verbose (Some true) "Verbose should be Some true"
            }
            
            test "handles None values for optional fields" {
                let config = {
                    Data = [||]  // Will fail - no Data field
                    Format = ExportFormat.Pts
                    OutFile = None
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None  // Will fail - field doesn't exist
                    Verbose = None        // Will fail - field doesn't exist
                }
                
                Expect.isTrue config.ForceDownload.IsNone "ForceDownload should be None"
                Expect.isTrue config.Verbose.IsNone "Verbose should be None"
            }
        ]
        
        testList "ExportProject JSON" [
            test "uses 'data' array property like other commands" {
                // This will test that ExportProject type has Data field instead of DataDir
                let project : ExportProject = {
                    Command = "export"
                    Data = Some [|  // Will fail - ExportProject has DataDir not Data
                        { Path = "/data/opc1"; Type = Some DataType.Opc; Transform = None }
                    |]
                    Format = Some "ply"
                    Out = Some "output.ply"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = Some false  // Will fail - field doesn't exist
                    Verbose = Some true         // Will fail - field doesn't exist
                }
                
                Expect.isSome project.Data "Data array should be present"
                Expect.equal project.Data.Value.Length 1 "Should have 1 data entry"
            }
            
            test "supports remote URLs in data array" {
                let sftpUrl = "sftp://server/path/to/data"
                let httpUrl = "https://example.com/data.zip"
                
                let project : ExportProject = {
                    Command = "export"
                    Data = Some [|  // Will fail - no Data field
                        { Path = sftpUrl; Type = Some DataType.Opc; Transform = None }
                        { Path = httpUrl; Type = Some DataType.Opc; Transform = None }
                    |]
                    Format = Some "pts"
                    Out = Some "combined.pts"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None  // Will fail - field doesn't exist
                    Verbose = None        // Will fail - field doesn't exist
                }
                
                let data = project.Data.Value
                Expect.equal data.[0].Path sftpUrl "First entry should be SFTP URL"
                Expect.equal data.[1].Path httpUrl "Second entry should be HTTP URL"
            }
            
            test "includes all configuration options" {
                let project : ExportProject = {
                    Command = "export"
                    Data = Some [||]  // Will fail - no Data field
                    Format = Some "ply"
                    Out = Some "test.ply"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = Some true  // Will fail - field doesn't exist
                    Verbose = Some false       // Will fail - field doesn't exist
                }
                
                Expect.equal project.ForceDownload (Some true) "ForceDownload should be supported"
                Expect.equal project.Verbose (Some false) "Verbose should be supported"
            }
        ]
    ]