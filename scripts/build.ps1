# CA-O 2.0 - restore + build + analyze (dev gate).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 [-Configuration Debug]
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

Write-Host "== dotnet restore ==" -ForegroundColor Cyan
dotnet restore CA-O.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "== dotnet build ($Configuration) ==" -ForegroundColor Cyan
dotnet build CA-O.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Build OK" -ForegroundColor Green
