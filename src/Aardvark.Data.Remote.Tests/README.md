# Aardvark.Data.Remote Test Suite

## Running Tests

```bash
dotnet test
```

## SFTP Server Testing

The test suite uses a **single Python Paramiko-based SFTP server** for integration testing. This is the only SFTP testing solution after removing all problematic implementations (SFTPGo, Rebex, Docker-based, and mock servers).

### Python Paramiko SFTP Server
- **Status**: Fully functional, all tests passing
- **Requirements**: Python 3.x and paramiko package
- **Auto-install**: Tests will attempt to install paramiko automatically if not present
- **Fallback**: Tests are skipped gracefully if Python is not available
- **No popups**: Runs completely in background without any installation dialogs
- **Real SFTP protocol**: Implements actual SSH/SFTP protocol, not mocks or simulations

### Setup for CI/CD

#### GitHub Actions / Linux CI
```yaml
- name: Setup test dependencies
  run: |
    chmod +x ./tests/Aardvark.Data.Remote.Tests/setup-test-dependencies.sh
    ./tests/Aardvark.Data.Remote.Tests/setup-test-dependencies.sh
```

#### Azure DevOps / Windows CI
```yaml
- task: PowerShell@2
  displayName: 'Setup test dependencies'
  inputs:
    filePath: 'tests/Aardvark.Data.Remote.Tests/setup-test-dependencies.ps1'
```

#### Manual Setup
```bash
# Install Python dependencies
pip install -r requirements.txt

# Or just paramiko
pip install paramiko
```

### Test Behavior

1. **With Python + Paramiko**: All SFTP tests run
2. **With Python, no Paramiko**: Tests attempt auto-install, skip if it fails
3. **No Python**: SFTP tests are skipped gracefully, other tests still run


## Test Categories

- **Parser Tests**: DataRef parsing and validation
- **Provider Tests**: Local, HTTP, and SFTP providers
- **Integration Tests**: End-to-end resolver functionality
- **SFTP Tests**: Real server integration testing

## Troubleshooting

### Python SFTP Tests Not Running
1. Check Python installation: `python --version`
2. Check paramiko: `python -c "import paramiko"`
3. Install manually: `pip install paramiko`

### SFTP Cache Issues
Clear the cache directory:
- Windows: `rmdir /s /q %TEMP%\sftp`
- Linux/Mac: `rm -rf /tmp/sftp`

### Port Conflicts
Tests use ports 2251-2253. Ensure these are available or modify the test configuration.