#!/bin/bash
echo "========================================"
echo "Running Aardvark.Data.Remote Tests"
echo "========================================"

# Setup Python dependencies if available
echo "Setting up test dependencies..."
cd ../Aardvark.Data.Remote.Tests
if [ -f "setup-test-dependencies.sh" ]; then
    chmod +x setup-test-dependencies.sh
    ./setup-test-dependencies.sh
fi
cd ../Aardvark.Data.Remote

# Run the tests
echo ""
echo "Running tests..."
dotnet test ../Aardvark.Data.Remote.Tests -c Release

if [ $? -ne 0 ]; then
    echo ""
    echo "Tests failed!"
    exit 1
fi

echo ""
echo "All tests passed!"