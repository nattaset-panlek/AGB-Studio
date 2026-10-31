# G-EMOJI.01: no emoji in commit messages.
# Scans the full message of every commit in -Range (default: everything reachable from HEAD) with the code point
# ranges of Lint.IsEmoji in tools/AgbStudio.Build/Lint.cs (the file scan of `agb lint`); change both together.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-commit-emoji.ps1 [-Range <git revision range>]
# Exit code: 0 = clean (or not a git checkout), 1 = at least one message holds an emoji.
param([string]$Range = 'HEAD')

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Test-Emoji([int]$cp) {
    return ($cp -ge 0x1F000 -and $cp -le 0x1FAFF) -or ($cp -ge 0x2600 -and $cp -le 0x27BF) -or ($cp -eq 0xFE0F) -or
        ($cp -ge 0x1F1E6 -and $cp -le 0x1F1FF) -or ($cp -eq 0x200D) -or ($cp -ge 0x2B00 -and $cp -le 0x2BFF) -or
        ($cp -ge 0x2300 -and $cp -le 0x23FF)
}

git rev-parse --git-dir 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Output 'commit-emoji: not a git checkout, skipped'
    exit 0
}

# One record per commit: hash, unit separator, message, record separator.
$raw = (git log --format='%h%x1f%B%x1e' $Range) -join "`n"
if ($LASTEXITCODE -ne 0) {
    Write-Output "commit-emoji: git log $Range failed"
    exit 1
}

$commits = 0
$found = 0
foreach ($record in $raw -split [char]0x1e) {
    $parts = $record -split [char]0x1f, 2
    if ($parts.Count -lt 2) { continue }
    $hash = $parts[0].Trim()
    $message = $parts[1]
    $commits++
    $lineNo = 0
    foreach ($line in $message.Trim("`n") -split "`n") {
        $lineNo++
        for ($i = 0; $i -lt $line.Length; $i++) {
            $c = $line[$i]
            if ([char]::IsHighSurrogate($c) -and $i + 1 -lt $line.Length -and [char]::IsLowSurrogate($line[$i + 1])) {
                $cp = [char]::ConvertToUtf32($c, $line[$i + 1])
                $i++
            } else {
                $cp = [int]$c
            }
            if (Test-Emoji $cp) {
                Write-Output ('{0} line {1}: U+{2:X4} in "{3}"' -f $hash, $lineNo, $cp, $line.Trim())
                $found++
                break
            }
        }
    }
}

if ($found -gt 0) {
    Write-Output "commit-emoji: $found message lines with emoji in $commits commits (G-EMOJI.01); reword them before merging"
    exit 1
}
Write-Output "commit-emoji: no emoji in $commits commit messages"
exit 0
