#!/bin/bash

dotnet tool restore
dotnet paket restore
#dotnet adaptify --local --verbose --force .\src\PRo3D.WebViewer\PRo3D.OpcViewer.fsproj
dotnet build -c Release

#dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained -o publish/PRo3D.OpcViewer src/PRo3D.OpcViewer
