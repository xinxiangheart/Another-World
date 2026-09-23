# 顶栏 v1 —— 与战场底板 v5 同一套语言（深蓝黑石面 + 金细线 + 暗刻）
# 轮廓逐列照抄现有 sprite，保证 9-slice 与场景里的位置完全对得上。
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

$ROOT = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$SRC  = Join-Path $ROOT 'Assets\_Game\Art\Sprites\UI'

$INK     = @(6, 9, 14)
$INK2    = @(10, 15, 23)
$NAVY_T  = @(22, 29, 43)
$NAVY_B  = @(11, 15, 24)
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
function New-TopPlate([string]$out) {
  $prof = Get-BottomProfile (Join-Path $SRC 'TopBorder.png')
  $r = New-Bmp 2048 128; $b = $r[0]; $g = $r[1]
  $p = New-ProfilePath $prof
  $st = $g.Save(); $g.SetClip($p)
  Fill-VGrad $g 0 0 2048 128 $NAVY_T $NAVY_B 255 255
  Fill-HGrad $g 0 0 300 128 $INK $INK 80 0
  Fill-HGrad $g 1748 0 300 128 $INK $INK 0 80
  foreach ($yy in @(8.5, 12.5, 16.5)) {
    $pen = New-Object System.Drawing.Pen ((New-Col $INK 44)), 1.6
    $g.DrawLine($pen, 0, $yy, 2048, $yy); $pen.Dispose()
  }
  for ($x = 620; $x -le 1430; $x += 26) {
    $pen = New-Object System.Drawing.Pen ((New-Col $INK 34)), 1.2
    $g.DrawLine($pen, $x, 20, $x, 25); $pen.Dispose()
  }
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 168)), 2.6
  $g.DrawLine($pen, 0, 1.6, 2048, 1.6); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 76)), 1.2
  $g.DrawLine($pen, 0, 4.4, 2048, 4.4); $pen.Dispose()
  Stroke-Profile $g $prof $INK 7 76 3
  Stroke-Profile $g $prof $GOLD 2.6 205 -1.6
  Stroke-Profile $g $prof $GOLD_L 1.2 84 -3.8
  foreach ($cx in @(114, 530, 1519, 1935)) {
    $pc = $prof[$cx]
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 96)), 2
    $g.DrawLine($pen, $cx, ($pc - 13), $cx, $pc); $pen.Dispose()
  }
  foreach ($cx in @(573, 1476)) {
    $pc = $prof[$cx]
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 96)), 2
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLine($pen, ($cx - 10), ($pc - 11), $cx, ($pc - 1))
    $g.DrawLine($pen, $cx, ($pc - 1), ($cx + 10), ($pc - 11))
    $pen.Dispose()
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
# 2. 阶段条 853x55（轮廓照抄 PhaseBand.png）
# ══════════════════════════════════════════════════════════
function New-TopBand([string]$out) {
  $prof = Get-BottomProfile (Join-Path $SRC 'PhaseBand.png')
  $r = New-Bmp 853 55; $b = $r[0]; $g = $r[1]
  $p = New-ProfilePath $prof
  $st = $g.Save(); $g.SetClip($p)
  Fill-VGrad $g 0 0 853 55 @(27,35,52) @(13,18,29) 255 255
  Fill-HGrad $g 0 0 100 55 $INK $INK 74 0
  Fill-HGrad $g 753 0 100 55 $INK $INK 0 74
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 52)), 1.6
  $g.DrawLine($pen, 0, 11, 853, 11); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 38)), 1.4
  $g.DrawLine($pen, 0, 44, 853, 44); $pen.Dispose()
  $g.Restore($st)
  Stroke-Profile $g $prof $INK 6 72 3
  Stroke-Profile $g $prof $GOLD 2.2 190 -1.4
  Stroke-Profile $g $prof $GOLD_L 1.2 80 -3.4
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2.2
  $g.DrawLine($pen, 0, 1.4, 853, 1.4); $pen.Dispose()
  foreach ($s in @(1, -1)) {
    $x0 = 42.0
    if ($s -lt 0) { $x0 = 811.0 }
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 118)), 2.2
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLine($pen, ($x0 - 9 * $s), 18, $x0, 27)
    $g.DrawLine($pen, $x0, 27, ($x0 - 9 * $s), 36)
    $pen.Dispose()
  }
  return (Save-Bmp $b $g $out)
}

