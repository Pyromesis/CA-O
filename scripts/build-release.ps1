<# 
.SYNOPSIS
    CA-O Full Release Pipeline
.DESCRIPTION
    Restore -> Build -> Test -> Verify Gates -> Publish -> Sign -> Hashes
    All paths derived from BuildConstants.cs - no hardcoded paths.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

# Parse constants from shared BuildConstants.cs
$constantsPath = Join-Path $repoRoot 'src\CA-O.Shared\Constants\BuildConstants.cs'
function Get-BuildConstant {
    param([string]$Name)
    $content = Get-Content $constantsPath -Raw
    $pattern = 'public const string {0} = "([^"]+)"' -f [regex]::Escape($Name)
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    $pattern = 'public const int {0} = (\d+)' -f [regex]::Escape($Name)
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    throw "Constant $Name not found in BuildConstants.cs"
}

$ProductVersion = Get-BuildConstant 'ProductVersion'
$RuntimeIdentifier = Get-BuildConstant 'RuntimeIdentifier'
$Configuration = Get-BuildConstant 'Configuration'
$TargetFramework = Get-BuildConstant 'TargetFramework'
$UiSubdirectory = Get-BuildConstant 'UiSubdirectory'
$ServiceSubdirectory = Get-BuildConstant 'ServiceSubdirectory'
$UninstallSubdirectory = Get-BuildConstant 'UninstallSubdirectory'
$GuiInstallerSubdirectory = Get-BuildConstant 'GuiInstallerSubdirectory'
$SetupSubdirectory = Get-BuildConstant 'SetupSubdirectory'
$UiExecutable = Get-BuildConstant 'UiExecutable'
$ServiceExecutable = Get-BuildConstant 'ServiceExecutable'
$UninstallerExecutable = Get-BuildConstant 'UninstallerExecutable'
$GuiInstallerExecutable = Get-BuildConstant 'GuiInstallerExecutable'
$SetupExecutable = Get-BuildConstant 'SetupExecutable'
$Sha256ManifestName = Get-BuildConstant 'Sha256ManifestName'
$SbomFileName = Get-BuildConstant 'SbomFileName'

$repository = $repoRoot
$solution = Join-Path $repository 'CA-O.sln'
$artifactRoot = Join-Path $repository (Get-BuildConstant 'ReleaseArtifactsDir')
$uiOutput = Join-Path $artifactRoot $UiSubdirectory
$serviceOutput = Join-Path $artifactRoot $ServiceSubdirectory
$uninstallOutput = Join-Path $artifactRoot $UninstallSubdirectory
$guiOutput = Join-Path $artifactRoot $GuiInstallerSubdirectory
$setupOutput = Join-Path $artifactRoot $SetupSubdirectory
$sbomDir = Join-Path $repository (Get-BuildConstant 'SbomDir')

Write-Host "== CA-O v$ProductVersion Release Pipeline ==" -ForegroundColor Cyan
Write-Host "Artifacts: $artifactRoot" -ForegroundColor Gray

# Clean
Remove-Item $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item $uiOutput, $serviceOutput, $uninstallOutput, $guiOutput, $setupOutput -ItemType Directory -Force | Out-Null

Write-Host '== Restore ==' -ForegroundColor Cyan
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== Build (Release) ==' -ForegroundColor Cyan
dotnet build $solution --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== Tests (Release Gate) ==' -ForegroundColor Cyan
dotnet test $solution --configuration $Configuration --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== Publish UI (self-contained, WinUI 3) ==' -ForegroundColor Cyan
$uiProject = Join-Path $repository 'src\CA-O.UI\CA-O.UI.csproj'
dotnet publish $uiProject --configuration $Configuration --runtime $RuntimeIdentifier --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false --output $uiOutput --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Workaround WinUI: publish self-contained omits .pri file
$priSrc = Join-Path $repository "src\CA-O.UI\bin\x64\$Configuration\$TargetFramework\$RuntimeIdentifier\$(Get-BuildConstant 'UiExecutable').pri"
if (-not (Test-Path $priSrc)) {
    # Fallback: search for .pri
    $priFiles = Get-ChildItem (Join-Path $repository "src\CA-O.UI\bin") -Filter '*.pri' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($priFiles) { $priSrc = $priFiles.FullName }
}
if (Test-Path $priSrc) { Copy-Item $priSrc $uiOutput -Force; Write-Host "  Patched .pri -> $uiOutput" -ForegroundColor Yellow }
else { Write-Warning "CA-O.UI.pri not found" }

# Ensure XAML resources
$assetsSrc = Join-Path $repository "src\CA-O.UI\bin\x64\$Configuration\$TargetFramework\$RuntimeIdentifier\Microsoft.UI.Xaml"
if (Test-Path $assetsSrc) { Copy-Item $assetsSrc $uiOutput -Recurse -Force }

# Verify UI output
$uiExePath = Join-Path $uiOutput (Get-BuildConstant 'UiExecutable')
if (-not (Test-Path $uiExePath)) { throw "UI executable not found at $uiExePath" }
$uiPriPath = Join-Path $uiOutput "$(Get-BuildConstant 'UiExecutable').pri"
if (-not (Test-Path $uiPriPath)) { throw "UI .pri not found at $uiPriPath" }

