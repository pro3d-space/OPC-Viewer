# TDD-Based Export Remote Data Support Implementation

**Date**: 2025-09-07  
**Feature**: Enhanced export command with remote data support, property consistency, and missing configuration options  
**Status**: IN PROGRESS  
**Approach**: Test-Driven Development (TDD) with test-first methodology

## Overview

This implementation enhances the export command to support remote data sources (SFTP, HTTP/HTTPS, zip files), fixes property naming inconsistencies across project commands, and adds missing configuration options. The implementation follows strict TDD principles with tests written before code.

## Requirements

### Functional Requirements
1. Export command must support remote data sources (SFTP URLs, HTTP/HTTPS URLs, zip files)
2. Export project files must use consistent `data` array property (not `dataDir`)
3. Export must support multiple data sources merged into single output
4. Export must support `ForceDownload` flag for cache bypass
5. Export must support `Verbose` flag for diagnostic output
6. Export must handle authentication via SFTP config files

### Non-Functional Requirements
1. Maintain backward compatibility with CLI arguments
2. Follow existing configuration patterns (Config → execute)
3. Reuse existing remote data handling from CommandUtils
4. Maintain 0 errors, 0 warnings policy
5. All tests must be written before implementation
6. Tests must fail initially then pass after implementation

### Success Criteria
- All unit tests pass (red → green progression)
- Integration tests verify end-to-end functionality
- Export can directly process files from examples/diff-ai-comparison.json
- Documentation updated with new capabilities
- Build succeeds with 0 errors, 0 warnings

## Design Decisions

### TDD Approach
**Decision**: Strict test-first development  
**Process**:
1. Write failing test that defines expected behavior
2. Run test to verify it fails for the right reason
3. Implement minimal code to make test pass
4. Refactor while keeping tests green
5. Document each step in this plan

### Configuration Architecture
**Decision**: Follow existing Config → execute pattern  
**Reasoning**: 
- Consistency with View and Diff commands
- Clean separation of concerns
- Easier testing and maintenance

### Property Naming Fix
**Decision**: Change `dataDir` to `data` array  
**Breaking Change**: Yes, for JSON project files only  
**Migration**: Document in RELEASE_NOTES.md

### Remote Data Handling
**Decision**: Reuse CommandUtils.resolveDataPaths  
**Benefits**:
- DRY principle adherence
- Proven code for remote data
- Consistent behavior across commands

## Implementation Progress

### Phase 1: Create Plan Document
**Status**: COMPLETED  
**Time**: 2025-09-07 10:15  
**Actions**:
- Created this plan document at `ai/plans/2025-09-07_export-remote-data-tdd.md`
- Documented requirements, design decisions, and TDD approach

### Phase 2: Write Failing Tests for Remote Data Support
**Status**: COMPLETED  
**Time**: 2025-09-07 10:20  
**Actions**:
- Created `src/PRo3D.Viewer.Tests/ExportTests.fs` with failing tests
- Added ExportTests to Main.fs test list
- Added ExportTests.fs to PRo3D.Viewer.Tests.fsproj

**Test Results (Expected Failures)**:
```
Build errors confirm missing features:
- FS0656: Record contains fields from inconsistent types (Data field doesn't exist)
- FS0039: Type doesn't define ForceDownload member
- FS0039: Type doesn't define Verbose member
- FS0001: Expected 'string array option' but got 'DataEntry array' (Data vs DataDir mismatch)
```

These failures confirm our tests are correctly identifying the missing functionality.

### Phase 3: Write Failing Tests for Property Consistency
**Status**: PENDING

### Phase 4: Write Failing Tests for Configuration Builder
**Status**: PENDING

### Phase 5: Implement ExportConfig Changes (Red→Green)
**Status**: PENDING

### Phase 6: Implement ConfigurationBuilder for Export (Red→Green)
**Status**: PENDING

### Phase 7: Refactor ExportCommand to Use Configuration (Red→Green)
**Status**: PENDING

### Phase 8: Integration Testing
**Status**: PENDING

### Phase 9: Documentation Updates
**Status**: PENDING

## Testing Strategy

### Unit Test Categories
1. **Configuration Tests**: ExportConfig structure and field validation
2. **ProjectFile Tests**: JSON parsing with new properties
3. **ConfigurationBuilder Tests**: Argument and project conversion
4. **CommandUtils Tests**: Remote data resolution for export

### Integration Test Scenarios
1. Export from SFTP URL via project file
2. Export with HTTP zip file download
3. Export with force download flag
4. Export with verbose logging
5. Export multiple OPC directories to single file

### Test Execution Plan
```bash
# Run tests after each phase
dotnet run --project src/PRo3D.Viewer.Tests

# Verify specific test failures
dotnet run --project src/PRo3D.Viewer.Tests -- --filter "ExportConfig"

# Final validation
dotnet build -c Release
```

## Key Code Changes

### Configuration.fs
```fsharp
type ExportConfig = {
    Data: DataEntry array        // Changed from DataDir: string
    Format: ExportFormat
    OutFile: string option
    Sftp: string option
    BaseDir: string option
    ForceDownload: bool option   // NEW
    Verbose: bool option         // NEW
}
```

### ProjectFile.fs
```fsharp
type ExportProject = {
    Command: string
    Data: DataEntry array option  // Changed from DataDir
    Format: string option
    Out: string option
    ForceDownload: bool option    // NEW
    Verbose: bool option          // NEW
}
```

### ExportCommand.fs
```fsharp
let execute (config: ExportConfig) : int =
    // New implementation using config pattern
    // Leverages CommandUtils.resolveDataPaths
```

## Lessons Learned
- (To be updated as implementation progresses)

## Final Summary
- (To be completed after implementation)