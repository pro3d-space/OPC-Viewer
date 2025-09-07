#!/usr/bin/env dotnet fsi

// F# Script to analyze AI candidate datasets and generate diff project files
// Usage: dotnet fsi scripts/AnalyzeAICandidates.fsx

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

// AI Candidate dataset pairs extracted from PDF
let aiCandidatePairs = [
    // Sol 300
    ("Job_0320_8341-034-rad", "Job_0320_8341-034-rad-AI", 300)
    ("Job_0349_8380-110-rad", "Job_0349_8380-110-rad-AI", 300)
    ("Job_0360_8390-079-rad", "Job_0360_8390-079-rad-AI", 300)
    ("Job_0361_3326-100-rad", "Job_0361_3326-100-rad-AI", 300)
    
    // Sol 400
    ("Job_0461_8482-110-rad", "Job_0461_8482-110-rad-AI", 400)
    ("Job_0464_3381-034-rad", "Job_0464_3381-034-rad-AI", 400)
    ("Job_0492_8517-034-rad", "Job_0492_8517-034-rad-AI", 400)
    ("Job_0466_8486-110-rad", "Job_0466_8486-110-rad-AI", 400)
    
    // Sol 500
    ("Job_0501_8522-079-rad", "Job_0501_8522-079-rad-AI", 500)
    ("Job_0557_3439-034-rad", "Job_0557_3439-034-rad-AI", 500)
    ("Job_0565_8586-034-rad", "Job_0565_8586-034-rad-AI", 500)
    
    // Sol 800
    ("Job_0856_8860-110-rad", "Job_0856_8860-110-rad-AI", 800)
    ("Job_0817_8833-034-rad", "Job_0817_8833-034-rad-AI", 800)
    
    // Sol 900
    ("Job_0910_8915-063-rad", "Job_0910_8915-063-rad-AI", 900)
    ("Job_0917_3773-034-rad", "Job_0917_3773-034-rad-AI", 900)
    ("Job_0911_3765-034-rad", "Job_0911_3765-034-rad-AI", 900)
    ("Job_0911_8917-110-rad", "Job_0911_8917-110-rad-AI", 900)
    ("Job_0913_3768-110-rad", "Job_0913_3768-110-rad-AI", 900)
    ("Job_0913_8920-110-rad", "Job_0913_8920-110-rad-AI", 900)
    ("Job_0914_8922-110-rad", "Job_0914_8922-110-rad-AI", 900)
    ("Job_0933_8945-063-rad", "Job_0933_8945-063-rad-AI", 900)
    ("Job_0955_8973-110-rad", "Job_0955_8973-110-rad-AI", 900)
    ("Job_0958_8980-110-rad", "Job_0958_8980-110-rad-AI", 900)
    
    // Sol 1000
    ("Job_1043_9063-034-rad", "Job_1043_9063-034-rad-AI", 1000)
    ("Job_1045_9060-034-rad", "Job_1045_9060-034-rad-AI", 1000)
    
    // Sol 1100
    ("Job_1130_9150-079-rad", "Job_1130_9150-079-rad-AI", 1100)
    ("Job_1155_9193-048-rad", "Job_1155_9193-048-rad-AI", 1100)
    ("Job_1177_9218-110-rad", "Job_1177_9218-110-rad-AI", 1100)
    ("Job_1178_9219-063-rad", "Job_1178_9219-063-rad-AI", 1100)
    
    // Sol 1200
    ("Job_1203_9246-110-rad", "Job_1203_9246-110-rad-AI", 1200)
    ("Job_1229_9279-110-rad", "Job_1229_9279-110-rad-AI", 1200)
    
    // Sol 1300
    ("Job_1311_9374-048-rad", "Job_1311_9374-048-rad-AI", 1300)
    ("Job_1337_9408-034-rad", "Job_1337_9408-034-rad-AI", 1300)
]

// Function to extract job number from job name
let extractJobNumber (jobName: string) =
    let regex = Regex(@"Job_(\d+)_")
    let m = regex.Match(jobName)
    if m.Success then 
        int m.Groups[1].Value
    else
        failwithf "Could not extract job number from %s" jobName

// Function to map job number to Mission folder (based on example pattern)
let jobToMissionFolder jobNumber =
    let hundreds = jobNumber / 100 * 100
    sprintf "%04d" hundreds

// Function to map job number to specific folder (based on example pattern)  
let jobToSpecificFolder jobNumber =
    sprintf "%04d" jobNumber

// Generate SFTP path for a job
let generateSftpPath (jobName: string) =
    let jobNumber = extractJobNumber jobName
    let missionFolder = jobToMissionFolder jobNumber
    let specificFolder = jobToSpecificFolder jobNumber
    
    sprintf "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/%s/%s/%s/result/%s_opc.zip" 
        missionFolder specificFolder jobName jobName

// Generate diff project file for a pair
let generateDiffProject (noAiJob: string, aiJob: string, sol: int) =
    let noAiPath = generateSftpPath noAiJob
    let aiPath = generateSftpPath aiJob
    
    let project = {|
        command = "diff"
        data = [| noAiPath; aiPath |]
        sftp = @"W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml"
        screenshots = sprintf @"W:\Datasets\Pro3D\AI-Comparison-Screenshots\Sol_%d\%s_vs_%s" sol noAiJob aiJob
        speed = 1.0
        verbose = true
    |}
    
    let options = JsonSerializerOptions()
    options.WriteIndented <- true
    JsonSerializer.Serialize(project, options)

