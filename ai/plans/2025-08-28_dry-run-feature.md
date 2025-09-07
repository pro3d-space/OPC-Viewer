# Dry-Run Feature Implementation Plan

## Overview
Implement a `--dry-run` flag that outputs all command-line arguments as nicely formatted JSON without executing the actual viewer or processing pipelines. This enables testing and validation of CLI argument parsing without launching the full application.

## Requirements

### Functional Requirements
1. Add `--dry-run` flag available for all commands
2. When dry-run is active:
   - Parse and validate all arguments
   - Convert arguments to project JSON format
   - Output formatted JSON to console
   - Exit without executing actual command logic
3. If input is already a project JSON file, output its contents formatted
4. Support all existing commands in JSON format (view, list, diff, export, project)
5. Enable creation of test cases without launching viewer

### Non-Functional Requirements
- Maintain 0 errors, 0 warnings policy
- Follow existing F# patterns and Argu conventions
- Preserve backward compatibility
- Output should be valid, parseable JSON

## Design Decisions

### Architecture Choices
1. **Global Flag**: Make `--dry-run` a global flag available to all commands
2. **JSON Conversion**: Create serialization functions for each command type
3. **Project File Extension**: Extend existing ProjectFile module to support all commands
4. **Early Exit**: Check dry-run flag early in Program.fs to bypass execution

### Implementation Strategy
1. Extend discriminated union in Usage.fs with DryRun case
2. Add JSON serialization for List, Diff, Export commands in ProjectFile.fs
3. Handle dry-run logic in Program.fs before command dispatch
4. Create unified JSON output function

## Implementation Plan

### Phase 1: Analyze Existing Structure [COMPLETED]
- [x] Review Usage.fs for current CLI argument structure
- [x] Study ProjectFile.fs for existing JSON format
- [x] Understand Program.fs command dispatch logic
- [x] Identify all command types needing JSON support

**Findings:**
- Usage.fs defines top-level CliArguments with 5 commands (View, List, Diff, Export, Project) and Version flag
- Each command has its own Args type in separate modules
- ProjectFile.fs currently supports only View and Diff commands in JSON format
- Program.fs dispatches commands based on parsed arguments
- Need to add List and Export commands to project JSON support
- Configuration module provides type-safe config structures

### Phase 2: Add Dry-Run Flag [COMPLETED]
- [x] Add DryRun case to CLIArguments discriminated union
- [x] Implement IArgParserTemplate usage description
- [x] Make flag available globally across all commands

### Phase 3: Extend Project JSON Support [COMPLETED]
- [x] Add List command to ProjectConfig union
- [x] Add Export command to ProjectConfig union  
- [x] Implement JSON parsing for List and Export commands
- [x] Add handling in ProjectCommand.run for new types

### Phase 4: Implement Dry-Run Logic [COMPLETED]
- [x] Add dry-run check in Program.fs
- [x] Create DryRunSerializer module for argument-to-JSON conversion
- [x] Implement JSON serialization for all command types
- [x] Ensure early exit before command execution

### Phase 5: Testing and Validation [COMPLETED]
- [x] Build with 0 errors/warnings
- [x] Test each command type with dry-run
- [x] Verify JSON output is valid and complete
- [x] Create test-dryrun.cmd test script
- [x] Create example JSON files for all commands

### Phase 6: Documentation [COMPLETED]
- [x] Update README.md with dry-run usage and examples
- [x] Create example JSON outputs in examples/ directory
- [x] Document all project file formats including List and Export

## Implementation Progress

### Phase 1: Analyze Existing Structure [COMPLETED]

Analyzed the codebase and identified:
1. CLI argument structure using Argu library
2. Existing project JSON support for View and Diff commands only
3. Need to extend JSON support for List and Export commands
4. Command dispatch happens in Program.fs

### Phase 2: Design Implementation [COMPLETED]

Design decisions were successfully implemented:
1. Added `DryRun` flag as a top-level CliArgument
2. Created DryRunSerializer module for JSON serialization
3. Implemented early-exit logic in Program.fs
4. Used System.Text.Json with proper indentation for output

## Implementation Summary

### Files Modified
1. **Usage.fs** - Added DryRun flag to CliArguments discriminated union
2. **ProjectFile.fs** - Added ListProject and ExportProject types, parsing and serialization functions
3. **Project/DryRunSerializer.fs** (new) - Module for converting CLI arguments to JSON
4. **Program.fs** - Added dry-run check and early exit logic
5. **ProjectCommand.fs** - Added pattern matching for ListConfig and ExportConfig
6. **PRo3D.Viewer.fsproj** - Added DryRunSerializer.fs to build
7. **README.md** - Added dry-run documentation and examples

### Test Results
All commands tested successfully with dry-run:
- View command with data directories, OBJ files, and options
- Diff command with all parameters
- List command with stats flag
- Export command with format selection
- Project command with JSON file input
- Version command

### Deliverables
1. ✅ Dry-run flag implementation
2. ✅ JSON serialization for all commands
3. ✅ Support for List and Export in project files
4. ✅ Test script (test-dryrun.cmd)
5. ✅ Example JSON files in examples/ directory
6. ✅ Updated documentation

## Lessons Learned

1. **F# Project File Order**: The order of files in .fsproj is critical for F# compilation. DryRunSerializer had to be placed after Usage.fs since it depends on CliArguments.

2. **Pattern Matching**: F# requires fully qualified names for discriminated union cases when matching from different modules.

3. **Argu Library Behavior**: GetResults returns a list of lists for repeatable arguments, requiring flattening with List.concat.

4. **JSON Serialization**: System.Text.Json provides good control over formatting with JsonWriterOptions and Utf8JsonWriter.

5. **Testing Strategy**: Creating a comprehensive test script early helps validate the implementation continuously.

## Final Status

✅ **COMPLETED** - The dry-run feature has been fully implemented with 0 errors and 0 warnings. All requirements have been met:
- Parse and validate all arguments without execution
- Output formatted JSON for all command types  
- Support project JSON files for all commands
- Enable test creation without launching the viewer
- Maintain backward compatibility