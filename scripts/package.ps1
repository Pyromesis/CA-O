# CA-O 2.0 - package release output + SHA-256 checksums (spec 86-87).
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$releaseDir = Join-Path "artifacts" "release"
if (-not (Test-Path $releaseDir)) { throw "Ejecute scripts\build-release.ps1 antes de empaquetar." }

$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$zip = Join-Path "artifacts" "CA-O-2.0.0-$stamp-win-x64.zip"

Write-Host "== Empaquetando $zip ==" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zip -Force

Write-Host "== SHA-256 ==" -ForegroundColor Cyan
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256"
Get-Content "$zip.sha256"

# SBOM mínimo (spec 88): paquetes NuGet del grafo restaurado.
$sbom = Join-Path "artifacts" "sbom.spdx.json"
@{
    spdxVersion   = "SPDX-2.3"
    name          = "CA-O 2.0.0"
    created       = (Get-Date).ToUniversalTime().ToString("o")
    packages      = @(dotnet list CA-O.sln package --include-transitive |
        Select-String -Pattern "^\s+>\s+(\S+)\s+(\S+)" |
        ForEach-Object { @{ name = $_.Matches[0].Groups[1].Value; version = $_.Matches[0].Groups[2].Value } })
} | ConvertTo-Json -Depth 4 | Set-Content $sbom
Write-Host "SBOM generado en $sbom" -ForegroundColor Green
