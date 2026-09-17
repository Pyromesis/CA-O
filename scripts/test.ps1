# CA-O 2.0 - run the full test suite.
param(
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet SDK no encontrado en PATH." }
Push-Location (Join-Path $PSScriptRoot "..")
try {
dotnet test CA-O.sln -c $Configuration --logger "console;verbosity=normal"
if ($null -eq $LASTEXITCODE -or $LASTEXITCODE -ne 0) { exit 1 }
} finally { Pop-Location }
