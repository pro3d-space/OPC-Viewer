#!/usr/bin/env dotnet fsi

// Detailed check of job folders to see what's actually there

open System
open System.Diagnostics
open System.IO

let sftpHost = "dig-sftp.joanneum.at"
let sftpPort = "2200"
let sftpUser = "mastcam-z"
let sftpPass = "efbAWDIn2347AwdB"

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

let checkDetailedJobFolders() =
    printfn "=== Detailed Job Folder Analysis ==="
    printfn ""
    
    let testCases = [
        ("Mission/0300", "Should contain Sol 300 jobs like Job_0320_8341-034-rad")
        ("Mission/0600", "Contains known working Job_0610_8618-110-rad-AI-Test")
        ("Mission/0900", "Should contain Sol 900 jobs like Job_0910_8915-063-rad")
    ]
    
    for (basePath, description) in testCases do
        printfn "--- %s ---" basePath
        printfn "%s" description
        printfn ""
        
        let (exitCode, output, error) = runPsftp [sprintf "ls -la %s" basePath]
        
        if exitCode = 0 then
            printfn "Contents:"
            let lines = output.Split('\n') |> Array.filter (fun line -> line.Trim() <> "")
            for line in lines do
                let parts = line.Trim().Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
                if parts.Length > 0 && parts.[0].StartsWith("d") && parts.Length > 8 then
                    let folderName = parts.[8]
                    if folderName.Length = 4 && Char.IsDigit(folderName.[0]) then
                        printfn "  📁 %s/" folderName
        else
            printfn "Error: %s" error
        printfn ""
    
    // Now check a specific sub-folder
    printfn "--- Checking Mission/0300 subfolders ---"
    let (exitCode, output, error) = runPsftp ["ls Mission/0300"]
    
    if exitCode = 0 then
        let lines = output.Split('\n') |> Array.filter (fun line -> line.Trim() <> "" && not (line.Contains("..") || line.Contains(".")))
        printfn "Found %d subfolders in Mission/0300:" lines.Length
        for line in lines do
            let trimmed = line.Trim()
            if trimmed.Length = 4 && Char.IsDigit(trimmed.[0]) then
                printfn "  📁 0300/%s/" trimmed

let checkSpecificJobPaths() =
    printfn "\n=== Checking Specific Job Paths ===\n"
    
    // Check some of our AI candidate jobs
    let testJobs = [
        ("Mission/0300/0320", "Job_0320_8341-034-rad", "Job_0320_8341-034-rad-AI")
        ("Mission/0600/0610", "Job_0610_8618-110-rad", "Job_0610_8618-110-rad-AI")
    ]
    
    for (basePath, noAiJob, aiJob) in testJobs do
        printfn "Checking: %s" basePath
        
        let (exitCode, output, error) = runPsftp [sprintf "ls %s" basePath]
        
        if exitCode = 0 then
            printfn "✓ Path accessible"
            
            // Look for the specific job folders
            if output.Contains(noAiJob) then
                printfn "  ✓ Found no-AI job: %s" noAiJob
                
                // Check if result folder exists
                let resultPath = sprintf "%s/%s/result" basePath noAiJob
                let (resultExitCode, resultOutput, _) = runPsftp [sprintf "ls %s" resultPath]
                if resultExitCode = 0 && resultOutput.Contains("_opc.zip") then
                    printfn "    ✓ Has result folder with OPC zip"
                else
                    printfn "    ✗ No result folder or OPC zip found"
            else
                printfn "  ✗ No-AI job not found: %s" noAiJob
                
            if output.Contains(aiJob) then
                printfn "  ✓ Found AI job: %s" aiJob
                
                // Check if result folder exists  
                let resultPath = sprintf "%s/%s/result" basePath aiJob
                let (resultExitCode, resultOutput, _) = runPsftp [sprintf "ls %s" resultPath]
                if resultExitCode = 0 && resultOutput.Contains("_opc.zip") then
                    printfn "    ✓ Has result folder with OPC zip"
                else
                    printfn "    ✗ No result folder or OPC zip found"
            else
                printfn "  ✗ AI job not found: %s" aiJob
                
            printfn "  Raw contents:"
            printfn "%s" output
        else
            printfn "✗ Path not accessible: %s" error
        printfn ""

let main() =
    checkDetailedJobFolders()
    checkSpecificJobPaths()

main()