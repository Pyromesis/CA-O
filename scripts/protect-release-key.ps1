<#
.SYNOPSIS
    Reexporta un PFX de CA-O con contrasena sin cambiar de identidad.
.DESCRIPTION
    Carga el certificado y la clave privada existentes, los vuelve a exportar
    como PFX protegido con una contrasena nueva y comprueba que el thumbprint
    y la clave publica no cambian. No genera otra identidad: eso rompería el
    ancla que ya tienen las versiones instaladas.

    Si no se indica -NewPassword ni CAO_RELEASE_NEW_PFX_PASSWORD, se genera
    una contrasena robusta y se guarda cifrada con DPAPI del usuario actual.
    Usa scripts\show-release-key-password.ps1 para copiarla al gestor de
    contrasenas.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\protect-release-key.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\protect-release-key.ps1 -Role Recovery
#>
[CmdletBinding()]
param(
    [ValidateSet('Primary', 'Recovery')]
    [string]$Role = 'Primary',
    [string]$PfxPath,
    [string]$CurrentPassword,
    [string]$NewPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptRoot 'release-key-common.ps1')

$cfg = Get-CaoReleaseRoleConfig -Role $Role
if ([string]::IsNullOrWhiteSpace($PfxPath)) {
    $PfxPath = Join-Path (Join-Path $env:USERPROFILE $cfg.Directory) $cfg.PfxName
}
if (-not (Test-Path $PfxPath -PathType Leaf)) {
    throw "No existe el PFX: $PfxPath"
}
$resolvedPath = (Resolve-Path $PfxPath).Path

if ([string]::IsNullOrEmpty($CurrentPassword)) {
    $CurrentPassword = [Environment]::GetEnvironmentVariable($cfg.PasswordEnv)
}
if ([string]::IsNullOrEmpty($CurrentPassword)) {
    $CurrentPassword = Resolve-CaoReleasePassword -Config $cfg -PfxPath $resolvedPath
}

if ([string]::IsNullOrEmpty($NewPassword)) {
    $NewPassword = [Environment]::GetEnvironmentVariable('CAO_RELEASE_NEW_PFX_PASSWORD')
}

$generatedPassword = $false
if ([string]::IsNullOrEmpty($NewPassword)) {
    $NewPassword = New-CaoReleasePassword
    $generatedPassword = $true
}

$directory = Split-Path -Parent $resolvedPath
$newPath = "$resolvedPath.new"
$passwordPath = Join-Path $directory $cfg.PasswordName

if (Test-Path $newPath) { Remove-Item $newPath -Force }

$oldCert = $null
$newCert = $null
$oldPublic = $null
$newPublic = $null
$newPrivate = $null
try {
    # Exportable es obligatorio para reexportar: al importar un PFX sin esa
    # marca, .NET puede cargar la clave como no exportable y Export(Pfx) falla
    # con "Key not valid for use in specified state".
    $oldCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $resolvedPath,
        $CurrentPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    if (-not $oldCert.HasPrivateKey) {
        throw "El PFX no contiene clave privada: $resolvedPath"
    }

    $pfxBytes = $oldCert.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
        $NewPassword)
    [IO.File]::WriteAllBytes($newPath, $pfxBytes)

    $newCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $newPath,
        $NewPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    if (-not $newCert.HasPrivateKey) {
        throw "El PFX reexportado no conserva la clave privada."
    }
    if ($newCert.Thumbprint -ne $oldCert.Thumbprint) {
        throw "El thumbprint cambio: $($oldCert.Thumbprint) -> $($newCert.Thumbprint). Abortado."
    }

    $oldPublic = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($oldCert)
    $newPublic = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($newCert)
    if ($null -eq $oldPublic -or $null -eq $newPublic) {
        throw "El certificado no tiene clave RSA publica."
    }
    if ($newPublic.KeySize -lt 3072) {
        throw "La clave mide $($newPublic.KeySize) bits; el minimo es 3072."
    }

    $oldModulus = [Convert]::ToBase64String($oldPublic.ExportParameters($false).Modulus)
    $newModulus = [Convert]::ToBase64String($newPublic.ExportParameters($false).Modulus)
    if ($oldModulus -ne $newModulus) {
        throw "La clave publica cambio. Abortado: esto romperia el ancla ya distribuida."
    }

    # Prueba real de firma con la clave privada reexportada.
    $newPrivate = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($newCert)
    if ($null -eq $newPrivate) { throw "No se pudo leer la clave privada reexportada." }
    $probe = [Text.Encoding]::UTF8.GetBytes('CA-O release key password rotation probe')
    $signature = $newPrivate.SignData(
        $probe,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $verified = $newPublic.VerifyData(
        $probe,
        $signature,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    if (-not $verified) { throw "La firma de prueba con el PFX reexportado no verifica." }
}
catch {
    if (Test-Path $newPath) { Remove-Item $newPath -Force }
    throw
}
finally {
    if ($null -ne $newPrivate) { $newPrivate.Dispose() }
    if ($null -ne $newPublic) { $newPublic.Dispose() }
    if ($null -ne $oldPublic) { $oldPublic.Dispose() }
    if ($null -ne $newCert) { $newCert.Dispose() }
    if ($null -ne $oldCert) { $oldCert.Dispose() }
}

# Reemplazo atomico: el PFX viejo (sin contrasena) desaparece en la misma
# operacion. No se deja una copia sin cifrar como backup.
[IO.File]::Replace($newPath, $resolvedPath, [NullString]::Value)

if ($generatedPassword) {
    Protect-CaoReleasePassword -Password $NewPassword -Path $passwordPath
}
else {
    if (Test-Path $passwordPath) { Remove-Item $passwordPath -Force }
}

Set-CaoReleaseKeyAcl -Directory $directory -Files @($resolvedPath, $passwordPath)

$final = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $resolvedPath,
    $NewPassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)
try {
    $finalThumbprint = $final.Thumbprint
    $finalHasKey = $final.HasPrivateKey
}
finally { $final.Dispose() }

Write-Host ''
Write-Host '== PFX reexportado con contrasena ==' -ForegroundColor Green
Write-Host "Rol:                        $Role"
Write-Host "PFX:                        $resolvedPath"
Write-Host "Thumbprint (sin cambios):   $finalThumbprint"
Write-Host "Clave privada presente:     $finalHasKey"
if ($generatedPassword) {
    Write-Host "Contrasena cifrada (DPAPI): $passwordPath"
    Write-Host ''
    Write-Host 'Copia la contrasena al gestor de contrasenas:' -ForegroundColor Yellow
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\show-release-key-password.ps1 -Role $Role"
}
else {
    Write-Host 'Contrasena:                 indicada por ti; guardala en el gestor de contrasenas.'
}
Write-Host ''
Write-Host 'El PFX anterior sin contrasena ya no existe. Haz una copia cifrada' -ForegroundColor Yellow
Write-Host 'offline y guarda la contrasena por separado.' -ForegroundColor Yellow
