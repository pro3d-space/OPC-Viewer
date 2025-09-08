#!/usr/bin/env dotnet fsi

// Script to check the downloaded zip file for corruption or issues

open System
open System.IO
open System.IO.Compression
open System.Diagnostics

let zipPath = @"c:\Data\Development\OPC-Viewer\data\sftp\dig-sftp.joanneum.at\Mission\0300\0320\Job_0320_8341-034-rad\result\Job_0320_8341-034-rad_opc.zip"

let checkZipFile() =
    printfn "=== Checking Downloaded Zip File ==="
    printfn "Path: %s" zipPath
    printfn ""
    
    if not (File.Exists(zipPath)) then
        printfn "❌ File does not exist!"
        exit 1
    
    let fileInfo = FileInfo(zipPath)
    printfn "✅ File exists"
    printfn "Size: %d bytes (%.2f MB)" fileInfo.Length (float fileInfo.Length / 1024.0 / 1024.0)
    printfn "Created: %s" (fileInfo.CreationTime.ToString())
    printfn "Modified: %s" (fileInfo.LastWriteTime.ToString())
    printfn ""
    
    // Test with .NET ZipArchive
    printfn "--- Testing with .NET ZipArchive ---"
    try
        use zip = ZipFile.OpenRead(zipPath)
        printfn "✅ Successfully opened with .NET ZipArchive"
        printfn "Entry count: %d" zip.Entries.Count
        
        printfn "Contents:"
        for entry in zip.Entries do
            printfn "  📄 %s (%d bytes)" entry.FullName entry.Length
    with
    | ex -> 
        printfn "❌ Failed to open with .NET ZipArchive"
        printfn "Error: %s" ex.Message
    
    printfn ""
    
    // Test with Windows built-in extraction
    printfn "--- Testing Windows ZIP handling ---"
    try
        let testExtractPath = Path.Combine(Path.GetTempPath(), "zip_test_" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testExtractPath) |> ignore
        
        ZipFile.ExtractToDirectory(zipPath, testExtractPath)
        
        let extractedFiles = Directory.GetFiles(testExtractPath, "*", SearchOption.AllDirectories)
        printfn "✅ Successfully extracted %d files" extractedFiles.Length
        
        // Clean up
        Directory.Delete(testExtractPath, true)
    with
    | ex ->
        printfn "❌ Failed to extract with Windows ZIP"
        printfn "Error: %s" ex.Message
    
    printfn ""
    
    // Test with command line tools
    printfn "--- Testing with command line tools ---"
    
    // Test with PowerShell Expand-Archive
    let testWithPowerShell() =
        let tempDir = Path.Combine(Path.GetTempPath(), "ps_zip_test_" + Guid.NewGuid().ToString("N"))
        try
            let startInfo = ProcessStartInfo()
            startInfo.FileName <- "powershell"
            startInfo.Arguments <- sprintf "-Command \"Expand-Archive -Path '%s' -DestinationPath '%s'\"" zipPath tempDir
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.CreateNoWindow <- true
            
            let proc = Process.Start(startInfo)
            let output = proc.StandardOutput.ReadToEnd()
            let error = proc.StandardError.ReadToEnd()
            proc.WaitForExit()
            
            if proc.ExitCode = 0 then
                let extractedFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
                printfn "✅ PowerShell Expand-Archive succeeded (%d files)" extractedFiles.Length
                Directory.Delete(tempDir, true)
            else
                printfn "❌ PowerShell Expand-Archive failed"
                printfn "Error: %s" error
        with
        | ex -> printfn "❌ PowerShell test failed: %s" ex.Message
    
    testWithPowerShell()

let checkServerFileSize() =
    printfn "\n=== Comparing with Server File Size ==="
    
    // Use psftp to check the original file size on server
    let tempPwFile = Path.GetTempFileName()
    let tempCmdFile = Path.GetTempFileName()
    
    try
        File.WriteAllText(tempPwFile, "efbAWDIn2347AwdB")
        File.WriteAllLines(tempCmdFile, ["ls Mission/0300/0320/Job_0320_8341-034-rad/result/"; "quit"])
        
        let startInfo = ProcessStartInfo()
        startInfo.FileName <- "psftp"
        startInfo.Arguments <- sprintf "-P 2200 -l mastcam-z -pwfile %s -b %s -batch dig-sftp.joanneum.at" tempPwFile tempCmdFile
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.CreateNoWindow <- true
        
        let proc = Process.Start(startInfo)
        let output = proc.StandardOutput.ReadToEnd()
        proc.WaitForExit()
        
        if output.Contains("Job_0320_8341-034-rad_opc.zip") then
            printfn "✅ Found file on server"
            // Try to extract file size from ls output
            let lines = output.Split('\n')
            for line in lines do
                if line.Contains("Job_0320_8341-034-rad_opc.zip") then
                    printfn "Server listing: %s" (line.Trim())
        else
            printfn "❌ File not found in server listing"
            printfn "Server output: %s" output
    finally
        try File.Delete(tempPwFile) with _ -> ()
        try File.Delete(tempCmdFile) with _ -> ()

let main() =
    checkZipFile()
    checkServerFileSize()
    
    printfn "\n=== Recommendations ==="
    printfn "1. If .NET extraction failed but PowerShell worked, there may be a .NET version issue"
    printfn "2. Compare local vs server file sizes to check for incomplete downloads"
    printfn "3. Try re-downloading if sizes don't match"
    printfn "4. Check if the OPC viewer has specific zip handling requirements"

main()