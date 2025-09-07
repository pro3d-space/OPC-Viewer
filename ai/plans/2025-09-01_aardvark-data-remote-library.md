# Aardvark.Data.Remote Library Implementation Plan

**Date**: 2025-01-09  
**Feature**: Extract data reference functionality into reusable Aardvark.Data.Remote library  
**Branch**: feature/aardvark-data-remote

## Overview

Extract the robust data reference system from PRo3D.Viewer (handling local directories, zip files, HTTP/HTTPS URLs, and SFTP servers) into a standalone, reusable F# library that follows Aardvark ecosystem conventions. This will enable other projects to benefit from this functionality while maintaining full backward compatibility with PRo3D.Viewer.

## Requirements

### Functional Requirements
1. **Data Reference Parsing**: Parse strings into structured DataRef types (local directories, zip files, HTTP/HTTPS URLs, SFTP URLs)
2. **Resolution Engine**: Resolve DataRef instances to local file system paths with automatic downloading and caching
3. **HTTP/HTTPS Support**: Download files from web servers with retry logic and progress reporting
4. **SFTP Support**: Download files from SFTP servers using FileZilla configuration files
5. **Zip Handling**: Automatic extraction of zip files with smart caching
6. **Caching**: Intelligent caching system to avoid repeated downloads
7. **Progress Reporting**: Callback mechanisms for download progress
8. **Error Handling**: Comprehensive error types and handling

### Non-Functional Requirements
1. **Aardvark Integration**: Follow Aardvark ecosystem naming conventions and patterns
2. **F# Idiomatic**: Use F# best practices (discriminated unions, pattern matching, Result types)
3. **Backward Compatibility**: PRo3D.Viewer must continue working without changes
4. **Testability**: Comprehensive test coverage with mocking support
5. **Documentation**: Clear API documentation and usage examples
6. **Cross-Platform**: Support Windows, Linux, and macOS
7. **Performance**: Efficient caching and download strategies

### Success Criteria
- All existing PRo3D.Viewer functionality preserved
- 0 errors, 0 warnings build policy maintained
- New library can be used independently by other projects
- Comprehensive test coverage (>80%)
- Clear documentation with examples
- Clean separation of concerns

## Design Decisions

### Library Structure
```
Aardvark.Data.Remote/
├── src/Aardvark.Data.Remote/
│   ├── Types.fs              // Core DU types
│   ├── Parser.fs             // String parsing logic
│   ├── Provider.fs           // Provider interface
│   ├── Resolver.fs           // Main resolution engine
│   ├── Cache.fs              // Caching strategies
│   ├── Zip.fs                // Zip extraction
│   ├── FileZillaConfig.fs    // SFTP config parsing
│   ├── Progress.fs           // Progress reporting
│   ├── Builder.fs            // Fluent API
│   └── Providers/
│       ├── LocalProvider.fs  // Local file handling
│       ├── HttpProvider.fs   // HTTP/HTTPS downloads
│       └── SftpProvider.fs   // SFTP downloads
└── tests/Aardvark.Data.Remote.Tests/
    ├── DataRefTests.fs
    ├── ProviderTests.fs
    └── IntegrationTests.fs
```

### API Design Philosophy
1. **Provider Pattern**: Extensible architecture allowing new protocols
2. **Result Types**: Use Result&lt;'T, 'Error&gt; for error handling instead of exceptions
3. **Async Support**: Full async/await support for I/O operations
4. **Fluent Builder**: Optional builder pattern for configuration
5. **Configuration Objects**: Centralized configuration with sensible defaults

### Key Types
```fsharp
type DataRef =
    | LocalDir of path: string * exists: bool
    | LocalZip of path: string
    | RemoteZip of uri: Uri * protocol: Protocol
    | Invalid of reason: string

type Protocol = Http | Https | Sftp | Ftp

type RemoteDataError =
    | ParseError of string
    | NetworkError of Uri * exn
    | FileNotFound of string
    | SftpConfigMissing of Uri
    | CacheError of string
```

