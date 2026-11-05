# Puts AGB Studio's own compiler in toolchain\ (P24.06): Arm GNU Toolchain, the version pinned below, checked against
# Arm's published SHA-256, with only what a GBA build needs (C and C++ for ARM7TDMI: the default ARM libraries and
# thumb/nofp). The build looks there first (Toolchain.Detect), so devkitPro is no longer needed to build a game.
#
#   powershell -ExecutionPolicy Bypass -File tools\fetch-toolchain.ps1                  download from developer.arm.com
#   powershell -ExecutionPolicy Bypass -File tools\fetch-toolchain.ps1 -Zip <file.zip>  use a zip downloaded before
#
# The toolchain is GPL software from Arm: its sources are at
# https://developer.arm.com/downloads/-/arm-gnu-toolchain-downloads (same release, "Source code").
param(
    [string]$Zip = "",
    [string]$Out = ""
)
$ErrorActionPreference = 'Stop'
$Version = '15.2.rel1'
$Name = "arm-gnu-toolchain-$Version-mingw-w64-x86_64-arm-none-eabi"
$Url = "https://developer.arm.com/-/media/Files/downloads/gnu/$Version/binrel/$Name.zip"
$Sha256 = '7936cac895611023ffb22a64b8e426098c7104cb689778c1894572ca840b9ece'

$repo = Split-Path -Parent $PSScriptRoot
if ($Out -eq "") { $Out = Join-Path $repo 'toolchain' }
$work = Join-Path ([System.IO.Path]::GetTempPath()) "agb-toolchain-$Version"
New-Item -ItemType Directory -Force $work | Out-Null

if ($Zip -eq "") {
    $Zip = Join-Path $work "$Name.zip"
    if (-not (Test-Path $Zip)) {
        Write-Host "Downloading $Url"
        Invoke-WebRequest -Uri $Url -OutFile $Zip -UseBasicParsing
    }
}
$hash = (Get-FileHash -Algorithm SHA256 $Zip).Hash.ToLowerInvariant()
if ($hash -ne $Sha256) { throw "The zip's SHA-256 is $hash, not $Sha256 as Arm published for $Version. Download it again." }

$unpacked = Join-Path $work 'unpacked'
if (Test-Path $unpacked) { Remove-Item -Recurse -Force $unpacked }
Write-Host "Unpacking"
Expand-Archive -Path $Zip -DestinationPath $unpacked
$src = $unpacked
if (-not (Test-Path (Join-Path $src 'bin\arm-none-eabi-g++.exe'))) { $src = (Get-ChildItem $unpacked -Directory | Select-Object -First 1).FullName }

if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory -Force $Out | Out-Null
function Keep([string]$rel) {
    $from = Join-Path $src $rel
    if (-not (Test-Path $from)) { return }
    $to = Join-Path $Out $rel
    New-Item -ItemType Directory -Force (Split-Path -Parent $to) | Out-Null
    Copy-Item -Recurse -Force $from $to
}

$tools = 'g++','gcc','c++','cpp','as','ld','ld.bfd','ar','nm','objcopy','objdump','ranlib','readelf','size','strip','addr2line','c++filt'
foreach ($t in $tools) { Keep "bin\arm-none-eabi-$t.exe" }
Get-ChildItem (Join-Path $src 'bin') -Filter *.dll -ErrorAction SilentlyContinue | ForEach-Object { Keep "bin\$($_.Name)" }

$gccVer = (Get-ChildItem (Join-Path $src 'lib\gcc\arm-none-eabi') -Directory | Select-Object -First 1).Name
foreach ($f in 'cc1.exe','cc1plus.exe','collect2.exe','lto-wrapper.exe','lto1.exe','liblto_plugin.dll') { Keep "libexec\gcc\arm-none-eabi\$gccVer\$f" }
Keep 'lib\bfd-plugins'

$gcclib = "lib\gcc\arm-none-eabi\$gccVer"
Get-ChildItem (Join-Path $src $gcclib) -File | Where-Object { $_.Name -notin 'libcaf_single.a','libgcov.a' } | ForEach-Object { Keep "$gcclib\$($_.Name)" }
foreach ($d in 'include','include-fixed') { Keep "$gcclib\$d" }
Get-ChildItem (Join-Path $src "$gcclib\thumb\nofp") -File | ForEach-Object { Keep "$gcclib\thumb\nofp\$($_.Name)" }

Keep 'arm-none-eabi\bin'
Keep 'arm-none-eabi\include'
$libc = 'arm-none-eabi\lib'
$drop = '^(libgfortran.*|librdimon.*|librdpmon.*|libgloss-linux\.a|linux.*|redboot.*|rdimon.*|rdpmon.*|aprofile.*|iq80310\.specs|pid\.specs|.*\.py|.*\.json)$'
Get-ChildItem (Join-Path $src $libc) -File | Where-Object { $_.Name -notmatch $drop } | ForEach-Object { Keep "$libc\$($_.Name)" }
Keep "$libc\ldscripts"
Get-ChildItem (Join-Path $src "$libc\thumb\nofp") -File | Where-Object { $_.Name -notmatch $drop } | ForEach-Object { Keep "$libc\thumb\nofp\$($_.Name)" }

foreach ($f in 'license.txt','manifest.txt') { Keep $f }
Get-ChildItem $src -File | Where-Object { $_.Name -match 'licen|copying|readme' } | ForEach-Object { Keep $_.Name }
Set-Content -Path (Join-Path $Out 'AGB-STUDIO-TOOLCHAIN.txt') -Encoding utf8 -Value @(
    "Arm GNU Toolchain $Version (arm-none-eabi), from $Url",
    "SHA-256 of the zip: $Sha256",
    "Only the parts a GBA build needs were kept (tools\fetch-toolchain.ps1). The full release and its source code are at",
    "https://developer.arm.com/downloads/-/arm-gnu-toolchain-downloads")

$size = (Get-ChildItem -Recurse $Out | Measure-Object -Property Length -Sum).Sum
Write-Host ("toolchain ready in {0}: {1:N0} MB" -f $Out, ($size / 1MB))
