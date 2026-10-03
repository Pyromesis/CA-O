$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'src\CA-O.Privileged\CA-O.Privileged.csproj'
$output = Join-Path $repository 'artifacts\service'

dotnet publish $project --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false --output $output
if (-not (Test-Path (Join-Path $output 'coreclr.dll'))) { throw "Privileged service payload is not self-contained (coreclr.dll missing in $output)" }
Write-Host "Published privileged service to $output"