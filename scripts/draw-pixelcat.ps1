#Requires -Version 5.1
<#
  Pixel-art orange tabby cat, hand-authored pixel map.
  Base pose: sitting, front view. Symmetric body via mirrored half-rows.
  Outputs: artifacts/mascot-new/pixelcat-sit-v1.png (40x44 px, scaled x12)
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$palette = @{
  'K' = [System.Drawing.Color]::FromArgb(0x4A, 0x2A, 0x12)  # outline dark brown
  'O' = [System.Drawing.Color]::FromArgb(0xEF, 0x93, 0x34)  # orange base
  'D' = [System.Drawing.Color]::FromArgb(0xC0, 0x66, 0x1A)  # dark stripe
  'L' = [System.Drawing.Color]::FromArgb(0xF7, 0xB4, 0x5C)  # light highlight
  'C' = [System.Drawing.Color]::FromArgb(0xF9, 0xE7, 0xC8)  # cream muzzle/chest/paws
  'G' = [System.Drawing.Color]::FromArgb(0x46, 0xB9, 0x5A)  # green eyes
  'N' = [System.Drawing.Color]::FromArgb(0x20, 0x20, 0x20)  # pupil
  'H' = [System.Drawing.Color]::White                       # eye highlight
  'P' = [System.Drawing.Color]::FromArgb(0xE8, 0x8A, 0x80)  # pink nose/inner ear
  'W' = [System.Drawing.Color]::FromArgb(200, 255, 255, 255) # whiskers
  'S' = [System.Drawing.Color]::FromArgb(60, 0, 0, 0)        # ground shadow
}

# Left half rows (20 chars each, x0..19). Mirrored to full 40 px width.
$half = @(
  '....................', # y00
  '........K...........', # y01 ear tip
  '.......KOK..........', # y02
  '.......KOOK.........', # y03
  '......KOOPK.........', # y04 inner ear pink
  '......KOOPPK........', # y05
  '.....KOOOPPPK.......', # y06
  '.....KOOOPPPPK......', # y07
  '.....KKKKKKKKK......', # y08 head top
  '....KOODOODOOOOOOOOD', # y09 forehead stripes
  '....KOODOODOOOOOOOOO', # y10
  '....KOOOOOOOOOOOOOOO', # y11
  '...KOOOOOOOOOOOOOOOO', # y12 head widens
  '...KOOOOOKKKOOOOOOOO', # y13 eyes top
  '...KOOOOKHNGKOOOOOOO', # y14 eyes (highlight/pupil/green)
  '...KOOOOKGNGKOOOOOOO', # y15
  '...KODDOOKKKOOOOOOOO', # y16 eyes bottom + cheek stripes
  '....KDDOOOOOOOOOOOOO', # y17 taper + cheek stripes
  '....KOOOOOOOCCCCCCC', # y18 muzzle cream  (19 chars -> padded below)
  '....KOOOOOOOCCCCCCP', # y19 nose
  '....KOOOOOOOCCCCCCK', # y20 mouth line
  '.....KOOOOOCCCCCCCK.', # y21 chin
  '......KOOOOOOCCCCCCK', # y22 shoulders/chest
  '......KOOOOOCCCCCCCK', # y23
  '......KOOOOOCCCCCCCK', # y24
  '.....KOOOOOOCCCCCCCK', # y25 body widens
  '.....KOOOOOOCCCCCCCK', # y26
  '.....KOOOOOOCCCCCCCK', # y27
  '.....KOOOOOOOOKCCCCK', # y28 front leg split
  '.....KOOOOOOOOKCCCCK', # y29
  '....KOOOOOOOOOKCCCCK', # y30 haunch bulge
  '....KOOOOOOOOOKCCCCK', # y31
  '....KOOOOOOOOOKCCCCK', # y32
  '....KOOOOOOOOOKCCCCK', # y33
  '....KOOOOOOOOOKCCCCK', # y34
  '....KOOOOOOOOOKCCCCK', # y35 paws
  '....KOOOOOOOOOKCKCCK', # y36 toe line
  '....KOOOOOOOOOKCKCCK', # y37
  '....KOOOOOOOOOKCKCCK', # y38
  '....KKKKKKKKKKKKKKK.', # y39 base
  '....................', # y40 (shadow stamped in code)
  '....................', # y41
  '....................', # y42
  '....................'  # y43
)

