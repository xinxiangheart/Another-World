# 卡框 v6 —— 深青钢 + 金饰 + 辉光（对齐开始界面夜色与 STS2 实机 UI 语言）
Add-Type -AssemblyName System.Drawing

$FW = 1152; $FH = 2016
$SIL_L = 52;  $SIL_T = 38;  $SIL_R = 1100; $SIL_B = 1980; $SIL_RAD = 60
$PAN_INSET = 40; $PAN_RAD = 20
$ART_L = 134; $ART_T = 436; $ART_R = 1018; $ART_B = 1597; $ART_CH = 72; $ART_RAD = 20
$RIB_L = 96;  $RIB_R = 1056; $RIB_T = 132; $RIB_B = 312; $RIB_RAD = 14
$ROW_CY = 222
$TAG_CX = 950; $TAG_CY = 222; $TAG_W = 172; $TAG_H = 172; $TAG_RAD = 26
$PRE_CX = 576; $PRE_CY = 372; $PRE_W = 152;  $PRE_H = 134; $PRE_RAD = 20
$BOT_L = 118; $BOT_R = 1034; $BOT_T = 1616; $BOT_B = 1912; $BOT_RAD = 22
$PLT_R = 104; $PLT_CY = 1764; $HP_CX = 275; $ATK_CX = 877

$INK   = @(5, 8, 13)
$BASE_T = @(30, 41, 56)
$BASE_B = @(14, 20, 29)
$PLATE_T = @(36, 49, 66)
$PLATE_B = @(20, 28, 39)
$STEEL = @(92, 124, 152)
$GOLD  = @(200, 164, 74)
$GOLD_L = @(232, 209, 138)
$GOLD_D = @(128, 100, 42)

$COST_ACCENT = @{
  0 = @(150, 160, 170); 1 = @(226, 226, 220); 2 = @(74, 168, 84)
  3 = @(70, 124, 206);  4 = @(140, 96, 200);  5 = @(232, 186, 72)
}

function New-Col([int[]]$rgb, [int]$a = 255) { return [System.Drawing.Color]::FromArgb($a, $rgb[0], $rgb[1], $rgb[2]) }
function Mix-Col([int[]]$a, [int[]]$b, [double]$t) { return @([int]($a[0] + ($b[0] - $a[0]) * $t), [int]($a[1] + ($b[1] - $a[1]) * $t), [int]($a[2] + ($b[2] - $a[2]) * $t)) }

function New-RoundPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  if ($r -le 0.5) { $p.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h))); return $p }
  $d = $r * 2
  if ($d -gt $w) { $d = $w }
  if ($d -gt $h) { $d = $h }
  $p.AddArc($x, $y, $d, $d, 180, 90)
  $p.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
  $p.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
  $p.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

function Fill-RoundGrad($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, [int[]]$cTop, [int[]]$cBot) {
  $p = New-RoundPath $x $y $w $h $r
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle([int]$x, [int]$y, [int]$w, [int]$h)), (New-Col $cTop), (New-Col $cBot), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $sv = $g.Clip; $g.SetClip($p); $g.FillRectangle($lg, $x, $y, $w, $h); $g.Clip = $sv
  $lg.Dispose(); $p.Dispose()
}

function Stroke-RoundCol($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, [int[]]$rgb, [single]$t, [int]$a = 255) {
  $p = New-RoundPath $x $y $w $h $r
  $pen = New-Object System.Drawing.Pen (New-Col $rgb $a), $t
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
}

# 辉光：同一条轮廓叠几层由粗到细的低透明描边
function Stroke-Glow($g, $path, [int[]]$rgb, [single]$base, [int]$steps) {
  for ($i = $steps; $i -ge 1; $i--) {
    $w = $base * $i
    $a = [int](70 / $i)
    if ($a -lt 8) { $a = 8 }
    $pen = New-Object System.Drawing.Pen (New-Col $rgb $a), $w
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path); $pen.Dispose()
  }
}