# ══════════════════════════════════════════════════════════
# 3. 收起按钮 168x42（轮廓照抄 PhaseHideBtn.png）+ 箭头 34x26
# ══════════════════════════════════════════════════════════
function New-TopHideBtn([string]$out) {
  $prof = Get-BottomProfile (Join-Path $SRC 'PhaseHideBtn.png')
  $r = New-Bmp 168 42; $b = $r[0]; $g = $r[1]
  $p = New-ProfilePath $prof
  $st = $g.Save(); $g.SetClip($p)
  Fill-VGrad $g 0 0 168 42 @(31,40,58) @(14,19,30) 255 255
  Fill-HGrad $g 0 0 60 42 $INK $INK 60 0
  Fill-HGrad $g 108 0 60 42 $INK $INK 0 60
  $g.Restore($st)
  Stroke-Profile $g $prof $INK 5 82 2.5
  Stroke-Profile $g $prof $GOLD 2 195 -1
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawLine($pen, 0, 1.2, 168, 1.2); $pen.Dispose()
  return (Save-Bmp $b $g $out)
}
function New-TopArrow([string]$out) {
  $r = New-Bmp 34 26; $b = $r[0]; $g = $r[1]
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(4, 5)), (New-Object System.Drawing.PointF(30, 5)),
    (New-Object System.Drawing.PointF(17, 21))))
  $p.CloseFigure()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 230)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose()
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 240)
  $g.FillPath($bs, $p); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 150)), 1.4
  $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
  return (Save-Bmp $b $g $out)
}

# ══════════════════════════════════════════════════════════
# 4. 五个 34x34 的图标（256x256 出图）
#    统一配方：① 深墨描边 ② 平涂主色 ③ 硬边暗块 ④ 一处金色点缀
# ══════════════════════════════════════════════════════════
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
# ══════════════════════════════════════════════════════════
# 5. 出口：整套 + 预览
# ══════════════════════════════════════════════════════════
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
  return $made
}

function Draw-SlicedPlate($g, $img, [single]$dx, [single]$dy, [single]$dw, [single]$dh, [int]$b) {
  $sw = $img.Width; $sh = $img.Height
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]$dx, [int]$dy, $b, [int]$dh)), 0, 0, $b, $sh, [System.Drawing.GraphicsUnit]::Pixel)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]($dx + $dw - $b), [int]$dy, $b, [int]$dh)), ($sw - $b), 0, $b, $sh, [System.Drawing.GraphicsUnit]::Pixel)
  $mw = [int]($dw - 2 * $b)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]($dx + $b), [int]$dy, $mw, [int]$dh)), $b, 0, ($sw - 2 * $b), $sh, [System.Drawing.GraphicsUnit]::Pixel)
}

# 按场景里的真实几何画一遍（参考分辨率 1920x1080，左上为原点）
function Draw-TopBarReal($g, $dir, [single]$ox, [single]$oy) {
  $imgs = @{}
  foreach ($n in @('TopBorder','PhaseBand','PhaseHideBtn','PhaseHideArrow','Icon_Health','Icon_Energy','Icon_CardCount','Icon_TurnCount','Icon_Settings')) {
    $imgs[$n] = [System.Drawing.Image]::FromFile((Join-Path $dir "$n.png"))
  }
  Draw-SlicedPlate $g $imgs['TopBorder'] $ox ($oy + 0) 1920 120 82
  $g.DrawImage($imgs['PhaseBand'], (New-Object System.Drawing.Rectangle([int]($ox + 560), [int]($oy + 5), 800, 52)))
  $g.DrawImage($imgs['PhaseHideBtn'], (New-Object System.Drawing.Rectangle([int]($ox + 876), [int]($oy + 57), 168, 42)))
  $g.DrawImage($imgs['PhaseHideArrow'], (New-Object System.Drawing.Rectangle([int]($ox + 943), [int]($oy + 65), 34, 26)))
  $cells = @(
    @('Icon_Health',     154,  18, 34, 34, '20', 218),
    @('Icon_Energy',     279,  18, 34, 34, '0',  343),
    @('Icon_CardCount',  404,  18, 34, 34, '3',  463),
    @('Icon_TurnCount', 1626,  18, 34, 34, '1', 1685),
    @('Icon_CardCount', 1501,  18, 34, 34, '3', 1560),
    @('Icon_Settings',  1830,  20, 40, 40, '',     0)
  )
  $f  = New-Object System.Drawing.Font('Microsoft YaHei', 20, [System.Drawing.FontStyle]::Bold)
  $sh = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(180,4,7,12))
  $fw = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,234,240,250))
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
}

