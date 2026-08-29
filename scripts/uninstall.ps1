# CA-O 2.0 - uninstaller (FASE 29). Run ELEVATED.

$ErrorActionPreference = 'Stop'

$target = Join-Path $env:ProgramFiles 'CA-O'
$svc = 'CAO Privileged Service'

& sc.exe stop $svc | Out-Null
Start-Sleep -Seconds 2
& sc.exe delete $svc
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1060) { throw "sc delete fallo ($LASTEXITCODE)" }

foreach ($path in @(
    (Join-Path $env:ProgramData 'CA-O\transactions'),
    (Join-Path $env:ProgramData 'CA-O\snapshots'),
    $target)) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue }
}

# History is PRESERVED by design (auditoría); borrar solo si el usuario lo pide:
if ($args -contains '--purge-history') {
    Remove-Item (Join-Path $env:ProgramData 'CA-O') -Recurse -Force -ErrorAction SilentlyContinue
}

$sm = [Environment]::GetFolderPath('CommonStartMenu') + '\Programs\CA-O'
if (Test-Path $sm) { Remove-Item $sm -Recurse -Force -ErrorAction SilentlyContinue }
$sm2 = [Environment]::GetFolderPath('StartMenu') + '\Programs\CA-O'
if (Test-Path $sm2) { Remove-Item $sm2 -Recurse -Force -ErrorAction SilentlyContinue }
foreach ($lnk in @(
    [Environment]::GetFolderPath('CommonDesktopDirectory') + '\CA-O.lnk',
    [Environment]::GetFolderPath('Desktop') + '\CA-O.lnk'
)) { if (Test-Path $lnk) { Remove-Item $lnk -Force -ErrorAction SilentlyContinue } }

# Eliminar entrada de Programas y características
try { Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O" -Recurse -Force -ErrorAction Stop; Write-Host "  Registro desinstalador eliminado" } catch { }
try { Remove-Item -Path "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CA-O" -Recurse -Force -ErrorAction Stop } catch { }

Write-Host 'Desinstalacion completa (historial conservado; use --purge-history para borrarlo).' -ForegroundColor Green
