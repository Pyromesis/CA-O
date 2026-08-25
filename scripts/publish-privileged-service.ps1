$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'src\CA-O.Privileged\CA-O.Privileged.csproj'
$output = Join-Path $repository 'artifacts\service'

dotnet publish $project --configuration Release --runtime win-x64 --self-contained false --output $output
Write-Host "Published privileged service to $output"