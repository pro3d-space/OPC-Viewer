@echo off
echo ========================================
echo Running Full Test Suite
echo ========================================

REM Run PRo3D.Viewer tests
echo.
echo [1/2] Running PRo3D.Viewer tests...
echo ----------------------------------------
dotnet run --project tests/PRo3D.Viewer.Tests
if %ERRORLEVEL% NEQ 0 (
    echo PRo3D.Viewer tests failed!
    exit /b %ERRORLEVEL%
)

REM Run Aardvark.Data.Remote tests
echo.
echo [2/2] Running Aardvark.Data.Remote tests...
echo ----------------------------------------
cd src\Aardvark.Data.Remote
call test.cmd
if %ERRORLEVEL% NEQ 0 (
    cd ..\..
    echo Aardvark.Data.Remote tests failed!
    exit /b %ERRORLEVEL%
)
cd ..\..

echo.
echo ========================================
echo All test suites passed successfully!
echo ========================================