## Implementation Plan

### Phase 1: Setup and Structure ✅ COMPLETED
- [x] Create branch `feature/aardvark-data-remote` 
- [x] Create plan document `ai/plans/2025-01-09_aardvark-data-remote-library.md`
- [x] Create library directory structure
- [x] Create F# project files (.fsproj)
- [x] Update paket.dependencies and paket.references
- [x] **BUILD & VERIFY**: 0 errors, 0 warnings ✅

### Phase 2: Extract Core Types ✅ COMPLETED
- [x] Create `Types.fs` with DataRef DU and error types
- [x] Create `Parser.fs` with string parsing logic
- [x] **BUILD & VERIFY**: 0 errors, 0 warnings ✅

### Phase 3: Create Provider Infrastructure 🔄 IN PROGRESS
- [ ] Create `Provider.fs` with IDataProvider interface
- [ ] Implement LocalProvider, HttpProvider, SftpProvider
- [ ] **BUILD & VERIFY**: 0 errors, 0 warnings

### Phase 4: Implement Core Functionality 📝 PENDING
- [ ] Create `Resolver.fs` with main resolution logic
- [ ] Create `Cache.fs`, `Zip.fs`, `FileZillaConfig.fs`
- [ ] **BUILD & VERIFY**: 0 errors, 0 warnings

### Phase 5: Add Enhanced Features 📝 PENDING
- [ ] Create `Progress.fs` and `Builder.fs`
- [ ] **BUILD & VERIFY**: 0 errors, 0 warnings

### Phase 6: Create Test Suite 📝 PENDING
- [ ] Create comprehensive tests
- [ ] **RUN TESTS**: All passing

### Phase 7: Update PRo3D.Viewer 📝 PENDING
- [ ] Update PRo3D.Viewer to use new library
- [ ] **BUILD & VERIFY**: 0 errors, 0 warnings

### Phase 8: Documentation 📝 PENDING
- [ ] Create README.md and examples
- [ ] Update main README.md

## Implementation Progress

### Phase 1 Progress: Setup and Structure

#### ✅ COMPLETED: Create Branch (2025-01-09 15:30)
- **Action**: Created branch `feature/aardvark-data-remote`
- **Command**: `git checkout -b feature/aardvark-data-remote`
- **Result**: Successfully switched to new branch
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Create Plan Document (2025-01-09 15:35)
- **Action**: Created comprehensive plan document
- **File**: `ai/plans/2025-01-09_aardvark-data-remote-library.md`
- **Details**: Following workflow requirements for detailed documentation
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Create Library Directory Structure (2025-01-09 18:29)
- **Action**: Created directory structure for new library
- **Command**: `mkdir -p Aardvark.Data.Remote/src/Aardvark.Data.Remote/Providers && mkdir -p Aardvark.Data.Remote/tests/Aardvark.Data.Remote.Tests`
- **Directories Created**: 
  - `Aardvark.Data.Remote/src/Aardvark.Data.Remote/`
  - `Aardvark.Data.Remote/src/Aardvark.Data.Remote/Providers/`
  - `Aardvark.Data.Remote/tests/Aardvark.Data.Remote.Tests/`
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Create F# Project Files (2025-01-09 18:30)
- **Action**: Created .fsproj files for library and tests
- **Files Created**:
  - `Aardvark.Data.Remote/src/Aardvark.Data.Remote/Aardvark.Data.Remote.fsproj` - Library project with NuGet metadata
  - `Aardvark.Data.Remote/tests/Aardvark.Data.Remote.Tests/Aardvark.Data.Remote.Tests.fsproj` - Test project with Expecto
- **Key Features**: Package metadata, proper Paket.Restore.targets paths, project references
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Update Paket Configuration (2025-01-09 18:31)
- **Action**: Created paket.references files for new projects
- **Files Created**:
  - `Aardvark.Data.Remote/src/Aardvark.Data.Remote/paket.references` - FSharp.Core, Aardvark.Base, SSH.NET
  - `Aardvark.Data.Remote/tests/Aardvark.Data.Remote.Tests/paket.references` - Above + Expecto, Expecto.FsCheck
