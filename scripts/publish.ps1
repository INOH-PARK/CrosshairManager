# Single-file self-contained publish (todo 20). Windows PowerShell 5.1.
# Output: artifacts\publish\CrosshairManager.exe. Exit non-zero on any failure.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\publish'
$proj = Join-Path $root 'src\CrosshairManager.App\CrosshairManager.App.csproj'

try {
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    & dotnet publish $proj -p:PublishProfile=win-x64 -p:DebugType=embedded -p:PublishDir="$out\" -nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $exe = Join-Path $out 'CrosshairManager.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "missing $exe" }
    $exes = @(Get-ChildItem -LiteralPath $out -Filter *.exe -File)
    $dlls = @(Get-ChildItem -LiteralPath $out -Filter *.dll -File -Recurse)
    if ($exes.Count -ne 1) { throw "expected exactly one .exe in $out, found $($exes.Count)" }
    if ($dlls.Count -ne 0) { throw "expected no .dll in $out, found: $(($dlls | ForEach-Object Name) -join ', ')" }

    $size = (Get-Item -LiteralPath $exe).Length
    Write-Host ('exe: {0}' -f $exe)
    Write-Host ('size: {0} bytes ({1:N1} MB)' -f $size, ($size / 1MB))
    Write-Host 'RESULT PASS'
    exit 0
} catch {
    Write-Host ('RESULT FAIL: {0}' -f $_.Exception.Message)
    exit 1
}
