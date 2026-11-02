<#
.SYNOPSIS
    Genera la clave de firma de releases de CA-O (RSA-3072, autofirmada).
.DESCRIPTION
    Crea un certificado autofirmado SIN CA comercial. No hace falta comprar
    nada: la confianza viene de que la clave publica queda clavada (pinned)
    dentro de CA-O (src\CA-O.Shared\Updates\ReleaseTrust.cs) y el .pfx no
    sale nunca del equipo del mantenedor.

    La clave privada se queda FUERA del repositorio, en
    %USERPROFILE%\.cao\release-signing. No la subas a git, no la pongas en
    OneDrive ni la compartas: quien la tenga puede firmar un update que
    CA-O aceptara como legitimo.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\generate-release-key.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path $env:USERPROFILE '.cao\release-signing'),
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pfxPath = Join-Path $OutputDir 'cao-release-signing.pfx'
$cerPath = Join-Path $OutputDir 'cao-release-signing.cer'
$base64Path = Join-Path $OutputDir 'cao-release-signing.cert.base64'
$password = $env:CAO_RELEASE_PFX_PASSWORD

if ((Test-Path $pfxPath) -and -not $Force) {
    throw "Ya existe $pfxPath. Usa -Force solo si de verdad quieres rotar la clave (rompe los updates de versiones antiguas)."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 3072 bits: el mismo minimo que exige ReleaseSignatureVerifier.
$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $subject = 'CN=CA-O Release Signing 2026, O=CA-O, C=ES'
    $request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
        $subject,
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    $request.CertificateExtensions.Add(
        (New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
            $true))) | Out-Null

    # EKU Code Signing: el certificado sirve para firmar datos y, si algun dia
    # se compra/emite uno chainable, para Authenticode.
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

# ACL: solo el usuario actual. Best-effort (si falla, el perfil ya esta
# restringido por NTFS, pero mejor dejarlo explicito).
try {
    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $OutputDir /inheritance:r /grant:r "$($me):(OI)(CI)F" | Out-Null
} catch { Write-Warning "No se pudo endurecer la ACL de $OutputDir : $_" }

if ([string]::IsNullOrEmpty($password)) {
    Write-Warning "CAO_RELEASE_PFX_PASSWORD no definido: el .pfx se exporta SIN contrasena (protegido solo por NTFS)."
}

Write-Host ''
Write-Host '== Clave de firma de releases creada ==' -ForegroundColor Green
Write-Host "PFX (PRIVADA, no sale del equipo): $pfxPath"
Write-Host "Certificado publico (DER):        $cerPath"
Write-Host "Base64 para ReleaseTrust.cs:      $base64Path"
Write-Host "Thumbprint:                       $thumbprint"
Write-Host "Valido hasta:                     $expires"
Write-Host ''
Write-Host 'Copia el contenido de:' -ForegroundColor Yellow
Write-Host "  $base64Path"
Write-Host 'en la constante ReleaseTrust.SigningCertificateBase64 y haz commit SOLO de eso.'
Write-Host ''
Write-Host 'Backup de la clave privada: guardala offline. Si se pierde, no se podran' -ForegroundColor Yellow
Write-Host 'firmar mas updates para las versiones ya distribuidas (habria que rotar' -ForegroundColor Yellow
Write-Host 'el ancla publicando una version nueva a mano).' -ForegroundColor Yellow
