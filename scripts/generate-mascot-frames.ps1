<#
.SYNOPSIS
    Genera los frames de la mascota CA-O (silueta de gato frame a frame).

.DESCRIPTION
    La mascota es un flipbook de 8 PNG por mood. Este script produce las hojas
    finales que consume la app:

        assets/mascot/frames/<tema>/<mood>/f00.png .. f07.png

    Temas: light (silueta oscura) y dark (silueta clara).
    Moods: idle, working, celebrate, warn, sleep.

    Pipeline:
      1. (-Regenerate) genera las imágenes crudas con el motor local
         Open Generative AI -> stable-diffusion.cpp (sd-cli.exe) + DreamShaper 8:
         text2img para las poses clave de cada mood (movimientos grandes que el
         img2img no alcanza) e img2img de baja fuerza para los intermedios, que es
         lo que da la sensación de animación dibujada fotograma a fotograma.
      2. Limpia cada crudo: umbral -> mayor componente conexa -> relleno de
         huecos -> alineación (misma línea de suelo y mismo anclaje) ->
         espejado -> color del tema con alfa suavizado -> PNG 256x256.

    Sin -Regenerate se reutilizan los crudos ya presentes en -RawDir, así que el
    post-procesado es determinista y no necesita GPU.

.PARAMETER Regenerate
    Vuelve a generar los crudos con el motor local (requiere la app instalada).

.PARAMETER RawDir
    Carpeta de crudos. Por defecto artifacts/mascot-preview/gen (ignorada por git).

.PARAMETER Mood
    Limita el trabajo a un mood (Idle, Working, Celebrate, Warn, Sleep).

.EXAMPLE
    pwsh -File scripts/generate-mascot-frames.ps1 -Regenerate
    pwsh -File scripts/generate-mascot-frames.ps1 -Mood Idle
