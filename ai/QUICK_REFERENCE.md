# OPC Viewer Quick Reference

## Build & Test

### Build Commands
```bash
dotnet build -c Release src/PRo3D.Viewer.sln  # Build release version
dotnet tool restore            # Restore dotnet tools
dotnet paket restore          # Restore packages (Paket, not NuGet)
build.cmd                     # Main build script (Windows)
build.sh                      # Main build script (Linux/Mac)
```

### Running & Testing
```bash
# Basic usage
./PRo3D.OpcViewer.exe <command> <args>

# Show help
./PRo3D.OpcViewer.exe --help
./PRo3D.OpcViewer.exe view --help

# Test GUI apps with timeout (prevents blocking)
timeout 5s ./PRo3D.OpcViewer.exe view dataset --obj model.obj

# Capture output
timeout 5s ./PRo3D.OpcViewer.exe view args 2>&1 | grep "pattern"
```

## Commands

### View Command
```bash
opcviewer view <dataset> [options]

Options:
  --speed <float>              # Camera speed
  --sftp|-s <config.xml>       # SFTP config file
  --basedir|-b <dir>           # Base directory for relative paths
  --objfiles|-o <file1> <file2> # OBJ files to load alongside OPC data
```

### Other Commands
```bash
opcviewer ls <directory>                    # List available datasets
opcviewer diff <layer1> <layer2>           # Compare layers
opcviewer export <dataset> --format <pts|ply> --out <file> # Export
```

## Adding Features Checklist

### Planning Phase
- [ ] Create plan document in `docs/plans/<feature-name>.md`
- [ ] Research existing patterns in codebase
- [ ] Design argument structure and validation approach
- [ ] Plan integration points

### Implementation Phase  
- [ ] Update discriminated union in Args (with attributes)
- [ ] Add usage description in IArgParserTemplate
- [ ] Implement processing logic in run function
- [ ] Add validation with user feedback (printfn + flush)
- [ ] Handle edge cases (missing files, empty lists, etc.)

### Testing Phase
- [ ] Test help output shows new argument
- [ ] Test valid arguments work correctly  
- [ ] Test invalid/missing inputs show proper warnings
- [ ] Test backward compatibility (existing usage still works)
- [ ] Test with timeout to avoid GUI blocking

### Documentation Phase
- [ ] Update plan document with implementation details
- [ ] Document any new patterns discovered
- [ ] Add usage examples

## Key Files

### Command Structure
- `src/PRo3D.OpcViewer/Usage.fs` - Main CLI argument definitions
- `src/PRo3D.OpcViewer/View/ViewCommand.fs` - View command implementation
- `src/PRo3D.OpcViewer/Diff/DiffCommand.fs` - Diff command implementation
- `src/PRo3D.OpcViewer/Export/ExportCommand.fs` - Export command implementation
- `src/PRo3D.OpcViewer/Program.fs` - Main entry point

### Data & Processing
- `src/PRo3D.OpcViewer/Data.fs` - Data loading (OPC, OBJ via Wavefront module)
- `src/PRo3D.OpcViewer/View/Viewer.fs` - Main OpenGL viewer
- `src/PRo3D.OpcViewer/View/OpcRendering.fs` - OPC-specific rendering

### Configuration
- `paket.dependencies` - Package dependencies (use Paket, not NuGet)
- `src/PRo3D.OpcViewer/paket.references` - Project package references

## Common Patterns

### CLI Arguments (F#/Argu)
```fsharp
// In Args type:
| [<AltCommandLine("-x")>] NewFeature of string list

// In Usage interface:
| NewFeature _ -> "description"

// In run function:
let items = args.GetResult(Args.NewFeature, defaultValue = [])
let validItems = items |> List.filter validateWithSideEffects
```

### File Validation Pattern
```fsharp
let validItems = 
    items |> List.filter (fun path ->
        if System.IO.File.Exists path then 
            printfn "[FEATURE] Found: %s" path
            System.Console.Out.Flush()  // Important for GUI testing
            true
        else 
            printfn "[FEATURE WARNING] Not found: %s" path
            System.Console.Out.Flush()
            false
    )
```

### Testing GUI Applications
```bash
# Use timeout to prevent infinite blocking
timeout 5s ./PRo3D.OpcViewer.exe view testdir --obj file.obj

# Capture and filter output
timeout 5s ./program 2>&1 | grep "[FEATURE]"
```

## Documentation Structure

### Main Documentation
- `CLAUDE.md` - High-level guidance for AI assistants
- `README.md` - User-facing documentation
- `docs/QUICK_REFERENCE.md` - This file

### Implementation Details  
- `docs/plans/` - Detailed implementation plans for each feature
- `docs/templates/` - Code templates and examples

### Project Dependencies
- **Aardvark Framework** - 3D graphics and scene graph
- **FShade** - GPU shader programming in F#
- **Argu** - Command-line argument parsing
- **Paket** - Package management (not NuGet!)
- **SSH.NET** - SFTP support

## Troubleshooting

### Build Issues
- Always use `dotnet paket restore` (not `dotnet restore`)
- Check `paket.dependencies` for package versions
- Use `dotnet tool restore` for tools

### Testing Issues  
- GUI apps block indefinitely - always use `timeout`
- Console output may be buffered - use `System.Console.Out.Flush()`
- Check help output first: `./program.exe <command> --help`

### Development Tips
- Follow existing patterns in similar commands
- Add plenty of user feedback (printfn messages)
- Test edge cases (missing files, empty inputs)
- Keep detailed implementation notes in `docs/plans/`