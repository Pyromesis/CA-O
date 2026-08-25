$ErrorActionPreference = 'Stop'

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