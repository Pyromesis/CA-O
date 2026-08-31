<# 
.SYNOPSIS
    CA-O Release Gates - Strict verification
.DESCRIPTION
    All gates are mandatory. ANY failure blocks release.
    No missing artifact skips. No optional gates.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$constantsPath = Join-Path $repoRoot 'src\CA-O.Shared\Constants\BuildConstants.cs'

function Get-BuildConstant {
    param([string]$Name)
    $content = Get-Content $constantsPath -Raw
    $pattern = 'public const string {0} = "([^"]+)"' -f [regex]::Escape($Name)
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    throw "Constant $Name not found"
}

$Configuration = Get-BuildConstant 'Configuration'
$ProductVersion = Get-BuildConstant 'ProductVersion'
$UiExecutable = Get-BuildConstant 'UiExecutable'
$ServiceExecutable = Get-BuildConstant 'ServiceExecutable'
$UninstallerExecutable = Get-BuildConstant 'UninstallerExecutable'
$GuiInstallerExecutable = Get-BuildConstant 'GuiInstallerExecutable'
$SetupExecutable = Get-BuildConstant 'SetupExecutable'
$Sha256ManifestName = Get-BuildConstant 'Sha256ManifestName'
$SbomFileName = Get-BuildConstant 'SbomFileName'

$artifactRoot = Join-Path $repoRoot (Get-BuildConstant 'ReleaseArtifactsDir')
$sbomDir = Join-Path $repoRoot (Get-BuildConstant 'SbomDir')
$failed = $false

Write-Host "== CA-O v$ProductVersion Release Verification ==" -ForegroundColor Cyan
Write-Host "Artifacts: $artifactRoot" -ForegroundColor Gray
Write-Host ""

function Assert-FileExists {
    param([string]$Path, [string]$Description)
    if (-not (Test-Path $Path)) {
        Write-Host "FAIL: $Description missing at $Path" -ForegroundColor Red
        $global:failed = $true
    } else {
        Write-Host "OK: $Description" -ForegroundColor Green
    }
}

function Assert-Gate {
    param([string]$Name, [scriptblock]$Test)
    Write-Host "== Gate: $Name ==" -ForegroundColor Cyan
    try {
        & $Test
        Write-Host "PASS: $Name" -ForegroundColor Green
    } catch {
        Write-Host "FAIL: $Name - $($_.Exception.Message)" -ForegroundColor Red
        $global:failed = $true
    }
    Write-Host ""
}

# Gate 1: Clean Build
Assert-Gate "Clean Build" {
    dotnet build "$repoRoot\CA-O.sln" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}

# Gate 2: Full Test Suite
Assert-Gate "Full Test Suite" {
    dotnet test "$repoRoot\CA-O.sln" -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}

# Gate 3: Optimization Contract Coverage
Assert-Gate "Optimization Contract Coverage" {
    dotnet test "tests\CA-O.Core.Tests" -c $Configuration --no-build --filter "FullyQualifiedName~OptimizationCatalogContractTests"
    if ($LASTEXITCODE -ne 0) { throw "Contract tests failed" }
}

# Gate 4: Persistence + Gaming + IPC Coverage
Assert-Gate "Persistence + Gaming + IPC" {
    dotnet test "tests\CA-O.Core.Tests" -c $Configuration --no-build --filter "FullyQualifiedName~AnalysisStateStoreTests|FullyQualifiedName~GameCompatibilityTests"
    if ($LASTEXITCODE -ne 0) { throw "Persistence/Gaming tests failed" }
    dotnet test "tests\CA-O.Security.Tests" -c $Configuration --no-build --filter "FullyQualifiedName~IpcPingTests"
    if ($LASTEXITCODE -ne 0) { throw "IPC tests failed" }
    dotnet test "tests\CA-O.Infrastructure.Tests" -c $Configuration --no-build --filter "FullyQualifiedName~HistoryRobustnessTests|FullyQualifiedName~SnapshotRepositoryTests"
    if ($LASTEXITCODE -ne 0) { throw "Infrastructure tests failed" }
}

# Gate 5: E2E Flows
Assert-Gate "E2E Flows" {
    dotnet test "tests\CA-O.Integration.Tests" -c $Configuration --no-build --filter "FullyQualifiedName~E2EFlowsTests"
    if ($LASTEXITCODE -ne 0) { throw "E2E tests failed" }
}

