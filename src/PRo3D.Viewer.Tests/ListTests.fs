module ListTests

open Expecto
open PRo3D.Viewer.Configuration

// RED phase: These tests will fail because ListConfig doesn't have the required fields
let tests =
    testList "ListConfig Tests" [
        
        test "ListConfig should have Data array field (not DataDir)" {
            // Test that ListConfig has Data: string array field
            let config = {
                Data = [| "dir1"; "dir2" |]
                Stats = Some true
                Sftp = Some "config.xml"
                BaseDir = Some "./base"
                ForceDownload = Some false
                Verbose = Some true
            }
            
            Expect.equal config.Data.Length 2 "Should have Data array with 2 items"
            Expect.equal config.Data.[0] "dir1" "First item should be dir1"
        }
        
        test "ListConfig should have Stats option field" {
            let config = {
                Data = [||]
                Stats = Some true
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            Expect.equal config.Stats (Some true) "Should have Stats option field"
        }
        
        test "ListConfig should have Sftp option field" {
            let config = {
                Data = [||]
                Stats = None
                Sftp = Some "config.xml"
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            Expect.equal config.Sftp (Some "config.xml") "Should have Sftp option field"
        }
        
        test "ListConfig should have BaseDir option field" {
            let config = {
                Data = [||]
                Stats = None
                Sftp = None
                BaseDir = Some "./tmp/data"
                ForceDownload = None
                Verbose = None
            }
            
            Expect.equal config.BaseDir (Some "./tmp/data") "Should have BaseDir option field"
        }
        
        test "ListConfig should have ForceDownload option field" {
            let config = {
                Data = [||]
                Stats = None
                Sftp = None
                BaseDir = None
                ForceDownload = Some true
                Verbose = None
            }
            
            Expect.equal config.ForceDownload (Some true) "Should have ForceDownload option field"
        }
        
        test "ListConfig should have Verbose option field" {
            let config = {
                Data = [||]
                Stats = None
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = Some true
            }
            
            Expect.equal config.Verbose (Some true) "Should have Verbose option field"
        }
        
        test "ListConfig should support empty configuration" {
            let config = {
                Data = [||]
                Stats = None
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            Expect.equal config.Data.Length 0 "Should support empty Data array"
            Expect.equal config.Stats None "Should support None for Stats"
        }
    ]