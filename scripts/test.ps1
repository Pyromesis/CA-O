# CA-O 2.0 - run the full test suite.
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

dotnet test CA-O.sln -c $Configuration --no-build --logger "console;verbosity=normal"
exit $LASTEXITCODE
