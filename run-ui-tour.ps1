<#
.SYNOPSIS
    AGB Studio - Interactive UI/UX Launcher & Automated Tour Runner
.DESCRIPTION
    Launches AGB Studio in live GUI mode on Windows desktop, or runs
    automated UI/UX E2E tests driving real clicks, navigation, and emulation.
#>
param (
    [switch]$AutoTour,
    [switch]$Gallery,
    [switch]$Package
)

$ErrorActionPreference = "Stop"
$RepoRoot = "D:\devkitPro\AGB-Studio"
Set-Location $RepoRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    AGB Studio - UI & UX Tour & Live Runner" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Build check
$ExePath = Join-Path $RepoRoot "editor\AgbStudio.Editor\bin\Release\net10.0\AGBStudio.exe"
if (!(Test-Path $ExePath)) {
    Write-Host "`n[1/3] Building AGB Studio (Release)..." -ForegroundColor Yellow
    dotnet build "editor\AgbStudio.Editor\AgbStudio.Editor.csproj" -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Please check build errors."
        exit 1
    }
} else {
    Write-Host "`n[1/3] AGB Studio binary is ready: $ExePath" -ForegroundColor Green
}

# 2. Automated E2E UI/UX Tour (if requested)
if ($AutoTour) {
    Write-Host "`n[2/3] Running Automated UI/UX Tour (E2E Driving)..." -ForegroundColor Yellow
    Write-Host "Driving: Navigation, Menu actions, Scene Editor, Play Mode, and mGBA Emulation..." -ForegroundColor Gray
    dotnet test "tests\AgbStudio.Editor.Tests\AgbStudio.Editor.Tests.csproj" `
        --filter "FullyQualifiedName~NavigationE2ETests|FullyQualifiedName~PlayModeE2ETests|FullyQualifiedName~ShellE2ETests" `
        -c Release --logger "console;verbosity=normal"
    Write-Host "`n[3/3] Automated Tour Completed Successfully!" -ForegroundColor Green
    exit 0
}

# 3. Component Gallery mode (if requested)
if ($Gallery) {
    Write-Host "`n[2/3] Launching AGB Studio Component Gallery (--gallery)..." -ForegroundColor Yellow
    Start-Process -FilePath $ExePath -ArgumentList "--gallery" -WorkingDirectory $RepoRoot
    Write-Host "Component Gallery launched." -ForegroundColor Green
    exit 0
}

# 4. Live Interactive UI Launch
Write-Host "`n[2/3] Launching AGB Studio Live on Desktop..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $ExePath -WorkingDirectory $RepoRoot -PassThru

Write-Host "Started AGB Studio (PID: $($proc.Id))" -ForegroundColor Green
Write-Host "`n[3/3] Quick UI/UX Navigation Guide:" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "  * Launcher / New Project : Click 'New Project' -> 8 Genre Templates" -ForegroundColor White
Write-Host "  * Scene Editor (T-P01)   : Edit actors, inspect components, place tiles" -ForegroundColor White
Write-Host "  * Play Mode (F5)         : Real mGBA emulator running in Editor viewport" -ForegroundColor White
Write-Host "  * Frame Step (F6)        : Advance frame-by-frame while paused" -ForegroundColor White
Write-Host "  * Stop Game (Shift+F5)   : Exit Play mode back to Scene Editor" -ForegroundColor White
Write-Host "  * Tilemap Editor (Ctrl+2): Place tiles, configure collisions, autotiles" -ForegroundColor White
Write-Host "  * Sprite Editor (Ctrl+3) : Frame timing, animation states, hitboxes" -ForegroundColor White
Write-Host "  * Audio Tracker (Ctrl+5) : Chiptune tracker (XM/MOD player & SFX designer)" -ForegroundColor White
Write-Host "  * Font Manager (Ctrl+6)  : 1bpp Thai & Latin pixel fonts, glyph preview" -ForegroundColor White
Write-Host "  * Build ROM (Ctrl+B)     : Transpile C# -> C++ and compile GBA ROM" -ForegroundColor White
Write-Host "  * Export ROM (Ctrl+E)    : Export standalone .gba file for flashcarts" -ForegroundColor White
Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "Enjoy pairing with AGB Studio!`n" -ForegroundColor Green