Write-Host '== Publish Privileged Service ==' -ForegroundColor Cyan
$serviceProject = Join-Path $repository 'src\CA-O.Privileged\CA-O.Privileged.csproj'
dotnet publish $serviceProject --configuration $Configuration --runtime $RuntimeIdentifier --self-contained false --output $serviceOutput --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$svcExePath = Join-Path $serviceOutput (Get-BuildConstant 'ServiceExecutable')
if (-not (Test-Path $svcExePath)) { throw "Service executable not found at $svcExePath" }

Write-Host '== Publish Uninstaller (GUI, no single-file) ==' -ForegroundColor Cyan
$uninstallProject = Join-Path $repository 'src\CA-O.Uninstaller\CA-O.Uninstaller.csproj'
dotnet publish $uninstallProject --configuration $Configuration --runtime $RuntimeIdentifier --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false /p:TreatWarningsAsErrors=false --output $uninstallOutput --no-restore
$uninstallPriSrc = Join-Path $repository "src\CA-O.Uninstaller\bin\x64\$Configuration\$TargetFramework\$RuntimeIdentifier\$(Get-BuildConstant 'UninstallerExecutable').pri"
if (Test-Path $uninstallPriSrc) { Copy-Item $uninstallPriSrc $uninstallOutput -Force }
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$uninstallExePath = Join-Path $uninstallOutput (Get-BuildConstant 'UninstallerExecutable')
if (-not (Test-Path $uninstallExePath)) { throw "Uninstaller executable not found at $uninstallExePath" }

Write-Host '== Publish GUI Installer (self-contained, no single-file - WinUI 3) ==' -ForegroundColor Cyan
$guiProject = Join-Path $repository 'src\CA-O.InstallerGui\CA-O.InstallerGui.csproj'
dotnet publish $guiProject --configuration $Configuration --runtime $RuntimeIdentifier --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false /p:TreatWarningsAsErrors=false --output $guiOutput --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$guiExePath = Join-Path $guiOutput (Get-BuildConstant 'GuiInstallerExecutable')
if (-not (Test-Path $guiExePath)) { throw "GUI Installer executable not found at $guiExePath" }

# For distribution: copy exe and zip full folder
Copy-Item $guiExePath (Join-Path $artifactRoot (Get-BuildConstant 'GuiInstallerExeName')) -Force
Compress-Archive -Path (Join-Path $guiOutput '*') -DestinationPath (Join-Path $artifactRoot (Get-BuildConstant 'GuiInstallerPackageName')) -Force

Write-Host '== Publish Console Setup (fallback, single-file) ==' -ForegroundColor Cyan
$setupProject = Join-Path $repository 'src\CA-O.Setup\CA-O.Setup.csproj'
dotnet publish $setupProject --configuration $Configuration --runtime $RuntimeIdentifier --self-contained true /p:PublishSingleFile=true /p:TreatWarningsAsErrors=false --output $setupOutput --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$setupExePath = Join-Path $setupOutput (Get-BuildConstant 'SetupExecutable')
if (-not (Test-Path $setupExePath)) { throw "Setup executable not found at $setupExePath" }

Write-Host '== Signing ==' -ForegroundColor Cyan
$signScript = Join-Path $scriptRoot 'sign.ps1'
if (Test-Path $signScript) {
    & $signScript -Files @(
        $uiExePath,
        $svcExePath,
        (Join-Path $artifactRoot (Get-BuildConstant 'GuiInstallerExeName')),
        $setupExePath,
        $uninstallExePath
    )
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Warning "sign.ps1 not found - skipping signing (dev build)"
}

Write-Host '== SHA-256 Manifest ==' -ForegroundColor Cyan
Get-ChildItem $artifactRoot -File -Recurse |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $($_.Path.Substring($artifactRoot.Length + 1))" } |
    Set-Content (Join-Path $artifactRoot $Sha256ManifestName)

Write-Host "== SBOM (CycloneDX) ==" -ForegroundColor Cyan
$hasTool = (dotnet tool list --global | Select-String -Quiet "cyclonedx")
if (-not $hasTool) {
    Write-Error "dotnet-cyclonedx not installed. Install with: dotnet tool install --global CycloneDX"
    Write-Error "Release CANNOT be published without SBOM."
    exit 1
}
dotnet CycloneDX $solution --output $sbomDir --filename $SbomFileName
if ($LASTEXITCODE -ne 0) { throw "CycloneDX failed" }
Write-Host "SBOM generated at $sbomDir\$SbomFileName" -ForegroundColor Green

Write-Host "`nRelease artifacts written to $artifactRoot" -ForegroundColor Green
Get-ChildItem $artifactRoot -File -Recurse | Sort-Object FullName | ForEach-Object { 
    $rel = $_.FullName.Substring($artifactRoot.Length + 1)
    Write-Host "  $rel ($([math]::Round($_.Length/1MB, 2)) MB)"
}