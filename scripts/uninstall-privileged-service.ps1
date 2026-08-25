$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated Administrator PowerShell session.'
}

$serviceName = 'CAO.Privileged'
sc.exe stop $serviceName | Out-Null
sc.exe delete $serviceName | Out-Null
Write-Host "Removed $serviceName."