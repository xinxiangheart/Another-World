# 顶栏 v1 —— 与战场底板 v5 同一套语言（深蓝黑石面 + 金细线 + 暗刻）
# 轮廓逐列照抄现有 sprite，保证 9-slice 与场景里的位置完全对得上。
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

$ROOT = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$SRC  = Join-Path $ROOT 'Assets\_Game\Art\Sprites\UI'

$INK     = @(6, 9, 14)
$INK2    = @(10, 15, 23)
$BAR_T   = @(30, 41, 56)
$BAR_B   = @(12, 17, 26)
$GOLD    = @(200, 164, 74)
$GOLD_L  = @(232, 209, 138)
$GOLD_D  = @(120, 94, 40)
$STEEL   = @(110, 147, 176)
$STEEL_L = @(186, 206, 228)
$CRIM    = @(182, 72, 72)
$CRIM_D  = @(118, 40, 46)
$BOLT    = @(104, 154, 214)
$BOLT_D  = @(60, 100, 152)
$SAND    = @(214, 194, 152)
$SAND_D  = @(154, 134, 100)
$METAL   = @(154, 172, 194)
$METAL_D = @(98, 116, 140)
$HILITE  = @(233, 241, 250)

function New-Bmp([int]$w, [int]$h) {
  $b = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  return @($b, $g)
}
function Save-Bmp($b, $g, [string]$out) {
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); return $out
}
function Get-BottomProfile([string]$path) {
  $im = [System.Drawing.Bitmap]::FromFile($path)
  $w = $im.Width; $h = $im.Height
  $prof = New-Object 'int[]' $w
  for ($x = 0; $x -lt $w; $x++) {
    $hi = 0
    for ($y = $h - 1; $y -ge 0; $y--) { if ($im.GetPixel($x, $y).A -gt 8) { $hi = $y; break } }
    $prof[$x] = $hi
  }
  $im.Dispose()
  return ,$prof
}
function New-ProfilePath([int[]]$prof) {
  $w = $prof.Length
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pf = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  $pf.Add((New-Object System.Drawing.PointF(-1, -1)))
  $pf.Add((New-Object System.Drawing.PointF($w, -1)))
  $pf.Add((New-Object System.Drawing.PointF($w, ($prof[$w - 1] + 1))))
  for ($x = $w - 1; $x -ge 0; $x--) { $pf.Add((New-Object System.Drawing.PointF($x, ($prof[$x] + 1)))) }
  $pf.Add((New-Object System.Drawing.PointF(-1, ($prof[0] + 1))))
  $p.AddPolygon($pf.ToArray())
  $p.CloseFigure()
  return $p
}
function Stroke-Profile($g, [int[]]$prof, [int[]]$col, [single]$wd, [int]$a, [single]$dy) {
  $pen = New-Object System.Drawing.Pen ((New-Col $col $a)), $wd
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pf = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($x = 0; $x -lt $prof.Length; $x++) { $pf.Add((New-Object System.Drawing.PointF($x, ($prof[$x] + $dy)))) }
  $g.DrawLines($pen, $pf.ToArray()); $pen.Dispose()
}
function Fill-HGrad($g, [single]$x, [single]$y, [single]$w, [single]$h, [int[]]$cl, [int[]]$cr, [int]$al, [int]$ar) {
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle([int]$x, [int]$y, [int]$w, [int]$h)), (New-Col $cl $al), (New-Col $cr $ar), [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
  $g.FillRectangle($lg, $x, $y, $w, $h); $lg.Dispose()
}
function Fill-VGrad($g, [single]$x, [single]$y, [single]$w, [single]$h, [int[]]$ct, [int[]]$cb, [int]$at, [int]$ab) {
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle([int]$x, [int]$y, [int]$w, [int]$h)), (New-Col $ct $at), (New-Col $cb $ab), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $x, $y, $w, $h); $lg.Dispose()
}
function New-Diamond([single]$cx, [single]$cy, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($cx, ($cy - $r))), (New-Object System.Drawing.PointF(($cx + $r), $cy)),
    (New-Object System.Drawing.PointF($cx, ($cy + $r))), (New-Object System.Drawing.PointF(($cx - $r), $cy))))
  $p.CloseFigure()
  return $p
}
# ══════════════════════════════════════════════════════════
# 1. 顶栏底板 2048x128（轮廓照抄 TopBorder.png，9-slice 左右各 82px 不变）
# ══════════════════════════════════════════════════════════
function Bar-FaceCol([single]$dy) {
  $t = $dy / 120.0
  if ($t -lt 0) { $t = 0 }
  if ($t -gt 1) { $t = 1 }
  return (Mix-Col $BAR_T $BAR_B $t)
}
# ══════════════════════════════════════════════════════════
# 1. 顶栏底板 2048x128（轮廓照抄 TopBorder.png，9-slice 左右各 82px 不变）
# ══════════════════════════════════════════════════════════
function New-TopPlate([string]$out) {
  $prof = Get-BottomProfile (Join-Path $SRC 'TopBorder.png')
  $r = New-Bmp 2048 128; $b = $r[0]; $g = $r[1]
  $p = New-ProfilePath $prof
  $st = $g.Save(); $g.SetClip($p)
  Fill-VGrad $g 0 0 2048 128 $BAR_T $BAR_B 255 255
  Fill-HGrad $g 0 0 300 128 $INK $INK 86 0
  Fill-HGrad $g 1748 0 300 128 $INK $INK 0 86
  foreach ($yy in @(8.5, 12.5, 16.5)) {
    $pen = New-Object System.Drawing.Pen ((New-Col $INK 44)), 1.6
    $g.DrawLine($pen, 0, $yy, 2048, $yy); $pen.Dispose()
  }
  for ($x = 620; $x -le 1430; $x += 26) {
    $pen = New-Object System.Drawing.Pen ((New-Col $INK 34)), 1.2
    $g.DrawLine($pen, $x, 20, $x, 25); $pen.Dispose()
  }
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 200)), 2.6
  $g.DrawLine($pen, 0, 1.6, 2048, 1.6); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_L 54)), 1.2
  $g.DrawLine($pen, 0, 4.6, 2048, 4.6); $pen.Dispose()
  Stroke-Profile $g $prof $INK 7 78 3
  Stroke-Profile $g $prof $GOLD 2.6 205 -1.6
  Stroke-Profile $g $prof $GOLD_L 1.2 84 -3.8
  foreach ($cx in @(114, 530, 1519, 1935)) {
    $pc = $prof[$cx]
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 96)), 2
    $g.DrawLine($pen, $cx, ($pc - 13), $cx, $pc); $pen.Dispose()
  }
  foreach ($dx in @(41, 2007)) {
    $dd = New-Diamond $dx 60 12
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 235)
    $g.FillPath($bs, $dd); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 150)), 2
    $g.DrawPath($pen, $dd); $pen.Dispose(); $dd.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 72)), 2
    $g.DrawLine($pen, $dx, 80, $dx, 96); $pen.Dispose()
  }
  return (Save-Bmp $b $g $out)
}
# ══════════════════════════════════════════════════════════
# 2. 阶段底衬 853x55（轮廓照抄 PhaseBand.png）
#    与顶栏同一渐变函数；描边只画在顶栏下沿以下（局部 y >= 21.5），上端让给顶栏
# ══════════════════════════════════════════════════════════
function New-TopBand([string]$out) {
  $prof = Get-BottomProfile (Join-Path $SRC 'PhaseBand.png')
  $r = New-Bmp 853 55; $b = $r[0]; $g = $r[1]
  $p = New-ProfilePath $prof
  $cTop = Bar-FaceCol 5
  $cBot = Bar-FaceCol 57
  $st = $g.Save(); $g.SetClip($p)
  Fill-VGrad $g 0 0 853 55 $cTop $cBot 255 255
  $stc = $g.Save()
  $g.SetClip((New-Object System.Drawing.RectangleF(0, 21.5, 853, 33.5)), [System.Drawing.Drawing2D.CombineMode]::Intersect)
  Fill-HGrad $g 0 21.5 110 33.5 $INK $INK 74 0
  Fill-HGrad $g 743 21.5 110 33.5 $INK $INK 0 74
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 42)), 1.4
  $g.DrawLine($pen, 0, 38, 853, 38); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 28)), 1.2
  $g.DrawLine($pen, 0, 46, 853, 46); $pen.Dispose()
  $g.Restore($stc)
  $g.Restore($st)
  $stc = $g.Save()
  $g.SetClip((New-Object System.Drawing.RectangleF(0, 21.5, 853, 33.5)))
  Stroke-Profile $g $prof $INK 6 74 3
  Stroke-Profile $g $prof $GOLD 2.2 195 -1.4
  Stroke-Profile $g $prof $GOLD_L 1.2 80 -3.4
  foreach ($s in @(1, -1)) {
    $x0 = 42.0
    if ($s -lt 0) { $x0 = 811.0 }
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 130)), 2.2
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLine($pen, ($x0 - 9 * $s), 25, $x0, 34)
    $g.DrawLine($pen, $x0, 34, ($x0 - 9 * $s), 43)
    $pen.Dispose()
  }
  $g.Restore($stc)
  return (Save-Bmp $b $g $out)
}
# ══════════════════════════════════════════════════════════
# 3. 收起按钮 168x42（轮廓照抄 PhaseHideBtn.png）+ 箭头 34x26
# ══════════════════════════════════════════════════════════
function New-TopHideBtn([string]$out) {
  $cx = 84.0; $tw = 67.2; $bw = 47.6; $th = 32.8
  $r = New-Bmp 168 42; $b = $r[0]; $g = $r[1]
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(($cx - $tw), 0)), (New-Object System.Drawing.PointF(($cx + $tw), 0)),
    (New-Object System.Drawing.PointF(($cx + $bw), $th)), (New-Object System.Drawing.PointF(($cx - $bw), $th))))
  $p.CloseFigure()
  $st = $g.Save(); $g.SetClip($p)
  Fill-VGrad $g 0 0 168 42 (Bar-FaceCol 54) (Bar-FaceCol 96) 255 255
  Fill-HGrad $g 0 0 56 42 $INK $INK 62 0
  Fill-HGrad $g 112 0 56 42 $INK $INK 0 62
  $g.Restore($st)
  $segs = @(); $segs += , @(2.5, $INK, 5, 84); $segs += , @(-1.0, $GOLD, 2, 200)
  foreach ($sg in $segs) {
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $pts.Add((New-Object System.Drawing.PointF(($cx - $tw), (0 + $sg[0]))))
    $pts.Add((New-Object System.Drawing.PointF(($cx - $bw), ($th + $sg[0]))))
    $pts.Add((New-Object System.Drawing.PointF(($cx + $bw), ($th + $sg[0]))))
    $pts.Add((New-Object System.Drawing.PointF(($cx + $tw), (0 + $sg[0]))))
    $pen = New-Object System.Drawing.Pen ((New-Col $sg[1] $sg[3])), $sg[2]
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLines($pen, $pts.ToArray()); $pen.Dispose()
  }
  return (Save-Bmp $b $g $out)
}
function New-TopArrow([string]$out) {
  $r = New-Bmp 34 26; $b = $r[0]; $g = $r[1]
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(6.5, 17.5)), (New-Object System.Drawing.PointF(27.5, 17.5)),
    (New-Object System.Drawing.PointF(17, 2.5))))
  $p.CloseFigure()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 235)), 4
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose()
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 240)
  $g.FillPath($bs, $p); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 165)), 1.4
  $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
  return (Save-Bmp $b $g $out)
}
function Add-Shade($g, $path, [int[]]$col, [single]$yL, [single]$yR) {
  $st = $g.Save()
  $g.SetClip($path, [System.Drawing.Drawing2D.CombineMode]::Intersect)
  $bs = New-Object System.Drawing.SolidBrush (New-Col $col 255)
  $g.FillPolygon($bs, [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, $yL)), (New-Object System.Drawing.PointF(256, $yR)),
    (New-Object System.Drawing.PointF(256, 256)), (New-Object System.Drawing.PointF(0, 256))))
  $bs.Dispose(); $g.Restore($st)
}
function Add-Outline($g, $path, [single]$w) {
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 255)), $w
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose()
}
function New-HeartPath {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.StartFigure()
  $p.AddBezier([single]128, [single]214, [single]52, [single]166, [single]24, [single]124, [single]24, [single]88)
  $p.AddBezier([single]24, [single]88, [single]24, [single]44, [single]60, [single]24, [single]94, [single]24)
  $p.AddBezier([single]94, [single]24, [single]114, [single]24, [single]125, [single]40, [single]128, [single]56)
  $p.AddBezier([single]128, [single]56, [single]131, [single]40, [single]142, [single]24, [single]162, [single]24)
  $p.AddBezier([single]162, [single]24, [single]196, [single]24, [single]232, [single]44, [single]232, [single]88)
  $p.AddBezier([single]232, [single]88, [single]232, [single]124, [single]204, [single]166, [single]128, [single]214)
  $p.CloseFigure()
  return $p
}
function New-BoltPath {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(158, 18)), (New-Object System.Drawing.PointF(52, 140)),
    (New-Object System.Drawing.PointF(112, 140)), (New-Object System.Drawing.PointF(92, 238)),
    (New-Object System.Drawing.PointF(202, 108)), (New-Object System.Drawing.PointF(140, 108))))
  $p.CloseFigure()
  return $p
}
function New-CardsPath {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPath((New-RoundPath 30 82 118 146 18), $true)
  $p.AddPath((New-RoundPath 104 30 124 152 18), $true)
  return $p
}
function New-HourglassPath {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(70, 62)), (New-Object System.Drawing.PointF(186, 62)),
    (New-Object System.Drawing.PointF(140, 128)), (New-Object System.Drawing.PointF(186, 194)),
    (New-Object System.Drawing.PointF(70, 194)), (New-Object System.Drawing.PointF(116, 128))))
  $p.CloseFigure()
  return $p
}
function New-GearPath {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pf = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($i = 0; $i -lt 8; $i++) {
    $base = $i * 45.0
    foreach ($q in @(@(-11, 102), @(11, 102), @(17, 76), @(28, 76))) {
      $a = ($base + $q[0]) * [Math]::PI / 180.0
      $rr = [single]$q[1]
      $pf.Add((New-Object System.Drawing.PointF([single](128 + $rr * [Math]::Cos($a)), [single](128 + $rr * [Math]::Sin($a)))))
    }
  }
  $p.AddPolygon($pf.ToArray())
  $p.AddEllipse(94, 94, 68, 68)
  return $p
}
function Add-Hilite($g, $path, [single]$x, [single]$y, [single]$w, [single]$h, [int]$a) {
  $st = $g.Save()
  $g.SetClip($path, [System.Drawing.Drawing2D.CombineMode]::Intersect)
  $bs = New-Object System.Drawing.SolidBrush (New-Col $HILITE $a)
  $g.FillEllipse($bs, $x, $y, $w, $h); $bs.Dispose()
  $g.Restore($st)
}
function Add-Wedge($g, $path, [System.Drawing.PointF[]]$pts, [int[]]$col, [int]$a) {
  $st = $g.Save()
  $g.SetClip($path, [System.Drawing.Drawing2D.CombineMode]::Intersect)
  $bs = New-Object System.Drawing.SolidBrush (New-Col $col $a)
  $g.FillPolygon($bs, $pts); $bs.Dispose()
  $g.Restore($st)
}
function New-UiIcon([string]$kind, [string]$out) {
  $r = New-Bmp 256 256; $b = $r[0]; $g = $r[1]
  if ($kind -eq 'health') {
    $p = New-HeartPath
    Add-Outline $g $p 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $CRIM 255)
    $g.FillPath($bs, $p); $bs.Dispose()
    Add-Shade $g $p $CRIM_D 196 170
    Add-Hilite $g $p 58 44 84 66 120
  } elseif ($kind -eq 'energy') {
    $p = New-BoltPath
    Add-Outline $g $p 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $BOLT 255)
    $g.FillPath($bs, $p); $bs.Dispose()
    Add-Shade $g $p $BOLT_D 196 172
    Add-Wedge $g $p ([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(156, 28)), (New-Object System.Drawing.PointF(120, 28)),
      (New-Object System.Drawing.PointF(64, 120)), (New-Object System.Drawing.PointF(98, 120)))) $HILITE 120
  } elseif ($kind -eq 'cards') {
    $back = New-RoundPath 34 84 118 146 18
    Add-Outline $g $back 20
    $bs = New-Object System.Drawing.SolidBrush (New-Col $SAND_D 255)
    $g.FillPath($bs, $back); $bs.Dispose()
    $front = New-RoundPath 104 30 124 152 18
    Add-Outline $g $front 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $SAND 255)
    $g.FillPath($bs, $front); $bs.Dispose()
    Add-Shade $g $front $SAND_D 210 198
    Add-Wedge $g $front ([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(104, 30)), (New-Object System.Drawing.PointF(228, 30)),
      (New-Object System.Drawing.PointF(228, 64)), (New-Object System.Drawing.PointF(104, 64)))) $HILITE 78
    $dd = New-Diamond 166 112 24
    $bs = New-Object System.Drawing.SolidBrush (New-Col $SAND_D 200)
    $g.FillPath($bs, $dd); $bs.Dispose(); $dd.Dispose()
    $front.Dispose(); $back.Dispose()
  } elseif ($kind -eq 'turn') {
    $p = New-HourglassPath
    Add-Outline $g $p 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $SAND 255)
    $g.FillPath($bs, $p); $bs.Dispose()
    Add-Shade $g $p $SAND_D 196 176
    Add-Wedge $g $p ([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(80, 68)), (New-Object System.Drawing.PointF(124, 68)),
      (New-Object System.Drawing.PointF(124, 102)), (New-Object System.Drawing.PointF(80, 102)))) $HILITE 120
    foreach ($yy in @(40, 200)) {
      $bar = New-RoundPath 50 $yy 156 20 7
      $pen = New-Object System.Drawing.Pen ((New-Col $INK 255)), 18
      $g.DrawPath($pen, $bar); $pen.Dispose()
      $bs = New-Object System.Drawing.SolidBrush (New-Col $SAND_D 255)
      $g.FillPath($bs, $bar); $bs.Dispose()
      $bar.Dispose()
    }
  } elseif ($kind -eq 'settings') {
    $p = New-GearPath
    $p.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
    Add-Outline $g $p 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $METAL 255)
    $g.FillPath($bs, $p); $bs.Dispose()
    Add-Shade $g $p $METAL_D 196 168
    Add-Hilite $g $p 44 34 92 70 96
  }
  return (Save-Bmp $b $g $out)
}
function Fill-Disc($g, [single]$cx, [single]$cy, [single]$r, [int[]]$col, [int]$a) {
  $bs = New-Object System.Drawing.SolidBrush (New-Col $col $a)
  $g.FillEllipse($bs, ($cx - $r), ($cy - $r), ($r * 2), ($r * 2)); $bs.Dispose()
}
function New-RingDisc($g, [int]$a = 255) {
  Fill-Disc $g 255.5 255.5 252.0 $INK $a
  Fill-Disc $g 255.5 255.5 243.0 $GOLD $a
  Fill-Disc $g 255.5 255.5 234.0 $INK $a
  $well = New-Object System.Drawing.Drawing2D.GraphicsPath
  $well.AddEllipse(25.5, 25.5, 460, 460)
  $st = $g.Save(); $g.SetClip($well)
  Fill-VGrad $g 25.5 25.5 460 460 $BAR_T $BAR_B $a $a
  Fill-Disc $g 255.5 255.5 186.0 (Mix-Col $BAR_B $INK 0.42) $a
  $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
  $inner.AddEllipse(69.5, 69.5, 372, 372)
  $st2 = $g.Save(); $g.SetClip($inner, [System.Drawing.Drawing2D.CombineMode]::Intersect)
  Fill-VGrad $g 69.5 69.5 372 372 $INK $INK 130 0
  $g.Restore($st2); $inner.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 120)), 7
  $g.DrawEllipse($pen, 29.5, 29.5, 452, 452); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_L 92)), 4
  $g.DrawArc($pen, 33.5, 33.5, 444, 444, 196, 78); $pen.Dispose()
  $g.Restore($st)
  $well.Dispose()
}
# ══════════════════════════════════════════════════════════
# 6. 阶段环 511x511（ring_empty.png，空槽）
# ══════════════════════════════════════════════════════════
function New-TopRing([string]$out) {
  $r = New-Bmp 511 511; $b = $r[0]; $g = $r[1]
  New-RingDisc $g 255
  return (Save-Bmp $b $g $out)
}
# ══════════════════════════════════════════════════════════
# 7. 战斗阶段环 511x511（Battle Phase.png，双剑交叉）
# ══════════════════════════════════════════════════════════
function New-SwordPath {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, -216)), (New-Object System.Drawing.PointF(32, -162)),
    (New-Object System.Drawing.PointF(32, 19)), (New-Object System.Drawing.PointF(-32, 19)),
    (New-Object System.Drawing.PointF(-32, -162))))
  $p.CloseFigure()
  $p.AddEllipse(-20, 103, 40, 40)
  $cg = New-RoundPath -72 19 144 26 8
  $p.AddPath($cg, $true); $cg.Dispose()
  $gp = New-RoundPath -14 45 28 68 6
  $p.AddPath($gp, $true); $gp.Dispose()
  return $p
}
function New-TopBattle([string]$out) {
  $r = New-Bmp 511 511; $b = $r[0]; $g = $r[1]
  New-RingDisc $g 255
  foreach ($ang in @(-44.0, 44.0)) {
    $sw = New-SwordPath
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.RotateAt($ang, (New-Object System.Drawing.PointF(0, 0)))
    $sw.Transform($m); $m.Dispose()
    $m2 = New-Object System.Drawing.Drawing2D.Matrix
    $m2.Translate(255.5, 255.5)
    $sw.Transform($m2); $m2.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK 255)), 26
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $sw); $pen.Dispose()
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 255)
    $g.FillPath($bs, $sw); $bs.Dispose()
    $st = $g.Save()
    $g.SetClip($sw, [System.Drawing.Drawing2D.CombineMode]::Intersect)
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 150)
    $g.FillPolygon($bs, [System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(0, -228)), (New-Object System.Drawing.PointF(66, -132)),
      (New-Object System.Drawing.PointF(66, 150)), (New-Object System.Drawing.PointF(0, 150))))
    $bs.Dispose()
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 130)
    $g.FillPolygon($bs, [System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(-32, -162)), (New-Object System.Drawing.PointF(-8, -162)),
      (New-Object System.Drawing.PointF(-8, 19)), (New-Object System.Drawing.PointF(-32, 19))))
    $bs.Dispose()
    $g.Restore($st)
    $sw.Dispose()
  }
  return (Save-Bmp $b $g $out)
}
function Draw-SlicedPlate($g, $img, [single]$dx, [single]$dy, [single]$dw, [single]$dh, [int]$b) {
  $sw = $img.Width; $sh = $img.Height
  $om = $g.InterpolationMode
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::Bilinear
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]$dx, [int]$dy, $b, [int]$dh)), 0, 0, $b, $sh, [System.Drawing.GraphicsUnit]::Pixel)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]($dx + $dw - $b), [int]$dy, $b, [int]$dh)), ($sw - $b), 0, $b, $sh, [System.Drawing.GraphicsUnit]::Pixel)
  $mw = [int]($dw - 2 * $b)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]($dx + $b), [int]$dy, $mw, [int]$dh)), $b, 0, ($sw - 2 * $b), $sh, [System.Drawing.GraphicsUnit]::Pixel)
  $g.InterpolationMode = $om
}
function Draw-Alpha($g, $img, [single]$x, [single]$y, [single]$w, [single]$h, [single]$a) {
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = 1.0; $cm.Matrix11 = 1.0; $cm.Matrix22 = 1.0; $cm.Matrix33 = $a; $cm.Matrix44 = 1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $dst = New-Object System.Drawing.Rectangle([int]$x, [int]$y, [int]$w, [int]$h)
  $g.DrawImage($img, $dst, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
}
function Draw-TopBarReal($g, $dir, [single]$ox, [single]$oy, [string]$resDir = '') {
  if (-not $resDir) { $resDir = Join-Path $ROOT 'Assets\_Game\Resources\UI' }
  $imgs = @{}
  foreach ($n in @('TopBorder', 'PhaseBand', 'PhaseHideBtn', 'PhaseHideArrow', 'Icon_Health', 'Icon_Energy', 'Icon_CardCount', 'Icon_TurnCount', 'Icon_Settings')) {
    $imgs[$n] = [System.Drawing.Image]::FromFile((Join-Path $dir "$n.png"))
  }
  $rp = Join-Path $dir 'ring_empty.png'; if (-not (Test-Path $rp)) { $rp = Join-Path $resDir 'ring_empty.png' }
  $bpp = Join-Path $dir 'Battle Phase.png'; if (-not (Test-Path $bpp)) { $bpp = Join-Path $resDir 'Battle Phase.png' }
  $ring = [System.Drawing.Image]::FromFile($rp)
  $battle = [System.Drawing.Image]::FromFile($bpp)
  Draw-SlicedPlate $g $imgs['TopBorder'] $ox ($oy + 0) 1920 120 82
  $g.DrawImage($imgs['PhaseBand'], (New-Object System.Drawing.Rectangle([int]($ox + 560), [int]($oy + 5), 800, 52)))
  $ringCells = @(
    @(0, 750, 41, 0.85), @(1, 960, 47, 1.00), @(2, 1170, 41, 0.85))
  foreach ($rc in $ringCells) {
    $im = $ring; if ($rc[1] -eq 1170) { $im = $battle }
    $cx = $ox + $rc[1]; $cy = $oy + 31; $sz = [single]$rc[2]
    Draw-Alpha $g $im ($cx - $sz / 2) ($cy - $sz / 2) $sz $sz ([single]$rc[3])
  }
  $g.DrawImage($imgs['PhaseHideBtn'], (New-Object System.Drawing.Rectangle([int]($ox + 876), [int]($oy + 57), 168, 42)))
  $g.DrawImage($imgs['PhaseHideArrow'], (New-Object System.Drawing.Rectangle([int]($ox + 943), [int]($oy + 65), 34, 26)))
  $cells = @(
    @('Icon_Health', 154, 18, 34, 34, '20', 218),
    @('Icon_Energy', 279, 18, 34, 34, '0', 343),
    @('Icon_CardCount', 404, 18, 34, 34, '3', 463),
    @('Icon_CardCount', 1501, 18, 34, 34, '3', 1560),
    @('Icon_TurnCount', 1626, 18, 34, 34, '1', 1685),
    @('Icon_Settings', 1830, 20, 40, 40, '', 0))
  $f = New-Object System.Drawing.Font('Microsoft YaHei', 20, [System.Drawing.FontStyle]::Bold)
  $sh = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(180, 4, 7, 12))
  $fw = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 234, 240, 250))
  foreach ($c in $cells) {
    $g.DrawImage($imgs[$c[0]], (New-Object System.Drawing.Rectangle([int]($ox + $c[1]), [int]($oy + $c[2]), [int]$c[3], [int]$c[4])))
    if ($c[5] -ne '') {
      $fmt = New-Object System.Drawing.StringFormat
      $fmt.Alignment = [System.Drawing.StringAlignment]::Center
      $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
      $rf = New-Object System.Drawing.RectangleF([single]($ox + $c[6] - 40), [single]($oy + $c[2] - 4), [single]80, [single]42)
      $g.DrawString($c[5], $f, $sh, (New-Object System.Drawing.RectangleF([single]($rf.X + 1.5), [single]($rf.Y + 1.5), [single]80, [single]42)), $fmt)
      $g.DrawString($c[5], $f, $fw, $rf, $fmt)
      $fmt.Dispose()
    }
  }
  $f.Dispose(); $sh.Dispose(); $fw.Dispose()
  foreach ($k in $imgs.Keys) { $imgs[$k].Dispose() }
  $ring.Dispose(); $battle.Dispose()
}
function New-TopBarSet([string]$OutDir) {
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
  $made = @()
  $made += New-TopPlate   (Join-Path $OutDir 'TopBorder.png')
  $made += New-TopBand    (Join-Path $OutDir 'PhaseBand.png')
  $made += New-TopHideBtn (Join-Path $OutDir 'PhaseHideBtn.png')
  $made += New-TopArrow   (Join-Path $OutDir 'PhaseHideArrow.png')
  $made += New-UiIcon 'health'   (Join-Path $OutDir 'Icon_Health.png')
  $made += New-UiIcon 'energy'   (Join-Path $OutDir 'Icon_Energy.png')
  $made += New-UiIcon 'cards'    (Join-Path $OutDir 'Icon_CardCount.png')
  $made += New-UiIcon 'turn'     (Join-Path $OutDir 'Icon_TurnCount.png')
  $made += New-UiIcon 'settings' (Join-Path $OutDir 'Icon_Settings.png')
  $made += New-TopRing   (Join-Path $OutDir 'ring_empty.png')
  $made += New-TopBattle (Join-Path $OutDir 'Battle Phase.png')
  return $made
}
function New-TopBarRealPreview($dir, [string]$out, [string]$board) {
  $b = New-Object System.Drawing.Bitmap(1920, 700, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(9, 12, 18))
  $f13 = New-Object System.Drawing.Font('Microsoft YaHei', 13)
  $g.DrawString('① 真机尺寸（1920 宽 1:1，含阶段圆环 / 收起按钮 / 数字）', $f13, [System.Drawing.Brushes]::White, 20, 8)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 7, 9, 14))), 0, 34, 1920, 170)
  Draw-TopBarReal $g $dir 0 34
  if ($board -and (Test-Path $board)) {
    $bi = [System.Drawing.Image]::FromFile($board)
    $g.DrawImage($bi, (New-Object System.Drawing.Rectangle(0, 230, 1920, 1080)))
    $g.DrawString('② 压在底板 v5 上（真机尺寸，1:1）', $f13, [System.Drawing.Brushes]::White, 12, 214)
    $bi.Dispose()
  }
  $f13.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}
