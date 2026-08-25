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

# SBOM estándar (FASE 31): CycloneDX vía dotnet-CycloneDX si está instalada;
# sin la herramienta, se advierte y el release NO debe publicarse sin SBOM.
$sbomDir = Join-Path "artifacts" "sbom"
$hasTool = (dotnet tool list --global | Select-String -Quiet "cyclonedx")
if (-not $hasTool) {
    Write-Warning "dotnet-cyclonedx no instalado. Instale con:"
    Write-Warning "  dotnet tool install --global CycloneDX"
    Write-Warning "y regenere el SBOM antes de publicar."
} else {
    dotnet CycloneDX CA-O.sln --output $sbomDir --filename bom.json
    if ($LASTEXITCODE -ne 0) { throw "CycloneDX fallo" }
    Write-Host "SBOM CycloneDX generado en $sbomDir\bom.json" -ForegroundColor Green
}
