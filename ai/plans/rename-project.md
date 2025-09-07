# Implementation Plan: Rename Project from PRo3D.OpcViewer to PRo3D.Viewer

## Overview
Rename the entire project from "PRo3D.OpcViewer" to "PRo3D.Viewer", updating all references throughout the codebase, build scripts, and documentation.

## Requirements
- Rename solution and project files
- Update all namespace declarations in source files
- Update all imports/references to the namespace
- Update build scripts and launchers
- Update documentation
- **Strict adherence to 0 errors, 0 warnings policy**
- Maintain all existing functionality
- Ensure clean build after renaming

## Impact Analysis

### Files to Rename
1. `PRo3D.OpcViewer.sln` → `PRo3D.Viewer.sln`
2. `src/PRo3D.OpcViewer/PRo3D.OpcViewer.fsproj` → `src/PRo3D.Viewer/PRo3D.Viewer.fsproj`
3. Directory: `src/PRo3D.OpcViewer/` → `src/PRo3D.Viewer/`

### Namespace Updates Required
- All `.fs` files with `namespace PRo3D.OpcViewer` → `namespace PRo3D.Viewer`
- All `.fs` files with `open PRo3D.OpcViewer` → `open PRo3D.Viewer`

### Build Script Updates
- `build.cmd` - Update project paths
- `build.sh` - Update project paths
- `opcviewer.cmd` - Update executable path
- `opcviewer` (bash script) - Update executable path