function New-TopBarCompare($oldDir, $newDir, [string]$out, [string]$board) {
  $b = New-Object System.Drawing.Bitmap(1920, 620, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(9, 12, 18))
  $f = New-Object System.Drawing.Font('Microsoft YaHei', 14)
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 209, 138))
  $w = [System.Drawing.Brushes]::White
  if ($board -and (Test-Path $board)) {
    $bi = [System.Drawing.Image]::FromFile($board)
    $g.DrawImage($bi, (New-Object System.Drawing.Rectangle(0, 0, 1920, 1080)))
    $bi.Dispose()
  }
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 0, 0, 0))), 0, 0, 1920, 620)
  $g.DrawString('旧：棕木条 + 五套互不相干的图标 + 米黄圆环', $f, $w, 20, 12)
  Draw-TopBarReal $g $oldDir 0 36
  $g.DrawString('新：轮廓、9-slice、场景位置一字未动，只换皮 —— 与底板 v5 同源的深蓝黑石面 + 金细线，五个图标与阶段圆环同一套配方', $f, $gold, 20, 196)
  Draw-TopBarReal $g $newDir 0 220
  $g.DrawString('同一张底板、同一套位置与字号，两张都是真机尺寸，未做任何放大', $f, $w, 20, 372)
  $f.Dispose(); $gold.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}
