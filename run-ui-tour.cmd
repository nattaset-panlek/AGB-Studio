@echo off
rem One-click launcher for AGB Studio UI & UX Tour
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-ui-tour.ps1" %*
endlocal
