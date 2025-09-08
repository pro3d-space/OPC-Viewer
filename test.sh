#!/bin/bash
echo "========================================"
echo "Running Full Test Suite"
echo "========================================"

# Run PRo3D.Viewer tests
echo ""
echo "Running PRo3D.Viewer tests..."
echo "----------------------------------------"
dotnet run --project src/PRo3D.Viewer.Tests
if [ $? -ne 0 ]; then
    echo "PRo3D.Viewer tests failed!"
    exit 1
fi

echo ""
echo "========================================"
echo "All tests passed successfully!"
echo "========================================"