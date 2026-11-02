$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repository 'artifacts\release'
$hashFile = Join-Path $artifactRoot 'SHA256SUMS.txt'

if (-not (Test-Path $hashFile)) {
    throw "Release hash manifest not found: $hashFile"
}

Get-Content $hashFile | ForEach-Object {
    $parts = $_ -split '\s{2}', 2
    if ($parts.Count -ne 2) { throw "Invalid hash manifest entry: $_" }
    $file = Join-Path $artifactRoot $parts[1]
    if (-not (Test-Path $file)) { throw "Missing release file: $file" }
    $actual = (Get-FileHash $file -Algorithm SHA256).Hash
    if ($actual -ne $parts[0]) { throw "Hash mismatch: $file" }
}

Write-Host 'Release hashes verified.'

# ---------------------------------------------------------------------------
# Manifiesto firmado del paquete distribuible.
# ---------------------------------------------------------------------------
$constantsPath = Join-Path $repository 'src\CA-O.Shared\Constants\BuildConstants.cs'
$constants = Get-Content $constantsPath -Raw
$versionMatch = [regex]::Match($constants, 'public const string ProductVersion = "([^"]+)"')
if (-not $versionMatch.Success) { throw "ProductVersion not found in BuildConstants.cs" }
$productVersion = $versionMatch.Groups[1].Value

$trustPath = Join-Path $repository 'src\CA-O.Shared\Updates\ReleaseTrust.cs'
if (-not (Test-Path $trustPath)) { throw "Trust anchor not found: $trustPath" }
$trustSource = Get-Content $trustPath -Raw

$zip = Join-Path $repository "artifacts\CA-O-$productVersion-win-x64.zip"
$manifestPath = "$zip.manifest.json"
$signaturePath = "$zip.manifest.sig"

if (-not (Test-Path $zip)) { throw "Missing distributable ZIP: $zip" }
if (-not (Test-Path $manifestPath)) { throw "Missing signed manifest: $manifestPath" }
if (-not (Test-Path $signaturePath)) { throw "Missing manifest signature: $signaturePath" }

$manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
$signatureBytes = [Convert]::FromBase64String(([IO.File]::ReadAllText($signaturePath)).Trim())

# Las dos anclas son validas: Primary para el dia a dia, Recovery para una
# transicion si la primera se pierde o se compromete.
$trustAnchors = @(
    @{ Role = 'primary';  Pattern = 'SigningCertificateBase64\s*=\s*"([^"]+)"' },
    @{ Role = 'recovery'; Pattern = 'RecoveryCertificateBase64\s*=\s*"([^"]+)"' }
)

$matchedRole = $null
foreach ($anchor in $trustAnchors) {
    $match = [regex]::Match($trustSource, $anchor.Pattern)
    if (-not $match.Success) { throw "$($anchor.Role) trust anchor not found in ReleaseTrust.cs" }

    $certificate = $null
    $publicKey = $null
    try {
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            [byte[]][Convert]::FromBase64String($match.Groups[1].Value))
        $publicKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($certificate)
        if ($null -eq $publicKey) { throw "$($anchor.Role) trust anchor is not an RSA certificate." }
        if ($publicKey.KeySize -lt 3072) { throw "$($anchor.Role) trust anchor key is $($publicKey.KeySize) bits; expected >= 3072." }

        $signatureOk = $publicKey.VerifyData(
            $manifestBytes,
            $signatureBytes,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        if ($signatureOk) {
            $matchedRole = $anchor.Role
            break
        }
    }
    finally {
        if ($null -ne $publicKey) { $publicKey.Dispose() }
        if ($null -ne $certificate) { $certificate.Dispose() }
    }
}

if ($null -eq $matchedRole) {
    throw "Manifest signature does not verify against the primary or recovery anchor."
}

$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
if ($manifest.schema -ne 1) { throw "Unsupported manifest schema: $($manifest.schema)" }
if ($manifest.asset -ne (Split-Path $zip -Leaf)) { throw "Manifest asset mismatch: $($manifest.asset)" }
if (([int64]$manifest.size) -ne (Get-Item $zip).Length) { throw "Manifest size mismatch." }
if ($manifest.version -ne $productVersion) { throw "Manifest version mismatch: $($manifest.version) != $productVersion" }

$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($manifest.sha256 -ne $zipHash) { throw "Manifest SHA-256 mismatch." }

Write-Host "Signed manifest verified for CA-O $productVersion (key: $matchedRole)." -ForegroundColor Green
