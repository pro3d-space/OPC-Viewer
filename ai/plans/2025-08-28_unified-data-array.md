# Unified Data Array in Project JSON Format

## Overview
Unify the separate `dataDirs` and `objFiles` arrays in the project JSON format for the "view" command into a single `data` array with an optional `type` property that can be "obj" or "opc". When type is not specified, it should be inferred from the file path.

## Requirements

### Functional Requirements
1. Replace `dataDirs` and `objFiles` arrays with a single `data` array
2. Each data entry should support:
   - `path`: The file or directory path (required)
   - `type`: Optional type specification ("obj" or "opc")
3. Type inference when not specified:
   - Files ending with `.obj` → type "obj"
   - Directories or other files → type "opc"
4. Maintain backward compatibility with existing functionality
5. Support all existing path resolution (URLs, absolute, relative paths)

### Non-Functional Requirements
- 0 errors, 0 warnings policy
- Maintain existing code style and patterns
- Update all affected example files
- Ensure smooth migration path

## Design Decisions

1. **Data Structure**: Use discriminated union for data entry types
2. **Type Inference**: Implement at parsing time, not runtime
3. **Backward Compatibility**: Keep existing command structure, convert at parse time
4. **Path Resolution**: Reuse existing path resolution logic

## Implementation Plan

1. **Phase 1: Analyze Current Structure** [PENDING]
   - Examine ProjectFile.fs for current JSON parsing
   - Review ViewCommandConfig structure
   - Identify all affected code paths

2. **Phase 2: Update Data Model** [PENDING]
   - Define new DataEntry type with path and optional type
   - Update ViewCommandConfig JSON structure
   - Implement type inference logic

3. **Phase 3: Update Parsing Logic** [PENDING]
   - Modify JSON deserialization
   - Implement unified data array parsing
   - Add type inference from file extensions

4. **Phase 4: Update Command Conversion** [PENDING]
   - Convert unified data to separate dataDirs/objFiles
   - Maintain existing command execution

5. **Phase 5: Update Examples** [PENDING]
   - Convert existing example files
   - Create new examples showing type inference

6. **Phase 6: Testing & Validation** [PENDING]
   - Build with 0 errors/warnings
   - Test with various data configurations
   - Verify type inference works correctly

## Implementation Progress

### Phase 1: Analyze Current Structure [COMPLETED]

Examined `ProjectFile.fs`:
- Current structure uses separate `DataDirs` and `ObjFiles` arrays in `ViewProject` type
- `DataDirs` and `ObjFiles` both use `TransformablePath` type with optional transforms
- Manual JSON parsing for security and control
- Path resolution happens in `ProjectCommand.fs`

Key findings:
- Lines 18-25: Current ViewProject type with separate arrays
- Lines 140-175: Manual parsing of dataDirs and objFiles
- Lines 434-461: Serialization logic for separate arrays

### Phase 2: Update Data Model [COMPLETED]

Added new types in `ProjectFile.fs` (lines 16-26):
- `DataType` discriminated union with `Opc` and `Obj` cases
- `DataEntry` record with Path, optional Type, and optional Transform
- Updated `ViewProject` to include `Data` array alongside legacy fields

### Phase 3: Update Parsing Logic [COMPLETED]

Modified `parseViewProject` in `ProjectFile.fs` (lines 159-184):
- Added parsing for unified "data" array
- Implemented type parsing from JSON ("opc" or "obj")
- Preserved backward compatibility with dataDirs/objFiles

### Phase 4: Update Command Conversion [COMPLETED]

Updated `ConfigurationBuilder.fs` (lines 46-95):
- Modified `fromViewProject` to handle unified data array
- Implemented type inference using `ProjectFile.inferDataType`
- Separates unified data into OPC directories and OBJ files
- Falls back to legacy format when no data array present

### Phase 5: Update Examples [COMPLETED]

Created new example files:
- `examples/view-unified-data.json` - Basic unified array with type inference
- `examples/view-unified-transforms.json` - Unified array with transforms

### Phase 6: Testing & Validation [COMPLETED]

Build results:
- 0 errors, 0 warnings
- Successfully compiles with .NET 8.0

Testing results:
- Dry-run correctly displays unified data array format
- Backward compatibility maintained for legacy format
- Type inference works correctly (defaults to OPC for non-.obj files)
- Transforms properly parsed and serialized

## Lessons Learned

1. **Type Inference Pattern**: Simple file extension check (.obj) provides intuitive behavior
2. **Backward Compatibility**: Maintaining both formats ensures smooth migration
3. **F# Access Control**: Private functions need to be made public for cross-module access
4. **Record Initialization**: All fields must be initialized when adding new fields to existing records

## Final Summary

Successfully implemented unified data array feature for view command in project JSON format:
- ✅ Single "data" array replaces separate dataDirs/objFiles
- ✅ Optional "type" property ("opc" or "obj")
- ✅ Automatic type inference from file extension
- ✅ Full backward compatibility maintained
- ✅ Transform support preserved
- ✅ 0 errors, 0 warnings
- ✅ Updated documentation and examples

Total files modified: 4
- `src/PRo3D.Viewer/Project/ProjectFile.fs`
- `src/PRo3D.Viewer/ConfigurationBuilder.fs`
- `src/PRo3D.Viewer/Project/DryRunSerializer.fs`
- `README.md`

Total files created: 2
- `examples/view-unified-data.json`
- `examples/view-unified-transforms.json`