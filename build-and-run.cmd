@echo off
rem One-click build of AGB Studio (Release) and launch it for manual testing.
rem   build-and-run.cmd          build the editor and start it
rem   build-and-run.cmd package  also write the portable zip to build\package
setlocal
cd /d "%~dp0"
if not exist "D:\agb-studio-tmp" mkdir "D:\agb-studio-tmp"
set "TEMP=D:\agb-studio-tmp"
set "TMP=D:\agb-studio-tmp"
set "MSBUILDDISABLENODEREUSE=1"

echo [1/2] Building AGB Studio (Release)...
dotnet build editor\AgbStudio.Editor\AgbStudio.Editor.csproj -c Release -nologo -v q
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Read the errors above.
  pause
  exit /b 1
)

if /i "%~1"=="package" (
  echo [extra] Writing the portable zip...
  dotnet build tools\agb\agb.csproj -c Release -nologo -v q
  if errorlevel 1 ( echo PACKAGE BUILD FAILED & pause & exit /b 1 )
  tools\agb\bin\Release\net10.0\agb.exe package
)

echo [2/2] Starting AGB Studio...
set "EXE=editor\AgbStudio.Editor\bin\Release\net10.0\AGBStudio.exe"
if not exist "%EXE%" for /r "editor\AgbStudio.Editor\bin\Release" %%f in (AGBStudio.exe) do if exist "%%f" set "EXE=%%f"
start "" "%EXE%"
echo Done.
endlocal