### Documentation Updates
- `README.md` - Update project name references
- Any references in docs/plans/*.md files

## Implementation Plan

### Phase 1: Inventory Current State
**Status**: COMPLETED

- List all files containing "PRo3D.OpcViewer"
- Document current project structure
- Identify all namespace declarations and references

### Phase 2: Rename Project Infrastructure
**Status**: COMPLETED

1. Rename directory `src/PRo3D.OpcViewer/` → `src/PRo3D.Viewer/`
2. Rename project file inside the directory
3. Update solution file references
4. Test build to ensure project structure is valid

### Phase 3: Update Source Code Namespaces
**Status**: COMPLETED

- Update namespace declarations in all .fs files
- Update open statements referencing the namespace
- Update any fully qualified references

### Phase 4: Update Build Infrastructure
**Status**: COMPLETED

- Update build.cmd
- Update build.sh
- Rename and update opcviewer.cmd → pro3dviewer.cmd
- Rename and update opcviewer → pro3dviewer (bash script)
- Update executable references in scripts
- Update any other build-related files

### Phase 5: Update Documentation
**Status**: COMPLETED

- Update README.md
- Update any references in plan documents
- Update CLAUDE.md if present

### Phase 6: Final Validation
**Status**: COMPLETED

- Clean build with 0 errors, 0 warnings
- Test all commands still work
- Verify executable names are correct

## Risk Assessment
- **Medium Risk**: Many files to update, potential for missed references
- **Mitigation**: Systematic search and replace, thorough testing

## Success Criteria
- ✅ All files renamed successfully
- ✅ Build completes with 0 errors, 0 warnings
- ✅ All tests pass
- ✅ Documentation updated
- ✅ Executable runs correctly with new name
- ✅ Command renamed from `opcviewer` to `pro3dviewer`

---

## Implementation Progress

### Phase 1: Inventory Current State
**Status**: COMPLETED

#### Files Found with PRo3D.OpcViewer References:
- **40 total files** contain references to PRo3D.OpcViewer
- **23 F# source files** with namespace declarations
- **Solution file**: PRo3D.OpcViewer.sln
- **Project file**: src/PRo3D.OpcViewer/PRo3D.OpcViewer.fsproj
- **Build scripts**: build.cmd, build.sh, opcviewer.cmd, opcviewer
- **Documentation**: README.md, CLAUDE.md, various plan documents

#### Namespace Patterns Found:
- `PRo3D.OpcViewer` (root namespace)
- `PRo3D.OpcViewer.View`
- `PRo3D.OpcViewer.Project`
- `PRo3D.OpcViewer.Shared`
- `PRo3D.OpcViewer.Diff`

#### Key Observations:
- 23 files have namespace declarations
- Multiple files have open statements referencing the namespace
- Build scripts reference the project path
- Documentation contains multiple references

### Phase 2: Rename Project Infrastructure
**Status**: COMPLETED

#### Actions Taken:
1. ✅ Renamed directory: `src/PRo3D.OpcViewer/` → `src/PRo3D.Viewer/`
2. ✅ Renamed project file: `PRo3D.OpcViewer.fsproj` → `PRo3D.Viewer.fsproj`
3. ✅ Renamed solution file: `PRo3D.OpcViewer.sln` → `PRo3D.Viewer.sln`
4. ✅ Updated solution file to reference new project path

### Phase 3: Update Source Code Namespaces
**Status**: COMPLETED

#### Actions Taken:
1. ✅ Updated all namespace declarations from `PRo3D.OpcViewer` to `PRo3D.Viewer`
2. ✅ Updated all open statements from `open PRo3D.OpcViewer` to `open PRo3D.Viewer`
3. ✅ Updated PROGRAM_NAME in Program.fs to "PRo3D.Viewer"

### Phase 4: Update Build Infrastructure
**Status**: COMPLETED

#### Actions Taken:
1. ✅ Updated build.cmd with new project paths
2. ✅ Updated build.sh with new project paths
3. ✅ Renamed opcviewer.cmd → pro3dviewer.cmd with updated paths
4. ✅ Renamed opcviewer → pro3dviewer (bash) with updated paths
5. ✅ Updated solution file to reference new script names
6. ✅ Updated launchSettings.json profile name

### Phase 5: Update Documentation
**Status**: COMPLETED

#### Actions Taken:
1. ✅ Updated README.md - all references to `opcviewer` → `pro3dviewer`
2. ✅ Updated README.md - `PRo3D.OpcViewer` → `PRo3D.Viewer` in usage text
3. ✅ Updated CLAUDE.md - project name and command references
4. ✅ Updated .gitignore - project path reference

### Phase 6: Final Validation
**Status**: COMPLETED

#### Test Results:
1. ✅ Build successful: 0 errors, 0 warnings
2. ✅ Executable renamed: `PRo3D.Viewer.exe` created
3. ✅ Command works: `pro3dviewer --help` displays correct program name
4. ✅ JSON shortcut works: Direct JSON file argument still functional
5. ✅ All commands tested successfully

---

## Final Summary

### What Was Achieved
Successfully renamed the entire project from **PRo3D.OpcViewer** to **PRo3D.Viewer**, including:
- Solution and project files
- All namespace declarations and references (40+ files)
- Command-line tool from `opcviewer` to `pro3dviewer`
- All documentation and build scripts

### Statistics
- **Files Modified**: 40+ source files, documentation, and scripts
- **Namespaces Updated**: 23 F# source files
- **Scripts Renamed**: opcviewer → pro3dviewer (both .cmd and bash)
- **Build Result**: 0 errors, 0 warnings maintained

### Key Changes by Category

#### Project Structure
- `PRo3D.OpcViewer.sln` → `PRo3D.Viewer.sln`
- `src/PRo3D.OpcViewer/` → `src/PRo3D.Viewer/`
- `PRo3D.OpcViewer.fsproj` → `PRo3D.Viewer.fsproj`

#### Namespaces
- `PRo3D.OpcViewer` → `PRo3D.Viewer`
- `PRo3D.OpcViewer.View` → `PRo3D.Viewer.View`
- `PRo3D.OpcViewer.Project` → `PRo3D.Viewer.Project`
- `PRo3D.OpcViewer.Shared` → `PRo3D.Viewer.Shared`
- `PRo3D.OpcViewer.Diff` → `PRo3D.Viewer.Diff`

#### Command-Line Interface
- `opcviewer` → `pro3dviewer` (executable name)
- `opcviewer.cmd` → `pro3dviewer.cmd`
- `opcviewer` (bash) → `pro3dviewer` (bash)

#### Documentation
- README.md updated with new command names
- CLAUDE.md updated with new project references
- All example commands updated

### Lessons Learned
1. **Systematic approach essential**: Following phases ensured nothing was missed
2. **Namespace updates cascade**: Both declarations and open statements need updating
3. **Build scripts need attention**: Multiple places reference project paths
4. **Documentation is extensive**: Many references in README and other docs
5. **Testing confirms success**: 0 errors/warnings policy maintained throughout

### Final Status
✅ **PROJECT SUCCESSFULLY RENAMED TO PRo3D.Viewer**
✅ **ALL FUNCTIONALITY PRESERVED**
✅ **0 ERRORS, 0 WARNINGS MAINTAINED**