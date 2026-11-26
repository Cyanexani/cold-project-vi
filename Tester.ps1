# Test de config / config test: launches the game, presses A on the title
# screen, measures a few minutes in the world, takes screenshots, closes the
# game and writes a report (rapport.md + a zip to attach) in rapports\.
#
#   Tester.cmd                      (double-click: 5 minutes in the world)
#   Tester.ps1 -Minutes 10 -Console series-x
#
# Nothing is sent anywhere: the report stays on your PC until you post it.
param(
    [int]$Minutes = 5,
    [ValidateSet('series-s', 'series-x')][string]$Console = 'series-s',
    [int]$ShotEvery = 30,
    [switch]$NoPress,
    # No questions at the end (scripts).
    [switch]$Auto
)
$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot

function Say([string]$fr, [string]$en, [string]$color = 'White') {
    Write-Host "  $fr" -ForegroundColor $color
    Write-Host "  $en" -ForegroundColor DarkGray
}
function Fail([string]$fr, [string]$en) {
    Say $fr $en 'Red'; if (-not $Auto) { Read-Host '  Entree pour fermer / Enter to close' }; exit 1
}

Write-Host ''
Write-Host '  Cold Project VI - test de config / config test' -ForegroundColor Yellow
Write-Host '  ----------------------------------------------' -ForegroundColor Yellow

# --- The runtime: the release package's victory.exe, or a build from source.
$exe = @('victory.exe', 'target\diag\victory.exe', 'target\release\victory.exe') |
    ForEach-Object { Join-Path $PSScriptRoot $_ } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) {
    Fail 'victory.exe introuvable : prendre le paquet de la derniere release, ou compiler (cargo build --profile diag -p victory --features diagnostics).' `
         'victory.exe not found: get the latest release package, or build it (cargo build --profile diag -p victory --features diagnostics).'
}

# --- The game: game_dir in victory.toml, or VICTORY_GAME_DIR.
$dir = $env:VICTORY_GAME_DIR
if (-not $dir -and (Test-Path 'victory.toml')) {
    $line = Select-String -Path 'victory.toml' -Pattern "^\s*game_dir\s*=\s*['""](.+)['""]" | Select-Object -First 1
    if ($line) { $dir = $line.Matches[0].Groups[1].Value }
}
$game = if ($dir) { Join-Path $dir 'game_scarlett_master.exe' } else { $null }
if (-not $game -or -not (Test-Path $game)) {
    Fail 'Jeu introuvable : mettre game_dir dans victory.toml (modele : victory.example.toml).' `
         'Game not found: set game_dir in victory.toml (see victory.example.toml).'
}

# --- The PC.
function Get-Specs {
    $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name.Trim()
    $ramGb = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB)
    $page = (Get-CimInstance Win32_PageFileUsage -EA 0 | Measure-Object AllocatedBaseSize -Sum).Sum
    $pageTxt = if ($page) { "{0} GB" -f [math]::Round($page / 1024) } else { 'auto / unknown' }
    $os = Get-CimInstance Win32_OperatingSystem
    # Video memory from the driver's registry entry (Win32 caps it at 4 GB).
    $vram = @{}
    Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}' -EA 0 |
        ForEach-Object {
            $k = Get-ItemProperty $_.PSPath -EA 0
            if ($k.DriverDesc -and $k.'HardwareInformation.qwMemorySize') {
                $vram[$k.DriverDesc] = [math]::Round([double]$k.'HardwareInformation.qwMemorySize' / 1GB)
            }
        }
    $gpus = Get-CimInstance Win32_VideoController | Where-Object { $_.Name -notmatch 'Basic|Virtual|Remote|Parsec|Meta' } |
        ForEach-Object {
            $v = if ($vram.ContainsKey($_.Name)) { ", $($vram[$_.Name]) GB" } else { '' }
            "$($_.Name)$v (driver $($_.DriverVersion))"
        }
    [ordered]@{
        'CPU' = $cpu
        'GPU' = ($gpus -join ' + ')
        'RAM' = "$ramGb GB"
        'Page file' = $pageTxt
        'Windows' = "$($os.Caption) $($os.Version)"
    }
}
$specs = Get-Specs
Write-Host ''
foreach ($k in $specs.Keys) { Write-Host ("  {0,-10} {1}" -f $k, $specs[$k]) }
if ([int]($specs['RAM'] -replace '\D') -lt 16) {
    Say 'Moins de 16 Go de RAM : le jeu va sans doute se fermer.' 'Less than 16 GB of RAM: the game will likely close.' 'Yellow'
}

