@echo off
SETLOCAL

dotnet tool restore
dotnet paket restore
REM dotnet adaptify --local --verbose --force .\src\PRo3D.WebViewer\PRo3D.OpcViewer.fsproj
dotnet build -c Release

REM dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true  -o publish/PRo3D.OpcViewer src/PRo3D.OpcViewer