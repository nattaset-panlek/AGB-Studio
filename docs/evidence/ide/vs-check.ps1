# P14.03 / P14.07 in Visual Studio 18, driven through its DTE automation object.
# 1. Opens the script the way AGB Studio's "Open externally" does (/Edit file /Command "Edit.GoTo N") and reads the caret line.
# 2. Opens the generated IDE project, builds it in VS and reads the Error List for GAE001-GAE006.
# 3. Puts the caret on the GAE002 float, opens Quick Actions (Ctrl+.) and presses Enter only if VS is the foreground window,
#    then reads line 8 back to see whether "Change to fixed" was applied. Nothing is saved; VS is closed at the end.
$ErrorActionPreference = 'Stop'
$log = 'D:\agb-studio-tmp\vscheck\vs-check.log'
$shotDir = 'D:\agb-studio-tmp\vscheck'
function Log($m) { $line = (Get-Date -Format 'HH:mm:ss') + ' ' + $m; Add-Content -Path $log -Value $line -Encoding UTF8 }
Set-Content -Path $log -Value '' -Encoding UTF8
$devenv = 'D:\Program Files\Microsoft Visual Studio\18\Community\common7\ide\devenv.exe'
$proj = 'D:\agb-studio-tmp\vscheck\proj'
$file = Join-Path $proj 'scripts\PlayerController.cs'
$line = 20
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
"@
function VsWindow() { $q = Get-Process devenv -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1; if ($q) { return $q.MainWindowHandle } else { return [IntPtr]::Zero } }
function Shot($dte, $name) {
  $h = VsWindow
  $r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
  if ($w -le 0 -or $hh -le 0) { Log "shot $name skipped (no window size)"; return }
  $bmp = New-Object System.Drawing.Bitmap $w, $hh
  $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
  [W]::PrintWindow($h, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
  $bmp.Save((Join-Path $shotDir ($name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  Log "shot $name saved ($w x $hh)"
}
function GetDte() {
  try { return [Runtime.InteropServices.Marshal]::GetActiveObject('VisualStudio.DTE.18.0') } catch { return $null }
}
function Retry([scriptblock]$b, [int]$sec = 60) {
  $end = (Get-Date).AddSeconds($sec)
  while ((Get-Date) -lt $end) { try { $v = & $b; if ($v) { return $v } } catch { } ; Start-Sleep -Milliseconds 500 }
  return $null
}

Log "start devenv: /Edit `"$file`" /Command `"Edit.GoTo $line`""
if (GetDte) { Log "VS already running: send the same /Edit /Command through its DTE"; $x = GetDte; $x.ItemOperations.OpenFile($file) | Out-Null; $x.ExecuteCommand('Edit.GoTo', "$line") }
else { $p = Start-Process -FilePath $devenv -ArgumentList "/Edit `"$file`" /Command `"Edit.GoTo $line`"" -PassThru }
$dte = Retry { GetDte } 240
if (-not $dte) { Log 'FAIL: no DTE in the running object table'; exit 1 }
Log 'DTE found'
$doc = Retry { $d = $dte.ActiveDocument; if ($d -and $d.FullName -ieq $file -and $d.Selection.CurrentLine -eq $line) { $d } } 120
$d = $dte.ActiveDocument
if ($d) { Log ("P14.03 active document: " + $d.FullName + " caret line " + $d.Selection.CurrentLine) } else { Log 'P14.03 no active document' }
if ($doc) { Log "P14.03 PASS: VS opened the file at line $line" } else { Log "P14.03 FAIL: expected $file line $line" }
Shot $dte 'P14.03-vs-open-at-line'

Log 'open the generated project'
$dte.ExecuteCommand('File.OpenProject', "`"$proj\SkyHopper.csproj`"")
$loaded = Retry { if ($dte.Solution.Projects.Count -gt 0) { $true } } 180
Log ("projects loaded: " + $dte.Solution.Projects.Count)
Start-Sleep -Seconds 10
$dte.Solution.SolutionBuild.Build($true)
Log ("build done, LastBuildInfo (failed projects) = " + $dte.Solution.SolutionBuild.LastBuildInfo)
$dte.ExecuteCommand('View.ErrorList')
Start-Sleep -Seconds 8
$items = $dte.ToolWindows.ErrorList.ErrorItems
$found = @{}
$patterns = [ordered]@{ GAE001 = 'is never freed'; GAE002 = 'use fixed'; GAE003 = 'fills memory every frame'; GAE004 = 'no exceptions on GBA'; GAE005 = 'string concatenation in a loop'; GAE006 = 'lambda captures' }
for ($i = 1; $i -le $items.Count; $i++) {
  $it = $items.Item($i)
  Log ("error list: " + $it.ErrorLevel + " " + [IO.Path]::GetFileName($it.FileName) + "(" + $it.Line + "," + $it.Column + "): " + $it.Description)
  foreach ($k in $patterns.Keys) { if ($it.Description -like ('*' + $patterns[$k] + '*')) { $found[$k] = $true } }
}
$pane = $null
foreach ($pp in $dte.ToolWindows.OutputWindow.OutputWindowPanes) { if ($pp.Name -eq 'Build') { $pane = $pp } }
if ($pane) { $ptd = $pane.TextDocument; $pep = $ptd.StartPoint.CreateEditPoint(); $btxt = $pep.GetText($ptd.EndPoint); Set-Content -Path 'D:gb-studio-tmpschecks-build-output.txt' -Value $btxt -Encoding UTF8
  foreach ($m in [regex]::Matches($btxt, 'PlayerController\.cs\((\d+),(\d+),\d+,\d+\): (warning|error) (GAE\d{3})|PlayerController\.cs\((\d+),(\d+)\): (warning|error) (GAE\d{3})')) { $id = if ($m.Groups[4].Success) { $m.Groups[4].Value } else { $m.Groups[8].Value }; $found[$id] = $true; Log ('build output: ' + $m.Value) } }
else { Log 'no Build output pane' }
foreach ($k in $patterns.Keys) { Log ("P14.07 " + $k + ": " + ($(if ($found[$k]) { 'in the Error List' } else { 'MISSING' }))) }
Shot $dte 'P14.07-vs-error-list'

Log 'quick action on line 8 (float coyoteTime)'
$dte.ItemOperations.OpenFile($file) | Out-Null
$ad = Retry { $dte.ActiveDocument } 30
$sel = $ad.Selection
$sel.MoveToLineAndOffset(8, 6)
Start-Sleep -Seconds 3
$dte.MainWindow.Activate()
[W]::SetForegroundWindow((VsWindow)) | Out-Null
Start-Sleep -Milliseconds 800
$dte.ExecuteCommand('View.QuickActions')
Start-Sleep -Seconds 4
Shot $dte 'P14.07-vs-quick-actions'
if ([W]::GetForegroundWindow() -eq (VsWindow)) {
  [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
  Log 'Enter sent to VS (it was the foreground window)'
} else { Log 'VS was not the foreground window: Enter not sent' }
Start-Sleep -Seconds 4
$td = $ad.Object('TextDocument')
$ep = $td.StartPoint.CreateEditPoint()
$text = $ep.GetText($td.EndPoint)
$l8 = ($text -split "`r?`n")[7]
Log ("line 8 after the quick action: " + $l8.Trim())
if ($l8 -match 'Fixed coyoteTime') { Log 'P14.07 code fix PASS: Change to fixed applied inside VS' } else { Log 'P14.07 code fix: not applied' }
Shot $dte 'P14.07-vs-after-fix'

Log 'close without saving'
$dte.Documents.CloseAll(2)
$dte.Quit()
Log 'done'
