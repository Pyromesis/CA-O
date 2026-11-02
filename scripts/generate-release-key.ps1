<#
.SYNOPSIS
    Genera una clave de firma de releases de CA-O (RSA-3072, autofirmada).
.DESCRIPTION
    Crea un certificado autofirmado SIN CA comercial. La confianza viene de
    que la clave publica queda clavada (pinned) dentro de CA-O
    (src\CA-O.Shared\Updates\ReleaseTrust.cs), no de una cadena de confianza.

    Roles:
      Primary  -> firma los releases del dia a dia.
      Recovery -> segunda raiz de confianza, para emergencias. Su clave
                  privada debe guardarse OFFLINE y no en el equipo habitual.

    El PFX SIEMPRE se exporta con contrasena. Si no defines
    CAO_RELEASE_PFX_PASSWORD (Primary) o CAO_RELEASE_RECOVERY_PASSWORD
    (Recovery), el script genera una contrasena robusta y la guarda cifrada
    con DPAPI del usuario actual junto al PFX. Para pasarla al gestor de
    contrasenas: scripts\show-release-key-password.ps1 -Role <rol>.

    La clave privada se queda FUERA del repositorio. No la subas a git, no la
    pongas en OneDrive ni la compartas: quien la tenga puede firmar un update
    que CA-O aceptara como legitimo.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\generate-release-key.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\generate-release-key.ps1 -Role Recovery
#>
[CmdletBinding()]
param(
    [ValidateSet('Primary', 'Recovery')]
    [string]$Role = 'Primary',
    [string]$OutputDir,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptRoot 'release-key-common.ps1')

$cfg = Get-CaoReleaseRoleConfig -Role $Role
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $env:USERPROFILE $cfg.Directory
}

$pfxPath = Join-Path $OutputDir $cfg.PfxName
$cerPath = Join-Path $OutputDir $cfg.CerName
$base64Path = Join-Path $OutputDir $cfg.Base64Name
$passwordPath = Join-Path $OutputDir $cfg.PasswordName

$password = [Environment]::GetEnvironmentVariable($cfg.PasswordEnv)
$generatedPassword = $false
if ([string]::IsNullOrWhiteSpace($password)) {
    $password = New-CaoReleasePassword
    $generatedPassword = $true
}

if ((Test-Path $pfxPath) -and -not $Force) {
    throw "Ya existe $pfxPath. Usa -Force solo si de verdad quieres rotar la clave (rompe los updates de versiones antiguas)."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 3072 bits: el mismo minimo que exige ReleaseSignatureVerifier.
$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
        $cfg.Subject,
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    $request.CertificateExtensions.Add(
        (New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
            $true))) | Out-Null

    # EKU Code Signing: el certificado sirve para firmar datos. No convierte
    # esta clave en un editor Authenticode de confianza para Windows.
    $oids = New-Object System.Security.Cryptography.OidCollection
    $oids.Add((New-Object System.Security.Cryptography.Oid('1.3.6.1.5.5.7.3.3'))) | Out-Null
    $request.CertificateExtensions.Add(
        (New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($oids, $false))) | Out-Null

    $request.CertificateExtensions.Add(
        (New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($false, $false, 0, $true))) | Out-Null

    $request.CertificateExtensions.Add(
        (New-Object System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension($request.PublicKey, $false))) | Out-Null

    $notBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5)
    $notAfter = [DateTimeOffset]::UtcNow.AddYears(20)
    $cert = $request.CreateSelfSigned($notBefore, $notAfter)
    if (-not $cert.HasPrivateKey) { $cert = $cert.CopyWithPrivateKey($rsa) }

    try {
        $pfxBytes = $cert.Export(
            [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
            $password)
        [IO.File]::WriteAllBytes($pfxPath, $pfxBytes)

        $der = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        [IO.File]::WriteAllBytes($cerPath, $der)

        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [IO.File]::WriteAllText($base64Path, [Convert]::ToBase64String($der), $utf8)

        $thumbprint = $cert.Thumbprint
        $expires = $cert.NotAfter.ToUniversalTime().ToString('yyyy-MM-dd')
    }
    finally { $cert.Dispose() }
}
finally { $rsa.Dispose() }

if ($generatedPassword) {
    Protect-CaoReleasePassword -Password $password -Path $passwordPath
}
elseif (Test-Path $passwordPath) {
    # Contrasena indicada por el usuario: el fichero DPAPI anterior ya no
    # corresponde a este PFX y dejarlo ahi solo induce a error.
    Remove-Item $passwordPath -Force
}

Set-CaoReleaseKeyAcl -Directory $OutputDir -Files @($pfxPath, $cerPath, $base64Path, $passwordPath)

Write-Host ''
Write-Host "== Clave de $($cfg.Label) creada ==" -ForegroundColor Green
Write-Host "Rol:                              $Role"
Write-Host "PFX (PRIVADA, no sale del equipo): $pfxPath"
Write-Host "Certificado publico (DER):        $cerPath"
Write-Host "Base64 para ReleaseTrust.cs:      $base64Path"
Write-Host "Thumbprint:                       $thumbprint"
Write-Host "Valido hasta:                     $expires"
if ($generatedPassword) {
    Write-Host "Contrasena cifrada (DPAPI):       $passwordPath"
} else {
    Write-Host "Contrasena:                       leida de $($cfg.PasswordEnv) (no se persiste)"
}
Write-Host ''
Write-Host 'Copia el contenido de:' -ForegroundColor Yellow
Write-Host "  $base64Path"
Write-Host "en la constante ReleaseTrust.$($cfg.TrustConstant) y haz commit SOLO de eso."
Write-Host ''
if ($generatedPassword) {
    Write-Host 'La contrasena no se imprime. Para copiarla al gestor de contrasenas:' -ForegroundColor Yellow
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\show-release-key-password.ps1 -Role $Role"
    Write-Host 'Guardala en el gestor y en un medio offline; el fichero DPAPI solo' -ForegroundColor Yellow
    Write-Host 'sirve en este usuario y en este equipo.' -ForegroundColor Yellow
}
Write-Host ''
Write-Host 'Backup de la clave privada: guardala offline. Si se pierde la clave' -ForegroundColor Yellow
Write-Host 'Primary, la Recovery permite publicar una version de transicion; si se' -ForegroundColor Yellow
Write-Host 'pierden ambas, no se podran firmar mas updates para las versiones ya' -ForegroundColor Yellow
Write-Host 'distribuidas y habra que rotar el ancla con descarga manual.' -ForegroundColor Yellow
