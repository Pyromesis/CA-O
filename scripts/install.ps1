<# 
.SYNOPSIS
    CA-O Machine-wide installer
.DESCRIPTION
    Installs CA-O UI and Privileged Service to Program Files.
    Registers the privileged service with recovery policy and hardens data ACLs.
    Run from an ELEVATED console.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Import shared constants if available, otherwise define inline
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

# Load build constants from shared location if available
$constantsPath = Join-Path $repoRoot 'src\CA-O.Shared\Constants\BuildConstants.cs'
if (-not (Test-Path $constantsPath)) {
    throw "Cannot find BuildConstants.cs at $constantsPath"
}

# Parse constants from C# file (simplified extraction for PowerShell)
function Get-BuildConstant {
    param([string]$Name)
    $content = Get-Content $constantsPath -Raw
    $pattern = "public const string $Name = \"([^\"]+)\""
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    $pattern = "public const int $Name = (\d+)"
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    throw "Constant $Name not found in BuildConstants.cs"
}

$ProductVersion = Get-BuildConstant 'ProductVersion'
$ServiceName = Get-BuildConstant 'ServiceName'
$ServiceDisplayName = Get-BuildConstant 'ServiceDisplayName'
$ServiceDescription = Get-BuildConstant 'ServiceDescription'
$InstallDirectoryName = Get-BuildConstant 'InstallDirectoryName'
$UiSubdirectory = Get-BuildConstant 'UiSubdirectory'
$ServiceSubdirectory = Get-BuildConstant 'ServiceSubdirectory'
$UiExecutable = Get-BuildConstant 'UiExecutable'
$ServiceExecutable = Get-BuildConstant 'ServiceExecutable'
$RuntimeIdentifier = Get-BuildConstant 'RuntimeIdentifier'
$Configuration = Get-BuildConstant 'Configuration'
$TargetFramework = Get-BuildConstant 'TargetFramework'

# Verify admin
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $elevated) { throw 'Ejecute este instalador desde una consola ELEVADA.' }

Write-Host "== CA-O v$ProductVersion Installer ==" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray

# Resolve artifact paths dynamically
$uiProject = Join-Path $repoRoot 'src\CA-O.UI\CA-O.UI.csproj'
$serviceProject = Join-Path $repoRoot 'src\CA-O.Privileged\CA-O.Privileged.csproj'
$artifactRoot = Join-Path $repoRoot 'artifacts\install'
$uiOutput = Join-Path $artifactRoot 'ui'
$serviceOutput = Join-Path $artifactRoot 'service'

# Clean previous install artifacts
if (Test-Path $artifactRoot) { Remove-Item $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue }
New-Item $uiOutput, $serviceOutput -ItemType Directory -Force | Out-Null

Write-Host "== 1/5 Publishing Release ==" -ForegroundColor Cyan
Write-Host "Publishing UI..." -ForegroundColor Gray
dotnet publish $uiProject -c $Configuration -r $RuntimeIdentifier --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false --output $uiOutput --no-restore
if ($LASTEXITCODE -ne 0) { throw "Publish UI failed with exit code $LASTEXITCODE" }

Write-Host "Publishing Privileged Service..." -ForegroundColor Gray
dotnet publish $serviceProject -c $Configuration -r $RuntimeIdentifier --self-contained false --output $serviceOutput --no-restore
if ($LASTEXITCODE -ne 0) { throw "Publish Service failed with exit code $LASTEXITCODE" }

# Verify publish outputs
$uiExe = Join-Path $uiOutput (Get-BuildConstant 'UiExecutable')
$svcExe = Join-Path $serviceOutput (Get-BuildConstant 'ServiceExecutable')
if (-not (Test-Path $uiExe)) { throw "UI executable not found at $uiExe" }
if (-not (Test-Path $svcExe)) { throw "Service executable not found at $svcExe" }

