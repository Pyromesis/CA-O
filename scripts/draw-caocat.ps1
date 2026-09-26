#Requires -Version 5.1
<#
  Dibuja la mascota Cao/Ca-O por código (vectorial determinista, sin IA).
  Gato cartoon: negro en tema claro, blanco en tema oscuro.
  Uso: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/draw-caocat.ps1 -Pose Sit -Theme Light -Out artifacts/mascot-new/frame01-sit-light.png
#>
param(
  [ValidateSet('Sit')] [string]$Pose = 'Sit',
  [ValidateSet('Light','Dark')] [string]$Theme = 'Light',
  [string]$Out = 'artifacts/mascot-new/frame01-sit-light.png',
  [int]$Size = 256,
  [int]$Super = 4
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$S = $Super
function px($v) { return [int][Math]::Round($v * $S) }

if ($Theme -eq 'Light') {
  $Body    = '#1B2130'
  $Outline = '#0B0E15'
  $Inner   = '#2C3550'
  $Light   = '#FFFFFF'
  $Eye     = '#F2B23E'
  $Pupil   = '#101319'
  $Nose    = '#D98E96'
  $EarIn   = '#8E5B64'
  $Mouth   = '#0B0E15'
} else {
  $Body    = '#F4F6FA'
  $Outline = '#B9C0CE'
  $Inner   = '#FFFFFF'
  $Light   = '#1B2130'
  $Eye     = '#2E9E5B'
  $Pupil   = '#101319'
  $Nose    = '#C97E88'
  $EarIn   = '#D9A3AB'
  $Mouth   = '#1B2130'
}

$W = $Size * $S
$bmp = New-Object System.Drawing.Bitmap($W, $W)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.PixelOffsetMode = 'HighQuality'
$g.Clear([System.Drawing.Color]::Transparent)

function brush($hex) { return New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($hex)) }
function pen($hex, $w256) {
  $p = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($hex), (px $w256))
  $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
  return $p
}
function ell($x,$y,$w,$h,$hex) {
  $b = brush $hex
  $g.FillEllipse($b, (px $x), (px $y), (px $w), (px $h))
  $b.Dispose()
}
function ellEdge($x,$y,$w,$h,$fill,$edge,$w256) {
  ell $x $y $w $h $fill
  $p = pen $edge $w256
  $g.DrawEllipse($p, (px $x), (px $y), (px $w), (px $h))
  $p.Dispose()
}
function poly($pts, $hex) {
  $arr = @()
  foreach ($pt in $pts) { $arr += New-Object System.Drawing.Point((px $pt[0]), (px $pt[1])) }
  $b = brush $hex
  $g.FillPolygon($b, $arr)
  $b.Dispose()
}
function polyEdge($pts, $fill, $edge, $w256) {
  poly $pts $fill
  $arr = @()
  foreach ($pt in $pts) { $arr += New-Object System.Drawing.Point((px $pt[0]), (px $pt[1])) }
  $p = pen $edge $w256
  $g.DrawPolygon($p, $arr)
  $p.Dispose()
}
function capsule($x1,$y1,$x2,$y2,$w256,$hex) {
  $p = pen $hex $w256
  $g.DrawLine($p, (px $x1), (px $y1), (px $x2), (px $y2))
  $p.Dispose()
}

if ($Pose -eq 'Sit') {
  # sombra suelo
  $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(46, 0, 0, 0))
  $g.FillEllipse($sb, (px 62), (px 228), (px 132), (px 17))
  $sb.Dispose()

  # cola: curva que envuelve por la izquierda hasta el frente
  capsule 84 214 62 220 19 $Body
  capsule 62 220 60 202 19 $Body
  capsule 60 202 78 196 19 $Body

  # cuerpo
  ellEdge 70 118 116 114 $Body $Outline 3.5
  # muslos
  ell 64 178 46 54 $Body
  ell 146 178 46 54 $Body

  # pecho blanco
  ell 100 148 56 74 $Light

  # patas delanteras (cápsulas) + calcetines blancos
  capsule 112 184 112 222 21 $Body
  capsule 144 184 144 222 21 $Body
  ell 101 216 23 17 $Light
  ell 133 216 23 17 $Light

  # orejas (detrás de la cabeza)
  polyEdge @(@(88,54), @(98,6), @(126,44)) $Body $Outline 3.5
  polyEdge @(@(168,54), @(158,6), @(130,44)) $Body $Outline 3.5
  poly @(@(96,44), @(101,20), @(116,40)) $EarIn
  poly @(@(160,44), @(155,20), @(140,40)) $EarIn

  # cabeza
  ellEdge 82 38 92 92 $Body $Outline 3.5

  # hocico blanco
  ell 100 94 56 36 $Light

  # ojos ámbar con pupila vertical + brillo
  ell 95 66 23 23 $Eye
  ell 138 66 23 23 $Eye
  ell 103.5 69 6 17 $Pupil
  ell 146.5 69 6 17 $Pupil
  ell 99 71 5.5 5.5 '#FFFFFF'
  ell 142 71 5.5 5.5 '#FFFFFF'

  # nariz + boca
  poly @(@(121,102), @(135,102), @(128,111)) $Nose
  capsule 128 111 120 119 2.5 $Mouth
  capsule 128 111 136 119 2.5 $Mouth
}

# downscale supersampleado -> Size
$final = New-Object System.Drawing.Bitmap($Size, $Size)
$g2 = [System.Drawing.Graphics]::FromImage($final)
$g2.SmoothingMode = 'HighQuality'
$g2.InterpolationMode = 'HighQualityBicubic'
$g2.PixelOffsetMode = 'HighQuality'
$g2.DrawImage($bmp, 0, 0, $Size, $Size)
$g2.Dispose(); $g.Dispose(); $bmp.Dispose()

$dir = Split-Path $Out -Parent
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
$final.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$final.Dispose()
"frame dibujado: $Out"
