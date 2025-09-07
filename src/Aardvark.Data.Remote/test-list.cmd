@echo off
echo ========================================
echo Available Aardvark.Data.Remote Tests
echo ========================================

cd ..\Aardvark.Data.Remote.Tests

REM Build if needed
if not exist bin\Release\net8.0\Aardvark.Data.Remote.Tests.dll (
    echo Building tests...
    dotnet build -c Release --nologo -v quiet
)

echo.
cd bin\Release\net8.0
dotnet Aardvark.Data.Remote.Tests.dll --list-tests

cd ..\..\..\..\Aardvark.Data.Remote

echo.
echo ========================================
echo To run specific tests, use:
echo   dotnet test --filter "TestName"
echo Or run test-verbose.cmd for detailed output
echo ========================================