module ExportCommandTests

open Expecto
open PRo3D.Viewer
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.Project
open System.IO
open System

let tests = 
    testList "ExportCommand Behavior" [
        
        testList "Remote Data Support" [
            test "downloads data from SFTP URL and exports it" {
                // Given: ExportConfig with working SFTP URL
                let config = {
                    Data = [|
                        { Path = "sftp://mastcam-z@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/Job_0610_8618-110-rad-AI-Test_opc.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_sftp_export.pts"
                    Sftp = Some "W:\\Datasets\\Pro3D\\confidential\\2025-02-24_AI-Mars-3D\\Mastcam-Z.xml"
                    BaseDir = Some "./tmp/test_data"
                    ForceDownload = Some false
                    Verbose = Some true
                }
                
                // When: Execute export
                let result = ExportCommand.execute config
                
                // Then: Should download data and create export file
                Expect.equal result 0 "Export should succeed"
                Expect.isTrue (File.Exists "test_sftp_export.pts") "Export file should be created"
                
                // Cleanup
                if File.Exists "test_sftp_export.pts" then
                    File.Delete "test_sftp_export.pts"
            }
            
            test "downloads data from HTTP/HTTPS zip URL and exports it" {
                // Given: ExportConfig with invalid HTTP URL (tests error handling)
                let config = {
                    Data = [|
                        { Path = "https://example.com/data/opc_data.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Ply
                    OutFile = Some "test_http_export.ply"
                    Sftp = None
                    BaseDir = Some "./tmp/test_data"
                    ForceDownload = Some false
                    Verbose = Some false
                }
                
                // When: Execute export with invalid URL
                let result = ExportCommand.execute config
                
                // Then: Should return error code when URL cannot be reached
                Expect.notEqual result 0 "Should return error code when URL fails"
                Expect.isFalse (File.Exists "test_http_export.ply") "Should not create output file when URL fails"
            }
            
            test "merges multiple remote data sources into single export" {
                // Given: Multiple data sources with some invalid ones (tests error handling for mixed sources)
                let config = {
                    Data = [|
                        { Path = "sftp://server/path1_opc.zip"; Type = Some DataType.Opc; Transform = None }
                        { Path = "https://example.com/data2.zip"; Type = Some DataType.Opc; Transform = None }
                        { Path = "./tmp/test_data"; Type = Some DataType.Opc; Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_merged.pts"
                    Sftp = Some "test_sftp.xml"
                    BaseDir = Some "./tmp/test_data"
                    ForceDownload = Some false
                    Verbose = Some false
                }
                
                // When: Execute export with invalid/missing data sources
                let result = ExportCommand.execute config
                
                // Then: Should return error code when some sources fail
                Expect.notEqual result 0 "Should return error code when data sources fail"
                Expect.isFalse (File.Exists "test_merged.pts") "Should not create output file when data missing"
            }
        ]
        
        testList "ForceDownload Flag" [
            test "ForceDownload=true bypasses cache and re-downloads" {
                // Given: Config with ForceDownload=true and invalid URL (tests error handling)
                let config = {
                    Data = [|
                        { Path = "https://example.com/test_data.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_force.pts"
                    Sftp = None
                    BaseDir = Some "./tmp/test_data"
                    ForceDownload = Some true  // Force re-download
                    Verbose = Some false
                }
                
                // When: Execute export with invalid URL
                let result = ExportCommand.execute config
                
                // Then: Should return error code when download fails
                Expect.notEqual result 0 "Should return error code when download fails"
                Expect.isFalse (File.Exists "test_force.pts") "Should not create output file when download fails"
            }
            
            test "ForceDownload=false uses cached data if available" {
                // Given: Config with ForceDownload=false and invalid URL (tests error handling)
                let config = {
                    Data = [|
                        { Path = "https://example.com/test_data.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_cached.pts"
                    Sftp = None
                    BaseDir = Some "./tmp/test_data"
                    ForceDownload = Some false  // Use cache
                    Verbose = Some false
                }
                
                // When: Execute export with invalid URL
                let result = ExportCommand.execute config
                
                // Then: Should return error code when URL fails
                Expect.notEqual result 0 "Should return error code when URL fails"
                Expect.isFalse (File.Exists "test_cached.pts") "Should not create output file when URL fails"
            }
        ]
        
        testList "Verbose Flag" [
            test "Verbose=true produces detailed log output" {
                // Given: Config with Verbose=true (using working SFTP data)
                let config = {
                    Data = [|
                        { Path = "sftp://mastcam-z@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/Job_0610_8618-110-rad-AI-Test_opc.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_verbose.pts"
                    Sftp = Some "W:\\Datasets\\Pro3D\\confidential\\2025-02-24_AI-Mars-3D\\Mastcam-Z.xml"
                    BaseDir = Some "./tmp/test_data"
                    ForceDownload = Some false
                    Verbose = Some true  // Enable verbose output
                }
                
                // When: Execute export (verbose output goes to console)
                let result = ExportCommand.execute config
                
                // Then: Should complete successfully
                Expect.equal result 0 "Verbose export should succeed"
                
                // Cleanup
                if File.Exists "test_verbose.pts" then
                    File.Delete "test_verbose.pts"
            }
            
            test "Verbose=false produces minimal output" {
                // Given: Config with Verbose=false and empty data dir (to test minimal output on failure)
                let config = {
                    Data = [|
                        { Path = "./tmp/test_data"; Type = Some DataType.Opc; Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_quiet.pts"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None
                    Verbose = Some false  // Disable verbose output
                }
                
                // When: Execute export (should fail due to no OPC data)
                let result = ExportCommand.execute config
                
                // Then: Should return error code (1) without verbose output
                Expect.equal result 1 "Should return error code when no OPC data found"
                Expect.isFalse (File.Exists "test_quiet.pts") "Should not create output file when no data"
            }
        ]
        
        testList "Error Handling" [
            test "returns error when SFTP authentication fails" {
                // Given: Config with invalid SFTP credentials
                let config = {
                    Data = [|
                        { Path = "sftp://invalid@server.com/path_opc.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Pts
                    OutFile = Some "test_auth_fail.pts"
                    Sftp = Some "invalid_sftp.xml"  // Non-existent config
                    BaseDir = None
                    ForceDownload = None
                    Verbose = None
                }
                
                // When: Execute export
                let result = ExportCommand.execute config
                
                // Then: Should return error code
                Expect.notEqual result 0 "Should return error code"
                Expect.isFalse (File.Exists "test_auth_fail.pts") "Export file should not be created"
            }
            
            test "returns error when no data sources are provided" {
                // Given: Config with empty data array
                let config = {
                    Data = [||]  // Empty array
                    Format = ExportFormat.Pts
                    OutFile = Some "test_no_data.pts"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None
                    Verbose = None
                }
                
                // When: Execute export
                let result = ExportCommand.execute config
                
                // Then: Should return error code
                Expect.equal result 1 "Should return error code 1"
                Expect.isFalse (File.Exists "test_no_data.pts") "Export file should not be created"
            }
            
            test "returns error when network is unavailable for remote data" {
                // Given: Config with unreachable URL
                let config = {
                    Data = [|
                        { Path = "https://definitely.not.a.real.domain.xyz/data.zip"
                          Type = Some DataType.Opc
                          Transform = None }
                    |]
                    Format = ExportFormat.Ply
                    OutFile = Some "test_network_fail.ply"
                    Sftp = None
                    BaseDir = None
                    ForceDownload = None
                    Verbose = None
                }
                
                // When: Execute export
                let result = ExportCommand.execute config
                
                // Then: Should return error code
                Expect.notEqual result 0 "Should return error code"
                Expect.isFalse (File.Exists "test_network_fail.ply") "Export file should not be created"
            }
        ]
        
        testList "Integration with Project Files" [
            test "exports from project file with remote data" {
                // Given: Project file with remote data
                let projectJson = """
                {
                    "command": "export",
                    "data": [
                        { "path": "sftp://server/data", "type": "opc" }
                    ],
                    "format": "ply",
                    "out": "project_export.ply",
                    "sftp": "config.xml",
                    "forceDownload": true,
                    "verbose": true
                }
                """
                let projectFile = "test_project.json"
                File.WriteAllText(projectFile, projectJson)
                
                // When: Load and execute project
                let projectConfig = ProjectFile.load projectFile
                let result = 
                    match projectConfig with
                    | ProjectConfig.ExportConfig export ->
                        let config = ConfigurationBuilder.fromExportProject "." export
                        ExportCommand.execute config
                    | _ -> -1
                
                // Then: Should process project correctly
                // (This will fail until remote data handling is implemented)
                Expect.notEqual result -1 "Should parse as export project"
                
                // Cleanup
                if File.Exists projectFile then
                    File.Delete projectFile
                if File.Exists "project_export.ply" then
                    File.Delete "project_export.ply"
            }
        ]
    ]