# Normalize: pad/truncate every row to exactly 20 chars.
for ($i = 0; $i -lt $half.Count; $i++) {
  $r = $half[$i]
  if ($r.Length -lt 20) { $r = $r.PadRight(20, '.') }
  elseif ($r.Length -gt 20) { $r = $r.Substring(0, 20) }
  $half[$i] = $r
}
if ($half.Count -ne 44) { throw "half must have 44 rows, has $($half.Count)" }

$W = 40; $H = 44
$grid = New-Object 'string[,]' $W, $H
for ($x = 0; $x -lt $W; $x++) { for ($y = 0; $y -lt $H; $y++) { $grid[$x, $y] = '.' } }

# Mirror left half -> full width (x19 mirrors to x20).
for ($y = 0; $y -lt $H; $y++) {
  $row = $half[$y]
  for ($x = 0; $x -lt 20; $x++) {
    $c = $row[$x].ToString()
    $mx = 39 - $x
    $grid[$x, $y] = $c
    $grid[$mx, $y] = $c
  }
}

function SetPx($x, $y, $c, $onlyIfEmpty) {
  if ($x -lt 0 -or $x -ge $W -or $y -lt 0 -or $y -ge $H) { return }
  if ($onlyIfEmpty -and $grid[$x, $y] -ne '.') { return }
  $grid[$x, $y] = $c
}

# Tail: striped curve rising on viewer-right side (asymmetric stamp).
$spine = @(@(31, 35), @(33, 33), @(35, 31), @(36, 29), @(37, 27), @(37, 25), @(36, 23), @(35, 21))
for ($i = 0; $i -lt $spine.Count; $i++) {
  $sx = $spine[$i][0]; $sy = $spine[$i][1]
  $fill = 'O'
  if ($i % 3 -eq 2) { $fill = 'D' }
  SetPx ($sx - 1) $sy $fill $true
  SetPx $sx $sy $fill $true
  SetPx ($sx + 1) $sy $fill $true
  SetPx ($sx - 2) $sy 'K' $true
  SetPx ($sx + 2) $sy 'K' $true
}
# Tail tip cap.
SetPx 34 20 'K' $true
SetPx 35 20 'K' $true
SetPx 36 20 'K' $true

# Whiskers (mirrored to both sides).
foreach ($wx in @(0, 1, 2, 3)) {
  SetPx $wx 17 'W' $true
  SetPx (39 - $wx) 17 'W' $true
}
foreach ($wx in @(0, 1, 2, 3)) {
  SetPx $wx 19 'W' $true
  SetPx (39 - $wx) 19 'W' $true
}
foreach ($wx in @(1, 2, 3, 4)) {
  SetPx $wx 21 'W' $true
  SetPx (39 - $wx) 21 'W' $true
}

# Ground shadow.
for ($x = 9; $x -le 30; $x++) { SetPx $x 40 'S' $true }

# Render at scale.
$scale = 12
$bmp = New-Object System.Drawing.Bitmap ($W * $scale), ($H * $scale)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$brushes = @{}
foreach ($k in $palette.Keys) { $brushes[$k] = New-Object System.Drawing.SolidBrush $palette[$k] }
for ($y = 0; $y -lt $H; $y++) {
  for ($x = 0; $x -lt $W; $x++) {
    $c = $grid[$x, $y]
    if ($c -eq '.') { continue }
    $g.FillRectangle($brushes[$c], $x * $scale, $y * $scale, $scale, $scale)
  }
}
$g.Dispose()

$outDir = 'C:\Users\berna\OneDrive\Documentos\CA-O\artifacts\mascot-new'
if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$out = Join-Path $outDir 'pixelcat-sit-v1.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved: $out"
