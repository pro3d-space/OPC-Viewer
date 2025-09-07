@echo off
echo ========================================
echo Running Aardvark.Data.Remote Tests
echo ========================================

REM Setup Python dependencies if available
echo Setting up test dependencies...
cd ..\Aardvark.Data.Remote.Tests
if exist setup-test-dependencies.ps1 (
    powershell -ExecutionPolicy Bypass -File setup-test-dependencies.ps1
)
cd ..\Aardvark.Data.Remote

REM Build the tests first
echo.
echo Building tests...
dotnet build ..\Aardvark.Data.Remote.Tests -c Release --nologo -v quiet

REM Run the tests directly with Expecto for better output
echo.
echo Running tests...
echo ----------------------------------------
cd ..\Aardvark.Data.Remote.Tests\bin\Release\net8.0
dotnet Aardvark.Data.Remote.Tests.dll --summary

set TEST_RESULT=%ERRORLEVEL%
cd ..\..\..\..\Aardvark.Data.Remote

if %TEST_RESULT% NEQ 0 (
    echo.
    echo ========================================
    echo TESTS FAILED!
    echo ========================================
    exit /b %TEST_RESULT%
)

echo.
echo ========================================
echo ALL TESTS PASSED!
echo ========================================