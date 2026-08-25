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
    exit 0
}

foreach ($file in $Files) {
    if (-not (Test-Path $file)) { throw "No existe: $file" }
    Set-AuthenticodeSignature -FilePath $file -Certificate (Get-Item "Cert:\CurrentUser\My\$Thumbprint") -TimestampServer "http://timestamp.digicert.com" | Out-Null
    $sig = (Get-AuthenticodeSignature -FilePath $file)
    Write-Host "$file -> $($sig.Status)"
    if ($sig.Status -ne "Valid") { exit 1 }
}
Write-Host "Firma OK." -ForegroundColor Green
