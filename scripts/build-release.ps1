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
dotnet publish (Join-Path $repository 'src\CA-O.UI\CA-O.UI.csproj') --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false --output $uiOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# Workaround WinUI 2.4: publish self-contained omite CA-O.UI.pri (necesario para XAML, HResult 0x802B000A)
$priSrc = Join-Path $repository 'src\CA-O.UI\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\CA-O.UI.pri'
if (Test-Path $priSrc) { Copy-Item $priSrc $uiOutput -Force; Write-Host "  patched CA-O.UI.pri -> $uiOutput" -ForegroundColor Yellow }
else { Write-Warning "CA-O.UI.pri no encontrado en $priSrc" }
# Asegurar recursos XAML completos (Assets, pri extras)
$assetsSrc = Join-Path $repository 'src\CA-O.UI\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\Microsoft.UI.Xaml'
if (Test-Path $assetsSrc) { Copy-Item $assetsSrc $uiOutput -Recurse -Force }

Write-Host '== publish privileged service ==' -ForegroundColor Cyan
dotnet publish (Join-Path $repository 'src\CA-O.Privileged\CA-O.Privileged.csproj') --configuration Release --runtime win-x64 --self-contained false --output $serviceOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== publish uninstaller ==' -ForegroundColor Cyan
$uninstallOutput = Join-Path $artifactRoot 'uninstall'
New-Item $uninstallOutput -ItemType Directory -Force | Out-Null
dotnet publish (Join-Path $repository 'src\CA-O.Uninstaller\CA-O.Uninstaller.csproj') --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=true /p:TreatWarningsAsErrors=false --output $uninstallOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== publish GUI installer (self-contained, no single-file - WinUI 3) ==' -ForegroundColor Cyan
$guiOutput = Join-Path $artifactRoot 'gui-installer'
New-Item $guiOutput -ItemType Directory -Force | Out-Null
dotnet publish (Join-Path $repository 'src\CA-O.InstallerGui\CA-O.InstallerGui.csproj') --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false /p:TreatWarningsAsErrors=false --output $guiOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# Para distribución: comprimir carpeta gui-installer; mantener exe suelto para compatibilidad
Copy-Item (Join-Path $guiOutput 'CA-O.InstallerGui.exe') (Join-Path $artifactRoot 'CA-O-Setup-GUI-x64.exe') -Force
# También empaquetar carpeta completa como zip para evitar pérdida de dlls
Compress-Archive -Path (Join-Path $guiOutput '*') -DestinationPath (Join-Path $artifactRoot 'CA-O-Setup-GUI-x64.zip') -Force

Write-Host '== publish console setup (fallback) ==' -ForegroundColor Cyan
$setupOutput = Join-Path $artifactRoot 'setup'
New-Item $setupOutput -ItemType Directory -Force | Out-Null
dotnet publish (Join-Path $repository 'src\CA-O.Setup\CA-O.Setup.csproj') --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=true /p:TreatWarningsAsErrors=false --output $setupOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== signing ==' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'sign.ps1') -Files @(
    (Join-Path $uiOutput 'CA-O.UI.exe'),
    (Join-Path $serviceOutput 'CA-O.Privileged.exe'),
    (Join-Path $artifactRoot 'CA-O-Setup-GUI-x64.exe'),
    (Join-Path $setupOutput 'CA-O.Setup.exe'),
    (Join-Path $uninstallOutput 'CA-O.Uninstaller.exe')
)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host '== SHA-256 manifest ==' -ForegroundColor Cyan
Get-ChildItem $artifactRoot -File -Recurse |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $($_.Path.Substring($artifactRoot.Length + 1))" } |
    Set-Content (Join-Path $artifactRoot 'SHA256SUMS.txt')

Write-Host "Release artifacts written to $artifactRoot" -ForegroundColor Green