function New-TopBarRealPreview($dir, [string]$out, [string]$board) {
  $b = New-Object System.Drawing.Bitmap(1920, 620, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(9,12,18))
  $g.DrawString('① 真机尺寸（1920 宽，1:1，未放大）—— 直接看数字读不读得清', (New-Object System.Drawing.Font('Microsoft YaHei', 15)), [System.Drawing.Brushes]::White, 20, 10)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,7,9,14))), 0, 44, 1920, 130)
  Draw-TopBarReal $g $dir 0 44
  if ($board -and (Test-Path $board)) {
    $bi = [System.Drawing.Image]::FromFile($board)
    $g.DrawImage($bi, (New-Object System.Drawing.Rectangle(0, 190, 1920, 1080)))
    $g.DrawString('② 压在底板 v5 上（真机尺寸）', (New-Object System.Drawing.Font('Microsoft YaHei', 13)), [System.Drawing.Brushes]::White, 12, 174)
    $bi.Dispose()
  }
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

function New-TopBarSheet($dir, [string]$out) {
  $b = New-Object System.Drawing.Bitmap(1920, 760, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(14,17,24))
  $f  = New-Object System.Drawing.Font('Microsoft YaHei', 15)
  $fs = New-Object System.Drawing.Font('Microsoft YaHei', 12)
  $w  = [System.Drawing.Brushes]::White
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
  $g.DrawString('顶栏 v1 —— 与底板 v5 同一套语言：深蓝黑石面 + 金细线 + 暗刻', $f, $gold, 24, 12)
  $g.DrawString('TopBorder.png 2048x128（轮廓与 9-slice 左右 82px 照抄旧的，直接换文件即可）', $fs, $w, 24, 44)
  $tb = [System.Drawing.Image]::FromFile((Join-Path $dir 'TopBorder.png'))
  $g.DrawImage($tb, (New-Object System.Drawing.Rectangle(24, 68, 1872, 117))); $tb.Dispose()
  $g.DrawString('图标（256x256 出图，真机 34x34）：左=真机大小，右=放大 3 倍', $fs, $w, 24, 200)
  $names = @(@('Icon_Health','生命'), @('Icon_Energy','能量'), @('Icon_CardCount','牌数'), @('Icon_TurnCount','回合'), @('Icon_Settings','设置'))
  $i = 0
  foreach ($n in $names) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir ($n[0] + '.png')))
    $x = 40 + $i * 74
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle($x, 228, 34, 34)))
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle((560 + $i * 140), 226, 104, 104)))
    $g.DrawString($n[1], $fs, $w, ($x - 2), 268)
    $im.Dispose(); $i++
  }
  $g.DrawString('阶段条 853x55', $fs, $w, 24, 350)
  $pb = [System.Drawing.Image]::FromFile((Join-Path $dir 'PhaseBand.png'))
  $g.DrawImage($pb, (New-Object System.Drawing.Rectangle(24, 372, 853, 55))); $pb.Dispose()
  $g.DrawString('收起按钮 168x42 + 箭头 34x26', $fs, $w, 24, 450)
  $hb = [System.Drawing.Image]::FromFile((Join-Path $dir 'PhaseHideBtn.png'))
  $g.DrawImage($hb, (New-Object System.Drawing.Rectangle(24, 472, 168, 42))); $hb.Dispose()
  $ar = [System.Drawing.Image]::FromFile((Join-Path $dir 'PhaseHideArrow.png'))
  $g.DrawImage($ar, (New-Object System.Drawing.Rectangle(60, 480, 34, 26))); $ar.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}
function New-TopBarCompare($oldDir, $newDir, [string]$out, [string]$board) {
  $b = New-Object System.Drawing.Bitmap(1920, 460, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(9,12,18))
  $f  = New-Object System.Drawing.Font('Microsoft YaHei', 14)
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
  $w = [System.Drawing.Brushes]::White
  if ($board -and (Test-Path $board)) {
    $bi = [System.Drawing.Image]::FromFile($board)
    $g.DrawImage($bi, (New-Object System.Drawing.Rectangle(0, 0, 1920, 1080)))
    $bi.Dispose()
  }
  $g.DrawString('旧（棕木条 + 五个体系各一套的图标）', $f, $w, 20, 14)
  Draw-TopBarReal $g $oldDir 0 36
  $g.DrawString('新（顶栏 v1：轮廓不变，换成与底板同源的深蓝黑 + 金细线；五个图标同一套配方）', $f, $gold, 20, 176)
  Draw-TopBarReal $g $newDir 0 198
  $g.DrawString('同一张底板、同一套位置与字号，左右各画一遍，未做任何放大', $f, $w, 20, 330)
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}