- **Solution Integration**: Added both projects to `src/PRo3D.Viewer.sln`
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Create Stub Files and Build Phase 1 (2025-01-09 18:32)
- **Action**: Created placeholder F# files and verified build
- **Files Created**: 13 stub files (Types.fs, Parser.fs, Provider.fs, Cache.fs, Zip.fs, FileZillaConfig.fs, Progress.fs, 3 Provider files, Resolver.fs, Builder.fs, 3 test files, Program.fs)
- **Build Results**: 
  - Library build: ✅ 0 errors, 0 warnings
  - Test build: ✅ 0 errors, 0 warnings  
  - Full solution build: ✅ 0 errors, 0 warnings
- **Status**: ✅ COMPLETED

### Phase 2 Progress: Extract Core Types

#### ✅ COMPLETED: Create Types.fs (2025-01-09 18:35)
- **Action**: Extracted and enhanced core types from PRo3D.Viewer/Data.fs
- **File**: `Aardvark.Data.Remote/src/Aardvark.Data.Remote/Types.fs`
- **Types Created**:
  - `DataRef` DU with cases: LocalDir, RelativeDir, LocalZip, RelativeZip, HttpZip, SftpZip, Invalid
  - `ResolveResult` DU for resolution outcomes: Resolved, DownloadError, SftpConfigMissing, InvalidPath  
  - `SftpConfig` record for SFTP connection details
  - `ResolverConfig` record with default configuration
- **Key Improvements**: Better type safety, clearer error handling, comprehensive configuration
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Create Parser.fs (2025-01-09 18:36)
- **Action**: Extracted and enhanced parsing logic from PRo3D.Viewer/Data.fs  
- **File**: `Aardvark.Data.Remote/src/Aardvark.Data.Remote/Parser.fs`
- **Functions Created**:
  - `Parser.parse` - Main parsing function (equivalent to old `getDataRefFromString`)
  - `Parser.tryParse` - Result-based parsing 
  - `Parser.isValid` - Validation helper
  - `Parser.describe` - Human-readable descriptions
- **Key Improvements**: Result types, better error messages, F#-idiomatic API
- **Issue Resolved**: Fixed naming conflict by renaming module from `DataRef` to `Parser`
- **Status**: ✅ COMPLETED

#### ✅ COMPLETED: Build and Verify Phase 2 (2025-01-09 18:37)
- **Action**: Built library and tests to verify Phase 2 implementation
- **Build Results**: 
  - Library build: ✅ 0 errors, 0 warnings
  - Test build: ✅ 0 errors, 0 warnings
- **Status**: ✅ COMPLETED

## Testing Strategy

### Unit Tests
- DataRef parsing tests (valid/invalid inputs)
- Provider tests with mocked dependencies  
- Cache behavior tests
- Error handling tests

### Integration Tests
- End-to-end download scenarios
- SFTP integration using Python Paramiko server (real SFTP protocol)
- Zip extraction workflows
- Cache persistence tests

### Compatibility Tests
- PRo3D.Viewer regression tests
- Cross-platform path handling
- Different zip formats

## Lessons Learned

### SFTP Testing Infrastructure (2025-09-02)
- **Problematic implementations removed**: SFTPGo (installation popups), Rebex (GUI dialogs), Docker-based tests (complexity), mock servers (don't test real protocol)
- **Single solution retained**: Python Paramiko SFTP server - real protocol, background execution, auto-install dependencies
- **Key insight**: Simple, working solutions beat complex infrastructure that causes user friction
- **Cleanup performed**: Removed SftpServerTests.fs, RealSftpServerTests.fs, FxSsh dependency, obsolete artifacts

## Final Summary

*This section will be completed at the end of implementation*

---

**Status**: 🔄 IN PROGRESS  
**Current Phase**: Phase 1 - Setup and Structure  
**Last Updated**: 2025-01-09 15:35