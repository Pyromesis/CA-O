<#
.SYNOPSIS
    Deriva los 80 frames de la mascota CA-O desde 3 masters OGA.

.DESCRIPTION
    Los frames antiguos se redibujaban con SD fotograma a fotograma (popping,
    gato gordo / gato flaco, doble cola). Este script deriva los 8 frames de
    cada mood por geometría determinista desde UNA sola silueta master por
    postura, así que la forma del gato es idéntica en todo el ciclo y solo se
    mueve (bote, salto, tembleque, respiración):

        master-sit.png  -> idle, working, warn
        master-jump.png -> celebrate
        master-c.png    -> sleep (loaf; con apertura morfológica anti-bigotes)

    Salida (sobrescribe los frames actuales):

        assets/mascot/frames/<tema>/<mood>/f00.png .. f07.png

    Reutiliza el pipeline del generador (umbral -> apertura -> mayor
    componente -> huecos -> anclaje de suelo -> color del tema): importado con
    dot-source, que es seguro gracias a la guardia de este último.

.EXAMPLE
    pwsh -File scripts/derive-mascot-frames.ps1
    pwsh -File scripts/derive-mascot-frames.ps1 -Mood Celebrate
#>
param(
    [string]$MastersDir = '',
    [string]$Mood = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
if (-not $MastersDir) { $MastersDir = Join-Path $repoRoot 'artifacts\mascot-preview\gen\masters' }
$framesRoot = Join-Path $repoRoot 'assets\mascot\frames'

# Funciones del generador (umbral, apertura, specks, huecos, anclaje, export).
$genPath = Join-Path $scriptRoot 'generate-mascot-frames.ps1'
if (-not (Test-Path $genPath)) { throw "no se encuentra el generador en $genPath" }
. $genPath
if (-not (Get-Command Export-CaoAppFrame -ErrorAction SilentlyContinue)) {
    throw "el generador no expuso Export-CaoAppFrame tras importar $genPath"
}

$themes = @(
    @{ Name = 'light'; Color = '#171C26' },
    @{ Name = 'dark'; Color = '#F2F4F8' }
)

# Tablas de movimiento por mood. Unidades: Shift en px de lienzo (256),
# ScaleMul relativo. Los 8 valores forman un bucle cerrado (f07 empalma f00).
function Get-CaoDeriveTable {
    param([string]$Name)
    switch ($Name) {
        'Idle' {
            # Respiración sentada: bote sinusoidal suave, amplitud visible a 64px.
            return @{ Master = 'master-sit.png'; Open = 0
                SX = @(0, 0, 0, 0, 0, 0, 0, 0)
                SY = @(0, -4, -5, -4, 0, 4, 5, 4)
                SM = @(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0) }
        }
        'Working' {
            # Tecleo: doble bote rápido con vaivén lateral.
            return @{ Master = 'master-sit.png'; Open = 0
                SX = @(0, 1, 0, -1, 0, 1, 0, -1)
                SY = @(0, -3, -6, -3, 0, -3, -6, -3)
                SM = @(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0) }
        }
        'Celebrate' {
            # Salto: agachado -> subida -> cima -> caída -> aterrizaje -> reposo.
            return @{ Master = '__jump__'; Open = 0
                SX = @(0, 0, 0, 0, 0, 0, 0, 0)
                SY = @(0, -14, -26, -34, -26, -14, 0, 0)
                SM = @(0.94, 1.0, 1.0, 1.0, 1.0, 1.0, 0.94, 0.97) }
        }
        'Warn' {
            # Alerta: tembleque lateral rápido, casi sin desplazamiento vertical.
            return @{ Master = 'master-sit.png'; Open = 0
                SX = @(0, 2, -2, 3, -3, 2, -2, 0)
                SY = @(0, -1, 0, -1, 0, -1, 0, 0)
                SM = @(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0) }
        }
        'Sleep' {
            # Dormido: respiración por escala (se hincha / se deshincha el loaf).
            return @{ Master = 'master-c.png'; Open = 2
                SX = @(0, 0, 0, 0, 0, 0, 0, 0)
                SY = @(0, 0, -1, -1, 0, 0, 0, 0)
                SM = @(1.0, 1.012, 1.02, 1.012, 1.0, 0.992, 0.985, 0.992) }
        }
        default { throw "mood desconocido: $Name" }
    }
}

# El master de salto sale del motor a 1024: se baja a 512 una vez para que
# los 16 exports no paguen el coste de GetPixel millonario.
$jumpMaster = Join-Path $MastersDir 'master-jump.png'
if (-not (Test-Path $jumpMaster)) { throw "falta el master de salto en $jumpMaster" }
$jumpSrc = $jumpMaster
$probe = New-Object System.Drawing.Bitmap($jumpMaster)
if ($probe.Width -gt 600) {
    $nw = 512
    $nh = [int][math]::Round($probe.Height * (512.0 / $probe.Width))
    $small = New-Object System.Drawing.Bitmap($nw, $nh)
    $g = [System.Drawing.Graphics]::FromImage($small)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::White)
    $g.DrawImage($probe, 0, 0, $nw, $nh)
    $g.Dispose()
    $jumpSrc = Join-Path $env:TEMP 'cao-jump-512.png'
    $small.Save($jumpSrc, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    Write-Host ("  salto reducido a {0}x{1}" -f $nw, $nh)
}
$probe.Dispose()

$moods = if ($Mood) { @($Mood) } else { @('Idle', 'Working', 'Celebrate', 'Warn', 'Sleep') }
$failures = @()

foreach ($moodName in $moods) {
    $table = Get-CaoDeriveTable -Name $moodName
    $src = if ($table.Master -eq '__jump__') { $jumpSrc } else { Join-Path $MastersDir $table.Master }
    if (-not (Test-Path $src)) { throw "falta el master $src" }
    foreach ($theme in $themes) {
        $outDir = Join-Path $framesRoot (Join-Path $theme.Name $moodName.ToLowerInvariant())
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        for ($i = 0; $i -lt 8; $i++) {
            $outFile = Join-Path $outDir ("f{0:d2}.png" -f $i)
            $ok = Export-CaoAppFrame -SourcePath $src -OutPath $outFile -Color $theme.Color -Mirror `
                -ShiftX $table.SX[$i] -ShiftY $table.SY[$i] -ScaleMul $table.SM[$i] -OpenRadius $table.Open
            if (-not $ok) { $failures += "$($theme.Name)/$($moodName.ToLowerInvariant())/f$($i.ToString('d2'))" }
        }
        Write-Host ("  {0}/{1}: 8 frames" -f $theme.Name, $moodName.ToLowerInvariant()) -ForegroundColor Green
    }
}

if ($failures.Count -gt 0) { throw ("frames inválidos: " + ($failures -join ', ')) }
Write-Host 'Mascota derivada (80 frames, 0 SD, 0 popping).' -ForegroundColor Green
