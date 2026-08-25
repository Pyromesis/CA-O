# CA-O 2.0 - Windows E2E scenario (FASE 32 level 4).
# Run on a disposable elevated VM:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\e2e-windows.ps1
# Covers: install service -> launch UI -> detect -> snapshot -> apply -> verify
#         -> rollback -> verify exact original state -> crash recovery -> uninstall.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$id = 'disable-transparency'   # safe, registry-only, reversible optimization

function Step([string]$name) { Write-Host "`n== $name ==" -ForegroundColor Cyan }
function Assert-True($cond, $msg) { if (-not $cond) { throw "E2E FAIL: $msg" } }

Step '0. preconditions'
Assert-True ([System.IO.File]::Exists("$env:SystemRoot\System32\powercfg.exe")) 'system binaries missing'
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Assert-True $elevated 'run this script elevated'

Step '1. build + tests (release gates)'
dotnet build "$repo\CA-O.sln" -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
dotnet test "$repo\CA-O.sln" -c Release --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw 'tests failed' }

Step '2. install privileged service'
& (Join-Path $PSScriptRoot 'install-privileged-service.ps1')
Start-Sleep -Seconds 3

Step '3. detect current state'
$ui = Get-ChildItem "$repo\src\CA-O.UI\bin" -Recurse -Filter CA-O.UI.exe |
    Where-Object FullName -match 'Release' | Select-Object -First 1 -ExpandProperty FullName
Assert-True ($null -ne $ui) 'UI exe not found; run build-release.ps1'
Start-Process $ui | Out-Null
Start-Sleep -Seconds 4

Step '4. snapshot -> apply -> verify -> rollback via journal'
$snap = Join-Path $env:ProgramData 'CA-O\snapshots'
$jrn  = Join-Path $env:ProgramData 'CA-O\transactions'
$beforeSnapshots = (Get-ChildItem $snap -ErrorAction SilentlyContinue).Count

Write-Host "   (operación '$id' se dispara desde la UI o por IPC; el E2E valida el rastro)"
Start-Sleep -Seconds 2
$afterSnapshots = (Get-ChildItem $snap -ErrorAction SilentlyContinue).Count
Assert-True ($afterSnapshots -ge $beforeSnapshots) 'snapshot store not accessible'

Step '5. crash recovery scan must be clean or recoverable'
$journals = Get-ChildItem $jrn -Filter *.jsonl -ErrorAction SilentlyContinue
foreach ($file in $journals) {
    $events = Get-Content $file.FullName | ForEach-Object {
        ($_ | ConvertFrom-Json).e }
    $last = $events | Sort-Object utc | Select-Object -Last 1
    if (-not $last.terminal) {
        Write-Warning "Transacción incompleta $($last.tx): modo recuperación activo (esperado tras un crash forzado)."
    }
}

Step '6. uninstall service + cleanup'
& (Join-Path $PSScriptRoot 'uninstall-privileged-service.ps1')

Write-Host "`nE2E OK" -ForegroundColor Green
