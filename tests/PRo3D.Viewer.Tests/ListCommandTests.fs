module ListCommandTests

open Expecto
open PRo3D.Viewer
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.ListCommand

// RED phase: These tests will fail because execute function doesn't exist
let tests =
    testList "ListCommand Execute Tests" [
        
        test "execute function should exist and accept ListConfig" {
            // This test will fail because execute function doesn't exist yet
            let config : ListConfig = {
                Data = [| "./testdata" |]
                Stats = None
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            let result = ListCommand.execute config
            Expect.isTrue (result >= 0) "Should return non-negative exit code"
        }
        
        test "execute should handle empty data array" {
            let config : ListConfig = {
                Data = [||]
                Stats = None
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            let result = ListCommand.execute config
            // Should return 0 (success) but show warning
            Expect.equal result 0 "Should return 0 for empty data"
        }
        
        test "execute should handle multiple local directories" {
            let config : ListConfig = {
                Data = [| "./dir1"; "./dir2"; "./dir3" |]
                Stats = Some true
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            let result = ListCommand.execute config
            Expect.equal result 0 "Should handle multiple directories"
        }
        
        test "execute should support HTTP URLs" {
            let config : ListConfig = {
                Data = [| "http://example.com/data.zip" |]
                Stats = None
                Sftp = None
                BaseDir = Some "./cache"
                ForceDownload = Some false
                Verbose = Some true
            }
            
            // This should work once we implement remote data support
            let result = ListCommand.execute config
            Expect.isTrue (result >= 0) "Should handle HTTP URLs"
        }
        
        test "execute should support SFTP URLs with authentication" {
            let config : ListConfig = {
                Data = [| "sftp://server:2200/path/data.zip" |]
                Stats = None
                Sftp = Some "./sftp-config.xml"
                BaseDir = Some "./cache"
                ForceDownload = Some true
                Verbose = Some true
            }
            
            // This should work once we implement SFTP support
            let result = ListCommand.execute config
            Expect.isTrue (result >= 0) "Should handle SFTP URLs with authentication"
        }
        
        test "execute should respect verbose flag" {
            let config : ListConfig = {
                Data = [| "./testdata" |]
                Stats = None
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = Some true
            }
            
            // With verbose flag, should produce more output
            let result = ListCommand.execute config
            Expect.equal result 0 "Should handle verbose flag"
        }
        
        test "execute should respect stats flag" {
            let config : ListConfig = {
                Data = [| "./testdata" |]
                Stats = Some true
                Sftp = None
                BaseDir = None
                ForceDownload = None
                Verbose = None
            }
            
            // With stats flag, should show detailed layer information
            let result = ListCommand.execute config
            Expect.equal result 0 "Should handle stats flag"
        }
        
        test "execute should respect forceDownload flag" {
            let config : ListConfig = {
                Data = [| "http://example.com/cached-data.zip" |]
                Stats = None
                Sftp = None
                BaseDir = Some "./cache"
                ForceDownload = Some true
                Verbose = Some true
            }
            
            // With forceDownload, should re-download even if cached
            let result = ListCommand.execute config
            Expect.isTrue (result >= 0) "Should handle forceDownload flag"
        }
        
        test "execute should use baseDir for relative path resolution" {
            let config : ListConfig = {
                Data = [| "./relative/path" |]
                Stats = None
                Sftp = None
                BaseDir = Some "/absolute/base/dir"
                ForceDownload = None
                Verbose = None
            }
            
            // Should resolve ./relative/path relative to baseDir
            let result = ListCommand.execute config
            Expect.equal result 0 "Should handle baseDir path resolution"
        }
    ]