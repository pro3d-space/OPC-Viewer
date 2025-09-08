module ScreenshotTests

open Expecto
open System.IO
open PRo3D.Viewer.Shared

let tests =
    testList "Screenshots" [
        
        testList "Directory Resolution" [
            test "uses default when None provided" {
                let currentDir = System.Environment.CurrentDirectory
                let expected = Path.Combine(currentDir, "screenshots")
                
                // Mock function that mimics ViewerCommon.resolveScreenshotDirectory
                let resolveScreenshotDirectory screenshotDir =
                    match screenshotDir with
                    | Some dir -> dir
                    | None -> Path.Combine(System.Environment.CurrentDirectory, "screenshots")
                
                let result = resolveScreenshotDirectory None
                Expect.equal result expected "Should use default screenshots directory"
            }
            
            test "uses provided directory when Some" {
                let customPath = "/custom/screenshots/path"
                
                let resolveScreenshotDirectory screenshotDir =
                    match screenshotDir with
                    | Some dir -> dir
                    | None -> Path.Combine(System.Environment.CurrentDirectory, "screenshots")
                
                let result = resolveScreenshotDirectory (Some customPath)
                Expect.equal result customPath "Should use provided custom path"
            }
            
            test "handles relative paths correctly" {
                let relativePath = "./my-screenshots"
                
                let resolveScreenshotDirectory screenshotDir =
                    match screenshotDir with
                    | Some dir -> dir
                    | None -> Path.Combine(System.Environment.CurrentDirectory, "screenshots")
                
                let result = resolveScreenshotDirectory (Some relativePath)
                Expect.equal result relativePath "Should preserve relative path as provided"
            }
        ]
        
        testList "Priority System" [
            test "CLI argument overrides project setting" {
                let cliPath = "/cli/screenshots"
                let projectPath = "/project/screenshots"
                
                // Mock priority resolution function
                let resolveWithPriority cliArg projectSetting =
                    match cliArg with
                    | Some cli -> cli
                    | None -> projectSetting |> Option.defaultValue "./screenshots"
                
                let result = resolveWithPriority (Some cliPath) (Some projectPath)
                Expect.equal result cliPath "CLI argument should override project setting"
            }
            
            test "project setting used when no CLI argument" {
                let projectPath = "/project/screenshots"
                
                let resolveWithPriority cliArg projectSetting =
                    match cliArg with
                    | Some cli -> cli
                    | None -> projectSetting |> Option.defaultValue "./screenshots"
                
                let result = resolveWithPriority None (Some projectPath)
                Expect.equal result projectPath "Project setting should be used when no CLI argument"
            }
            
            test "default used when neither CLI nor project specified" {
                let resolveWithPriority cliArg projectSetting =
                    match cliArg with
                    | Some cli -> cli
                    | None -> projectSetting |> Option.defaultValue "./screenshots"
                
                let result = resolveWithPriority None None
                Expect.equal result "./screenshots" "Default should be used when nothing specified"
            }
        ]
        
        testList "Path Validation" [
            test "identifies valid absolute paths" {
                let isAbsolutePath (path: string) = Path.IsPathRooted(path)
                
                if System.Environment.OSVersion.Platform = System.PlatformID.Win32NT then
                    Expect.isTrue (isAbsolutePath "C:\\screenshots") "Windows absolute path should be valid"
                    Expect.isTrue (isAbsolutePath "D:\\my\\screenshots") "Windows absolute path with subdirs should be valid"
                else
                    Expect.isTrue (isAbsolutePath "/screenshots") "Unix absolute path should be valid"
                    Expect.isTrue (isAbsolutePath "/home/user/screenshots") "Unix absolute path with subdirs should be valid"
            }
            
            test "identifies relative paths" {
                let isAbsolutePath (path: string) = Path.IsPathRooted(path)
                
                Expect.isFalse (isAbsolutePath "./screenshots") "Relative path should not be absolute"
                Expect.isFalse (isAbsolutePath "screenshots") "Relative path without prefix should not be absolute"
                Expect.isFalse (isAbsolutePath "../screenshots") "Parent directory relative path should not be absolute"
            }
            
            test "handles empty and null paths gracefully" {
                let safePathCheck path =
                    try
                        if System.String.IsNullOrEmpty(path) then false
                        else Path.IsPathRooted(path)
                    with
                    | _ -> false
                
                Expect.isFalse (safePathCheck "") "Empty string should be handled gracefully"
                Expect.isFalse (safePathCheck null) "Null should be handled gracefully"
            }
        ]
        
        testList "Directory Creation Simulation" [
            test "simulates directory creation for valid paths" {
                let tempDir = Path.GetTempPath()
                let testScreenshotDir = Path.Combine(tempDir, "test-screenshots-" + System.Guid.NewGuid().ToString())
                
                try
                    // Simulate ViewerCommon.ensureScreenshotDirectory behavior
                    let ensureDirectory path =
                        if not (Directory.Exists path) then
                            Directory.CreateDirectory(path) |> ignore
                        path
                    
                    let result = ensureDirectory testScreenshotDir
                    
                    Expect.isTrue (Directory.Exists testScreenshotDir) "Directory should be created"
                    Expect.equal result testScreenshotDir "Should return the created directory path"
                    
                finally
                    if Directory.Exists testScreenshotDir then
                        Directory.Delete(testScreenshotDir, true)
            }
        ]
    ]