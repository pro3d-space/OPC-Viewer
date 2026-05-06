@echo off
REM Smoke test: launches the SimpleGui pre-loaded with the Deimos OPC.
REM Usage: from the repo root run `src\PRo3D.Viewer.SimpleGui\run-deimos.cmd`.
REM (Renamed: Dimorphos_DRACO1 references stale .tif paths that the installed
REM  Aardvark.GeoSpatial.Opc 5.11.2 cannot follow — switch back once we update.)
SETLOCAL
SET TESTDATA=C:\pro3ddata\HERA\Workshop2\OPC\Dimorphos_DRACO1
pushd "%~dp0..\.."
dotnet run --project src\PRo3D.Viewer.SimpleGui\PRo3D.Viewer.SimpleGui.fsproj -c Debug -- "%TESTDATA%"
popd
