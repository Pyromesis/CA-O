<#
.SYNOPSIS
    Recupera localmente la contrasena DPAPI de una clave de release de CA-O.
.DESCRIPTION
    Copia al portapapeles la contrasena cifrada con DPAPI del usuario actual.
    No la imprime por defecto para no dejarla en el historial de la consola.

    Solo funciona en el mismo usuario y equipo donde se genero el fichero
    .password.dpapi. Para usarla en otro equipo, define la variable de
    entorno correspondiente (CAO_RELEASE_PFX_PASSWORD o
    CAO_RELEASE_RECOVERY_PASSWORD) o un fichero con
    CAO_RELEASE_PFX_PASSWORD_FILE / CAO_RELEASE_RECOVERY_PASSWORD_FILE.

    Guarda la contrasena en un gestor de contrasenas y en el backup offline.
    No la dejes en un .txt dentro del repositorio ni en OneDrive.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\show-release-key-password.ps1 -Role Primary
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\show-release-key-password.ps1 -Role Recovery -Show
#>
[CmdletBinding()]
param(
    [ValidateSet('Primary', 'Recovery')]
    [string]$Role = 'Primary',
    [string]$PasswordFile,
    [switch]$Show
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptRoot 'release-key-common.ps1')

$cfg = Get-CaoReleaseRoleConfig -Role $Role
if ([string]::IsNullOrWhiteSpace($PasswordFile)) {
    $directory = Join-Path $env:USERPROFILE $cfg.Directory
    $PasswordFile = Join-Path $directory $cfg.PasswordName
}

if (-not (Test-Path $PasswordFile -PathType Leaf)) {
    throw "No existe el fichero de contrasena: $PasswordFile. Define $($cfg.PasswordEnv) si la guardaste en otro sitio."
}

$password = Unprotect-CaoReleasePassword -Path $PasswordFile
if ($Show) {
    Write-Output $password
}
else {
    Set-Clipboard -Value $password
    Write-Host "Contrasena de $Role copiada al portapapeles." -ForegroundColor Green
    Write-Host 'Pegala en tu gestor de contrasenas y borra el portapapeles cuando termines.' -ForegroundColor Yellow
}
