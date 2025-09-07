@echo off
SETLOCAL

set EXECUTABLE_PATH=%~dp0src\PRo3D.Viewer\bin\Release\net8.0\PRo3D.Viewer.exe

if not exist "%EXECUTABLE_PATH%" (
    call "%~dp0build.cmd"
)

"%EXECUTABLE_PATH%" %*