# Gate 6: Packaging Integrity
Assert-Gate "Packaging Integrity" {
    $releaseDir = "artifacts/release"
    Assert-FileExists (Join-Path $releaseDir "ui\$UiExecutable") "UI executable"
    Assert-FileExists (Join-Path $releaseDir "ui\CA-O.UI.pri") "UI .pri"
    Assert-FileExists (Join-Path $releaseDir "service\$ServiceExecutable") "Service executable"
    Assert-FileExists (Join-Path $releaseDir "uninstall\$UninstallerExecutable") "Uninstaller executable"
    Assert-FileExists (Join-Path $releaseDir "gui-installer\$GuiInstallerExecutable") "GUI Installer executable"
    Assert-FileExists (Join-Path $releaseDir "setup\$SetupExecutable") "Console setup executable"
    Assert-FileExists (Join-Path $releaseDir "CA-O-Setup-GUI-x64.exe") "GUI Installer standalone exe"
    Assert-FileExists (Join-Path $releaseDir "CA-O-Setup-GUI-x64.zip") "GUI Installer zip"
    Assert-FileExists (Join-Path $releaseDir "CA-O.Setup.exe") "Console setup standalone exe"
    
    # Verify NO root uninstall files
    if (Test-Path (Join-Path $releaseDir "uninstall.exe")) { throw "uninstall.exe found in root - must only be in uninstall\" }
    if (Test-Path (Join-Path $releaseDir "uninstall.ps1")) { throw "uninstall.ps1 found in root - must only be in uninstall\" }
    Write-Host "Packaging structure OK" -ForegroundColor Green
}

# Gate 7: Artifact Manifest (SHA256SUMS)
Assert-Gate "SHA256 Manifest" {
    $manifest = Join-Path $artifactRoot $Sha256ManifestName
    if (-not (Test-Path $manifest)) { throw "SHA256SUMS.txt missing" }
    $content = Get-Content $manifest
    $lines = $content -split "`r?`n" | Where-Object { $_ -match '\S' }
    foreach ($line in $lines) {
        $parts = $line -split '\s+', 2
        if ($parts.Count -eq 2) {
            $expectedHash = $parts[0]
            $relPath = $parts[1].Trim()
            $fullPath = Join-Path $artifactRoot $relPath
            if (-not (Test-Path $fullPath)) { throw "Manifest references missing file: $relPath" }
            $actualHash = (Get-FileHash $fullPath -Algorithm SHA256).Hash
            if ($actualHash -ne $expectedHash) { throw ("Hash mismatch for {0}: expected {1}, got {2}" -f $relPath, $expectedHash, $actualHash) }
        }
    }
    Write-Host "All $($lines.Count) hashes verified" -ForegroundColor Green
}

# Gate 8: SBOM
Assert-Gate "SBOM CycloneDX" {
    $sbomPath = Join-Path $sbomDir $SbomFileName
    if (-not (Test-Path $sbomPath)) { throw "SBOM (bom.json) missing" }
    $sbom = Get-Content $sbomPath | ConvertFrom-Json
    if (-not $sbom.bomFormat -or $sbom.bomFormat -ne 'CycloneDX') { throw "Invalid SBOM format" }
    if (-not $sbom.specVersion) { throw "SBOM missing specVersion" }
    if (-not $sbom.components) { throw "SBOM missing components" }
    Write-Host "SBOM valid: $($sbom.components.Count) components" -ForegroundColor Green
}

# Gate 9: Version Consistency
Assert-Gate "Version Consistency" {
    $expected = $ProductVersion
    $buildProps = Get-Content "Directory.Build.props" -Raw
    if ($buildProps -notmatch "<Version>$expected</Version>") { throw "Directory.Build.props version mismatch" }
    $constants = Get-Content "src\CA-O.Shared\Constants\BuildConstants.cs" -Raw
    $pattern = 'public const string ProductVersion = "' + [regex]::Escape($expected) + '"'
    if ($constants -notmatch $pattern) { throw "BuildConstants version mismatch" }
    Write-Host "All versions consistent: $expected" -ForegroundColor Green
}

# Gate 10: Service Name Consistency
Assert-Gate "Service Name Consistency" {
    $expected = Get-BuildConstant 'ServiceName'
    $oldServiceName = 'CAO Privileged Service'
    $files = Get-ChildItem -Recurse -Include "*.cs", "*.ps1", "*.md", "*.props" | Where-Object { $_ -notmatch '\\obj\\|\\bin\\' }
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match [regex]::Escape($oldServiceName) -and $file.FullName -notmatch 'BuildConstants\.cs' -and $file.FullName -notmatch 'verify\.ps1') {
            throw "Found old service name '$oldServiceName' in $($file.FullName) - must use '$expected'"
        }
    }
    Write-Host "Service name consistent: $expected" -ForegroundColor Green
}

# Gate 11: No Hardcoded Paths
Assert-Gate "No Developer Paths" {
    $files = Get-ChildItem -Recurse -Include "*.cs", "*.ps1" | Where-Object { $_ -notmatch '\\obj\\|\\bin\\|\\.git\\' }
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match 'C:\\Users\\Hilo8' -or $content -match 'C:\\Users\\[^\\]+\\AppData\\Local\\Temp\\ipc_debug') {
            throw "Hardcoded developer path found in $($file.FullName)"
        }
    }
    Write-Host "No hardcoded developer paths" -ForegroundColor Green
}

# Gate 12: Unknown != Success
Assert-Gate "Unknown != Success" {
    $files = Get-ChildItem -Recurse -Include "*.cs" | Where-Object { $_ -notmatch '\\obj\\|\\bin\\' }
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match 'VerificationStatus\.Unknown.*==.*true' -or $content -match '== VerificationStatus\.Unknown.*Success') {
            throw "Potential Unknown == Success in $($file.FullName)"
        }
    }
    Write-Host "No Unknown == Success patterns" -ForegroundColor Green
}

# Final result
Write-Host ""
if ($failed) {
    Write-Host "RELEASE GATES FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "All release gates passed." -ForegroundColor Green