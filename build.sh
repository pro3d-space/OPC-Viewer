#!/bin/bash

dotnet tool restore
dotnet paket restore
#dotnet adaptify --local --verbose --force .\src\PRo3D.WebViewer\PRo3D.Viewer.fsproj
dotnet build -c Release src/PRo3D.Viewer.sln

#dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained -o publish/PRo3D.Viewer src/PRo3D.Viewer
