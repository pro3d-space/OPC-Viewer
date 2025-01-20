@echo off
dotnet tool restore
dotnet paket restore
REM dotnet adaptify --local --verbose --force .\src\PRo3D.WebViewer\PRo3D.WebViewer.fsproj
dotnet build