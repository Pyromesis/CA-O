$ErrorActionPreference = 'Stop'

# Verificar si es admin
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Elevando a administrador..." -ForegroundColor Yellow
    $scriptPath = $MyInvocation.MyCommand.Path
    Start-Process powershell -Verb RunAs -ArgumentList "-NoExit -File `"$scriptPath`"" -Wait
    exit
}

# Ejecutar como admin
Write-Host "[OK] Ejecutando como administrador" -ForegroundColor Green
cd 'c:\Users\berna\OneDrive\Documentos\CA-O'

Write-Host "[*] Instalando CA-O.Privileged Service..." -ForegroundColor Cyan
try {
    .\scripts\install-privileged-service.ps1
    Write-Host "[OK] Servicio instalado exitosamente" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Error al instalar: $_" -ForegroundColor Red
    Read-Host "Presiona Enter para continuar"
    exit 1
}

Write-Host "[*] Aguardando 2 segundos..." -ForegroundColor Gray
Start-Sleep -Seconds 2

Write-Host "[*] Reabriendo CA-O..." -ForegroundColor Cyan
$uiPath = 'c:\Users\berna\OneDrive\Documentos\CA-O\src\CA-O.UI\bin\Release\net10.0-windows10.0.19041.0\win-x64\CA-O.UI.exe'
& $uiPath

Write-Host "[OK] CA-O iniciada" -ForegroundColor Green
