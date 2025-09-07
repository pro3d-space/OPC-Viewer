#!/usr/bin/env dotnet fsi

// Script to verify SFTP paths exist using command line sftp tool

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions

// SFTP connection details from the XML config
let sftpHost = "dig-sftp.joanneum.at"
let sftpPort = "2200" 
let sftpUser = "mastcam-z"
let sftpPass = "efbAWDIn2347AwdB" // decoded from base64 in XML

// Sample of AI candidate pairs to test
let testPairs = [
    ("Job_0320_8341-034-rad", "Job_0320_8341-034-rad-AI", 300)
    ("Job_0610_8618-110-rad", "Job_0610_8618-110-rad-AI", 600) // from example
    ("Job_0910_8915-063-rad", "Job_0910_8915-063-rad-AI", 900)
    ("Job_1130_9150-079-rad", "Job_1130_9150-079-rad-AI", 1100)
]

let runSftpCommand (commands: string list) =
    let tempScript = Path.GetTempFileName()
    File.WriteAllLines(tempScript, commands @ ["quit"])
    
    let startInfo = ProcessStartInfo()
    startInfo.FileName <- "sftp"
    startInfo.Arguments <- sprintf "-P %s -o StrictHostKeyChecking=no -b %s %s@%s" sftpPort tempScript sftpUser sftpHost
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.CreateNoWindow <- true
    
    // Set password via environment (if supported)
    startInfo.Environment.Add("SSHPASS", sftpPass)
    
    try
        let proc = Process.Start(startInfo)
        let output = proc.StandardOutput.ReadToEnd()
        let error = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        
        File.Delete(tempScript)
        
        (proc.ExitCode, output, error)
    with
    | ex -> 
        File.Delete(tempScript)
        (-1, "", ex.Message)

let exploreMissionStructure() =
    printfn "=== Exploring SFTP Server Structure ==="
    printfn "Host: %s:%s" sftpHost sftpPort
    printfn "User: %s" sftpUser
    printfn ""
    
    // First, list the root Mission directory
    printfn "Listing Mission directory..."
    let (exitCode, output, error) = runSftpCommand ["ls -la Mission/"]
    
    if exitCode <> 0 then
        printfn "Error connecting to SFTP server:"
        printfn "Exit code: %d" exitCode
        printfn "Output: %s" output
        printfn "Error: %s" error
        None
    else
        printfn "Success! Found directories:"
        printfn "%s" output
        Some output

let checkSpecificPaths() =
    printfn "\n=== Checking Specific Dataset Paths ==="
    
    for (noAi, ai, sol) in testPairs do
        let jobNumber = Regex.Match(noAi, @"Job_(\d+)_").Groups.[1].Value |> int
        let missionFolder = sprintf "%04d" (jobNumber / 100 * 100)
        let specificFolder = sprintf "%04d" jobNumber
        
        printfn "\n--- Testing %s (Sol %d) ---" noAi sol
        printfn "Expected path: Mission/%s/%s/" missionFolder specificFolder
        
        // Check if the specific job folder exists
        let checkPath = sprintf "Mission/%s/%s/" missionFolder specificFolder
        let (exitCode, output, error) = runSftpCommand [sprintf "ls -la %s" checkPath]
        
        if exitCode = 0 then
            printfn "✓ Path exists!"
            // Look for the specific job folders
            if output.Contains(noAi) then
                printfn "  ✓ Found no-AI job: %s" noAi
            else
                printfn "  ✗ No-AI job not found: %s" noAi
                
            if output.Contains(ai) then
                printfn "  ✓ Found AI job: %s" ai  
            else
                printfn "  ✗ AI job not found: %s" ai
        else
            printfn "✗ Path does not exist or access denied"
            printfn "  Error: %s" error

let verifyOpcZipFiles() =
    printfn "\n=== Verifying OPC Zip Files ==="
    
    // Test the known working example from diff-ai-comparison.json
    let knownPath = "Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/"
    printfn "Testing known working path: %s" knownPath
    
    let (exitCode, output, error) = runSftpCommand [sprintf "ls -la %s" knownPath]
    
    if exitCode = 0 then
        printfn "✓ Known path accessible!"
        printfn "Files found:"
        printfn "%s" output
        
        if output.Contains("_opc.zip") then
            printfn "✓ Found OPC zip files in result directory"
        else
            printfn "? No obvious OPC zip files found"
    else
        printfn "✗ Could not access known working path"
        printfn "Error: %s" error

let main() =
    match exploreMissionStructure() with
    | Some _ -> 
        checkSpecificPaths()
        verifyOpcZipFiles()
        
        printfn "\n=== Summary ==="
        printfn "1. SFTP connection and authentication working"
        printfn "2. Check output above for which paths exist"
        printfn "3. Adjust path generation logic based on findings"
        printfn "4. Update generated project files with correct paths"
        
    | None -> 
        printfn "Failed to connect to SFTP server"
        printfn "Check credentials and network connectivity"

main()