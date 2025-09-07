@echo off
echo ========================================
echo Running Aardvark.Data.Remote Tests (Verbose)
echo ========================================

REM Setup Python dependencies if available
echo Setting up test dependencies...
cd ..\Aardvark.Data.Remote.Tests
if exist setup-test-dependencies.ps1 (
    powershell -ExecutionPolicy Bypass -File setup-test-dependencies.ps1
)
cd ..\Aardvark.Data.Remote

REM Run the tests with detailed output
echo.
echo Running tests with detailed output...
echo ----------------------------------------

REM Build and run with Expecto output
cd ..\Aardvark.Data.Remote.Tests\bin\Release\net8.0
if not exist Aardvark.Data.Remote.Tests.dll (
    echo Building tests first...
    cd ..\..\..\Aardvark.Data.Remote.Tests
    dotnet build -c Release
    cd bin\Release\net8.0
)

echo.
echo Executing tests...
dotnet Aardvark.Data.Remote.Tests.dll --sequenced

cd ..\..\..\..\Aardvark.Data.Remote

echo.
echo ========================================
echo Test run complete!
echo ========================================