Write-Host "== 2/5 Copying to Program Files ==" -ForegroundColor Cyan
$target = Join-Path $env:ProgramFiles (Get-BuildConstant 'InstallDirectoryName')
$uiTarget = Join-Path $target (Get-BuildConstant 'UiSubdirectory')
$svcTarget = Join-Path $target (Get-BuildConstant 'ServiceSubdirectory')

New-Item -ItemType Directory -Force -Path $uiTarget, $svcTarget | Out-Null
Copy-Item "$uiOutput\*" $uiTarget -Recurse -Force
Copy-Item "$serviceOutput\*" $svcTarget -Recurse -Force

Write-Host "== 3/5 Registering Privileged Service ==" -ForegroundColor Cyan
$svcName = Get-BuildConstant 'ServiceName'
$svcDisplayName = Get-BuildConstant 'ServiceDisplayName'
$svcDescription = Get-BuildConstant 'ServiceDescription'
$svcExePath = Join-Path $svcTarget (Get-BuildConstant 'ServiceExecutable')

# Idempotent: handle Upgrade/Repair — remove existing service first
$existing = sc.exe query $svcName 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Existing service detected — performing upgrade (stop/delete)..." -ForegroundColor Yellow
    & sc.exe stop $svcName | Out-Null
    Start-Sleep -Seconds 2
    & sc.exe delete $svcName | Out-Null
    Start-Sleep -Seconds 2
}

& sc.exe create $svcName binPath= "`"$svcExePath`"" start= demand obj= LocalSystem DisplayName= $svcDisplayName
if ($LASTEXITCODE -ne 0) { throw "sc create failed (exit code $LASTEXITCODE)" }

# Recovery policy
& sc.exe failure $svcName reset= 86400 actions= restart/5000/restart/10000/reboot/60000
if ($LASTEXITCODE -ne 0) { throw "sc failure failed (exit code $LASTEXITCODE)" }

Write-Host "== 3b/5 Verifying Service Registration ==" -ForegroundColor Cyan
$qc = sc.exe qc $svcName 2>&1 | Out-String
$qfail = sc.exe qfailure $svcName 2>&1 | Out-String
if ($qc -notmatch 'DEMAND_START' -or $qc -notmatch [regex]::Escape($svcExePath)) { throw "sc qc verification failed: $qc" }
if ($qfail -notmatch 'RESTART.*5000' -or $qfail -notmatch '86400') { throw "sc qfailure verification failed: $qfail" }
Write-Host 'Service verified: demand start + recovery policy OK' -ForegroundColor Green

Write-Host "== 4/5 Hardening Data ACLs ==" -ForegroundColor Cyan
& (Join-Path $scriptRoot 'harden-data-acls.ps1')

Write-Host "== 5/5 Creating Shortcuts ==" -ForegroundColor Cyan
$sm = [Environment]::GetFolderPath('CommonStartMenu') + '\Programs\' + (Get-BuildConstant 'StartMenuFolderName')
New-Item -ItemType Directory -Force -Path $sm | Out-Null
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path $sm (Get-BuildConstant 'StartMenuShortcutName')))
$lnk.TargetPath = Join-Path $uiTarget (Get-BuildConstant 'UiExecutable')
$lnk.Description = "CA-O Windows Optimizer"
$lnk.Save()

# Desktop shortcut
$desk = [Environment]::GetFolderPath('CommonDesktopDirectory') + '\' + (Get-BuildConstant 'DesktopShortcutName')
$lnk2 = $shell.CreateShortcut($desk)
$lnk2.TargetPath = Join-Path $uiTarget (Get-BuildConstant 'UiExecutable')
$lnk2.Description = "CA-O Windows Optimizer"
$lnk2.Save()

Write-Host "`nInstallation complete in $target" -ForegroundColor Green
Write-Host "UI executable: $uiTarget\$(Get-BuildConstant 'UiExecutable')" -ForegroundColor Green
Write-Host "Service executable: $svcTarget\$(Get-BuildConstant 'ServiceExecutable')" -ForegroundColor Green
Write-Host 'The UI runs elevated; privileged mutations use the SYSTEM service via Named Pipe.' -ForegroundColor Green