<# 
.SYNOPSIS
    CA-O Package Release Output + SHA-256 Checksums
.DESCRIPTION
    Creates versioned zip from release artifacts + SBOM.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$constantsPath = Join-Path $repoRoot 'src\CA-O.Shared\Constants\BuildConstants.cs'
function Get-BuildConstant {
    param([string]$Name)
    $content = Get-Content $constantsPath -Raw
    $pattern = "public const string $Name = \"([^\"]+)\""
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    throw "Constant $Name not found"
}

$Configuration = Get-BuildConstant 'Configuration'
$ProductVersion = Get-BuildConstant 'ProductVersion'
$FullPackageName = Get-BuildConstant 'FullPackageName'
$SbomDir = Get-BuildConstant 'SbomDir'
$SbomFileName = Get-BuildConstant 'SbomFileName'

$ErrorActionPreference = "Stop"
Set-Location $repoRoot

$releaseDir = Join-Path "artifacts" "release"
if (-not (Test-Path $releaseDir)) { throw "Ejecute scripts\build-release.ps1 antes de empaquetar." }

$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$zip = Join-Path "artifacts" ($FullPackageName -f $ProductVersion + "-$stamp")

Write-Host "== Empaquetando $zip ==" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zip -Force

Write-Host "== SHA-256 ==" -ForegroundColor Cyan
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256"
Get-Content "$zip.sha256"

# SBOM estándar (FASE 31): CycloneDX vía dotnet-CycloneDX si está instalada;
# sin la herramienta, se advierte y el release NO debe publicarse sin SBOM.
$hasTool = (dotnet tool list --global | Select-String -Quiet "cyclonedx")
if (-not $hasTool) {
    Write-Warning "dotnet-cyclonedx no instalado. Instale con:"
    Write-Warning "  dotnet tool install --global CycloneDX"
    Write-Warning "y regenere el SBOM antes de publicar."
} else {
    dotnet CycloneDX CA-O.sln --output $SbomDir --filename $SbomFileName
    if ($LASTEXITCODE -ne 0) { throw "CycloneDX fallo" }
    Write-Host "SBOM CycloneDX generado en $SbomDir\$SbomFileName" -ForegroundColor Green
}