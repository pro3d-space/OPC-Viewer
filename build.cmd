@echo off
SETLOCAL

dotnet tool restore
dotnet paket restore
REM dotnet adaptify --local --verbose --force .\src\PRo3D.WebViewer\PRo3D.Viewer.fsproj
dotnet build -c Release src/PRo3D.Viewer.sln

REM dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true  -o publish/PRo3D.Viewer src/PRo3D.Viewer