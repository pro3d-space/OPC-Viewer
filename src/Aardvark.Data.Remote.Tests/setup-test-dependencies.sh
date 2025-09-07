#!/bin/bash
# Setup script for test dependencies on CI/CD servers

echo "Setting up test dependencies..."

# Check if Python is available
if command -v python3 &> /dev/null; then
    PYTHON_CMD=python3
elif command -v python &> /dev/null; then
    PYTHON_CMD=python
else
    echo "Warning: Python not found. Python SFTP tests will be skipped."
    exit 0
fi

echo "Python found: $PYTHON_CMD"

# Install Python dependencies if requirements.txt exists
if [ -f "requirements.txt" ]; then
    echo "Installing Python dependencies..."
    $PYTHON_CMD -m pip install -r requirements.txt --quiet
    if [ $? -eq 0 ]; then
        echo "Python dependencies installed successfully"
    else
        echo "Warning: Failed to install Python dependencies. Some tests may be skipped."
    fi
else
    echo "No requirements.txt found"
fi

echo "Setup complete"