#>
param(
    [switch]$Regenerate,
    [string]$RawDir = '',
    [string]$Mood = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
if (-not $RawDir) { $RawDir = Join-Path $repoRoot 'artifacts\mascot-preview\gen' }
$framesRoot = Join-Path $repoRoot 'assets\mascot\frames'

# --------------------------------------------------- engine (local AI, opcional)
$sdBin = Join-Path $env:APPDATA 'open-generative-ai\local-ai\bin'
$sdExe = Join-Path $sdBin 'sd-cli.exe'
$sdModel = Join-Path $env:APPDATA 'open-generative-ai\local-ai\models\DreamShaper_8_pruned.safetensors'

$stylePrompt = 'black cat silhouette, solid black flat vector shape, no internal details, ' +
'minimalist icon, clean sharp edges, opaque pure black, floating on a plain pure white background, ' +
'no ground, no floor, no shadow, high contrast, simple, exactly one single tail, one tail only'
$styleNegative = 'photo, realistic, fur, eyes, whiskers, gradient, shading, gray, blurry, glow, text, ' +
'watermark, multiple cats, background, color, 3d render, frame, border, noise, outline, soft shadows, ' +
'painting, sketch, two tails, second tail, extra tail, duplicate tail, extra limbs, two cats, ' +
'ground, floor, ground plane, rectangle, box, pedestal, stage, block, shadow on the ground, wall'
function Invoke-CaoSd {
    param(
        [string]$Prompt, [string]$Out, [int]$Seed, [string]$Init = '',
        [double]$Strength = 0.5, [int]$Steps = 26, [int]$Width = 512, [int]$Height = 512
    )
    if (-not (Test-Path $sdExe)) {
        throw "Motor local no encontrado en $sdExe. Abra Open Generative AI una vez para instalarlo."
    }
    if (-not (Test-Path $sdModel)) { throw "Modelo no encontrado en $sdModel" }
    $argLine = "-m `"$sdModel`" -p `"$Prompt`" -o `"$Out`" -W $Width -H $Height " +
    "--steps $Steps --cfg-scale 7 -s $Seed --sampling-method euler_a -n `"$styleNegative`""
    if ($Init) { $argLine += " -i `"$Init`" --strength $Strength" }
    if (Test-Path $Out) { Remove-Item $Out -Force }

    for ($attempt = 1; $attempt -le 2; $attempt++) {
        # El segundo intento mueve los pesos a RAM: la VRAM puede estar ocupada
        # por otras apps y el motor falla con "cannot make enough memory".
        $line = if ($attempt -eq 1) { $argLine } else { "$argLine --offload-to-cpu --vae-tiling" }
        $pr = Start-Process -FilePath $sdExe -ArgumentList $line -WorkingDirectory $sdBin `
            -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$Out.log" -RedirectStandardError "$Out.err"
        if (Test-CaoPngReadable -Path $Out) { return }
        Write-Warning ("intento {0} fallido para {1}; reintentando" -f $attempt, (Split-Path $Out -Leaf))
    }
    throw "sd-cli no produjo un PNG válido en $Out"
}

# Un fallo del motor puede dejar un PNG a medias: sin esta comprobación, el
# post-procesado revienta al abrirlo en vez de regenerar el fotograma.
function Test-CaoPngReadable {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    if ((Get-Item $Path).Length -lt 1024) { return $false }
    try {
        $probe = New-Object System.Drawing.Bitmap($Path)
        $ok = $probe.Width -gt 0 -and $probe.Height -gt 0
        $probe.Dispose()
        return $ok
    }
    catch { return $false }
}

# ------------------------------------------------ limpieza y alineación del arte
# El estado de la máscara viaja en un hashtable porque PowerShell aplana los
# arrays bidimensionales al pasarlos entre funciones.
function New-CaoMaskState {
    param([int]$W, [int]$H)
    return @{
        W = $W; H = $H
        M = (New-Object 'bool[,]' $W, $H)
        Lum = (New-Object 'double[,]' $W, $H)
    }
}

function Get-CaoMaskFromImage {
    param($State, [string]$Path, [int]$Threshold = 150)
    $src = New-Object System.Drawing.Bitmap($Path)
    $w = $src.Width; $h = $src.Height
    $mask = New-Object 'bool[,]' $w, $h
    $lum = New-Object 'double[,]' $w, $h
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $src.GetPixel($x, $y)
            $l = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
            $lum[$x, $y] = $l
            if ($l -lt $Threshold) { $mask[$x, $y] = $true }
        }
    }
    $src.Dispose()
    $State.W = $w; $State.H = $h; $State.M = $mask; $State.Lum = $lum
    return $State
# Conserva solo la mayor región oscura conexa (descarta motas y restos).
function Remove-CaoSpecks {
    param($State)
    $mask = $State.M; $w = $State.W; $h = $State.H
    $label = New-Object 'int[,]' $w, $h
    $best = 0; $bestArea = 0; $current = 0
    $queue = New-Object System.Collections.Queue
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not $mask[$x, $y] -or $label[$x, $y] -ne 0) { continue }
            $current++
            $area = 0
            $queue.Clear(); $queue.Enqueue(($x * $h + $y)); $label[$x, $y] = $current
            while ($queue.Count -gt 0) {
                $code = $queue.Dequeue()
                $px = [int][math]::Floor($code / $h); $py = $code % $h
                $area++
                foreach ($d in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
                    $nx = $px + $d[0]; $ny = $py + $d[1]
                    if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
                    if (-not $mask[$nx, $ny] -or $label[$nx, $ny] -ne 0) { continue }
                    $label[$nx, $ny] = $current
                    $queue.Enqueue(($nx * $h + $ny))
                }
            }
            if ($area -gt $bestArea) { $bestArea = $area; $best = $current }
        }
    }
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($label[$x, $y] -ne $best) { $mask[$x, $y] = $false }
        }
    }
    $State.M = $mask
    return $State
}

# Rellena huecos internos (ojos, bigotes, oreja) -> silueta maciza de un color.
function Close-CaoHoles {
    param($State)
    $mask = $State.M; $w = $State.W; $h = $State.H
    $outside = New-Object 'bool[,]' $w, $h
    $queue = New-Object System.Collections.Queue
    for ($x = 0; $x -lt $w; $x++) {
        foreach ($y in @(0, ($h - 1))) {
            if (-not $mask[$x, $y] -and -not $outside[$x, $y]) {
                $outside[$x, $y] = $true; $queue.Enqueue(($x * $h + $y))
            }
        }
    }
    for ($y = 0; $y -lt $h; $y++) {
        foreach ($x in @(0, ($w - 1))) {
            if (-not $mask[$x, $y] -and -not $outside[$x, $y]) {
                $outside[$x, $y] = $true; $queue.Enqueue(($x * $h + $y))
            }
        }
    }
    while ($queue.Count -gt 0) {
        $code = $queue.Dequeue()
        $px = [int][math]::Floor($code / $h); $py = $code % $h
        foreach ($d in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
            $nx = $px + $d[0]; $ny = $py + $d[1]
            if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
            if ($mask[$nx, $ny] -or $outside[$nx, $ny]) { continue }
            $outside[$nx, $ny] = $true
            $queue.Enqueue(($nx * $h + $ny))
        }
    }
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not $outside[$x, $y]) { $mask[$x, $y] = $true }
        }
    }
    $State.M = $mask
    return $State
}
function Get-CaoMaskStats {
    param($State)
    $mask = $State.M; $w = $State.W; $h = $State.H
    $minX = $w; $maxX = -1; $minY = $h; $maxY = -1
    $sumX = 0.0; $sumY = 0.0; $n = 0
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not $mask[$x, $y]) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            $sumX += $x; $sumY += $y; $n++
        }
    }
    return @{
        MinX = $minX; MaxX = $maxX; MinY = $minY; MaxY = $maxY
        Cx = ($sumX / [math]::Max(1, $n)); Cy = ($sumY / [math]::Max(1, $n)); Area = $n
    }
}

# Anclaje horizontal: centroide de la banda inferior (patas/base), que apenas se
# mueve mientras la cabeza o la cola animan. Mantiene los frames sin temblar.
function Get-CaoGroundAnchor {
    param($State, $Stats, [double]$Band = 0.18)
    $mask = $State.M; $w = $State.W
    $from = [int]($Stats.MaxY - ($Stats.MaxY - $Stats.MinY) * $Band)
    $sum = 0.0; $n = 0
    for ($y = $from; $y -le $Stats.MaxY; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($mask[$x, $y]) { $sum += $x; $n++ }
        }
    }
    if ($n -eq 0) { return [double]$Stats.Cx }
    return [double]($sum / $n)
}

# Compone un frame final: color del tema, alfa suavizado en el borde, misma
# escala y misma línea de suelo para todos los frames del ciclo.
function Export-CaoAppFrame {
    param(
        [string]$SourcePath,
        [string]$OutPath,
        [string]$Color = '#171C26',
        [int]$Canvas = 256,
        [int]$TargetHeight = 200,
        [int]$GroundY = 246,
        [int]$Threshold = 150,
        [double]$Feather = 40.0,
        [switch]$Mirror
    )
    $state = New-CaoMaskState -W 1 -H 1
    $state = Get-CaoMaskFromImage -State $state -Path $SourcePath -Threshold $Threshold
    $state = Remove-CaoSpecks -State $state
    $state = Close-CaoHoles -State $state
    $stats = Get-CaoMaskStats -State $state
    if ($stats.Area -lt 400) {
        Write-Warning ("silueta demasiado pequeña en {0} ({1} px)" -f (Split-Path $SourcePath -Leaf), $stats.Area)
        return $false
    }
    if ($stats.Area -gt ($state.W * $state.H * 0.7)) {
        Write-Warning ("fondo fusionado con el gato en {0}: use un crudo con fondo blanco" -f (Split-Path $SourcePath -Leaf))
        return $false
    }
    $anchor = Get-CaoGroundAnchor -State $state -Stats $stats
    # Espejado horizontal: la silueta de referencia mira a la derecha.
    if ($Mirror) { $anchor = $state.W - $anchor }

    $mask = $state.M; $lum = $state.Lum
    $w = $state.W; $h = $state.H
    $col = [System.Drawing.ColorTranslator]::FromHtml($Color)
    $sprite = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not $mask[$x, $y]) { continue }
            $a = 255.0
            if ($lum[$x, $y] -gt ($Threshold - $Feather)) {
                $a = 255.0 * ($Threshold - $lum[$x, $y]) / $Feather
            }
            $a = [math]::Max(0, [math]::Min(255, $a))
            $dx = if ($Mirror) { $w - 1 - $x } else { $x }
            $sprite.SetPixel($dx, $y, [System.Drawing.Color]::FromArgb([int]$a, $col.R, $col.G, $col.B))
        }
    }

    $scale = $TargetHeight / [double](($stats.MaxY - $stats.MinY) + 1)
    # El gato tumbado es ancho: se limita también por ancho para que nunca se
    # corte en el lienzo (si no, la silueta se sale y el frame aparece cortado).
    $maxWidth = $Canvas - 16.0
    if ($w * $scale -gt $maxWidth) { $scale = $maxWidth / $w }
    $dstW = [int][math]::Round($w * $scale)
    $dstH = [int][math]::Round($h * $scale)
    $dstX = [int][math]::Round(($Canvas / 2.0) - ($anchor * $scale))
    $dstY = [int][math]::Round($GroundY - (($stats.MaxY + 1) * $scale))

    $out = New-Object System.Drawing.Bitmap($Canvas, $Canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($sprite, (New-Object System.Drawing.Rectangle($dstX, $dstY, $dstW, $dstH)))
    $g.Dispose()
    $sprite.Dispose()
    $out.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    return $true
}
# Poses clave por mood: movimientos grandes que el img2img no alcanza (saltar,
# dormir, bufar, arqueo) se dibujan de cero con text2img. Los intermedios se
# derivan de ellas con img2img de baja fuerza.
function Get-CaoKeyPoses {
    param([string]$Name)
    switch ($Name) {
        'Working' {
            return @(
                @{ Name = 'pawup'; Seed = 4100; Prompt = 'sitting with one front paw raised high in the air, about to tap down, head slightly lowered, tail curled up' },
                @{ Name = 'pawup2'; Seed = 4101; Prompt = 'sitting with one front paw lifted very high reaching up, head up, tail curved behind' })
        }
        'Celebrate' {
            return @(
                @{ Name = 'crouch'; Seed = 4000; Prompt = 'crouching low on the ground about to jump, body compressed, tail low and curled around the paws' },
                @{ Name = 'jump'; Seed = 4001; Prompt = 'leaping up in the air, body stretched, fully airborne, front paws lifted, tail sweeping down and back, ears up, joyful jump' },
                @{ Name = 'land'; Seed = 4002; Prompt = 'landing on the ground with front paws planted forward, body low, tail raised and curved, ears forward' })
        }
        'Warn' {
            return @(
                @{ Name = 'alert'; Seed = 4200; Prompt = 'sitting alert, ears pointing forward, tail raised and curving up, tense attentive posture' },
                @{ Name = 'flat'; Seed = 4201; Prompt = 'ears flattened back against the head, body tense, tail lifted and puffed, hissing, defensive posture' },
                @{ Name = 'arch'; Seed = 4202; Prompt = 'back arched high, four legs straight, tail straight up and bristled, ears flattened, startled defensive posture' })
        }
        'Sleep' {
            return @(
                @{ Name = 'loaf'; Seed = 4300; Prompt = 'sleeping curled in a loaf on the ground, front paws tucked under the chest, eyes closed, head lowered, tail wrapped along the side' })
        }
        default { return @() }
    }
}

# Resuelve la imagen cruda de una pose de partida ('base' = silueta de referencia).
# Las poses clave declaradas en el mismo mood ya están dibujadas en su fotograma.
function Get-CaoPoseRawPath {
    param([string]$RawDir, [string]$Mood, [string]$Name, [string]$BaseImage, [string]$MoodRawDir = '', $Plan = $null)
    if ($Name -eq 'base') { return $BaseImage }
    if ($MoodRawDir -and $Plan) {
        for ($k = 0; $k -lt $Plan.Count; $k++) {
            if ($Plan[$k].ContainsKey('Key') -and $Plan[$k]['Key'] -eq $Name) {
                $candidate = Join-Path $MoodRawDir ("raw-{0:d2}.png" -f $k)
                if (Test-Path $candidate) { return $candidate }
            }
        }
    }
    foreach ($other in @('working', 'celebrate', 'warn', 'sleep')) {
        $candidate = Join-Path (Join-Path $RawDir ("key-" + $other)) ("raw-" + $Name + ".png")
        if (Test-Path $candidate) { return $candidate }
    }
    throw "no encuentro el crudo de la pose '$Name' para el mood $Mood"
}

# Plan de los 8 fotogramas del ciclo. Cada entrada es una pose clave (Key) o un
# intermedio (From + Strength) derivado de una pose. El fotograma 8 empalma con
# el 1 para que el bucle no dé un salto visible.
function Get-CaoMoodPlan {
    param([string]$Name)
    $sit = 'sitting calm on the ground, tail curled up in a high loop above the back'
    switch ($Name) {
        'Idle' {
            return @(
                @{ From = 'base'; Strength = 0.30; Prompt = 'sitting calm on the ground, tail curled up in a high loop above the back' },
                @{ From = 'base'; Strength = 0.35; Prompt = 'sitting calm, tail curled high, tip swaying to the right' },
                @{ From = 'base'; Strength = 0.45; Prompt = 'sitting calm, tail curled high, tip swung far right and up' },
                @{ From = 'base'; Strength = 0.40; Prompt = 'sitting calm, tail curled, tip coming back to the centre' },
                @{ From = 'base'; Strength = 0.45; Prompt = 'sitting calm, tail lowered, tip sweeping down' },
                @{ From = 'base'; Strength = 0.50; Prompt = 'sitting calm, tail low, tip swung to the left' },
                @{ From = 'base'; Strength = 0.40; Prompt = 'sitting calm, tail rising again, tip moving right' },
                @{ From = 'base'; Strength = 0.30; Prompt = 'sitting calm, tail curled high, tip hanging to the right' })
        }
        'Working' {
            return @(
                @{ Key = 'pawup'; Prompt = 'sitting cat with one front paw raised high in the air, paw lifted up in front of the chest, head slightly lowered, tail curled up' },
                @{ From = 'pawup'; Strength = 0.35; Prompt = 'sitting cat, the raised front paw coming down a little, head following the paw, tail curled' },
                @{ From = 'base'; Strength = 0.40; Prompt = 'sitting cat with the front paw down on the ground, head lowered looking down, tail curled up' },
                @{ From = 'pawup2'; Strength = 0.40; Prompt = 'sitting cat, front paw lifted and stretched forward, head lifted, tail flicking right' },
                @{ From = 'base'; Strength = 0.35; Prompt = 'sitting cat with the front paw down, tail flicking to the left, alert' },
                @{ Key = 'pawup2'; Prompt = 'sitting cat with one front paw lifted very high reaching up, head up, tail curved behind' },
                @{ From = 'pawup2'; Strength = 0.35; Prompt = 'sitting cat, front paw coming down again, head following it, tail curling' },
                @{ From = 'base'; Strength = 0.30; Prompt = 'sitting cat with the paw down, tail curled up, ready to tap again' })
        }
        'Celebrate' {
            return @(
                @{ Key = 'crouch'; Prompt = 'crouching low on the ground about to jump, body compressed, tail low and curled around the paws' },
                @{ From = 'crouch'; Strength = 0.45; Prompt = 'starting to spring up, back legs pushing, front paws lifting off the ground, tail sweeping back' },
                @{ Key = 'jump'; Prompt = 'leaping up in the air, body stretched, fully airborne, front paws lifted, tail sweeping down and back, ears up, joyful jump' },
                @{ From = 'jump'; Strength = 0.40; Prompt = 'at the top of the jump, completely airborne, tail high, paws tucked, ears up' },
                @{ From = 'jump'; Strength = 0.45; Prompt = 'coming down from the jump, front paws reaching forward, tail coming round' },
                @{ From = 'land'; Strength = 0.45; Prompt = 'landing on the ground, body compressed, front paws down, tail curled around' },
                @{ Key = 'land'; Prompt = 'landing on the ground with front paws planted forward, body low, tail raised and curved, ears forward' },
                @{ From = 'crouch'; Strength = 0.40; Prompt = 'settling down on the ground again, tail curling around the paws, ready to jump' })
        }
'Warn' {
            return @(
                @{ Key = 'alert'; Prompt = 'sitting alert, ears pointing forward, tail raised and curving up, tense attentive posture' },
                @{ From = 'alert'; Strength = 0.40; Prompt = 'sitting tense, ears starting to tilt back, tail lifting higher' },
                @{ Key = 'flat'; Prompt = 'ears flattened back against the head, body tense, tail lifted and puffed, hissing, defensive posture' },
                @{ From = 'flat'; Strength = 0.40; Prompt = 'hissing with ears flat, tail puffed and bristled, leaning forward' },
                @{ Key = 'arch'; Prompt = 'back arched high, four legs straight, tail straight up and bristled, ears flattened, startled defensive posture' },
                @{ From = 'arch'; Strength = 0.40; Prompt = 'back arched, tail bristled and upright, body tense, ears flat' },
                @{ From = 'flat'; Strength = 0.40; Prompt = 'head turned to the side, ears flat, tail bristled, tense' },
                @{ From = 'alert'; Strength = 0.35; Prompt = 'sitting alert again, ears forward, tail lifted and curving' })
        }
        'Sleep' {
            return @(
                @{ Key = 'loaf'; Prompt = 'sleeping curled in a loaf on the ground, front paws tucked under the chest, eyes closed, head lowered, tail wrapped along the side' },
                @{ From = 'loaf'; Strength = 0.30; Prompt = 'sleeping in a loaf, head sinking a little lower, tail wrapped, eyes closed' },
                @{ From = 'loaf'; Strength = 0.30; Prompt = 'sleeping in a loaf, breathing in, body slightly rounder, eyes closed' },
                @{ From = 'loaf'; Strength = 0.35; Prompt = 'sleeping in a loaf, head resting right on the paws, eyes closed, tail tucked' },
                @{ From = 'loaf'; Strength = 0.30; Prompt = 'sleeping in a loaf, breathing out, body slightly flatter, eyes closed' },
                @{ From = 'loaf'; Strength = 0.35; Prompt = 'sleeping in a loaf, tail curled tighter around the body, eyes closed' },
                @{ From = 'loaf'; Strength = 0.30; Prompt = 'sleeping in a loaf, head sinking lower, completely relaxed, eyes closed' },
                @{ From = 'loaf'; Strength = 0.25; Prompt = 'sleeping curled in a loaf on the ground, front paws tucked, eyes closed, tail wrapped' })
        }
        default { throw "mood desconocido: $Name" }
    }
}

# ------------------------------------------------------------- flujo principal
# La silueta base (una sola cola, estilo de la referencia) de la que parten los
# ciclos. Debe ser un PNG con FONDO BLANCO: un PNG transparente se aplana a
# negro dentro del motor y rompe la detección de silueta.
$baseImage = Join-Path $RawDir 'base2\raw-1234.png'

# Tema claro: silueta oscura. Tema oscuro: silueta clara.
$themes = @(
    @{ Name = 'light'; Color = '#171C26' },
    @{ Name = 'dark'; Color = '#F2F4F8' }
)

$moods = if ($Mood) { @($Mood) } else { @('Idle', 'Working', 'Celebrate', 'Warn', 'Sleep') }

Write-Host "CA-O mascota - frames frame a frame" -ForegroundColor Cyan
Write-Host "  crudos:  $RawDir"
Write-Host "  salida:  $framesRoot"
Write-Host "  moods:   $($moods -join ', ')"

if ($Regenerate) {
    if (-not (Test-Path $baseImage)) {
        throw "falta la silueta base en $baseImage (silueta de referencia, fondo blanco)"
    }
    foreach ($moodName in $moods) {
        foreach ($keyPose in (Get-CaoKeyPoses -Name $moodName)) {
            $keyDir = Join-Path $RawDir ("key-" + $moodName.ToLowerInvariant())
            New-Item -ItemType Directory -Force -Path $keyDir | Out-Null
            $keyRaw = Join-Path $keyDir ("raw-" + $keyPose.Name + ".png")
            Invoke-CaoSd -Prompt "$($keyPose.Prompt), $stylePrompt" -Out $keyRaw -Seed $keyPose.Seed -Steps 28
            Write-Host ("  {0}: pose clave '{1}' lista" -f $moodName, $keyPose.Name)
        }
    }
}

foreach ($moodName in $moods) {
    $plan = Get-CaoMoodPlan -Name $moodName
    $moodRaw = Join-Path $RawDir $moodName.ToLowerInvariant()
    New-Item -ItemType Directory -Force -Path $moodRaw | Out-Null

    # Dos pasadas: primero las poses clave (text2img) y luego los intermedios, que
    # pueden depender de una pose clave situada más adelante en el ciclo.
    $order = @()
    for ($i = 0; $i -lt $plan.Count; $i++) { if (-not $plan[$i].ContainsKey('From')) { $order += $i } }
    for ($i = 0; $i -lt $plan.Count; $i++) { if ($plan[$i].ContainsKey('From')) { $order += $i } }

    foreach ($i in $order) {
        $raw = Join-Path $moodRaw ("raw-{0:d2}.png" -f $i)
        if (Test-Path $raw) { continue }
        if (-not $Regenerate) { throw "falta el crudo $raw (ejecute con -Regenerate)" }
        $prompt = "$($plan[$i].Prompt), $stylePrompt"
        if ($plan[$i].ContainsKey('From')) {
            $init = Get-CaoPoseRawPath -RawDir $RawDir -Mood $moodName -Name $plan[$i].From `
                -BaseImage $baseImage -MoodRawDir $moodRaw -Plan $plan
            Invoke-CaoSd -Prompt $prompt -Out $raw -Seed (5000 + $i) -Steps 26 `
                -Init $init -Strength $plan[$i].Strength
            Write-Host ("  {0} crudo {1:d2} intermedio" -f $moodName, $i)
        }
        else {
            Invoke-CaoSd -Prompt $prompt -Out $raw -Seed (5000 + $i) -Steps 28
            Write-Host ("  {0} crudo {1:d2} pose clave" -f $moodName, $i)
        }
    }

    foreach ($theme in $themes) {
        $outDir = Join-Path $framesRoot (Join-Path $theme.Name $moodName.ToLowerInvariant())
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        for ($i = 0; $i -lt $plan.Count; $i++) {
            $raw = Join-Path $moodRaw ("raw-{0:d2}.png" -f $i)
            if (-not (Test-Path $raw)) { throw "falta el crudo $raw (ejecute con -Regenerate)" }
            $outFile = Join-Path $outDir ("f{0:d2}.png" -f $i)
            if (-not (Export-CaoAppFrame -SourcePath $raw -OutPath $outFile -Color $theme.Color -Mirror)) {
                throw "frame inválido: $raw"
            }
        }
        Write-Host ("  {0}/{1}: {2} frames" -f $theme.Name, $moodName.ToLowerInvariant(), $plan.Count) -ForegroundColor Green
    }
}

Write-Host 'Mascota generada.' -ForegroundColor Green
}
