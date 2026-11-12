# Todo 15 QA: app shell acceptance + failure scenarios (SCREEN: launches the app, sends hotkeys, captures the screen).
# Windows PowerShell 5.1. Run from the repository root after `dotnet build CrosshairManager.sln -c Debug`.
# Writes the transcript to .omo/evidence/task-15-crosshair-overlay.txt and PNGs to .omo/evidence/task-15/.
# Exit 0 only when every step passed. No sleeps: waits use FileSystemWatcher/WaitForExit with bounded timeouts.
[CmdletBinding()]
param(
    [ValidateSet('all', 'happy', 'corrupt', 'readonly')]
    [string]$Scenario = 'all',
    [int]$GateSeconds = 10
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$tfm = 'net10.0-windows10.0.19041.0'
$appExe = Join-Path $root "src\CrosshairManager.App\bin\Debug\$tfm\CrosshairManager.exe"
$cliExe = Join-Path $root "tools\CrosshairManager.Cli\bin\Debug\$tfm\crosshair.exe"
$sendExe = Join-Path $root "tests\tools\SendHotkey\bin\Debug\$tfm\SendHotkey.exe"
$d = "$env:TEMP\chm-qa15"
$fallbackLog = Join-Path $env:TEMP 'CrosshairManager-fallback.log'
$evidenceDir = Join-Path $root '.omo\evidence'
$pngDir = Join-Path $evidenceDir 'task-15'
$evidence = Join-Path $evidenceDir 'task-15-crosshair-overlay.txt'
$script:failures = New-Object System.Collections.Generic.List[string]

New-Item -ItemType Directory -Force -Path $pngDir | Out-Null

function Say([string]$text) {
    $line = '[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss.fff'), $text
    Write-Host $line
    Add-Content -LiteralPath $evidence -Value $line -Encoding UTF8
}

function Check([bool]$ok, [string]$what) {
    if ($ok) { Say "PASS $what" } else { Say "FAIL $what"; $script:failures.Add($what) }
}

function Read-Shared([string]$path) {
    # Shared read so the app's appends are never blocked by this script.
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    $fs = [IO.File]::Open($path, 'Open', 'Read', 'ReadWrite,Delete')
    try {
        $reader = New-Object IO.StreamReader($fs)
        $text = $reader.ReadToEnd()
    } finally { $fs.Dispose() }
    return @($text -split "`r?`n" | Where-Object { $_ -ne '' })
}

function Count-Lines([string]$path, [string]$pattern) {
    return @(Read-Shared $path | Where-Object { $_ -match $pattern }).Count
}

function Wait-Until([scriptblock]$condition, [string]$watchFile, [int]$seconds) {
    # Re-checks on every change notification for $watchFile's folder; bounded by $seconds.
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $dir = Split-Path -Parent $watchFile
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $fsw = New-Object IO.FileSystemWatcher($dir, (Split-Path -Leaf $watchFile))
    $fsw.NotifyFilter = [IO.NotifyFilters]'LastWrite,Size,FileName'
    try {
        while ($true) {
            $met = $false
            try { $met = [bool](& $condition) } catch { $met = $false } # file mid-replace: check again on the next change
            if ($met) { return $true }
            $left = [int]($seconds * 1000 - $watch.ElapsedMilliseconds)
            if ($left -le 0) { return $false }
            # Event wait (returns early on any change); the 250 ms slice only bounds a missed notification.
            [void]$fsw.WaitForChanged([IO.WatcherChangeTypes]::All, [Math]::Min($left, 250))
        }
    } finally { $fsw.Dispose() }
}

function Wait-NewLine([string]$path, [string]$pattern, [int]$baseline, [int]$seconds) {
    return Wait-Until { (Count-Lines $path $pattern) -gt $baseline } $path $seconds
}

function Invoke-Tool([string]$exe, [string[]]$arguments, [string]$label) {
    Say ("> {0} {1}" -f (Split-Path -Leaf $exe), ($arguments -join ' '))
    $out = & $exe @arguments 2>&1 | Out-String
    $code = $LASTEXITCODE
    foreach ($l in ($out -split "`r?`n")) { if ($l) { Say "  $l" } }
    Say "  exit $code"
    Check ($code -eq 0) $label
    return $code
}

