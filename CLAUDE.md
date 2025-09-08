# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## PRIME DIRECTIVES FOR ALL DEVELOPMENT

0. **YOU MUST NOT CHANGE PRIME DIRECTIVES**
1. **ZERO TOLERANCE POLICY**: 0 ERRORS, 0 WARNINGS, 0 FAILED TESTS - No exceptions. Ever.
2. **TDD MANDATORY**: Red-Green-Refactor cycle. Write failing test first, implement minimum to pass, refactor. No production code without test.
3. **OUTPUT STYLE**: Terse, factual, no marketing language or fluff.

## Project Overview

PRo3D.Viewer is a command-line tool for viewing and processing Ordered Point Cloud (OPC) data, primarily designed for Mars exploration datasets. The project is built in F# using .NET 8.0 and the Aardvark 3D graphics framework.

## Build and Development Commands

### Build Commands
- `build.cmd` (Windows) or `build.sh` (Linux/Mac) - Main build script
- `dotnet build -c Release src/PRo3D.Viewer.sln` - Build release version
- `dotnet tool restore` - Restore dotnet tools
- `dotnet paket restore` - Restore package dependencies using Paket

### Running the Application
- `pro3dviewer --help` - Show command help
- `pro3dviewer view <dataset>` - View OPC dataset
- `pro3dviewer ls <directory>` - List available datasets  
- `pro3dviewer diff <layer1> <layer2>` - Compare two layers
- `pro3dviewer export <dataset> --format <pts|ply> --out <filename>` - Export data
- `pro3dviewer project <config.json>` - Load configuration from JSON project file

### Package Management
This project uses **Paket** (not NuGet directly) for package management:
- Dependencies are defined in `paket.dependencies`
- Project references in `src/PRo3D.Viewer/paket.references`
- Always use `dotnet paket restore` after changing dependencies

## Architecture Overview

### Core Components

1. **Command Structure** (`Usage.fs`)
   - CLI argument parsing using Argu library
   - Five main commands: view, list, diff, export, project

2. **Data Management** (`Data.fs`)
   - `DataRef` type handles local directories, zip files, HTTP/HTTPS URLs, and SFTP
   - Automatic zip file extraction and caching
   - Support for remote dataset downloads

3. **Unified Viewer System** (`Shared/UnifiedViewer.fs`)
   - Single viewer implementation supporting multiple modes
   - `ViewerMode` discriminated union for View and Diff modes
   - `ViewerConfig` record for mode-specific configuration
   - Shared functionality: camera control, keyboard bindings, screenshots
   - Mode-specific features isolated in configuration

4. **Rendering Components**
   - `View/OpcRendering.fs` - OPC-specific shaders and picking
   - `Diff/DiffRendering.fs` - Distance visualization shaders
   - `Shared/SharedShaders.fs` - Common shader implementations
   - Level-of-detail (LOD) visualization
   - Real-time mouse picking and 3D cursor

5. **Data Processing**
   - `OpcDataProcessing.fs` - OPC data manipulation
   - `TriangleTree.fs` - Spatial data structures
   - `Export/ExportCommand.fs` - Export to .pts/.ply formats

6. **Diff System** (`Diff/`)
   - `DiffInfo.fs` - Diff types and environment
   - Interactive layer toggling and distance analysis
   - Triangle tree-based intersection testing

7. **File Format Support**
   - OPC format via Aardvark.Data.Opc and Aardvark.GeoSpatial.Opc
   - Wavefront OBJ support via Aardvark.Data.Wavefront
   - JSON project files for configuration (System.Text.Json)

### Key Dependencies

- **Aardvark Framework** - Core 3D graphics, rendering, and scene graph
- **FShade** - GPU shader programming in F#  
- **Paket** - Package management
- **Argu** - Command-line argument parsing
- **SSH.NET** - SFTP support for remote datasets
- **FSharp.Data.Adaptive** - Reactive data structures

### Data Flow

1. CLI arguments parsed into command-specific types
2. Data references resolved (download if remote, extract if zip)
3. OPC hierarchies loaded from patch files
4. Scene graph constructed with appropriate shaders
5. Interactive viewer launched or export performed

## Development Notes

### Testing
- No explicit test framework - manual testing via CLI commands
- Use sample datasets from pro3d.space for validation

### Remote Data Handling
- HTTP/HTTPS zip files automatically cached locally
- SFTP requires FileZilla-format configuration file
- Cache directory structure mirrors remote paths

