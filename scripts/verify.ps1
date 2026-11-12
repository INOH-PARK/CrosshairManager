# Full verification: build + Core/Desktop/Ui tests. Windows PowerShell 5.1 compatible.
# Fails (exit 1) when: CrosshairManager is running, the build fails, any test fails or is skipped,
# or a trx file is missing.
# Scaffold rule: a test project that contains no test methods yet ([Fact]/[Theory]) is allowed to
# produce no trx (0 tests = pass). Once a project has any test method, a missing trx is a failure.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (Get-Process -Name CrosshairManager -ErrorAction SilentlyContinue) {
    Write-Host 'FAIL: CrosshairManager is already running; close it before verifying.'
    exit 1
}

dotnet build CrosshairManager.sln -c Debug
if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL: build failed'; exit 1 }

$suites = @(
    @{ Name = 'core';    Project = 'tests/CrosshairManager.Core.Tests';    Filter = $null },
    @{ Name = 'desktop'; Project = 'tests/CrosshairManager.Desktop.Tests'; Filter = 'Category=Desktop' },
    @{ Name = 'ui';      Project = 'tests/CrosshairManager.Ui.Tests';      Filter = 'Category=Ui' }
)

$failed = $false
$resultsDir = Join-Path $root 'TestResults'
foreach ($s in $suites) {
    $trx = Join-Path $resultsDir ($s.Name + '.trx')
    if (Test-Path $trx) { Remove-Item $trx -Force }   # never trust a stale result

    $testArgs = @('test', $s.Project, '-c', 'Debug', '--no-build', '--results-directory', $resultsDir, '--logger', ('trx;LogFileName=' + $s.Name + '.trx'))
    if ($s.Filter) { $testArgs += @('--filter', $s.Filter) }
    dotnet @testArgs
    $code = $LASTEXITCODE

    $hasTests = [bool](Get-ChildItem -Path $s.Project -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        Select-String -Pattern '\[(Fact|Theory)' -Quiet)

    if (-not (Test-Path $trx)) {
        if ($hasTests) {
            Write-Host ('FAIL: {0}: trx missing but test methods exist' -f $s.Name)
            $failed = $true
        }
        else {
            Write-Host ('{0}: no test methods yet, no trx (allowed for scaffold state): tests=0 skipped=0' -f $s.Name)
        }
        continue
    }

    [xml]$doc = Get-Content -Raw $trx
    $c = $doc.TestRun.ResultSummary.Counters
    $total = [int]$c.total; $passed = [int]$c.passed; $fail = [int]$c.failed
    $skipped = [int]$c.notExecuted + ([int]$c.total - [int]$c.executed)
    Write-Host ('{0}: total={1} passed={2} failed={3} skipped={4}' -f $s.Name, $total, $passed, $fail, $skipped)
    if ($code -ne 0 -or $fail -gt 0 -or $skipped -gt 0 -or ($hasTests -and $total -eq 0)) {
        Write-Host ('FAIL: {0}' -f $s.Name)
        $failed = $true
    }
}

if ($failed) { exit 1 }
Write-Host 'verify OK'
exit 0
