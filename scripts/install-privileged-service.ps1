$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated Administrator PowerShell session.'
}

$repository = Split-Path -Parent $PSScriptRoot
$serviceBinary = Join-Path $repository 'artifacts\service\CA-O.Privileged.exe'
$serviceName = 'CAO.Privileged'

if (-not (Test-Path $serviceBinary)) {
    throw "Published service not found: $serviceBinary"
}

$existing = sc.exe query $serviceName 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Servicio existente — upgrade (stop/delete)..." -ForegroundColor Yellow
    sc.exe stop $serviceName | Out-Null
    Start-Sleep -Seconds 1
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

sc.exe create $serviceName binPath= "`"$serviceBinary`"" start= demand DisplayName= "CA-O Privileged Service" | Out-Host
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/reboot/60000 | Out-Host
# Verification (FASE 29)
$qc = sc.exe qc $serviceName 2>&1 | Out-String
$qfail = sc.exe qfailure $serviceName 2>&1 | Out-String
if ($qc -notmatch 'DEMAND_START') { throw "sc qc fallo: $qc" }
if ($qfail -notmatch '86400') { throw "sc qfailure fallo: $qfail" }
Write-Host "Installed $serviceName (verificado). Start it explicitly with: sc.exe start $serviceName"