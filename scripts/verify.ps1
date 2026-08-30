# CA-O 2.0 - release gates (spec 120): build, tests, catalog integrity.
# Failing ANY gate blocks the release.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$failed = $false

Write-Host "== Gate 1: clean build ==" -ForegroundColor Cyan
dotnet build CA-O.sln -c $Configuration
if ($LASTEXITCODE -ne 0) { $failed = $true }

Write-Host "== Gate 2: full test suite ==" -ForegroundColor Cyan
dotnet test CA-O.sln -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { $failed = $true }

Write-Host "== Gate 3: optimization contract coverage ==" -ForegroundColor Cyan
# Contract tests live in CA-O.Core.Tests; a targeted filter proves they ran.
dotnet test tests\CA-O.Core.Tests -c $Configuration --no-build --filter "FullyQualifiedName~OptimizationCatalogContractTests"
if ($LASTEXITCODE -ne 0) { $failed = $true }

Write-Host "== Gate 4: persistence + gaming + IPC coverage (Fase 9) ==" -ForegroundColor Cyan
dotnet test tests\CA-O.Core.Tests -c $Configuration --no-build --filter "FullyQualifiedName~AnalysisStateStoreTests|FullyQualifiedName~GameCompatibilityTests" 
if ($LASTEXITCODE -ne 0) { $failed = $true }
dotnet test tests\CA-O.Security.Tests -c $Configuration --no-build --filter "FullyQualifiedName~IpcPingTests"
if ($LASTEXITCODE -ne 0) { $failed = $true }
dotnet test tests\CA-O.Infrastructure.Tests -c $Configuration --no-build --filter "FullyQualifiedName~HistoryRobustnessTests|FullyQualifiedName~SnapshotRepositoryTests"
if ($LASTEXITCODE -ne 0) { $failed = $true }

Write-Host "== Gate 5: E2E 10 flujos ==" -ForegroundColor Cyan
dotnet test tests\CA-O.Integration.Tests -c $Configuration --no-build --filter "FullyQualifiedName~E2EFlowsTests"
if ($LASTEXITCODE -ne 0) { $failed = $true }

Write-Host "== Gate 6: packaging (no root uninstall.exe/.ps1) ==" -ForegroundColor Cyan
$releaseDir = "artifacts/release"
if (Test-Path $releaseDir) {
    if (Test-Path (Join-Path $releaseDir "uninstall.exe")) { Write-Host "FAIL: uninstall.exe in root" -ForegroundColor Red; $failed = $true }
    if (Test-Path (Join-Path $releaseDir "uninstall.ps1")) { Write-Host "FAIL: uninstall.ps1 in root" -ForegroundColor Red; $failed = $true }
    if (-not (Test-Path (Join-Path $releaseDir "uninstall/CA-O.Uninstaller.exe"))) { Write-Host "FAIL: uninstall/CA-O.Uninstaller.exe missing" -ForegroundColor Red; $failed = $true }
    else { Write-Host "Gate 6 packaging OK" -ForegroundColor Green }
} else { Write-Host "Gate 6 skipped (no artifacts/release)" -ForegroundColor Yellow }

if ($failed) {
    Write-Host "RELEASE GATES FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "All release gates passed." -ForegroundColor Green