function New-CardFrameV6([int]$cost, [string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($FW, $FH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $acc = $COST_ACCENT[$cost]
  $sw = $SIL_R - $SIL_L; $sh = $SIL_B - $SIL_T

  # 0) 费用色辉光 + 落影
  $glowP = New-RoundPath $SIL_L $SIL_T $sw $sh $SIL_RAD
  Stroke-Glow $g $glowP $acc 16 4
  $sh1 = New-RoundPath ($SIL_L + 12) ($SIL_T + 18) $sw $sh $SIL_RAD
  $pen = New-Object System.Drawing.Pen ((New-Col @(0,0,0) 120)), 26
  $g.DrawPath($pen, $sh1); $pen.Dispose(); $sh1.Dispose()
  $glowP.Dispose()

  # 1) 卡体：深青钢渐变
  Fill-RoundGrad $g $SIL_L $SIL_T $sw $sh $SIL_RAD $BASE_T $BASE_B
  # 金色内框 + 青钢细线
  Stroke-RoundCol $g ($SIL_L + 18) ($SIL_T + 18) ($sw - 36) ($sh - 36) ([Math]::Max(2, $SIL_RAD - 18)) $GOLD 7
  Stroke-RoundCol $g ($SIL_L + 32) ($SIL_T + 32) ($sw - 64) ($sh - 64) ([Math]::Max(2, $SIL_RAD - 32)) $STEEL 3 150
  # 外墨线
  $pen = New-Object System.Drawing.Pen (New-Col $INK), 9
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $p0 = New-RoundPath $SIL_L $SIL_T $sw $sh $SIL_RAD
  $g.DrawPath($pen, $p0); $pen.Dispose(); $p0.Dispose()

  # 2) 四角金饰角铁
  $arm = 96; $t = 10; $o = 34
  foreach ($pair in @(@(0,0),@(1,0),@(0,1),@(1,1))) {
    $sx = $pair[0]; $sy = $pair[1]
    $x0 = if ($sx -eq 0) { ($SIL_L + $o) } else { ($SIL_R - $o) }
    $y0 = if ($sy -eq 0) { ($SIL_T + $o) } else { ($SIL_B - $o) }
    $dx = if ($sx -eq 0) { 1 } else { -1 }
    $dy = if ($sy -eq 0) { 1 } else { -1 }
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddLines([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(($x0 + $dx * $arm), $y0)),
      (New-Object System.Drawing.PointF($x0, $y0)),
      (New-Object System.Drawing.PointF($x0, ($y0 + $dy * $arm)))))
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 210)), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }

  # 3) 名条：深钢板 + 金边 + 燕尾端头
  Fill-RoundGrad $g ($RIB_L + 10) ($RIB_B - 6) ($RIB_R - $RIB_L - 20) 40 14 @(0,0,0) @(0,0,0)
  foreach ($dir in @(-1, 1)) {
    $x0 = if ($dir -lt 0) { $RIB_L } else { $RIB_R }
    $out  = if ($dir -lt 0) { 56 } else { ($FW - 56) }
    $notch = if ($dir -lt 0) { 78 } else { ($FW - 78) }
    $pts = [System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF($x0, $RIB_T)),
      (New-Object System.Drawing.PointF($out, ($RIB_T + 46))),
      (New-Object System.Drawing.PointF($notch, ($RIB_T + 108))),
      (New-Object System.Drawing.PointF($out, ($RIB_B + 10))),
      (New-Object System.Drawing.PointF($x0, $RIB_B)))
    $tp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tp.AddPolygon($pts)
    $sv = $g.Clip; $g.SetClip($tp)
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(40, $RIB_T, 200, ($RIB_B + 10 - $RIB_T))), (New-Col @(26,36,48)), (New-Col @(14,20,29)), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($lg, 0, 0, $FW, $FH); $lg.Dispose()
    $g.Clip = $sv
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D)), 9
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $tp); $pen.Dispose(); $tp.Dispose()
  }
  $bar = New-RoundPath $RIB_L $RIB_T ($RIB_R - $RIB_L) ($RIB_B - $RIB_T) $RIB_RAD
  $sv = $g.Clip; $g.SetClip($bar)
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($RIB_L, $RIB_T, 100, ($RIB_B - $RIB_T))), (New-Col @(44,58,78)), (New-Col @(22,30,41)), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $RIB_L, $RIB_T, ($RIB_R - $RIB_L), ($RIB_B - $RIB_T)); $lg.Dispose()
  # 带面顶部一道高光 + 底部一道暗档
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(120,150,180) 90))), $RIB_L, ($RIB_T + 8), ($RIB_R - $RIB_L), 6)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(0,0,0) 110))), $RIB_L, ($RIB_B - 18), ($RIB_R - $RIB_L), 18)
  $g.Clip = $sv
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 9
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $bar); $pen.Dispose(); $bar.Dispose()

  # 4) 类型页签 / 前缀页签
  foreach ($t in @(@($TAG_CX, $TAG_CY, $TAG_W, $TAG_H, $TAG_RAD), @($PRE_CX, $PRE_CY, $PRE_W, $PRE_H, $PRE_RAD))) {
    $cx = $t[0]; $cy = $t[1]; $w = $t[2]; $h = $t[3]; $r = $t[4]
    $x0 = $cx - $w / 2; $y0 = $cy - $h / 2
    $pp = New-RoundPath ($x0 + 8) ($y0 + 10) $w $h $r
    $pen = New-Object System.Drawing.Pen ((New-Col @(0,0,0) 130)), 18
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
    Fill-RoundGrad $g $x0 $y0 $w $h $r $PLATE_T $PLATE_B
    Stroke-RoundCol $g ($x0 + 10) ($y0 + 10) ($w - 20) ($h - 20) ([Math]::Max(2, $r - 8)) $GOLD 5
    $pen = New-Object System.Drawing.Pen (New-Col $GOLD), 8
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $pp = New-RoundPath $x0 $y0 $w $h $r
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }

  # 5) 底栏牌座 + 攻血圆座
  $x0 = $BOT_L; $y0 = $BOT_T; $w = $BOT_R - $BOT_L; $h = $BOT_B - $BOT_T
  Fill-RoundGrad $g $x0 $y0 $w $h $BOT_RAD @(24,33,45) @(15,21,30)
  Stroke-RoundCol $g ($x0 + 14) ($y0 + 14) ($w - 28) ($h - 28) ([Math]::Max(2, $BOT_RAD - 10)) $GOLD 5 190
  Stroke-RoundCol $g $x0 $y0 $w $h $BOT_RAD $INK 10
  foreach ($cx in @($HP_CX, $ATK_CX)) {
    $c = New-Object System.Drawing.Drawing2D.GraphicsPath
    $c.AddEllipse(($cx - $PLT_R), ($PLT_CY - $PLT_R), (2 * $PLT_R), (2 * $PLT_R))
    $sv = $g.Clip; $g.SetClip($c)
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(($cx - $PLT_R), ($PLT_CY - $PLT_R), (2 * $PLT_R), (2 * $PLT_R))), (New-Col @(40,54,72)), (New-Col @(16,23,32)), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($lg, ($cx - $PLT_R), ($PLT_CY - $PLT_R), (2 * $PLT_R), (2 * $PLT_R)); $lg.Dispose()
    $g.Clip = $sv
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 8
    $g.DrawPath($pen, $c); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK)), 6
    $c2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $c2.AddEllipse(($cx - $PLT_R - 7), ($PLT_CY - $PLT_R - 7), (2 * ($PLT_R + 7)), (2 * ($PLT_R + 7)))
    $g.DrawPath($pen, $c2); $pen.Dispose(); $c2.Dispose(); $c.Dispose()
  }

  # 6) 立绘窗：切角 + 金框 + 窗内暗影
  $wp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $wp.AddLine(($ART_L + $ART_RAD), $ART_T, ($ART_R - $ART_RAD), $ART_T)
  $wp.AddArc(($ART_R - 2 * $ART_RAD), $ART_T, (2 * $ART_RAD), (2 * $ART_RAD), 270, 90)
  $wp.AddLine($ART_R, ($ART_T + $ART_RAD), $ART_R, ($ART_B - $ART_CH))
  $wp.AddLine($ART_R, ($ART_B - $ART_CH), ($ART_R - $ART_CH), $ART_B)
  $wp.AddLine(($ART_R - $ART_CH), $ART_B, ($ART_L + $ART_CH), $ART_B)
  $wp.AddLine(($ART_L + $ART_CH), $ART_B, $ART_L, ($ART_B - $ART_CH))
  $wp.AddLine($ART_L, ($ART_B - $ART_CH), $ART_L, ($ART_T + $ART_RAD))
  $wp.AddArc($ART_L, $ART_T, (2 * $ART_RAD), (2 * $ART_RAD), 180, 90)
  $wp.CloseFigure()

  # 先画窗外的金框（在擦除之前）
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 34
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $wp); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK)), 12
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $wp2 = $wp.Clone()
  $g.DrawPath($pen, $wp2); $pen.Dispose(); $wp2.Dispose()

  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0,0,0,0))), $wp)
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
  # 窗内：上沿柔和暗影（落在卡图上）
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(($ART_L + 20), ($ART_T + 8), ([int]($ART_R - $ART_L - 40)), 90)), (New-Col @(0,0,0) 150), (New-Col @(0,0,0) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, ($ART_L + 20), ($ART_T + 8), ($ART_R - $ART_L - 40), 90); $lg.Dispose()
  $wp.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
