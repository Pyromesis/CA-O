$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repository 'CA-O.sln'
$artifactRoot = Join-Path $repository 'artifacts\release'
$uiOutput = Join-Path $artifactRoot 'ui'
$serviceOutput = Join-Path $artifactRoot 'service'

Remove-Item $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item $uiOutput, $serviceOutput -ItemType Directory -Force | Out-Null

dotnet restore $solution
dotnet build $solution --configuration Release --no-restore
dotnet test (Join-Path $repository 'tests\CA-O.Core.Tests\CA-O.Core.Tests.csproj') --configuration Release --no-restore
dotnet publish (Join-Path $repository 'src\CA-O.UI\CA-O.UI.csproj') --configuration Release --runtime win-x64 --self-contained false --output $uiOutput --no-restore
dotnet publish (Join-Path $repository 'src\CA-O.Privileged\CA-O.Privileged.csproj') --configuration Release --runtime win-x64 --self-contained false --output $serviceOutput --no-restore

Get-ChildItem $artifactRoot -File -Recurse |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $($_.Path.Substring($artifactRoot.Length + 1))" } |
    Set-Content (Join-Path $artifactRoot 'SHA256SUMS.txt')

Write-Host "Release artifacts written to $artifactRoot"