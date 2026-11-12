# Builds the whole solution. Windows PowerShell 5.1 compatible.
param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build CrosshairManager.sln -c $Configuration
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
