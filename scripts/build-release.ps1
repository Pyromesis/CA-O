# CA-O 2.0 - full release pipeline (spec 119-120):
# restore -> build -> test -> verify gates -> publish UI+service -> sign -> hashes.
$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repository 'CA-O.sln'
$artifactRoot = Join-Path $repository 'artifacts\release'
$uiOutput = Join-Path $artifactRoot 'ui'
$serviceOutput = Join-Path $artifactRoot 'service'

Remove-Item $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item $uiOutput, $serviceOutput -ItemType Directory -Force | Out-Null

Write-Host '== restore ==' -ForegroundColor Cyan
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== build (Release) ==' -ForegroundColor Cyan
dotnet build $solution --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== tests (release gate) ==' -ForegroundColor Cyan
dotnet test $solution --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== publish UI ==' -ForegroundColor Cyan
dotnet publish (Join-Path $repository 'src\CA-O.UI\CA-O.UI.csproj') --configuration Release --runtime win-x64 --self-contained false --output $uiOutput --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== publish privileged service ==' -ForegroundColor Cyan
dotnet publish (Join-Path $repository 'src\CA-O.Privileged\CA-O.Privileged.csproj') --configuration Release --runtime win-x64 --self-contained false --output $serviceOutput --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== signing ==' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'sign.ps1') -Files @(
    (Join-Path $uiOutput 'CA-O.UI.exe'),
    (Join-Path $serviceOutput 'CA-O.Privileged.exe')
)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== SHA-256 manifest ==' -ForegroundColor Cyan
Get-ChildItem $artifactRoot -File -Recurse |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $($_.Path.Substring($artifactRoot.Length + 1))" } |
    Set-Content (Join-Path $artifactRoot 'SHA256SUMS.txt')

Write-Host "Release artifacts written to $artifactRoot" -ForegroundColor Green
