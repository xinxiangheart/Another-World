# 提示条底带 —— 与卡框 / 顶栏同一套「深青钢 + 金饰」语言，
# 但轮廓刻意做成牌匾类（切角 / 绶带 / 凸台），不与顶栏阶段推进条同形。
# 用法： powershell -File PromptPlateArt.ps1 [-Variant all|a|b|c] [-Out <dir>]
param(
  [string]$Variant = "all",
  [string]$Out = ""
)
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

$STEEL_L = @(186, 206, 228)   # CardFrameV6 未定义，取 TopBarV2 同名色

$PW = 1040.0; $PH = 80.0
$PL = 24.0; $PR = 1016.0; $PT = 7.0; $PB = 67.0; $PCY = ($PT + $PB) / 2.0

if (-not $Out) { $Out = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Assets\_Game\Art\Sprites\Generated\prompt-plate-v1' }
if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out -Force | Out-Null }

function Save-Bmp($bmp, $gr, [string]$out) {
  $gr.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose(); return $out
}

function New-Canvas() {
  $bmp = New-Object System.Drawing.Bitmap([int]$PW, [int]$PH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gr = [System.Drawing.Graphics]::FromImage($bmp)
  $gr.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $gr.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $gr.Clear([System.Drawing.Color]::Transparent)
  return @($bmp, $gr)
}

function Fill-PathV($gr, $path, [single]$y, [single]$h, [int[]]$ct, [int[]]$cb, [int]$at = 255, [int]$ab = 255) {
  $sv = $gr.Clip; $gr.SetClip($path)
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Rectangle([int]$PL, [int]$y, [int]($PR - $PL), [int]$h)),
    (New-Col $ct $at), (New-Col $cb $ab), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $gr.FillRectangle($lg, $PL, $y, ($PR - $PL), $h)
  $lg.Dispose(); $gr.Clip = $sv
}

function Stroke-Path($gr, $path, [int[]]$rgb, [single]$t, [int]$a = 255) {
  $pen = New-Object System.Drawing.Pen (New-Col $rgb $a), $t
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $gr.DrawPath($pen, $path); $pen.Dispose()
}

function New-OctPath([single]$dx, [single]$cut) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $x0 = $PL + $dx; $x1 = $PR - $dx; $y0 = $PT + $dx; $y1 = $PB - $dx
  $p.AddLines([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(($x0 + $cut), $y0)),
    (New-Object System.Drawing.PointF(($x1 - $cut), $y0)),
    (New-Object System.Drawing.PointF($x1, ($y0 + $cut))),
    (New-Object System.Drawing.PointF($x1, ($y1 - $cut))),
    (New-Object System.Drawing.PointF(($x1 - $cut), $y1)),
    (New-Object System.Drawing.PointF(($x0 + $cut), $y1)),
    (New-Object System.Drawing.PointF($x0, ($y1 - $cut))),
    (New-Object System.Drawing.PointF($x0, ($y0 + $cut)))
  ))
  $p.CloseFigure()
  return $p
}

function New-RibbonPath([single]$dx, [single]$notch) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $x0 = $PL + $dx; $x1 = $PR - $dx; $y0 = $PT + $dx; $y1 = $PB - $dx
  $p.AddLines([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($x0, $y0)),
    (New-Object System.Drawing.PointF($x1, $y0)),
    (New-Object System.Drawing.PointF(($x1 - $notch), $PCY)),
    (New-Object System.Drawing.PointF($x1, $y1)),
    (New-Object System.Drawing.PointF($x0, $y1)),
    (New-Object System.Drawing.PointF(($x0 + $notch), $PCY))
  ))
  $p.CloseFigure()
  return $p
}

function Add-Diamond($gr, [single]$cx, [single]$cy, [single]$rad, [int[]]$fill, [int]$a = 230) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddLines([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($cx, ($cy - $rad))),
    (New-Object System.Drawing.PointF(($cx + $rad), $cy)),
    (New-Object System.Drawing.PointF($cx, ($cy + $rad))),
    (New-Object System.Drawing.PointF(($cx - $rad), $cy))
  ))
  $p.CloseFigure()
  Stroke-Path $gr $p $INK 3.4 210
  $br = New-Object System.Drawing.SolidBrush (New-Col $fill $a)
  $gr.FillPath($br, $p); $br.Dispose(); $p.Dispose()
}

