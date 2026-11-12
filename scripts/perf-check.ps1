# Idle performance check (todo 19). Windows PowerShell 5.1.
# Starts the app against a temporary data folder with the settings window closed (disclaimerAccepted true),
# waits for the "overlay shown" log gate (bounded), then measures CPU and working set over an idle window.
# The Wait-Process timeout IS the measurement window (the app never exits on its own), not a sleep.
# Exit 0 when idle CPU% <= MaxCpuPercent, working set <= MaxWorkingSetMB, and the process exits on stop.
# Exit 1 otherwise. Run: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\perf-check.ps1
[CmdletBinding()]
param(
    [string]$Exe,
    [double]$MaxCpuPercent = 0.5,
    [double]$MaxWorkingSetMB = 150,
    [int]$GateSeconds = 10,
    [int]$WindowSeconds = 10
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tfm = 'net10.0-windows10.0.19041.0'
if (-not $Exe) { $Exe = Join-Path $root "src\CrosshairManager.App\bin\Debug\$tfm\CrosshairManager.exe" }
$script:failures = New-Object System.Collections.Generic.List[string]

function Say([string]$text) {
    Write-Host ('[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss.fff'), $text)
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
            try { $met = [bool](& $condition) } catch { $met = $false }
            if ($met) { return $true }
            $left = [int]($seconds * 1000 - $watch.ElapsedMilliseconds)
            if ($left -le 0) { return $false }
            # Event wait (returns early on any change); the 250 ms slice only bounds a missed notification.
            [void]$fsw.WaitForChanged([IO.WatcherChangeTypes]::All, [Math]::Min($left, 250))
        }
    } finally { $fsw.Dispose() }
}

function New-DataDir([string]$dataDir) {
    New-Item -ItemType Directory -Path $dataDir | Out-Null
    $settings = [ordered]@{
        schemaVersion = 1; activePresetId = 'builtin.default'; overlayVisible = $true
        monitor = [ordered]@{ mode = 'primary'; deviceName = $null }
        hotkeys = [ordered]@{ toggleOverlay = 'Ctrl+Alt+Shift+X'; nextPreset = 'Ctrl+Alt+Shift+Right'; previousPreset = 'Ctrl+Alt+Shift+Left'; openSettings = $null }
        language = 'en'; startWithWindows = $false; disclaimerAccepted = $true
        settingsWindowPlacement = [ordered]@{ left = 40; top = 40; width = 900; height = 620 }
    }
    [IO.File]::WriteAllText((Join-Path $dataDir 'settings.json'), ($settings | ConvertTo-Json -Depth 4))
}

function Measure-Idle([string]$exe, [string]$dataDir) {
    $log = Join-Path $dataDir 'logs\app.log'
    $p = Start-Process -FilePath $exe -ArgumentList "--data-dir `"$dataDir`"" -PassThru
    Say "started CrosshairManager pid $($p.Id)"
    try {
        $gate = Wait-Until { @(Read-Shared $log | Where-Object { $_ -match 'overlay shown' }).Count -gt 0 } $log $GateSeconds
        Check $gate "gate: 'overlay shown' in $log within $GateSeconds s"
        if (-not $gate) { return }

        $p.Refresh()
        $cpu0 = $p.TotalProcessorTime
        $watch = [Diagnostics.Stopwatch]::StartNew()
        # Process does not exit on its own: this wait is the idle measurement window.
        [void](Wait-Process -Id $p.Id -Timeout $WindowSeconds -ErrorAction SilentlyContinue)
        $watch.Stop()
        $p.Refresh()
        Check (-not $p.HasExited) "process still running after the ${WindowSeconds} s idle window"

        $cpuSec = ($p.TotalProcessorTime - $cpu0).TotalSeconds
        $windowSec = $watch.Elapsed.TotalSeconds
        $cpuPct = 100.0 * $cpuSec / $windowSec
        $wsMB = $p.WorkingSet64 / 1MB
        Say ('idle CPU: {0:N3} % of one core (CPU time {1:N3} s over {2:N2} s)' -f $cpuPct, $cpuSec, $windowSec)
        Say ('working set: {0:N1} MB' -f $wsMB)
        Check ($cpuPct -le $MaxCpuPercent) ('idle CPU {0:N3}% <= {1}%' -f $cpuPct, $MaxCpuPercent)
        Check ($wsMB -le $MaxWorkingSetMB) ('working set {0:N1} MB <= {1} MB' -f $wsMB, $MaxWorkingSetMB)
    } finally {
        if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
        $exited = $p.WaitForExit(5000)
        Check $exited 'process exited after stop'
    }
}

$dataDir = Join-Path $env:TEMP ('chm-perf19-' + [guid]::NewGuid().ToString('N'))
try {
    $Exe = (Resolve-Path -LiteralPath $Exe).Path
    if (@(Get-Process -Name CrosshairManager -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'CrosshairManager is already running; stop it first'
    }
    New-DataDir $dataDir
    Say "exe=$Exe dataDir=$dataDir MaxCpuPercent=$MaxCpuPercent MaxWorkingSetMB=$MaxWorkingSetMB"
    Measure-Idle $Exe $dataDir
} catch {
    Say ('FAIL {0}' -f $_.Exception.Message)
    $script:failures.Add($_.Exception.Message)
} finally {
    if ($dataDir -and (Test-Path -LiteralPath $dataDir)) { Remove-Item -LiteralPath $dataDir -Recurse -Force }
    Check (-not (Test-Path -LiteralPath $dataDir)) "removed $dataDir"
}

if ($script:failures.Count -gt 0) {
    Say ('RESULT FAIL ({0}): {1}' -f $script:failures.Count, ($script:failures -join '; '))
    exit 1
}
Say 'RESULT PASS'
exit 0
