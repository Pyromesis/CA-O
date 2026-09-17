$ErrorActionPreference = 'Stop'

# Verificar si es admin
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Elevando a administrador..." -ForegroundColor Yellow
    $scriptPath = $PSScriptRoot ? (Join-Path $PSScriptRoot 'install-and-launch.ps1') : $MyInvocation.MyCommand.Path
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
    exit
}

# Ejecutar como admin
Write-Host "[OK] Ejecutando como administrador" -ForegroundColor Green
$repoRoot = $PSScriptRoot
Push-Location $repoRoot
try {

Write-Host "[*] Instalando CA-O.Privileged Service..." -ForegroundColor Cyan
try {
    & (Join-Path $repoRoot 'scripts\install-privileged-service.ps1')
    if ($LASTEXITCODE -ne 0) { throw "install-privileged-service.ps1 exit $LASTEXITCODE" }
    Write-Host "[OK] Servicio instalado exitosamente" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Error al instalar: $_" -ForegroundColor Red
    if ([Environment]::UserInteractive -and -not [Environment]::GetCommandLineArgs().Contains('-NonInteractive')) { Read-Host "Presiona Enter para continuar" | Out-Null }
    exit 1
}

Write-Host "[*] Aguardando 2 segundos..." -ForegroundColor Gray
Start-Sleep -Seconds 2

Write-Host "[*] Reabriendo CA-O..." -ForegroundColor Cyan
$candidates = @(
    (Join-Path $repoRoot 'artifacts\release\ui\CA-O.UI.exe'),
    (Join-Path $repoRoot 'artifacts\install\ui\CA-O.UI.exe'),
    (Join-Path $repoRoot 'src\CA-O.UI\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\CA-O.UI.exe')
)
$uiPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $uiPath) {
    Write-Host "[ERROR] No se encontro la UI. Buscado: $($candidates -join ', ')" -ForegroundColor Red
    Write-Host "Ejecuta scripts\build-release.ps1 primero." -ForegroundColor Yellow
    if ([Environment]::UserInteractive -and -not [Environment]::GetCommandLineArgs().Contains('-NonInteractive')) { Read-Host "Presiona Enter para continuar" | Out-Null }
    exit 1
}
Start-Process $uiPath

Write-Host "[OK] CA-O iniciada" -ForegroundColor Green
} finally { Pop-Location }
