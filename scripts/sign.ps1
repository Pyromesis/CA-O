# CA-O 2.0 - Authenticode signing (spec 87).
# Requires a code-signing certificate; CI supplies it through env vars.
# Local usage:
#   $env:CAO_SIGN_THUMBPRINT = "<cert thumbprint from Cert:\CurrentUser\My>"
#   powershell -File scripts\sign.ps1 -Files bin\Release\...\CA-O.UI.exe,...
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [string]$Thumbprint = $env:CAO_SIGN_THUMBPRINT
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Thumbprint)) {
    Write-Warning "CAO_SIGN_THUMBPRINT no definido: los artefactos quedan SIN firmar (build de desarrollo)."
    return
}

function Find-SigningCert([string]$tp) {
    foreach ($store in @("Cert:\CurrentUser\My\$tp", "Cert:\LocalMachine\My\$tp")) {
        try { $c = Get-Item $store -ErrorAction Stop; if ($c) { return $c } } catch {}
    }
    throw "Certificado no encontrado en CurrentUser\My ni LocalMachine\My: $tp"
}

$cert = Find-SigningCert $Thumbprint
foreach ($file in $Files) {
    if (-not (Test-Path $file)) { throw "No existe: $file" }
    Set-AuthenticodeSignature -FilePath $file -Certificate $cert -TimestampServer "https://timestamp.digicert.com" -HashAlgorithm SHA256 | Out-Null
    $sig = (Get-AuthenticodeSignature -FilePath $file)
    Write-Host "$file -> $($sig.Status)"
    if ($sig.Status -ne "Valid") { throw "Firma inválida en $file ($($sig.Status))" }
}
Write-Host "Firma OK." -ForegroundColor Green
