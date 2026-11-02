<#
.SYNOPSIS
    Firma el manifiesto de un ZIP de release de CA-O.
.DESCRIPTION
    Genera <zip>.manifest.json con schema/version/asset/size/sha256 y firma
    sus bytes exactos con la clave privada del mantenedor (RSA-3072,
    PKCS#1 v1.5 + SHA-256). La firma va a <zip>.manifest.sig en base64.

    El updater y CA-O.Setup verifican ambos ficheros contra el certificado
    publico clavado en src\CA-O.Shared\Updates\ReleaseTrust.cs. Sin firma
    valida no se instala nada.

    La clave privada vive FUERA del repositorio:
      %USERPROFILE%\.cao\release-signing\cao-release-signing.pfx
    y se genera una sola vez con scripts\generate-release-key.ps1.

    La contrasena del PFX se lee de CAO_RELEASE_PFX_PASSWORD (si esta
    definida). No se pasa por linea de comandos para que no acabe en el
    historial de PowerShell.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\sign-release-manifest.ps1 `
        -ZipPath artifacts\CA-O-2.2.21-win-x64.zip
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [string]$Version,
    [string]$PfxPath = (Join-Path $env:USERPROFILE '.cao\release-signing\cao-release-signing.pfx'),
    [string]$PfxPassword = $env:CAO_RELEASE_PFX_PASSWORD
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ZipPath -PathType Leaf)) {
    throw "No existe el ZIP: $ZipPath"
}
$zip = (Resolve-Path $ZipPath).Path
$leaf = Split-Path $zip -Leaf

# Solo el paquete estable que descarga el updater. El ZIP con timestamp es un
# artefacto local y su nombre no encaja en el contrato asset/version.
if ($leaf -notmatch '^CA-O-(?<version>\d+(?:\.\d+){1,3})-win-x64\.zip$') {
    throw "Nombre no canonico: $leaf. Se esperaba CA-O-<version>-win-x64.zip"
}
$assetVersion = $Matches['version']

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $assetVersion
}
$normalizedVersion = $Version.Trim().TrimStart('v', 'V')
if ($normalizedVersion -ne $assetVersion) {
    throw "Version '$Version' no coincide con el asset '$leaf'."
}

if (-not (Test-Path $PfxPath -PathType Leaf)) {
    throw "No existe la clave privada: $PfxPath. Generala con scripts\generate-release-key.ps1 (o define -PfxPath)."
}

$manifestPath = "$zip.manifest.json"
$signaturePath = "$zip.manifest.sig"
$size = (Get-Item $zip).Length
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()

# Orden fijo y JSON compacto: los bytes exactos son lo que se firma.
$manifest = [ordered]@{
    schema = 1
    version = $assetVersion
    asset = $leaf
    size = $size
    sha256 = $hash
}
$json = $manifest | ConvertTo-Json -Compress
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestPath, $json, $utf8)

$cert = $null
$privateKey = $null
try {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
        $PfxPath,
        $PfxPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)
    $privateKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    if ($null -eq $privateKey) {
        throw "El PFX no contiene una clave RSA privada: $PfxPath"
    }
    if ($privateKey.KeySize -lt 3072) {
        throw "La clave mide $($privateKey.KeySize) bits; el minimo es 3072."
    }

    $payload = [IO.File]::ReadAllBytes($manifestPath)
    $signature = $privateKey.SignData(
        $payload,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    [IO.File]::WriteAllText($signaturePath, [Convert]::ToBase64String($signature) + "`n", $utf8)

    # Verificacion inmediata con la clave publica: si esto falla, el release
    # no debe publicarse.
    $publicKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($cert)
    try {
        if (-not $publicKey.VerifyData(
                $payload,
                $signature,
                [System.Security.Cryptography.HashAlgorithmName]::SHA256,
                [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)) {
            throw "La firma recien creada no verifica con su propia clave publica."
        }
    }
    finally { $publicKey.Dispose() }
}
finally {
    if ($null -ne $privateKey) { $privateKey.Dispose() }
    if ($null -ne $cert) { $cert.Dispose() }
}

Write-Host "Manifiesto firmado:" -ForegroundColor Green
Write-Host "  $manifestPath"
Write-Host "  $signaturePath"
Write-Host "  version=$assetVersion size=$size sha256=$hash"
