# Testing Aardvark.Data.Remote

## Quick Start - How to Run Tests

### Option 1: Run All Tests (Recommended)
From the Aardvark.Data.Remote directory:
```cmd
test.cmd          # Windows - runs tests with normal output
test-verbose.cmd  # Windows - shows detailed test results
./test.sh         # Linux/Mac
```

### See What Tests Are Available
```cmd
test-list.cmd     # Shows all 50+ available tests
```

Or using dotnet directly:
```bash
dotnet test ../Aardvark.Data.Remote.Tests
```

### Option 2: Run from Repository Root
From the OPC-Viewer root directory:
```cmd
test.cmd          # Runs both PRo3D.Viewer and Aardvark.Data.Remote tests
```

### Option 3: Run Specific Test Categories
```cmd
# Run only SFTP tests
cd src/Aardvark.Data.Remote
test-sftp.cmd

# Run with filter
cd ../Aardvark.Data.Remote.Tests
dotnet run -- --filter "Python"
dotnet run -- --filter "Parser"
```

## What Happens When You Run Tests

1. **Dependency Check**: 
   - Checks for Python installation
   - Attempts to install `paramiko` package if needed (for SFTP tests)
   - This happens automatically on first run

2. **Test Execution**:
   - Parser tests (always run)
   - Provider tests (always run)
   - SFTP tests (run if Python + paramiko available, skipped otherwise)
   - Integration tests (always run)

3. **SFTP Server**:
   - Python-based SFTP server starts automatically for tests
   - Uses ports 2251-2253 (ensure these are free)
   - Server stops automatically after tests

## Test Output

### Successful Run with Python SFTP:
```
Setting up test dependencies...
Python found: python
Python dependencies installed successfully
Running tests...
✓ Parser Tests: 15 passed
✓ Provider Tests: 12 passed  
✓ SFTP Tests: 3 passed
✓ Integration Tests: 8 passed
All tests passed!
```

### Run without Python (tests still pass):
```
Setting up test dependencies...
Warning: Python not found. Python SFTP tests will be skipped.
Running tests...
✓ Parser Tests: 15 passed
✓ Provider Tests: 12 passed
✓ Integration Tests: 8 passed
All tests passed!
```

## Manual Setup (Optional)

If automatic setup fails, you can manually install dependencies:

### Windows
```powershell
# Install Python from python.org or:
winget install Python.Python.3.12

# Install paramiko
pip install paramiko
```

### Linux/Mac
```bash
# Python usually pre-installed, just need:
pip3 install paramiko
```

## Troubleshooting

### "Python not found"
- Install Python 3.x from python.org
- Or tests will still run, just skip SFTP tests

### "Failed to install paramiko"
- Check internet connection
- Try manual install: `pip install paramiko`
- Or tests will skip SFTP tests gracefully

### Port conflicts
- Ensure ports 2251-2253 are free
- Check with: `netstat -an | findstr 225`

### SFTP tests fail with "Connection refused"
- Clear cache: `rmdir /s /q %TEMP%\sftp`
- Ensure Windows Firewall allows Python

## CI/CD Integration

The tests are CI/CD ready. Add to your pipeline:

### GitHub Actions
```yaml
- name: Run Aardvark.Data.Remote Tests
  run: |
    cd src/Aardvark.Data.Remote
    ./test.sh
```

### Azure DevOps
```yaml
- script: |
    cd src/Aardvark.Data.Remote
    test.cmd
  displayName: 'Run Aardvark.Data.Remote Tests'
```

## Summary

- **Just run `test.cmd`** - Everything else is automatic
- Tests work with or without Python
- SFTP tests provide better coverage when Python is available
- All setup is handled automatically