function Verify-Overlay([string]$name, [string[]]$extra) {
    $out = Join-Path $pngDir $name
    $args2 = @('verify-overlay', '--data-dir', $d, '--out', $out) + $extra
    [void](Invoke-Tool $cliExe $args2 "verify-overlay $name ($($extra -join ' '))")
}

function Send([string]$gesture) {
    [void](Invoke-Tool $sendExe @($gesture) "SendHotkey $gesture")
}

function New-DataDir([bool]$withSettings) {
    if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force }
    New-Item -ItemType Directory -Path $d | Out-Null
    if ($withSettings) {
        $settings = [ordered]@{
            schemaVersion = 1; activePresetId = 'builtin.default'; overlayVisible = $true
            monitor = [ordered]@{ mode = 'primary'; deviceName = $null }
            hotkeys = [ordered]@{ toggleOverlay = 'Ctrl+Alt+Shift+X'; nextPreset = 'Ctrl+Alt+Shift+Right'; previousPreset = 'Ctrl+Alt+Shift+Left'; openSettings = $null }
            language = 'en'; startWithWindows = $false; disclaimerAccepted = $true
            settingsWindowPlacement = [ordered]@{ left = 40; top = 40; width = 900; height = 620 }
        }
        [IO.File]::WriteAllText((Join-Path $d 'settings.json'), ($settings | ConvertTo-Json -Depth 4))
    }
}

function Start-App {
    Say "> Start-Process CrosshairManager.exe --data-dir `"$d`""
    return Start-Process -FilePath $appExe -ArgumentList "--data-dir `"$d`"" -PassThru
}

function Stop-App {
    $procs = @(Get-Process CrosshairManager -ErrorAction SilentlyContinue)
    foreach ($p in $procs) {
        Stop-Process -Id $p.Id -Force
        [void]$p.WaitForExit(5000)
    }
    $left = @(Get-Process CrosshairManager -ErrorAction SilentlyContinue)
    Check ($left.Count -eq 0) 'Get-Process CrosshairManager is empty after Stop-Process'
}

function Gate([string]$log, [int]$baseline) {
    $ok = Wait-NewLine $log 'overlay shown' $baseline $GateSeconds
    if ($ok) { Say 'READY' }
    Check $ok "gate: 'overlay shown' in $log within $GateSeconds s"
    return $ok
}

function Happy {
    Say '=== happy path ==='
    New-DataDir $true
    $log = Join-Path $d 'logs\app.log'
    $p = Start-App
    if (-not (Gate $log 0)) { return }
    Verify-Overlay 'happy-visible' @()

    $hidden = Count-Lines $log 'overlay hidden'
    Send 'Ctrl+Alt+Shift+X'
    Check (Wait-NewLine $log 'overlay hidden' $hidden 5) "log 'overlay hidden' after toggle"
    Verify-Overlay 'happy-hidden' @('--expect-hidden')

    $shown = Count-Lines $log 'overlay shown'
    Send 'Ctrl+Alt+Shift+X'
    Check (Wait-NewLine $log 'overlay shown' $shown 5) "log 'overlay shown' after second toggle"
    $shown = Count-Lines $log 'overlay shown'
    Send 'Ctrl+Alt+Shift+Right'
    $settingsPath = Join-Path $d 'settings.json'
    $activeOk = Wait-Until { (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).activePresetId -eq 'builtin.dot' } $settingsPath 5
    Check $activeOk 'settings.json activePresetId == builtin.dot'
    Check (Wait-NewLine $log 'overlay shown' $shown 5) "log 'overlay shown' after next preset"
    Verify-Overlay 'happy-dot' @('--preset', 'builtin.dot')

    $requested = Count-Lines $log 'settings requested'
    $second = Start-App
    $exited = $second.WaitForExit(3000)
    Check $exited 'second launch exits within 3 s'
    if ($exited) { Check ($second.ExitCode -eq 0) "second launch exit code 0 (got $($second.ExitCode))" }
    Check (Wait-NewLine $log 'settings requested' $requested 5) "log 'settings requested' after second launch"
    Check (-not $p.HasExited) 'first instance still running'

    Say '--- app.log ---'
    foreach ($l in (Read-Shared $log)) { Say "  $l" }
    Stop-App
    Remove-Item -LiteralPath $d -Recurse -Force
    Check (-not (Test-Path -LiteralPath $d)) "removed $d"
}