# --- Where it goes.
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$out = Join-Path $PSScriptRoot "rapports\test-$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out 'journal.txt'
$commit = ''
if (Test-Path (Join-Path $PSScriptRoot '.git')) { $commit = (git -C $PSScriptRoot log -1 --format='%h %cs' 2>$null) }
$version = (& $exe --version 2>$null | Select-Object -First 1)

Write-Host ''
Say "Mesure : $Minutes min dans le monde, $Console. Le jeu se lance ; ne touchez a rien, A est appuye tout seul." `
    "Measuring $Minutes min in the world, $Console. The game starts; leave it alone, A is pressed for you." 'Green'
Say 'Le premier lancement apres une mise a jour peut etre noir quelques minutes (shaders) : lancer Prechauffer.cmd avant.' `
    'The first run after an update may be black for minutes (shaders): run Prechauffer.cmd first.'

# --- Run.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class TesterWin {
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
}
"@
[TesterWin]::SetProcessDPIAware() | Out-Null

function Shot([System.Diagnostics.Process]$p, [string]$file) {
    try {
        $r = New-Object TesterWin+R
        [TesterWin]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
        $w = $r.Ri - $r.L; $h = $r.B - $r.T
        if ($w -lt 100 -or $h -lt 100) { return }
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $dc = $g.GetHdc()
        [TesterWin]::PrintWindow($p.MainWindowHandle, $dc, 2) | Out-Null
        $g.ReleaseHdc($dc)
        $small = New-Object System.Drawing.Bitmap $bmp, ([int]($w / 2)), ([int]($h / 2))
        $small.Save($file, [System.Drawing.Imaging.ImageFormat]::Jpeg)
        $small.Dispose(); $g.Dispose(); $bmp.Dispose()
    } catch {}
}
function PressA([System.Diagnostics.Process]$p) {
    [TesterWin]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
    [TesterWin]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
    [TesterWin]::keybd_event(0x0D, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [TesterWin]::keybd_event(0x0D, 0, 2, [UIntPtr]::Zero)
}

Get-ChildItem env: | Where-Object Name -like 'VICTORY_*' | Where-Object Name -ne 'VICTORY_GAME_DIR' |
    ForEach-Object { Remove-Item "env:$($_.Name)" }
$crashLogs = Get-ChildItem "$env:LOCALAPPDATA\victory\data\*\crashes.log" -EA 0
$crashBefore = @{}; foreach ($c in $crashLogs) { $crashBefore[$c.FullName] = $c.Length }

$p = Start-Process -FilePath $exe -ArgumentList 'run', ('"{0}"' -f $game), '--execute', '--console', $Console `
    -WorkingDirectory $PSScriptRoot -RedirectStandardError $log -RedirectStandardOutput "$log.out" -PassThru -WindowStyle Normal
$t0 = Get-Date; $world = $null; $menuSince = $null; $presses = 0; $shots = 0; $lastShot = $null
$died = $false; $timeout = $false; $titleAtEnd = ''
while ($true) {
    Start-Sleep 5
    $p.Refresh()
    if ($p.HasExited) { $died = $true; break }
    $t = $p.MainWindowTitle
    $draws = 0; if ($t -match '(\d+) draws/frame') { $draws = [int]$Matches[1] }
    $since = [int]((Get-Date) - $t0).TotalSeconds
    if (-not $world) {
        if ($draws -gt 1000) {
            $world = Get-Date
            Say "Dans le monde apres $since s." "In the world after $since s." 'Green'
        } elseif (-not $NoPress -and $draws -gt 0 -and $draws -lt 30 -and $t -match ' 30(\.0)? fps') {
            # The title screen runs at 30 fps with a handful of draws.
            if (-not $menuSince) { $menuSince = Get-Date }
            if (((Get-Date) - $menuSince).TotalSeconds -ge 40) {
                PressA $p; $presses++; $menuSince = Get-Date
                Say "A appuye ($since s)." "Pressed A ($since s)."
            }
        } else { $menuSince = $null }
    } else {
        if (-not $lastShot -or ((Get-Date) - $lastShot).TotalSeconds -ge $ShotEvery) {
            $shots++; Shot $p (Join-Path $out ("capture-{0:D2}.jpg" -f $shots)); $lastShot = Get-Date
        }
        if (((Get-Date) - $world).TotalMinutes -ge $Minutes) { $titleAtEnd = $t; break }
    }
    if (((Get-Date) - $t0).TotalMinutes -gt ($Minutes + 25)) { $timeout = $true; $titleAtEnd = $t; break }
}
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; Start-Sleep 3 }
$wall = [int]((Get-Date) - $t0).TotalSeconds

# --- What the log says.
$esc = [char]27
$lines = Get-Content $log -EA 0 | ForEach-Object { $_ -replace "$esc\[[0-9;]*m", '' }
$frames = foreach ($l in $lines) {
    if ($l -match ' ([\d.]+) fps, ([\d.]+) ms/frame \(([\d.]+) preparing: copies ([\d.]+) ms [\d.]+ MB, textures [\d.]+ ms [\d.]+ MB; ([\d.]+) on the GPU; ([\d.]+) copying back [\d.]+ MB\), (\d+) draws/frame') {
        if ([int]$Matches[7] -gt 1000) {
            [pscustomobject]@{ Fps = [double]$Matches[1]; Ms = [double]$Matches[2]; Prep = [double]$Matches[3]
                Copies = [double]$Matches[4]; Gpu = [double]$Matches[5]; Back = [double]$Matches[6]; Draws = [int]$Matches[7] }
        }
    }
}
# The first minute in the world still makes pipelines: leave it out when there is more.
$steady = if ($frames.Count -gt 16) { $frames | Select-Object -Skip 12 } else { $frames }
function Median($xs) { $s = @($xs | Sort-Object); if (-not $s.Count) { return $null }; $s[[int][math]::Floor($s.Count / 2)] }
$fpsMed = Median ($steady | ForEach-Object Fps)
$fpsMin = ($steady | Measure-Object Fps -Minimum).Minimum
$fpsMax = ($steady | Measure-Object Fps -Maximum).Maximum
# Medians: one stall of seconds would swamp an average.
$avg = { param($n) [math]::Round((Median ($steady | ForEach-Object $n)), 1) }
$stalls = @($steady | Where-Object { $_.Fps -lt 1 }).Count
$mem = ($lines | Select-String 'copystat: memory:' | Select-Object -Last 1)
$memTxt = if ($mem -and $mem.Line -match '([\d]+) MB private \(([\d]+) MB of it in memory') { "$($Matches[1]) MB private, $($Matches[2]) MB in RAM" } else { '?' }
$busy = ($lines | Select-String "about (\d+)% busy" | Select-Object -Last 1)
$busyTxt = if ($busy) { "$($busy.Matches[0].Groups[1].Value) %" } else { '?' }
$errors = $lines | Select-String -Pattern 'panic|crash report|device removed|DEVICE_REMOVED|out of memory' | Select-Object -First 5 |
    ForEach-Object { $_.Line.Substring(0, [math]::Min(300, $_.Line.Length)) }
$newCrash = foreach ($c in (Get-ChildItem "$env:LOCALAPPDATA\victory\data\*\crashes.log" -EA 0)) {
    $before = if ($crashBefore.ContainsKey($c.FullName)) { $crashBefore[$c.FullName] } else { 0 }
    if ($c.Length -gt $before) {
        $fs = [IO.File]::Open($c.FullName, 'Open', 'Read', 'ReadWrite'); $fs.Seek($before, 'Begin') | Out-Null
        $txt = (New-Object IO.StreamReader $fs).ReadToEnd(); $fs.Close(); $txt.Substring(0, [math]::Min(3000, $txt.Length))
    }
}

$result = if ($died -and -not $world) { 'CLOSED BEFORE THE WORLD / ferme avant le monde' }
    elseif ($died) { 'CLOSED IN THE WORLD / ferme dans le monde' }
    elseif ($timeout) { 'NEVER REACHED THE WORLD / jamais arrive au monde' }
    else { 'OK' }

# --- The report (your user name is taken out of everything written).
$me = [regex]::Escape($env:USERNAME)
function Clean([string]$s) { if ($env:USERNAME) { $s -replace $me, '<user>' } else { $s } }
$r = New-Object System.Text.StringBuilder
function Add([string]$s = '') { [void]$r.AppendLine($s) }
Add "# Config test $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
Add ''
Add "**Result: $result**"
Add ''
Add '| | |'
Add '|---|---|'
foreach ($k in $specs.Keys) { Add "| $k | $($specs[$k]) |" }
Add "| Profile | $Console |"
Add "| Build | $(if ($commit) { $commit } else { $version }) |"
Add ''
if ($steady.Count) {
    Add "## In the world ($($steady.Count) samples of ~5 s)"
    Add ''
    Add "- **fps: $fpsMed median** (min $fpsMin, max $fpsMax)"
    Add "- stalls (samples under 1 fps): $stalls"
    Add "- ms a frame (medians): $(& $avg 'Ms') = preparing $(& $avg 'Prep') (copies $(& $avg 'Copies')) + GPU $(& $avg 'Gpu') + copying back $(& $avg 'Back') (they overlap)"
    Add "- draws a frame: $(& $avg 'Draws')"
    Add "- GPU busy: $busyTxt"
    Add "- memory: $memTxt"
} else {
    Add '## No measure in the world'
}
Add ''
Add "Time to the world: $(if ($world) { [int]($world - $t0).TotalSeconds } else { '-' }) s, A pressed $presses times, run $wall s, $shots screenshots."
if ($titleAtEnd) { Add "Window at the end: ``$(Clean $titleAtEnd)``" }
if ($errors) { Add ''; Add '## Errors in the log'; Add '```'; $errors | ForEach-Object { Add (Clean $_) }; Add '```' }
if ($newCrash) { Add ''; Add '## Crash report'; Add '```'; Add (Clean ($newCrash -join "`n")); Add '```' }
Add ''
Add '_Made by Tester.ps1. Attach the zip next to this file (log + screenshots) to your issue._'
$report = Join-Path $out 'rapport.md'
[IO.File]::WriteAllText($report, $r.ToString(), (New-Object Text.UTF8Encoding $false))

# The log without your user name, then everything in one zip.
$cleanLog = Join-Path $out 'journal-propre.txt'
[IO.File]::WriteAllLines($cleanLog, [string[]]($lines | ForEach-Object { Clean $_ }), (New-Object Text.UTF8Encoding $false))
Remove-Item $log, "$log.out" -EA 0
$zip = Join-Path $PSScriptRoot "rapports\test-$stamp.zip"
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force

Write-Host ''
Write-Host '  ----------------------------------------------' -ForegroundColor Yellow
Write-Host "  $result" -ForegroundColor $(if ($result -eq 'OK') { 'Green' } else { 'Red' })
if ($steady.Count) { Write-Host "  fps : $fpsMed (min $fpsMin, max $fpsMax)" -ForegroundColor White }
Say "Rapport : $report" "Report: $report"
Say "A joindre : $zip" "To attach: $zip"
Say 'Ouvrir une issue "Rapport de test" sur GitHub, coller rapport.md et joindre le zip (ou le poster sur Discord).' `
    'Open a "Config test" issue on GitHub, paste rapport.md and attach the zip (or post it on Discord).'
if (-not $Auto) {
    Start-Process explorer.exe "/select,`"$zip`""
    Read-Host '  Entree pour fermer / Enter to close'
}
