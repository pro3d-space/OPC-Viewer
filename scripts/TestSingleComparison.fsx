#!/usr/bin/env dotnet fsi

// Test script to validate a single AI comparison
// This runs a 5-second timeout to avoid blocking the analysis

open System
open System.Diagnostics
open System.IO

let testProjectPath = @"W:\Datasets\Pro3D\AI-Comparison\Projects\Sol_300_Job_0320_8341-034-rad_vs_Job_0320_8341-034-rad-AI.json"
let timeoutSeconds = 5

let runViewerTest() =
    printfn "=== Testing Single AI Comparison ==="
    printfn ""
    printfn "Project: %s" (Path.GetFileName(testProjectPath))
    printfn "Timeout: %d seconds" timeoutSeconds
    printfn ""
    
    if not (File.Exists(testProjectPath)) then
        printfn "ERROR: Test project file not found!"
        printfn "Please run AnalyzeAICandidates.fsx first to generate project files."
        exit 1
    
    // Create the command
    let startInfo = ProcessStartInfo()
    startInfo.FileName <- "dotnet"
    startInfo.Arguments <- sprintf "run --project src/PRo3D.Viewer --configuration Release -- project \"%s\"" testProjectPath
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.CreateNoWindow <- true
    startInfo.WorkingDirectory <- @"c:\Data\Development\OPC-Viewer"
    
    printfn "Starting viewer with timeout..."
    
    let proc = Process.Start(startInfo)
    let output = proc.StandardOutput.ReadToEnd()
    let error = proc.StandardError.ReadToEnd()
    
    // Wait with timeout
    let completed = proc.WaitForExit(timeoutSeconds * 1000)
    
    if not completed then
        printfn "Timeout reached - killing process"
        try proc.Kill() with _ -> ()
        proc.WaitForExit()
    
    printfn ""
    printfn "=== STDOUT ==="
    printfn "%s" output
    
    if not (String.IsNullOrWhiteSpace(error)) then
        printfn ""
        printfn "=== STDERR ==="
        printfn "%s" error
    
    printfn ""
    printfn "=== Results ==="
    printfn "Exit code: %d" proc.ExitCode
    printfn "Completed within timeout: %b" completed
    
    // Analyze results
    if output.Contains("downloading") || output.Contains("SFTP") then
        printfn "✓ SFTP connection initiated"
    elif output.Contains("error") || output.Contains("ERROR") then
        printfn "✗ Errors detected in output"
    elif output.Contains("Mission") then
        printfn "✓ SFTP paths resolved correctly"
    else
        printfn "? Unable to determine connection status from output"
    
    printfn ""
    printfn "Next steps if successful:"
    printfn "1. Try without timeout to see full GUI"
    printfn "2. Test other dataset pairs from different Sols"
    printfn "3. Run batch comparisons for full analysis"

// Run the test
runViewerTest()