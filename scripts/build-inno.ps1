<#
.SYNOPSIS
    CA-O Inno Setup: asistente de un solo .exe (CA-O-Instalador.exe).
.DESCRIPTION
    Compila installer\CA-O.iss con la versión de BuildConstants.cs y firma
    el resultado con CAO_SIGN_THUMBPRINT si está definido.
    Requiere artifacts\release\{ui,service} (scripts\build-release.ps1 antes)
    e Inno Setup 6 (C:\Tools\InnoSetup\ISCC.exe o PATH).
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$constantsPath = Join-Path $repoRoot 'src\CA-O.Shared\Constants\BuildConstants.cs'
function Get-BuildConstant {
    param([string]$Name)
    $content = Get-Content $constantsPath -Raw
    $pattern = 'public const string {0} = "([^"]+)"' -f [regex]::Escape($Name)
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) { return $match.Groups[1].Value }
    throw "Constant $Name not found"
}

$ProductVersion = Get-BuildConstant 'ProductVersion'
$SetupSingleExeName = Get-BuildConstant 'SetupSingleExeName'

foreach ($dir in @('artifacts\release\ui\CA-O.UI.exe', 'artifacts\release\service\CA-O.Privileged.exe')) {
    if (-not (Test-Path (Join-Path $repoRoot $dir))) { throw "Falta $dir. Ejecute scripts\build-release.ps1 antes." }
}

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    foreach ($candidate in @('C:\Tools\InnoSetup\ISCC.exe', 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe')) {
        if (Test-Path $candidate) { $iscc = @{ Source = $candidate }; break }
    }
}
if (-not $iscc) { throw "ISCC.exe no encontrado. Instale Inno Setup 6." }
$isccPath = $iscc.Source
Write-Host "== ISCC: $isccPath ==" -ForegroundColor Cyan

& $isccPath "/DAppVersion=$ProductVersion" "/DRepoRoot=$repoRoot" (Join-Path $repoRoot 'installer\CA-O.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC falló con código $LASTEXITCODE" }

$outExe = Join-Path $repoRoot "artifacts\$SetupSingleExeName"
if (-not (Test-Path $outExe)) { throw "No se generó $outExe" }

$thumbprint = $env:CAO_SIGN_THUMBPRINT
if ([string]::IsNullOrWhiteSpace($thumbprint)) {
    Write-Warning "CAO_SIGN_THUMBPRINT no definido: instalador SIN firmar."
} else {
    $cert = Get-Item "Cert:\CurrentUser\My\$thumbprint" -ErrorAction Stop
    Set-AuthenticodeSignature -FilePath $outExe -Certificate $cert -TimestampServer "http://timestamp.digicert.com" | Out-Null
    $sig = Get-AuthenticodeSignature -FilePath $outExe
    Write-Host "$outExe -> $($sig.Status) ($($sig.SignerCertificate.Subject))" -ForegroundColor Green
}

$hash = (Get-FileHash $outExe -Algorithm SHA256).Hash
"$hash  $SetupSingleExeName" | Set-Content "$outExe.sha256"
Write-Host "== $SetupSingleExeName ($([math]::Round((Get-Item $outExe).Length / 1MB, 1)) MB) ==" -ForegroundColor Green
Get-Content "$outExe.sha256"
