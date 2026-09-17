Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated Administrator PowerShell session.'
}

$repository = Split-Path -Parent $PSScriptRoot
# Constantes desde BuildConstants.cs (single source); fallback a valores legacy.
$serviceName = 'CAO.Privileged'
$serviceDisplay = 'CA-O Privileged Service'
$constantsPath = Join-Path $repository 'src\CA-O.Shared\Constants\BuildConstants.cs'
if (Test-Path $constantsPath) {
    $c = Get-Content $constantsPath -Raw
    $m = [regex]::Match($c, 'public const string ServiceName = "([^"]+)"'); if ($m.Success) { $serviceName = $m.Groups[1].Value }
    $m = [regex]::Match($c, 'public const string ServiceDisplayName = "([^"]+)"'); if ($m.Success) { $serviceDisplay = $m.Groups[1].Value }
    $m = [regex]::Match($c, 'public const string ServiceExecutable = "([^"]+)"'); $svcExe = if ($m.Success) { $m.Groups[1].Value } else { 'CA-O.Privileged.exe' }
    $m = [regex]::Match($c, 'public const string ReleaseArtifactsDir = "([^"]+)"'); $relDir = if ($m.Success) { $m.Groups[1].Value } else { 'artifacts/release' }
} else { $svcExe = 'CA-O.Privileged.exe'; $relDir = 'artifacts/release' }
$candidates = @(
    (Join-Path $repository "$relDir\service\$svcExe"),
    (Join-Path $repository "artifacts\service\$svcExe")
)
$serviceBinary = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $serviceBinary) { throw "Published service not found. Buscado: $($candidates -join ', ')" }

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

sc.exe create $serviceName binPath= "`"$serviceBinary`"" start= demand DisplayName= "`"$serviceDisplay`"" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "sc create fallo ($LASTEXITCODE)" }
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/reboot/60000 | Out-Host
if ($LASTEXITCODE -ne 0) { throw "sc failure fallo ($LASTEXITCODE)" }
# Verification (FASE 29)
$qc = sc.exe qc $serviceName 2>&1 | Out-String
$qfail = sc.exe qfailure $serviceName 2>&1 | Out-String
if ($qc -notmatch 'DEMAND_START') { throw "sc qc fallo: $qc" }
if ($qfail -notmatch '86400') { throw "sc qfailure fallo: $qfail" }
Write-Host "Installed $serviceName (verificado). Start it explicitly with: sc.exe start $serviceName" -ForegroundColor Green