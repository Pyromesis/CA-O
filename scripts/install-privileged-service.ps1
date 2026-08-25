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
    sc.exe stop $serviceName | Out-Null
    sc.exe delete $serviceName | Out-Null
}

sc.exe create $serviceName binPath= "`"$serviceBinary`"" start= demand DisplayName= "CA-O Privileged Service" | Out-Host
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/none/0 | Out-Host
Write-Host "Installed $serviceName. Start it explicitly with: sc.exe start $serviceName"