# ── A：切角牌匾（八边形） ──
function New-VariantA([string]$out) {
  $cv = New-Canvas; $bmp = $cv[0]; $gr = $cv[1]
  $cut = 18.0
  Stroke-Path $gr (New-OctPath -3 $cut) $INK 6.5 200
  $body = New-OctPath 0 $cut
  Fill-PathV $gr $body $PT ($PB - $PT) $PLATE_T $PLATE_B
  $sv = $gr.Clip; $gr.SetClip($body)
  $br = New-Object System.Drawing.SolidBrush (New-Col $STEEL_L 58)
  $gr.FillRectangle($br, $PL, ($PT + 2), ($PR - $PL), 5); $br.Dispose()
  $br = New-Object System.Drawing.SolidBrush (New-Col @(0,0,0) 92)
  $gr.FillRectangle($br, $PL, ($PB - 11), ($PR - $PL), 11); $br.Dispose()
  $gr.Clip = $sv
  Stroke-Path $gr $body $GOLD 3.2 232
  Stroke-Path $gr (New-OctPath 8 $cut) $GOLD_L 1.6 165
  Add-Diamond $gr 56 $PCY 7 $GOLD_L
  Add-Diamond $gr ($PW - 56) $PCY 7 $GOLD_L
  return (Save-Bmp $bmp $gr $out)
}

# ── B：双尾绶带（两端内切 V 口） ──
function New-VariantB([string]$out) {
  $cv = New-Canvas; $bmp = $cv[0]; $gr = $cv[1]
  $notch = 26.0
  Stroke-Path $gr (New-RibbonPath -3 $notch) $INK 6.5 200
  $body = New-RibbonPath 0 $notch
  Fill-PathV $gr $body $PT ($PB - $PT) $PLATE_T $PLATE_B
  $sv = $gr.Clip; $gr.SetClip($body)
  $br = New-Object System.Drawing.SolidBrush (New-Col $STEEL_L 60)
  $gr.FillRectangle($br, $PL, ($PT + 2), ($PR - $PL), 5); $br.Dispose()
  $br = New-Object System.Drawing.SolidBrush (New-Col @(0,0,0) 95)
  $gr.FillRectangle($br, $PL, ($PB - 11), ($PR - $PL), 11); $br.Dispose()
  $br = New-Object System.Drawing.SolidBrush (New-Col @(0,0,0) 55)
  $gr.FillRectangle($br, $PL, $PT, 30, ($PB - $PT))
  $gr.FillRectangle($br, ($PR - 30), $PT, 30, ($PB - $PT)); $br.Dispose()
  $gr.Clip = $sv
  Stroke-Path $gr $body $GOLD 3.2 232
  Stroke-Path $gr (New-RibbonPath 9 $notch) $GOLD_L 1.6 150
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 200)), 3.0
  $gr.DrawLine($pen, ($PL + 16), $PT, ($PL + 16), ($PT + 9))
  $gr.DrawLine($pen, ($PL + 16), ($PB - 9), ($PL + 16), $PB)
  $gr.DrawLine($pen, ($PR - 16), $PT, ($PR - 16), ($PT + 9))
  $gr.DrawLine($pen, ($PR - 16), ($PB - 9), ($PR - 16), $PB)
  $pen.Dispose()
  return (Save-Bmp $bmp $gr $out)
}

# ── C：圆角凸台 + 两端铆钉 ──
function New-VariantC([string]$out) {
  $cv = New-Canvas; $bmp = $cv[0]; $gr = $cv[1]
  $rad = 16.0
  Stroke-Path $gr (New-RoundPath ($PL - 2) ($PT + 3) ($PR - $PL + 4) ($PB - $PT + 2) $rad) $INK 7.0 175
  $body = New-RoundPath $PL $PT ($PR - $PL) ($PB - $PT) $rad
  Fill-PathV $gr $body $PT ($PB - $PT) $BASE_T $BASE_B
  $sv = $gr.Clip; $gr.SetClip($body)
  $br = New-Object System.Drawing.SolidBrush (New-Col $STEEL_L 92)
  $gr.FillRectangle($br, $PL, ($PT + 2), ($PR - $PL), 6); $br.Dispose()
  $br = New-Object System.Drawing.SolidBrush (New-Col @(0,0,0) 120)
  $gr.FillRectangle($br, $PL, ($PB - 13), ($PR - $PL), 13); $br.Dispose()
  $gr.Clip = $sv
  Stroke-Path $gr $body $GOLD 3.6 240
  Stroke-Path $gr (New-RoundPath ($PL + 9) ($PT + 9) ($PR - $PL - 18) ($PB - $PT - 18) 9) $GOLD_L 1.5 148
  foreach ($cx in @(50.0, ($PW - 50.0))) {
    $br = New-Object System.Drawing.SolidBrush (New-Col $INK 235)
    $gr.FillEllipse($br, ($cx - 5), ($PCY - 5), 10, 10); $br.Dispose()
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 245)
    $gr.FillEllipse($br, ($cx - 3.4), ($PCY - 3.4), 6.8, 6.8); $br.Dispose()
  }
  return (Save-Bmp $bmp $gr $out)
}

$map = @{ 'a' = 'New-VariantA'; 'b' = 'New-VariantB'; 'c' = 'New-VariantC' }
$names = if ($Variant -eq 'all') { @('a', 'b', 'c') } else { @($Variant) }
foreach ($k in $names) {
  $f = Join-Path $Out ("PromptPlate_{0}.png" -f $k)
  & $map[$k] $f
  Write-Host "$f"
}