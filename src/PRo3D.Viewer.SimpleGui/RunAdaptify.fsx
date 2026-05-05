// Run from your IDE / `dotnet fsi RunAdaptify.fsx` to (re)generate the adaptive
// model types (Model.g.fs) for this project using the locally-installed
// `dotnet adaptify` tool (see ../../.config/dotnet-tools.json).
//
// Uses `--local` so generation happens in standalone mode (no MSBuild integration).
open System
open System.Diagnostics
open System.IO

let projFileName = "PRo3D.Viewer.SimpleGui.fsproj"
let projFilePath = Path.Combine(__SOURCE_DIRECTORY__, projFileName)

let runProc (filename : string) (args : string) (workingDir : string) =
    printfn "running: %s %s  (cwd=%s)" filename args workingDir
    let psi =
        ProcessStartInfo(
            FileName = filename,
            Arguments = args,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true)
    use p = new Process(StartInfo = psi)
    p.OutputDataReceived.Add(fun e -> if not (isNull e.Data) then printfn "%s" e.Data)
    p.ErrorDataReceived.Add(fun e -> if not (isNull e.Data) then eprintfn "%s" e.Data)
    if not (p.Start()) then failwithf "failed to start %s" filename
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
    p.WaitForExit()
    p.ExitCode

// repository root holds .config/dotnet-tools.json so dotnet finds the local tool there
let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let exit = runProc "dotnet" (sprintf "adaptify --lenses --local --force \"%s\"" projFilePath) repoRoot
if exit <> 0 then failwithf "adaptify exited with code %d" exit