// Generate CLI command for a pair
let generateCliCommand (noAiJob: string, aiJob: string, sol: int) =
    let noAiPath = generateSftpPath noAiJob
    let aiPath = generateSftpPath aiJob
    let screenshotDir = sprintf @"W:\Datasets\Pro3D\AI-Comparison-Screenshots\Sol_%d\%s_vs_%s" sol noAiJob aiJob
    
    sprintf """pro3dviewer diff "%s" "%s" --sftp "W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml" --screenshots "%s" --speed 1.0 --verbose""" 
        noAiPath aiPath screenshotDir

// Create output directory structure
let createOutputDirectories() =
    let baseDir = @"W:\Datasets\Pro3D\AI-Comparison"
    Directory.CreateDirectory(baseDir) |> ignore
    Directory.CreateDirectory(Path.Combine(baseDir, "Projects")) |> ignore
    Directory.CreateDirectory(Path.Combine(baseDir, "Commands")) |> ignore
    Directory.CreateDirectory(Path.Combine(baseDir, "Screenshots")) |> ignore
    baseDir

// Generate all project files and commands
let generateAllFiles() =
    let baseDir = createOutputDirectories()
    
    printfn "Generating files for %d AI candidate pairs..." aiCandidatePairs.Length
    printfn ""
    
    // Generate individual project files
    aiCandidatePairs |> List.iteri (fun i (noAi, ai, sol) ->
        let projectJson = generateDiffProject (noAi, ai, sol)
        let projectPath = Path.Combine(baseDir, "Projects", sprintf "Sol_%d_%s_vs_%s.json" sol noAi ai)
        File.WriteAllText(projectPath, projectJson)
        
        printfn "Generated: %s" (Path.GetFileName(projectPath))
    )
    
    printfn ""
    
    // Generate batch command file
    let commands = aiCandidatePairs |> List.map generateCliCommand
    let commandsPath = Path.Combine(baseDir, "Commands", "all_comparisons.bat")
    File.WriteAllLines(commandsPath, commands)
    
    printfn "Generated batch commands: %s" commandsPath
    printfn ""
    
    // Generate PowerShell script for parallel execution
    let psCommands = commands |> List.mapi (fun i cmd ->
        sprintf """Write-Host "Starting comparison %d/%d..."
%s
if ($LASTEXITCODE -ne 0) { Write-Warning "Command %d failed" }""" (i+1) commands.Length cmd (i+1))
    
    let psScript = String.Join("\n\n", psCommands)
    let psPath = Path.Combine(baseDir, "Commands", "all_comparisons.ps1")
    File.WriteAllText(psPath, psScript)
    
    printfn "Generated PowerShell script: %s" psPath
    printfn ""
    
    // Generate summary report
    let solDistribution = 
        aiCandidatePairs 
        |> List.groupBy (fun (_, _, sol) -> sol)
        |> List.map (fun (sol, pairs) -> sprintf "- Sol %d: %d pairs" sol pairs.Length)
    
    let samplePaths = 
        aiCandidatePairs 
        |> List.take 3
        |> List.collect (fun (noAi, ai, sol) -> [
            sprintf "### %s vs %s (Sol %d)" noAi ai sol
            sprintf "- No-AI: %s" (generateSftpPath noAi)
            sprintf "- AI: %s" (generateSftpPath ai)
            ""
        ])
    
    let header = [
        "# AI Candidates Analysis Summary"
        ""
        sprintf "Total dataset pairs: %d" aiCandidatePairs.Length
        ""
        "## Sol Distribution:"
    ]
    
    let sampleSection = [
        ""
        "## Sample SFTP Paths:"
    ]
    
    let usageSection = [
        "## Usage:"
        ""
        "1. **Individual comparison:**"
        "   ```"
        "   pro3dviewer project Projects/Sol_300_Job_0320_8341-034-rad_vs_Job_0320_8341-034-rad-AI.json"
        "   ```"
        ""
        "2. **Batch execution:**"
        "   ```"
        "   Commands/all_comparisons.bat"
        "   ```"
        ""
        "3. **PowerShell parallel execution:**"
        "   ```"
        "   PowerShell -ExecutionPolicy Bypass -File Commands/all_comparisons.ps1"
        "   ```"
    ]
    
    let summary = List.concat [header; solDistribution; sampleSection; samplePaths; usageSection]
    
    let summaryPath = Path.Combine(baseDir, "README.md")
    File.WriteAllLines(summaryPath, summary)
    
    printfn "Generated summary: %s" summaryPath
    printfn ""
    printfn "All files generated successfully!"
    
    baseDir

// Validation function to check if SFTP paths follow expected pattern
let validateSftpPaths() =
    printfn "Validating SFTP path generation..."
    printfn ""
    
    let exampleJob = "Job_0610_8618-110-rad-AI-Test"
    let expectedPath = "sftp://mastcam-z-admin@dig-sftp.joanneum.at:2200/Mission/0600/0610/Job_0610_8618-110-rad-AI-Test/result/Job_0610_8618-110-rad-AI-Test_opc.zip"
    let generatedPath = generateSftpPath exampleJob
    
    printfn "Example from JSON: %s" expectedPath  
    printfn "Generated:         %s" generatedPath
    printfn "Match: %b" (expectedPath = generatedPath)
    printfn ""

// Main execution
let main() =
    printfn "=== AI Candidates Analysis ==="
    printfn ""
    
    validateSftpPaths()
    
    let baseDir = generateAllFiles()
    
    printfn "Next steps:"
    printfn "1. Test connectivity to SFTP server with a single dataset"
    printfn "2. Verify generated paths match actual server structure" 
    printfn "3. Run sample comparisons to validate viewer functionality"
    printfn ""
    printfn "Generated files in: %s" baseDir

// Run the analysis
main()