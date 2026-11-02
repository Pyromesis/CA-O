<#
.SYNOPSIS
    Utilidades compartidas para las claves de firma de releases de CA-O.
.DESCRIPTION
    Un solo sitio para nombres, entropia DPAPI y ACL. La clave privada nunca
    se guarda en el repositorio y el PFX siempre se exporta con contrasena.
    La contrasena se puede pasar por variable de entorno, por fichero o dejar
    que estos scripts la generen y la guarden cifrada con DPAPI del usuario.
#>

Set-StrictMode -Version Latest

$script:CaoReleasePasswordEntropy =
    [Text.Encoding]::UTF8.GetBytes('CA-O release key password v1')

function Get-CaoReleaseRoleConfig {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Primary', 'Recovery')]
        [string]$Role
    )

    switch ($Role) {
        'Primary' {
            return [pscustomobject]@{
                Role           = 'Primary'
                Directory      = '.cao\release-signing'
                PfxName        = 'cao-release-signing.pfx'
                CerName        = 'cao-release-signing.cer'
                Base64Name     = 'cao-release-signing.cert.base64'
                PasswordName   = 'cao-release-signing.password.dpapi'
                Subject        = 'CN=CA-O Release Signing 2026, O=CA-O, C=ES'
                PasswordEnv    = 'CAO_RELEASE_PFX_PASSWORD'
                PasswordFileEnv = 'CAO_RELEASE_PFX_PASSWORD_FILE'
                TrustConstant  = 'SigningCertificateBase64'
                Label          = 'firma'
            }
        }
        'Recovery' {
            return [pscustomobject]@{
                Role           = 'Recovery'
                Directory      = '.cao\release-recovery'
                PfxName        = 'cao-release-recovery.pfx'
                CerName        = 'cao-release-recovery.cer'
                Base64Name     = 'cao-release-recovery.cert.base64'
                PasswordName   = 'cao-release-recovery.password.dpapi'
                Subject        = 'CN=CA-O Release Recovery 2026, O=CA-O, C=ES'
                PasswordEnv    = 'CAO_RELEASE_RECOVERY_PASSWORD'
                PasswordFileEnv = 'CAO_RELEASE_RECOVERY_PASSWORD_FILE'
                TrustConstant  = 'RecoveryCertificateBase64'
                Label          = 'recuperacion'
            }
        }
    }
}

function New-CaoReleasePassword {
    # 32 bytes aleatorios en base64url: ~43 caracteres, sin caracteres que
    # PowerShell o las variables de entorno tengan que escapar.
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] 32
        $rng.GetBytes($bytes)
    }
    finally { $rng.Dispose() }

    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Protect-CaoReleasePassword {
    param(
        [Parameter(Mandatory = $true)][string]$Password,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Add-Type -AssemblyName System.Security
    $clear = [Text.Encoding]::UTF8.GetBytes($Password)
    $protected = [System.Security.Cryptography.ProtectedData]::Protect(
        $clear,
        $script:CaoReleasePasswordEntropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($Path, $protected)
}

function Unprotect-CaoReleasePassword {
    param([Parameter(Mandatory = $true)][string]$Path)

    Add-Type -AssemblyName System.Security
    $protected = [IO.File]::ReadAllBytes($Path)
    $clear = [System.Security.Cryptography.ProtectedData]::Unprotect(
        $protected,
        $script:CaoReleasePasswordEntropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    return [Text.Encoding]::UTF8.GetString($clear)
}

function Set-CaoReleaseKeyAcl {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [string[]]$Files = @()
    )

    if (-not (Test-Path $Directory)) { return }

    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

    # /T aplica la misma ACL a los ficheros y subdirectorios que ya existen:
    # sin /T, un PFX creado antes de endurecer la carpeta conserva su ACL
    # heredada. Luego se fija tambien el fichero de forma explicita.
    & icacls.exe $Directory /inheritance:r /grant:r "$($me):(OI)(CI)F" /T | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls fallo en $Directory" }

    foreach ($file in $Files) {
        if (Test-Path $file) {
            & icacls.exe $file /inheritance:r /grant:r "$($me):F" | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "icacls fallo en $file" }
        }
    }
}

function Resolve-CaoReleasePassword {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$PfxPath,
        [string]$ExplicitPassword,
        [string]$PasswordFile
    )

    if ([string]::IsNullOrEmpty($ExplicitPassword)) {
        $ExplicitPassword = [Environment]::GetEnvironmentVariable($Config.PasswordEnv)
    }
    if (-not [string]::IsNullOrEmpty($ExplicitPassword)) { return $ExplicitPassword }

    if ([string]::IsNullOrEmpty($PasswordFile)) {
        $PasswordFile = [Environment]::GetEnvironmentVariable($Config.PasswordFileEnv)
    }

    if (-not [string]::IsNullOrEmpty($PasswordFile)) {
        if (-not (Test-Path $PasswordFile -PathType Leaf)) {
            throw "No existe el fichero de contrasena indicado por $($Config.PasswordFileEnv): $PasswordFile"
        }

        return ([IO.File]::ReadAllText($PasswordFile)).Trim()
    }

    # Sin variable de entorno ni fichero: DPAPI junto al PFX. Solo el usuario
    # actual puede descifrarlo, y sigue siendo una copia local: el original
    # debe estar en el gestor de contrasenas / medio offline.
    $dpapiPath = Join-Path (Split-Path -Parent $PfxPath) $Config.PasswordName
    if (Test-Path $dpapiPath -PathType Leaf) {
        return Unprotect-CaoReleasePassword -Path $dpapiPath
    }

    return $null
}
