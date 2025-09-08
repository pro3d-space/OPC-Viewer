#!/usr/bin/env dotnet fsi

// Script to verify SFTP paths using psftp

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions

// SFTP connection details
let sftpHost = "dig-sftp.joanneum.at"
let sftpPort = "2200"
let sftpUser = "mastcam-z"
let sftpPass = "efbAWDIn2347AwdB" // decoded from base64

// Sample paths to test
let testPaths = [
    "Mission/0300/0320"  // Sol 300 - Job_0320
    "Mission/0600/0610"  // Sol 600 - known working from example  
    "Mission/0900/0910"  // Sol 900 - Job_0910
    "Mission/1100/1130"  // Sol 1100 - Job_1130
]

let runPsftp (commands: string list) =
    let tempPwFile = Path.GetTempFileName()
    let tempCmdFile = Path.GetTempFileName()
    
    try
        File.WriteAllText(tempPwFile, sftpPass)
        File.WriteAllLines(tempCmdFile, commands @ ["quit"])
        
        let startInfo = ProcessStartInfo()
        startInfo.FileName <- "psftp"
        startInfo.Arguments <- sprintf "-P %s -l %s -pwfile %s -b %s -batch %s" sftpPort sftpUser tempPwFile tempCmdFile sftpHost
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.CreateNoWindow <- true
        
        let proc = Process.Start(startInfo)
        let output = proc.StandardOutput.ReadToEnd()
        let error = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        
        (proc.ExitCode, output, error)
    finally
        try File.Delete(tempPwFile) with _ -> ()
        try File.Delete(tempCmdFile) with _ -> ()

let exploreMissionRoot() =
    printfn "=== Exploring Mission Root Directory ==="
    printfn ""
    
    let (exitCode, output, error) = runPsftp ["ls Mission"]
    
    if exitCode = 0 then
        printfn "✓ Successfully connected and listed Mission directory!"
        printfn ""
        printfn "Available mission folders:"
        
        // Parse the output to find folder numbers
        let lines = output.Split('\n') |> Array.filter (fun line -> line.Trim() <> "")
        for line in lines do
            if Regex.IsMatch(line.Trim(), @"^\d{4}$") then
                printfn "  - %s" (line.Trim())
        
        printfn ""
        printfn "Raw output:"
        printfn "%s" output
        true
    else
        printfn "✗ Failed to connect to SFTP server"
        printfn "Exit code: %d" exitCode
        printfn "Error: %s" error
        false

let checkSpecificPaths() =
    printfn "=== Checking Specific Dataset Paths ==="
    printfn ""
    
    for path in testPaths do
        printfn "Checking: %s" path
        
        let (exitCode, output, error) = runPsftp [sprintf "ls %s" path]
        
        if exitCode = 0 then
            printfn "✓ Path exists! Contents:"
            let lines = output.Split('\n') |> Array.filter (fun line -> line.Trim() <> "")
            for line in lines do
                let trimmed = line.Trim()
                if trimmed.StartsWith("Job_") then
                    printfn "  📁 %s" trimmed
            printfn ""
        else
            printfn "✗ Path not found or access denied"
            printfn "  Error: %s" error
            printfn ""

let checkKnownWorkingExample() =
    printfn "=== Checking Known Working Example ==="
    printfn ""
    
    let knownPath = "Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result"
    printfn "Testing: %s" knownPath
    
    let (exitCode, output, error) = runPsftp [sprintf "ls %s" knownPath]
    
    if exitCode = 0 then
        printfn "✓ Known example path exists!"
        printfn "Files in result directory:"
        
        let lines = output.Split('\n') |> Array.filter (fun line -> line.Trim() <> "")
        for line in lines do
            let trimmed = line.Trim()
            if trimmed.EndsWith(".zip") then
                printfn "  📦 %s" trimmed
        printfn ""
    else
        printfn "✗ Cannot access known working example"
        printfn "Error: %s" error

let main() =
    printfn "Using psftp to verify SFTP server structure"
    printfn "Host: %s:%s" sftpHost sftpPort
    printfn "User: %s" sftpUser
    printfn ""
    
    if exploreMissionRoot() then
        checkSpecificPaths()
        checkKnownWorkingExample()
        
        printfn "=== Next Steps ==="
        printfn "1. Based on findings above, adjust path generation logic"
        printfn "2. Update generated project files with correct paths"
        printfn "3. Test actual data downloads with corrected paths"
    else
        printfn "Cannot proceed - SFTP connection failed"

main()