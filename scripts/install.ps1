# CA-O 2.0 - Machine-wide installer (FASE 29).
# Layout: %ProgramFiles%\CA-O\{ui\*,service\*}
# Registers the privileged service with recovery policy and hardens data ACLs.
# Run from an ELEVATED console.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $elevated) { throw 'Ejecute este instalador desde una consola ELEVADA.' }

Write-Host '== 1/5 publish Release ==' -ForegroundColor Cyan
dotnet publish "$repo\src\CA-O.UI\CA-O.UI.csproj" -c Release -r win-x64 --self-contained false -o "$repo\artifacts\install\ui" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'publish UI fallo' }
dotnet publish "$repo\src\CA-O.Privileged\CA-O.Privileged.csproj" -c Release -r win-x64 --self-contained false -o "$repo\artifacts\install\service" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'publish servicio fallo' }

Write-Host '== 2/5 copy to Program Files ==' -ForegroundColor Cyan
$target = Join-Path $env:ProgramFiles 'CA-O'
New-Item -ItemType Directory -Force -Path "$target\ui", "$target\service" | Out-Null
Copy-Item "$repo\artifacts\install\ui\*" "$target\ui\" -Recurse -Force
Copy-Item "$repo\artifacts\install\service\*" "$target\service\" -Recurse -Force

Write-Host '== 3/5 register privileged service (recovery policy) ==' -ForegroundColor Cyan
$svcName = 'CAO Privileged Service'
$svcExe = Join-Path $target 'service\CA-O.Privileged.exe'
# Idempotent: handle Upgrade/Repair — remove existing service first
$existing = sc.exe query $svcName 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Servicio existente detectado — realizando upgrade (stop/delete)..." -ForegroundColor Yellow
    & sc.exe stop $svcName | Out-Null
    Start-Sleep -Seconds 1
    & sc.exe delete $svcName | Out-Null
    Start-Sleep -Seconds 1
}
& sc.exe create $svcName binPath= "`"$svcExe`"" start= demand obj= LocalSystem DisplayName= "CA-O Privileged Service"
if ($LASTEXITCODE -ne 0) { throw "sc create fallo ($LASTEXITCODE)" }
# Recovery: restart/5000, restart/10000, reboot/60000, reset=86400 (FASE 29)
& sc.exe failure $svcName reset= 86400 actions= restart/5000/restart/10000/reboot/60000
if ($LASTEXITCODE -ne 0) { throw "sc failure fallo ($LASTEXITCODE)" }
# Verification (FASE 29): sc qc + qfailure
Write-Host '== 3b/5 verificar servicio ==' -ForegroundColor Cyan
$qc = sc.exe qc $svcName 2>&1 | Out-String
$qfail = sc.exe qfailure $svcName 2>&1 | Out-String
if ($qc -notmatch 'DEMAND_START' -or $qc -notmatch [regex]::Escape($svcExe)) { throw "sc qc verificación falló: $qc" }
if ($qfail -notmatch 'RESTART.*5000' -or $qfail -notmatch '86400') { throw "sc qfailure verificación falló: $qfail" }
Write-Host 'Servicio verificado: demanda + recovery policy OK' -ForegroundColor Green

Write-Host '== 4/5 harden data ACLs ==' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'harden-data-acls.ps1')

Write-Host '== 5/5 shortcuts ==' -ForegroundColor Cyan
$sm = [Environment]::GetFolderPath('StartMenu') + '\Programs\CA-O'
New-Item -ItemType Directory -Force -Path $sm | Out-Null
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut("$sm\CA-O.lnk")
$lnk.TargetPath = Join-Path $target 'ui\CA-O.UI.exe'
$lnk.Save()

Write-Host "`nInstalacion completa en $target" -ForegroundColor Green
Write-Host 'La UI arranca sin privilegios; las mutaciones usan el servicio.' -ForegroundColor Green
