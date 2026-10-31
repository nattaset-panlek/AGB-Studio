# G-SCOPE.01 / P1.11: every change names the Figma window it implements ("T-P04", "T-D23" ...) or says
# "infra: <reason>" (checklist sections 1 and 2.6).
# Lists the commits in <Base>..HEAD whose message has neither. Merge commits are skipped: the changes they bring
# are in the commits they merge, which are listed themselves. History is never rewritten; commits made before the
# rule (before 2026-10-31) are reported once and stay as they are (Scope log, checklist section 8).
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-commit-refs.ps1 [-Base <rev>] [-Strict]
#   -Base    first commit NOT to check; default $env:AGB_COMMIT_BASE, else "main"
#   -Strict  exit 1 when a commit lacks a reference (default: report only, exit 0)
param([string]$Base = '', [switch]$Strict)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

git rev-parse --git-dir 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Output 'commit-refs: not a git checkout, skipped'
    exit 0
}
if ($Base -eq '') { $Base = if ($env:AGB_COMMIT_BASE) { $env:AGB_COMMIT_BASE } else { 'main' } }
git rev-parse --verify --quiet "$Base^0" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Output "commit-refs: base '$Base' does not exist; pass -Base <rev> or set AGB_COMMIT_BASE"
    if ($Strict) { exit 1 }
    exit 0
}

$raw = (git log --no-merges --format='%h%x1f%s%x1f%B%x1e' "$Base..HEAD") -join "`n"
$figmaRef = '\bT-[A-Z][0-9]{2}\b'
$infraRef = '(?i)\binfra:\s*\S'

$commits = 0
$missing = 0
foreach ($record in $raw -split [char]0x1e) {
    $parts = $record -split [char]0x1f, 3
    if ($parts.Count -lt 3) { continue }
    $commits++
    $message = $parts[2]
    if ($message -match $figmaRef -or $message -match $infraRef) { continue }
    $missing++
    Write-Output ('{0} {1}' -f $parts[0].Trim(), $parts[1].Trim())
}

if ($commits -eq 0) {
    Write-Output "commit-refs: no commits in $Base..HEAD"
    exit 0
}
if ($missing -gt 0) {
    Write-Output "commit-refs: $missing of $commits commits in $Base..HEAD name neither a T-id nor 'infra:' (G-SCOPE.01)"
    if ($Strict) { exit 1 }
    exit 0
}
Write-Output "commit-refs: all $commits commits in $Base..HEAD name a T-id or 'infra:'"
exit 0
