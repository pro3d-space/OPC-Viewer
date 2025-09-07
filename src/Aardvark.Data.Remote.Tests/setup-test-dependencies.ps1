# PowerShell setup script for test dependencies on Windows CI/CD servers

Write-Host "Setting up test dependencies..." -ForegroundColor Green

# Check if Python is available
$pythonCmd = $null
if (Get-Command python -ErrorAction SilentlyContinue) {
    $pythonCmd = "python"
} elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
    $pythonCmd = "python3"
}

if ($null -eq $pythonCmd) {
    Write-Host "Warning: Python not found. Python SFTP tests will be skipped." -ForegroundColor Yellow
    exit 0
}

Write-Host "Python found: $pythonCmd"

# Install Python dependencies if requirements.txt exists
if (Test-Path "requirements.txt") {
    Write-Host "Installing Python dependencies..." -ForegroundColor Cyan
    & $pythonCmd -m pip install -r requirements.txt --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Python dependencies installed successfully" -ForegroundColor Green
    } else {
        Write-Host "Warning: Failed to install Python dependencies. Some tests may be skipped." -ForegroundColor Yellow
    }
} else {
    Write-Host "No requirements.txt found"
}

Write-Host "Setup complete" -ForegroundColor Green