# Debugging Workflow for OPC Viewer Issues

This document captures the systematic debugging approach used to resolve SFTP and multi-layer dataset issues.

## General Debugging Methodology

### 1. Problem Isolation
- **Start Simple**: Use `--dryrun` flag to verify argument parsing without execution
- **Test Components**: Separate network, authentication, download, extraction, and processing issues
- **Use Timeouts**: `timeout 15s command` prevents GUI applications from blocking analysis

### 2. SFTP Debugging Sequence

#### Step 1: Verify Connectivity
```bash
# Use psftp for scripted SFTP access
psftp -P 2200 -l username -pwfile passwordfile -batch hostname

# Test commands in batch file:
ls Mission/
ls Mission/0300/
quit
```

#### Step 2: Compare File Sizes
```fsharp
// Check local vs server file sizes to detect incomplete downloads
let localSize = FileInfo(localPath).Length
// Use psftp ls to get server size and compare
```

#### Step 3: Test Zip Integrity
```fsharp
// Test with different extraction methods
use zip = ZipFile.OpenRead(zipPath)  // .NET method
ZipFile.ExtractToDirectory(zipPath, destPath)  // Windows built-in
// PowerShell Expand-Archive as fallback
```

### 3. Multi-Dataset Debugging

#### Identify Layer Structure
```bash
# Run with verbose flag to see what layers are found
dotnet run --project src/PRo3D.Viewer --configuration Release -- diff path1 path2 --verbose
```

#### Analyze Error Messages
- **"exactly 2 datasets"**: Multi-layer issue, not path issue
- **"End of Central Directory"**: Corrupted/incomplete zip file
- **"Permission denied"**: Authentication or path access issue

## Common Issues and Solutions

### Issue: SFTP Authentication Failure
**Symptoms**: "Permission denied (password,publickey)"
**Solution**: 
- Verify FileZilla XML format and base64 password encoding
- Use psftp with `-pwfile` instead of environment variables
- Check server host key acceptance with `-batch` flag

### Issue: Incomplete Zip Downloads  
**Symptoms**: "End of Central Directory record could not be found"
**Diagnosis**: Compare local vs server file sizes
**Solution**: 
- Check network stability
- Increase download timeouts in SFTP library
- Verify sufficient disk space

### Issue: Multi-Layer Dataset Conflicts
**Symptoms**: "Please specify exactly 2 datasets" with more than 2 layers found
**Root Cause**: AI datasets often contain multiple OPC layers
**Solution**: Modify diff logic to handle directory-based layer selection

## Debugging Tools and Commands

### F# Interactive (fsi)
```bash
dotnet fsi ai/scripts/debug-script.fsx
```
Benefits:
- Rapid prototyping and testing
- Direct .NET library access
- Easy file system operations

### Command Line SFTP Tools
```bash
# psftp (PuTTY SFTP) - best for Windows scripting
psftp -P port -l user -pwfile pwfile -b batchfile hostname

# Standard sftp - requires interactive password
sftp -P port user@hostname
```

### PowerShell for File Operations
```powershell
# Expand archives
Expand-Archive -Path "file.zip" -DestinationPath "dest"

# File size comparison
(Get-Item "file.zip").Length
```

## Code Modification Workflow

### 1. Identify Problem Location
```bash
# Use grep to find error message source
rg "exact error message" --type fs
```

### 2. Understand Current Logic
```fsharp
// Read the problematic function
// Trace data flow: input → processing → output
// Identify assumptions that may be invalid
```

### 3. Design Fix
- **Minimal Change**: Preserve existing behavior for normal cases
- **Verbose Logging**: Add detailed output to understand what's happening
- **Error Handling**: Provide clear error messages for edge cases

### 4. Test Fix
```bash
# Build
dotnet build -c Release

# Test with known problematic case
dotnet run --project src/PRo3D.Viewer --configuration Release -- test-args

# Verify normal cases still work
```

## Testing Strategy

### Progressive Testing
1. **Dry Run**: `--dryrun` to verify argument parsing
2. **Network Test**: Simple SFTP connection and file listing
3. **Download Test**: Single small file download and extraction
4. **Processing Test**: Single dataset diff with verbose logging
5. **Batch Test**: Multiple comparisons with error tracking

### Test Data Selection
- **Known Working**: Use examples from existing documentation
- **Edge Cases**: Multi-layer datasets, large files, different naming patterns
- **Error Cases**: Non-existent paths, authentication failures

## Documentation Patterns

### Error Investigation
```markdown
**Issue**: [Clear description of problem]
**Symptoms**: [What the user sees]
**Investigation**: [Steps taken to diagnose]
**Root Cause**: [Technical explanation]
**Solution**: [Code changes made]
**Testing**: [How fix was verified]
```

### Code Changes
```markdown
**File**: path/to/file
**Lines**: X-Y
**Before**: [original code]
**After**: [modified code]
**Reason**: [why change was needed]
```

## Performance Considerations

### Large Dataset Handling
- Monitor memory usage during diff computation
- Consider streaming for large zip files
- Implement progress reporting for long operations

### Network Optimization
- Cache downloaded files to avoid re-downloading
- Implement parallel downloads for batch operations
- Handle network timeouts gracefully

## Future Debugging Enhancements

### Logging Improvements
- Structured logging with levels (DEBUG, INFO, WARN, ERROR)
- Separate log files for network, processing, and UI operations
- Correlation IDs for tracing operations across components

### Automated Testing
- Integration tests with mock SFTP server
- Regression tests for multi-layer dataset handling
- Performance benchmarks for large dataset processing

This workflow proved effective for systematically resolving complex issues involving multiple systems (SFTP, file handling, OPC processing, and GUI components).