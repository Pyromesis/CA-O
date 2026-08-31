<#
.SYNOPSIS
    CA-O Uninstaller
.DESCRIPTION
    Removes CA-O UI, Privileged Service, shortcuts, and ARP entries.
    Run from an ELEVATED console.
    Use --purge-history to also remove audit data under ProgramData.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

# Parse constants from shared BuildConstants.cs
$constantsPath = Join-Path $repoRoot 'src\CA-O.Shared\Constants\BuildConstants.cs'
if (-not (Test-Path $constantsPath)) {
    throw "Cannot find BuildConstants.cs at $constantsPath"
}

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

$ServiceName = Get-BuildConstant 'ServiceName'
$InstallDirectoryName = Get-BuildConstant 'InstallDirectoryName'
$ProgramDataDirectoryName = Get-BuildConstant 'ProgramDataDirectoryName'
$StartMenuFolderName = Get-BuildConstant 'StartMenuFolderName'
$StartMenuShortcutName = Get-BuildConstant 'StartMenuShortcutName'
$DesktopShortcutName = Get-BuildConstant 'DesktopShortcutName'

# Verify admin
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $elevated) { throw 'Ejecute este desinstalador desde una consola ELEVADA.' }

Write-Host "== CA-O Uninstaller ==" -ForegroundColor Cyan

$target = Join-Path $env:ProgramFiles $InstallDirectoryName
$programData = Join-Path $env:ProgramData $ProgramDataDirectoryName

# 1. Stop and delete service
Write-Host "== 1/6 Stopping and removing service $ServiceName ==" -ForegroundColor Cyan
& sc.exe stop $ServiceName | Out-Null
Start-Sleep -Seconds 2
& sc.exe delete $ServiceName
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1060) { throw "sc delete failed (exit code $LASTEXITCODE)" }
Write-Host "Service removed" -ForegroundColor Green

# 2. Remove Program Files directory
Write-Host "== 2/6 Removing installation directory ==" -ForegroundColor Cyan
if (Test-Path $target) {
    Remove-Item $target -Recurse -Force -ErrorAction Stop
    Write-Host "Removed $target" -ForegroundColor Green
}

# 3. Remove ProgramData (transactions, snapshots) - keep history by default
Write-Host "== 3/6 Removing runtime data (transactions, snapshots) ==" -ForegroundColor Cyan
$pathsToRemove = @(
    Join-Path $programData (Get-BuildConstant 'TransactionsDirectory'),
    Join-Path $programData (Get-BuildConstant 'SnapshotsDirectory')
)
foreach ($path in $pathsToRemove) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue }
}

# 4. Handle history purge
$purgeHistory = $args -contains '--purge-history'
if ($purgeHistory) {
    Write-Host "== 4/6 Purging history (--purge-history specified) ==" -ForegroundColor Yellow
    if (Test-Path $programData) { Remove-Item $programData -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "History purged" -ForegroundColor Green
} else {
    Write-Host "== 4/6 Preserving history (use --purge-history to remove) ==" -ForegroundColor Green
}

# 5. Remove shortcuts
Write-Host "== 5/6 Removing shortcuts ==" -ForegroundColor Cyan
$shortcuts = @(
    Join-Path ([Environment]::GetFolderPath('CommonStartMenu')) "Programs\$StartMenuFolderName",
    Join-Path ([Environment]::GetFolderPath('StartMenu')) "Programs\$StartMenuFolderName",
    Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) (Get-BuildConstant 'DesktopShortcutName'),
    Join-Path ([Environment]::GetFolderPath('Desktop')) (Get-BuildConstant 'DesktopShortcutName')
)
foreach ($lnk in $shortcuts) {
    if (Test-Path $lnk) { Remove-Item $lnk -Force -ErrorAction SilentlyContinue }
}

# 6. Remove ARP entries
Write-Host "== 6/6 Removing ARP (Programs and Features) entries ==" -ForegroundColor Cyan
$arpPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CA-O"
)
foreach ($path in $arpPaths) {
    try { Remove-Item -Path $path -Recurse -Force -ErrorAction Stop; Write-Host "  Removed $path" -ForegroundColor Green } catch { }
}

Write-Host "`nDesinstalación completa." -ForegroundColor Green
if (-not $purgeHistory) { Write-Host "Historial conservado en $programData (use --purge-history para borrarlo)." -ForegroundColor Green }