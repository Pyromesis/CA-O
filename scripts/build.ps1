# CA-O 2.0 - restore + build + analyze (dev gate).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 [-Configuration Debug]
param(
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet SDK no encontrado en PATH." }
Push-Location (Join-Path $PSScriptRoot "..")
try {

Write-Host "== dotnet restore ==" -ForegroundColor Cyan
dotnet restore CA-O.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "== dotnet build ($Configuration) ==" -ForegroundColor Cyan
dotnet build CA-O.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Build OK" -ForegroundColor Green
} finally { Pop-Location }
