# CA-O 2.0 - Harden ACLs on %ProgramData%\CA-O (FASE 14).
# SYSTEM: full, Administrators: modify, Users: read+execute only.
# Run from an ELEVATED console.

$ErrorActionPreference = 'Stop'
$root = Join-Path $env:ProgramData 'CA-O'

if (-not (Test-Path $root)) {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
}

Write-Host "Endureciendo ACLs en $root" -ForegroundColor Cyan

# Disable inheritance and drop inherited ACEs; explicit grants only.
& icacls $root /inheritance:r | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls /inheritance fallo ($LASTEXITCODE)" }

& icacls $root /grant 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)M' 'Users:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls /grant fallo ($LASTEXITCODE)" }

# Snapshots/history/transactions must not be user-writable either.
foreach ($sub in @('snapshots', 'transactions', 'benchmarks')) {
    $dir = Join-Path $root $sub
    if (Test-Path $dir) {
        & icacls $dir /inheritance:r /grant 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)M' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls fallo en $dir" }
    }
}

Set-Content -Path (Join-Path $root 'acls-hardened.flag') -Value ("hardened $(Get-Date -Format o)")

Write-Host 'ACLs endurecidas.' -ForegroundColor Green
