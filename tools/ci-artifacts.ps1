# P1.06: keeps what a CI run produced, pass or fail, in build\ci\runs\<time>-<commit>\ (the local CI has no server
# to upload to, so this folder is the artifact store). Holds the test results (.trx), the runtime test ROMs with their
# maps and screenshots, the SHOT report, and summary.txt (commit, result, the step that failed). The newest 10 runs
# are kept.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\ci-artifacts.ps1 -Result passed|failed [-Step <name>]
param([Parameter(Mandatory)][ValidateSet('passed', 'failed')][string]$Result, [string]$Step = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$commit = (git -C $root rev-parse --short HEAD 2>$null)
if (-not $commit) { $commit = 'nogit' }
$runs = Join-Path $root 'build\ci\runs'
$run = Join-Path $runs ('{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $commit)
New-Item -ItemType Directory -Force -Path $run | Out-Null

$results = Join-Path $root 'build\ci\results'
if (Test-Path $results) {
    New-Item -ItemType Directory -Force -Path (Join-Path $run 'tests') | Out-Null
    Get-ChildItem $results -Filter *.trx | Copy-Item -Destination (Join-Path $run 'tests')
}
$roms = Join-Path $root 'build\runtime-tests'
if (Test-Path $roms) {
    New-Item -ItemType Directory -Force -Path (Join-Path $run 'roms') | Out-Null
    Get-ChildItem $roms -File | Where-Object { $_.Extension -in '.gba', '.map', '.png' } | Copy-Item -Destination (Join-Path $run 'roms')
}
foreach ($f in 'shot-report.json', 'shot-report.md') {
    $p = Join-Path $root "docs\evidence\$f"
    if (Test-Path $p) { Copy-Item $p $run }
}

$lines = @("commit: $(git -C $root rev-parse HEAD 2>$null)", "result: $Result", "time: $(Get-Date -Format s)")
if ($Step) { $lines += "failed step: $Step" }
$lines | Set-Content -Encoding ascii (Join-Path $run 'summary.txt')

Get-ChildItem $runs -Directory | Sort-Object Name -Descending | Select-Object -Skip 10 | Remove-Item -Recurse -Force
Write-Output "ci-artifacts: $Result, kept in $run"
