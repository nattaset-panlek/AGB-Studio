@echo off
rem AGB Studio local CI: build everything, lint, run the C# tests (which also build and run every
rem runtime test ROM in the emulator) and the editor tests. Exit code is non-zero on any failure.
setlocal
cd /d %~dp0
dotnet build AGB-Studio.sln -c Release -v q -nologo || exit /b 1
tools\agb\bin\Release\net10.0\agb.exe lint || exit /b 1
dotnet test AGB-Studio.sln -c Release --no-build -v q --nologo || exit /b 1
echo CI passed
