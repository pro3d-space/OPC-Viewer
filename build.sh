#!/bin/bash

dotnet tool restore
dotnet paket restore
#dotnet adaptify --local --verbose --force .\src\PRo3D.WebViewer\PRo3D.Viewer.fsproj
dotnet build -c Release src/PRo3D.Viewer.sln

# Publish commands for creating standalone executables
#dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/win-x64 src/PRo3D.Viewer
#dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64 src/PRo3D.Viewer
#dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o publish/osx-x64 src/PRo3D.Viewer
