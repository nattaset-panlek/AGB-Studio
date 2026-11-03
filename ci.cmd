@echo off
rem AGB Studio local CI: build everything, lint, check the commit messages, run the C# tests (which also build and run
rem every runtime test ROM in the emulator), the scripting compiler tests and the editor tests. Exit code is non-zero
rem on any failure. Every run, passed or failed, keeps its artifacts in build\ci\runs (tools\ci-artifacts.ps1, P1.06).
setlocal
cd /d %~dp0
set STEP=build
dotnet build AGB-Studio.sln -c Release -v q -nologo || goto failed
set STEP=lint
tools\agb\bin\Release\net10.0\agb.exe lint || goto failed
rem G-EMOJI.01: no emoji in any commit message (same code point ranges as the lint).
set STEP=commit-emoji
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-commit-emoji.ps1 || goto failed
rem G-SCOPE.01: every commit after the baseline names a T-id or "infra:"; the earlier commits without one are listed
rem in the checklist Scope log. AGB_COMMIT_BASE moves the baseline.
if not defined AGB_COMMIT_BASE set AGB_COMMIT_BASE=8d7fe2325029a9b968858715846f8adb17da92d6
set STEP=commit-refs
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-commit-refs.ps1 -Strict || goto failed
if exist build\ci\results rd /s /q build\ci\results
rem One test project at a time: they build ROMs into shared folders under build\.
for %%t in (AgbStudio.Tests AgbStudio.Scripting.Tests AgbStudio.Editor.Tests) do (
    set STEP=%%t
    dotnet test tests\%%t\%%t.csproj -c Release --no-build -v q --nologo --blame-hang --blame-hang-timeout 5m --blame-hang-dump-type none --logger "console;verbosity=normal" --logger "trx;LogFileName=%%t.trx" --results-directory build\ci\results || goto failed
)
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ci-artifacts.ps1 -Result passed
echo CI passed
exit /b 0
:failed
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ci-artifacts.ps1 -Result failed -Step %STEP%
echo CI failed at step %STEP%
exit /b 1
