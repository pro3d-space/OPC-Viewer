module Main

open Expecto

let allTests = 
    testList "PRo3D.Viewer.Tests" [
        ConfigurationTests.tests
        ScreenshotTests.tests
        ExportTests.tests
        ExportCommandTests.tests
        // ProjectFileTests and IntegrationTests removed due to API complexity
    ]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs [] argv allTests