function New-TopBarSheet($dir, [string]$out) {
  $b = New-Object System.Drawing.Bitmap(1700, 900, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(14, 17, 24))
  $f = New-Object System.Drawing.Font('Microsoft YaHei', 15)
  $fs = New-Object System.Drawing.Font('Microsoft YaHei', 12)
  $w = [System.Drawing.Brushes]::White
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 209, 138))
  $g.DrawString('顶栏 v1 —— 与底板 v5 / 卡框 v6 同一套语言：深蓝黑石面 + 金细线（金 = #C8A44A，与 CardFrameV6 同值）', $f, $gold, 24, 12)
  $g.DrawString('TopBorder.png 2048x128（轮廓与 9-slice 左右 82px 照抄旧的）', $fs, $w, 24, 46)
  $tb = [System.Drawing.Image]::FromFile((Join-Path $dir 'TopBorder.png'))
  $g.DrawImage($tb, (New-Object System.Drawing.Rectangle(24, 70, 1652, 103))); $tb.Dispose()
  $g.DrawString('图标（256x256 出图，真机 34x34）：左 = 真机大小，右 = 放大 3 倍', $fs, $w, 24, 190)
  $names = @(@('Icon_Health', '生命'), @('Icon_Energy', '能量'), @('Icon_CardCount', '牌数'), @('Icon_TurnCount', '回合'), @('Icon_Settings', '设置'))
  $i = 0
  foreach ($n in $names) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir ($n[0] + '.png')))
    $x = 40 + $i * 74
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle($x, 220, 34, 34)))
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle((620 + $i * 150), 210, 108, 108)))
    $g.DrawString($n[1], $fs, $w, ($x - 2), 258)
    $im.Dispose(); $i++
  }
  $g.DrawString('阶段条 853x55', $fs, $w, 24, 360)
  $pb = [System.Drawing.Image]::FromFile((Join-Path $dir 'PhaseBand.png'))
  $g.DrawImage($pb, (New-Object System.Drawing.Rectangle(24, 382, 853, 55))); $pb.Dispose()
  $g.DrawString('收起按钮 168x42 + 箭头 34x26', $fs, $w, 24, 460)
  $hb = [System.Drawing.Image]::FromFile((Join-Path $dir 'PhaseHideBtn.png'))
  $g.DrawImage($hb, (New-Object System.Drawing.Rectangle(24, 482, 168, 42))); $hb.Dispose()
  $ar = [System.Drawing.Image]::FromFile((Join-Path $dir 'PhaseHideArrow.png'))
  $g.DrawImage($ar, (New-Object System.Drawing.Rectangle(60, 490, 34, 26))); $ar.Dispose()
  $g.DrawString('阶段圆环 511x511：空槽 ring_empty.png / 战斗 Battle Phase.png（真机 34~47px）', $fs, $w, 24, 560)
  $rg = [System.Drawing.Image]::FromFile((Join-Path $dir 'ring_empty.png'))
  $g.DrawImage($rg, (New-Object System.Drawing.Rectangle(24, 584, 120, 120)))
  $g.DrawImage($rg, (New-Object System.Drawing.Rectangle(170, 630, 34, 34)))
  $g.DrawImage($rg, (New-Object System.Drawing.Rectangle(220, 630, 47, 47)))
  $rg.Dispose()
  $bt = [System.Drawing.Image]::FromFile((Join-Path $dir 'Battle Phase.png'))
  $g.DrawImage($bt, (New-Object System.Drawing.Rectangle(320, 584, 120, 120)))
  $g.DrawImage($bt, (New-Object System.Drawing.Rectangle(470, 630, 34, 34)))
  $g.DrawImage($bt, (New-Object System.Drawing.Rectangle(520, 630, 47, 47)))
  $bt.Dispose()
  $f.Dispose(); $fs.Dispose(); $gold.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ══════════════════════════════════════════════════════════
# 出口：整套出图 + 三张预览
# ══════════════════════════════════════════════════════════
function Invoke-TopBarV2([string]$OutDir, [string]$PrevDir, [string]$Board) {
  if (-not (Test-Path $PrevDir)) { New-Item -ItemType Directory -Path $PrevDir -Force | Out-Null }
  $made = New-TopBarSet $OutDir
  New-TopBarSheet         $OutDir (Join-Path $PrevDir 'preview-topbar-v1-sheet.png')
  New-TopBarRealPreview   $OutDir (Join-Path $PrevDir 'preview-topbar-v1-real.png')    $Board
  New-TopBarCompare $SRC  $OutDir (Join-Path $PrevDir 'preview-topbar-v1-compare.png') $Board
  return $made
}