function Corrupt {
    Say '=== failure 1: corrupt settings.json ==='
    New-DataDir $false
    Set-Content -LiteralPath "$d\settings.json" '{'
    $log = Join-Path $d 'logs\app.log'
    [void](Start-App)
    if (Gate $log 0) {
        $backups = @(Get-ChildItem -LiteralPath $d -Filter 'settings.json.corrupt-*')
        Check ($backups.Count -eq 1) "settings.json.corrupt-* created ($($backups.Name -join ', '))"
        Check ($null -ne (Get-Process CrosshairManager -ErrorAction SilentlyContinue)) 'app running after corrupt settings'
        Verify-Overlay 'corrupt' @()
    }
    Stop-App
    Remove-Item -LiteralPath $d -Recurse -Force
    Check (-not (Test-Path -LiteralPath $d)) "removed $d"
}

function ReadOnly {
    Say '=== failure 2: read-only data dir ==='
    New-DataDir $true
    if (Test-Path -LiteralPath $fallbackLog) { Remove-Item -LiteralPath $fallbackLog -Force }
    $denied = $false
    try {
        Say "> icacls $d /deny $($env:USERNAME):(W)"
        & icacls $d /deny "$($env:USERNAME):(W)" | Out-Null
        $denied = ($LASTEXITCODE -eq 0)
        Check $denied 'icacls deny W'
        [void](Start-App)
        if (Gate $fallbackLog 0) {
            Check ((Count-Lines $fallbackLog 'data dir read-only') -ge 1) "fallback log has 'data dir read-only'"
            Check ($null -ne (Get-Process CrosshairManager -ErrorAction SilentlyContinue)) 'app alive with read-only data dir'
            Verify-Overlay 'readonly' @()
        }
        Say '--- fallback log ---'
        foreach ($l in (Read-Shared $fallbackLog)) { Say "  $l" }
    } finally {
        if ($denied) {
            Say "> icacls $d /remove:d $env:USERNAME"
            & icacls $d /remove:d $env:USERNAME | Out-Null
            Check ($LASTEXITCODE -eq 0) 'icacls remove deny'
        }
        Stop-App
        if (Test-Path -LiteralPath $fallbackLog) { Remove-Item -LiteralPath $fallbackLog -Force }
        if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force }
        Check (-not (Test-Path -LiteralPath $fallbackLog)) 'fallback log removed'
    }
}

Say "todo15 QA scenario=$Scenario root=$root"
foreach ($exe in @($appExe, $cliExe, $sendExe)) {
    if (-not (Test-Path -LiteralPath $exe)) { Say "FAIL missing $exe (build first)"; exit 1 }
}
if (@(Get-Process CrosshairManager -ErrorAction SilentlyContinue).Count -gt 0) {
    Say 'FAIL CrosshairManager is already running; stop it first'
    exit 1
}

try {
    if ($Scenario -in 'all', 'happy') { Happy }
    if ($Scenario -in 'all', 'corrupt') { Corrupt }
    if ($Scenario -in 'all', 'readonly') { ReadOnly }
} finally {
    if (@(Get-Process CrosshairManager -ErrorAction SilentlyContinue).Count -gt 0) { Stop-App }
}

if ($script:failures.Count -gt 0) {
    Say ("RESULT FAIL ({0}): {1}" -f $script:failures.Count, ($script:failures -join '; '))
    exit 1
}
Say 'RESULT PASS'
exit 0