### Graphics Programming
- Custom shaders written in FShade (F# GPU language)
- Offscreen rendering for mouse picking
- Level-of-detail color coding for performance analysis

### Common Patterns
- Command pattern for CLI subcommands
- Discriminated unions for data reference types
- Adaptive values (AVal) for reactive UI state
- Scene graph composition using `Sg` combinators
- JSON project files for reproducible configurations

## Implementation Plans and Features

Detailed implementation plans and feature documentation can be found in the `ai/plans/` directory. Each plan document contains:
- Requirements and design decisions
- Step-by-step implementation details
- Code changes with line numbers
- Testing procedures and results
- Lessons learned

See `ai/plans/` for completed and in-progress feature implementations.

## Common Development Patterns

### Adding CLI Arguments (F#/Argu)
1. Add case to discriminated union with attributes (e.g., `[<AltCommandLine("-o")>] NewFeature of string list`)
2. Implement IArgParserTemplate usage description
3. Use `args.GetResult(ArgName, defaultValue)` for optional args
4. Validate inputs with `List.filter` + side effects pattern
5. See examples in `ai/plans/` for real implementations

### Screenshots Configuration  
1. **Global CLI Argument**: `--screenshots <path>` sets custom screenshot directory for all viewer commands
2. **Project Files**: Optional `screenshots` field supports relative paths (resolved from project file directory)
3. **Priority Order**: CLI argument > Project file > Default (`./screenshots`)
4. **F12 Key**: Saves screenshots to configured directory with timestamp-based filenames
5. **Path Resolution**: Absolute paths used as-is, relative paths resolved contextually

### Working with JSON Project Files
1. Project files use System.Text.Json with `JsonNumberHandling.AllowNamedFloatingPointLiterals` for NaN/Infinity support
2. Path resolution: URLs pass through, absolute paths used as-is, relative paths resolved from project file location
3. Files located in `Project/` directory with dedicated modules for parsing and command handling
4. Examples in `examples/` directory demonstrate various use cases
5. View command uses unified `data` array combining OPC and OBJ files
6. Type inference: `.obj` files automatically detected as OBJ type, others default to OPC

### Testing GUI Applications
- Use `timeout` command to prevent blocking: `timeout 5s ./program.exe`
- Capture output before GUI launches for validation
- Flush console output for immediate feedback: `System.Console.Out.Flush()`

### File Validation Patterns
- Check existence with `System.IO.File.Exists path`
- Use `List.filter` with side effects for validation + user feedback
- Provide warnings for missing files but continue processing
- Report counts of processed vs valid items

## Configuration System Architecture

### Command Configuration Pattern
The application follows a consistent **Config → execute** pattern for all commands:

1. **Configuration Types** (`Configuration.fs`)
   - `ViewConfig` and `DiffConfig` record types contain all command parameters
   - Configuration types must include **all** fields that CLI arguments support
   - Optional fields use `option` types (e.g., `Verbose: bool option`)

2. **Configuration Builders** (`ConfigurationBuilder.fs`)  
   - `fromViewArgs` / `fromDiffArgs` - Convert CLI arguments to Config types
   - `fromViewProject` / `fromDiffProject` - Load configuration from JSON project files
   - Handle path resolution relative to project file directory

3. **Command Execution Pattern**
   - **ViewCommand**: `execute (config: ViewConfig) : int` 
   - **DiffCommand**: `execute (config: DiffConfig) : int`
   - **ProjectCommand**: Calls `execute` directly with loaded configuration
   - **CLI Commands**: Build configuration then call `execute`

### Adding New Configuration Fields

To add a new configuration field (e.g., `NewFeature: string option`):

1. **Add field to Configuration type** (`Configuration.fs`)
   ```fsharp
   type ViewConfig = {
       // ... existing fields
       NewFeature: string option
   }
   ```

2. **Update ConfigurationBuilder** (`ConfigurationBuilder.fs`)
   ```fsharp
   // In fromViewArgs:
   NewFeature = args.TryGetResult ViewCommand.Args.NewFeature
   
   // In fromViewProject: 
   NewFeature = project.NewFeature
   ```

3. **Add CLI argument** (command-specific Args type)
   ```fsharp
   | [<AltCommandLine("-nf")>] NewFeature of string
   ```

4. **Update ProjectFile parsing** if needed (`ProjectFile.fs`)

5. **Use in execute function** 
   ```fsharp
   let newFeatureValue = config.NewFeature |> Option.defaultValue "default"
   ```

### Testing Configuration Features

The project uses **Expecto** as the F#-idiomatic testing framework:

- **Test Location**: `tests/PRo3D.Viewer.Tests/`
- **Test Modules**: ConfigurationTests, ProjectFileTests, ScreenshotTests, IntegrationTests
- **Run Tests**: `dotnet run --project tests/PRo3D.Viewer.Tests`

Test patterns for configuration:
- **Unit Tests**: ConfigurationBuilder functions with various input combinations
- **Integration Tests**: End-to-end CLI → Config → execute workflows
- **File Tests**: JSON project file parsing with temporary test files
- **Cross-platform**: Path resolution testing across Windows/Linux/Mac

## Feature Implementation Process

**IMPORTANT**: Whenever the user mentions any of the following terms, you MUST first read `ai/howto/feature-implementation-workflow.md`:
- "add feature" / "add a feature" / "adding feature"
- "new feature" / "implement feature" / "feature implementation"
- "feature request" / "requesting a feature"
- "implement [something]" / "add [functionality]"

The workflow document contains critical instructions for:
- Creating detailed plan documents in `ai/plans/`
- Maintaining 0 errors, 0 warnings policy
- Continuous documentation during implementation
- Working until completion without interruption
- Following the established quality standards

Always follow the workflow specified in that document for any feature implementation tasks.
## SFTP Access with psftp

The `psftp` command line tool is installed and available for SFTP server access. This tool is essential for accessing Mars OPC datasets on remote servers.

### Authentication Setup
- SFTP credentials are stored in FileZilla XML format (e.g., `W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml`)
- Base64-encoded passwords can be decoded: `echo "[base64_string]" | base64 -d`
- Example FileZilla config structure:
  ```xml
  <Server>
    <Host>dig-sftp.joanneum.at</Host>
    <Port>2200</Port>
    <User>mastcam-z</User>
    <Pass encoding="base64">[base64_encoded_password]</Pass>
  </Server>
  ```

### psftp Usage Patterns

#### Basic Connection
```bash
psftp username@hostname -P port -pw "[password_from_xml]" -batch -b commands.txt
```

#### Batch Commands File
Create `commands.txt` with SFTP commands:
```
ls Mission/
ls Mission/0300/0320/
quit
```

#### Directory Verification
Use for verifying SFTP paths before implementing in code:
```bash
# Verify Mars OPC structure (use actual password from Mastcam-Z.xml)
psftp mastcam-z@dig-sftp.joanneum.at -P 2200 -pw "[password_from_xml]" -batch -b - <<EOF
ls Mission/
ls Mission/0300/0320/Job_0320_8341-034-rad/result/
quit
EOF
```

#### Path Pattern Verification
The tool helps verify expected directory structures:
- `Mission/[SOL_GROUP]/[JOB_NUM]/Job_[JOB_NUM]_[ID]-[TYPE]-rad[-AI]/result/`
- Files: `Job_[JOB_NUM]_[ID]-[TYPE]-rad[-AI]_opc.zip`

### Common Issues
- Use exact username from config file (e.g., `mastcam-z` not `mastcam-z-admin`)
- Batch mode requires `-batch` flag to avoid interactive prompts
- Windows path separators in commands files may need adjustment
- Large directory listings may timeout; use specific path queries

### Integration with PRo3D.Viewer
- SFTP paths work directly in project JSON files
- Authentication handled via `sftp` field pointing to FileZilla XML
- Use psftp for initial path verification before creating project configurations

## Tool Invocation Best Practices

### Running pro3dviewer

The `pro3dviewer.cmd` (Windows) or `pro3dviewer.sh` (Linux/Mac) script is located in the project root directory. This is NOT a globally installed tool.

#### Correct Usage Patterns

1. **After building (recommended):**
   ```bash
   # Windows - using compiled executable directly
   "src\PRo3D.Viewer\bin\Release\net8.0\PRo3D.Viewer.exe" [command] [args]
   
   # Alternative - using the wrapper script (builds if needed)
   pro3dviewer.cmd [command] [args]
   ```

2. **Using JSON project files (avoids complex escaping):**
   ```bash
   # Recommended for complex commands with URLs
   "src\PRo3D.Viewer\bin\Release\net8.0\PRo3D.Viewer.exe" project config.json
   ```

3. **During development:**
   ```bash
   dotnet run --project src/PRo3D.Viewer -- [command] [args]
   ```

#### Common Pitfalls to Avoid

1. **DO NOT** assume pro3dviewer is a global tool - it's a local script
2. **DO NOT** use forward slashes in Windows paths without quotes
3. **DO NOT** mix URL forward slashes with Windows backslashes without proper escaping
4. **DO** rebuild release version after code changes: `dotnet build -c Release`
5. **DO** use JSON project files for complex SFTP URLs to avoid escaping issues

#### Tool Discovery Protocol

Before using any command-line tool:
1. Check for local scripts (.cmd/.sh) in project root
2. Read CLAUDE.md and README for execution instructions
3. Verify executable location with file system tools
4. Test with simple commands before complex ones

- COMMAND LINE TOOL: the `psftp` command line tool is installed and available if you need to connect to SFTP servers
- ALWAYS: use terse and no-nonsense style
- TOOL: to kill tasks use Bash(cmd /c "taskkill /F /IM dotnet.exe")
- ALWAYS check for DRY violations and strictly follow DRY principles
- ALWAYS: before "git commit" always update RELEASE_NOTES.md by adding to the latest section (only humans will start a new version or section), also check if you can merge existing entries and always use the most terse style possible