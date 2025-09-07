// Template for adding CLI arguments to ViewCommand (or other commands)
// Based on OBJ file support implementation

// 1. Add to Args discriminated union:
type Args =
    | [<MainCommand>] DataDirs of data_dir: string list
    | Speed of float
    | [<AltCommandLine("-s") >] Sftp of string
    | [<AltCommandLine("-b") >] BaseDir of string
    | [<AltCommandLine("-x") >] NewFeature of string list    // NEW: Replace with your feature

// 2. Add to IArgParserTemplate Usage:
interface IArgParserTemplate with
    member s.Usage =
        match s with
        | DataDirs _ -> "specify data directories"
        | Speed    _ -> "optional camera controller speed"
        | Sftp     _ -> "optional SFTP server config file (FileZilla format)"
        | BaseDir  _ -> "optional base directory for relative paths (default is ./data)"
        | NewFeature _ -> "description of your new feature"   // NEW: Replace with your description

// 3. Add processing logic in run function:
let run (args : ParseResults<Args>) : int =
    
    // ... existing code ...

    // Process your new feature (add after existing processing, before scene creation):
    let newFeatureItems = args.GetResult(Args.NewFeature, defaultValue = [])
    printfn "[FEATURE] Processing %d items..." newFeatureItems.Length
    let validItems = 
        newFeatureItems 
        |> List.filter (fun item ->
            // Replace with your validation logic
            if System.IO.File.Exists item then 
                printfn "[FEATURE] Found item: %s" item
                System.Console.Out.Flush()
                true
            else 
                printfn "[FEATURE WARNING] Item not found: %s" item
                System.Console.Out.Flush()
                false
        )

    printfn "[FEATURE] Loaded %d valid items" validItems.Length
    System.Console.Out.Flush()

    // Use validItems in your scene creation or pass to viewer
    
    // ... rest of existing code ...

// Usage examples:
// opcviewer view <dataset> --newfeature item1 item2
// opcviewer view <dataset> -x item1
//
// This template provides:
// - Optional argument with default empty list
// - File existence validation (customize as needed)  
// - User feedback with console output
// - Integration point for further processing