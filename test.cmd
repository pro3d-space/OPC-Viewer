@echo off
echo ========================================
echo Running Full Test Suite
echo ========================================

REM Run PRo3D.Viewer tests
echo.
echo Running PRo3D.Viewer tests...
echo ----------------------------------------
dotnet run --project tests/PRo3D.Viewer.Tests
if %ERRORLEVEL% NEQ 0 (
    echo PRo3D.Viewer tests failed!
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo All tests passed successfully!
echo ========================================