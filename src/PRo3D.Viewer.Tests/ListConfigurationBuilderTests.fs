module ListConfigurationBuilderTests

open Expecto
open Argu
open PRo3D.Viewer
open PRo3D.Viewer.Configuration
open PRo3D.Viewer.ConfigurationBuilder
open PRo3D.Viewer.Project

// RED phase: These tests will fail because the builder functions don't exist
let tests =
    testList "ListConfigurationBuilder Tests" [
        
        test "fromListArgs should exist" {
            // This test will pass once we implement the function
            let parser = ArgumentParser.Create<ListCommand.Args>()
            let args = parser.Parse([| "dir1"; "dir2"; "--stats" |])
            
            let config = ConfigurationBuilder.fromListArgs args
            
            Expect.equal config.Data.Length 2 "Should convert directories to Data array"
            Expect.equal config.Data.[0] "dir1" "First directory should be dir1"
            Expect.equal config.Data.[1] "dir2" "Second directory should be dir2"
            Expect.equal config.Stats (Some true) "Should set Stats from --stats flag"
        }
        
        test "fromListArgs should handle empty args" {
            let parser = ArgumentParser.Create<ListCommand.Args>()
            let args = parser.Parse([||])
            
            let config = ConfigurationBuilder.fromListArgs args
            
            Expect.equal config.Data.Length 0 "Should handle empty args"
            Expect.equal config.Stats None "Stats should be None by default"
        }
        
        test "fromListProject should exist" {
            // This test will pass once we implement the function
            let project : PRo3D.Viewer.Project.ListProject = {
                Command = "list"
                Data = Some [| "dir1"; "http://example.com/data.zip" |]
                Stats = Some true
            }
            
            let config = ConfigurationBuilder.fromListProject "./project" project
            
            Expect.equal config.Data.Length 2 "Should convert project data to config"
            Expect.equal config.Stats (Some true) "Should preserve stats setting"
        }
        
        test "fromListProject should handle missing data array" {
            let project : PRo3D.Viewer.Project.ListProject = {
                Command = "list"
                Data = None
                Stats = Some false
            }
            
            let config = ConfigurationBuilder.fromListProject "./project" project
            
            Expect.equal config.Data.Length 0 "Should handle missing data array"
            Expect.equal config.Stats (Some false) "Should preserve stats setting"
        }
    ]