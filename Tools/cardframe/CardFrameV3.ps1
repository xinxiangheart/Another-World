# 卡框 v3 —— 参考《杀戮尖塔 2》卡面语言重画（去掉珠串边）
# 语言：平涂色边框（外墨线 + 一条亮内沿）／缎带式名条（端头折角下垂 + 平涂投影）／
#       切角立绘窗（浅色内框线 + 窗内硬边投影）／类型页签 + 前缀页签／底栏牌座 + 攻血名牌
Add-Type -AssemblyName System.Drawing

$FW = 1152
$FH = 2016

# ── 几何 ──
$SIL_L = 52;  $SIL_T = 38;  $SIL_R = 1100; $SIL_B = 1980; $SIL_RAD = 60
$PAN_INSET = 40
$PAN_RAD   = 20
$ART_L = 134; $ART_T = 436; $ART_R = 1018; $ART_B = 1597; $ART_CH = 72; $ART_RAD = 20
$RIB_L = 96;  $RIB_R = 1056; $RIB_T = 132; $RIB_B = 312; $RIB_RAD = 12
$ROW_CY = 222
$TAG_CX = 950; $TAG_CY = 222; $TAG_W = 172; $TAG_H = 172; $TAG_RAD = 26
$PRE_CX = 576; $PRE_CY = 372; $PRE_W = 152;  $PRE_H = 134; $PRE_RAD = 20
$BOT_L = 118; $BOT_R = 1034; $BOT_T = 1616; $BOT_B = 1912; $BOT_RAD = 22
$PLT_W = 300; $PLT_H = 196; $PLT_CY = 1764; $PLT_RAD = 24
$HP_CX = 275; $ATK_CX = 877

# ── 配色 ──
$INK      = @(42, 26, 19)
$PARCH    = @(230, 219, 194)
$PARCH_L  = @(244, 238, 224)
$PARCH_D  = @(198, 181, 150)
$RIBBON_L = @(238, 232, 216)
$RIBBON_D = @(190, 175, 150)

$COST_ACCENT = @{
  0 = @(140, 140, 140); 1 = @(245, 240, 228); 2 = @(60, 140, 60)
  3 = @(50, 90, 160);   4 = @(100, 60, 150);  5 = @(232, 200, 80)
}

function New-Col([int[]]$rgb, [int]$a = 255) {
  return [System.Drawing.Color]::FromArgb($a, $rgb[0], $rgb[1], $rgb[2])
}

function Mix-Col([int[]]$a, [int[]]$b, [double]$t) {
  return @([int]($a[0] + ($b[0] - $a[0]) * $t), [int]($a[1] + ($b[1] - $a[1]) * $t), [int]($a[2] + ($b[2] - $a[2]) * $t))
}

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

function Fill-Round($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, [int[]]$rgb, [int]$a = 255) {
  $p = New-RoundPath $x $y $w $h $r
  $b = New-Object System.Drawing.SolidBrush (New-Col $rgb $a)
  $g.FillPath($b, $p)
  $b.Dispose(); $p.Dispose()
}

function Stroke-Round($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, [int[]]$rgb, [single]$t, [int]$a = 255) {
  $p = New-RoundPath $x $y $w $h $r
  $pen = New-Object System.Drawing.Pen (New-Col $rgb $a), $t
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p)
  $pen.Dispose(); $p.Dispose()
}

# 平涂牌座：外墨线 + 硬边暗档（下沿）+ 可选内沿亮线
function New-Slab($g, [single]$cx, [single]$cy, [single]$w, [single]$h, [single]$rad, [int[]]$fill, [int[]]$band, [int[]]$rim, [single]$bandH, [bool]$shadow) {
  if ($shadow) { Fill-Round $g ($cx - $w / 2 + 10) ($cy - $h / 2 + 14) $w $h $rad $INK 70 }
  $p = New-RoundPath ($cx - $w / 2) ($cy - $h / 2) $w $h $rad
  $b = New-Object System.Drawing.SolidBrush (New-Col $fill)
  $g.FillPath($b, $p); $b.Dispose()
  if ($bandH -gt 0) {
    $save = $g.Clip
    $g.SetClip($p)
    $b2 = New-Object System.Drawing.SolidBrush (New-Col $band)
    $g.FillRectangle($b2, ($cx - $w / 2), ($cy + $h / 2 - $bandH), $w, $bandH)
    $b2.Dispose()
    $g.Clip = $save
  }
  $pen = New-Object System.Drawing.Pen (New-Col $INK), 10
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p)
  $pen.Dispose()
  $p.Dispose()
  if ($rim -ne $null) {
    Stroke-Round $g ($cx - $w / 2 + 15) ($cy - $h / 2 + 15) ($w - 30) ($h - 30) ([Math]::Max(2, $rad - 10)) $rim 8
  }
}

function New-CardFrameV3([int]$cost, [string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($FW, $FH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
  $acc  = Mix-Col ($COST_ACCENT[$cost]) @(107, 74, 51) 0.15
  $accL = Mix-Col $acc @(255, 255, 255) 0.42
  $accD = Mix-Col $acc @(0, 0, 0) 0.45

  $sw = $SIL_R - $SIL_L; $sh = $SIL_B - $SIL_T

  # 0) 卡片投影（平涂硬边，偏移右下）
  Fill-Round $g ($SIL_L + 14) ($SIL_T + 20) $sw $sh $SIL_RAD $INK 80

  # 1) 色边框：本体 + 一条亮内沿 + 外墨线
  Fill-Round $g $SIL_L $SIL_T $sw $sh $SIL_RAD $acc
  Stroke-Round $g ($SIL_L + 30) ($SIL_T + 30) ($sw - 60) ($sh - 60) ([Math]::Max(2, $SIL_RAD - 30)) $accL 11
  Stroke-Round $g $SIL_L $SIL_T $sw $sh $SIL_RAD $INK 10

  # 2) 内容面板
  $panL = $SIL_L + $PAN_INSET; $panT = $SIL_T + $PAN_INSET
  $panW = $sw - 2 * $PAN_INSET; $panH = $sh - 2 * $PAN_INSET
  Fill-Round $g $panL $panT $panW $panH $PAN_RAD $PARCH
  Stroke-Round $g $panL $panT $panW $panH $PAN_RAD $INK 12

  # 3) 缎带名条：投影片 → 折角端头（下垂 + 燕尾） → 带面
  Fill-Round $g ($RIB_L + 14) ($RIB_B - 2) ($RIB_R - $RIB_L - 28) 32 12 $INK 55
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
    $b = New-Object System.Drawing.SolidBrush (New-Col $RIBBON_D)
    $g.FillPath($b, $tp); $b.Dispose()
    $pen = New-Object System.Drawing.Pen (New-Col $INK), 10
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $tp)
    $pen.Dispose(); $tp.Dispose()
  }
  $bar = New-RoundPath $RIB_L $RIB_T ($RIB_R - $RIB_L) ($RIB_B - $RIB_T) $RIB_RAD
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $RIBBON_L)), $bar)
  $save = $g.Clip
  $g.SetClip($bar)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $RIBBON_D)), $RIB_L, ($RIB_B - 16), ($RIB_R - $RIB_L), 16)
  $g.Clip = $save
  $pen = New-Object System.Drawing.Pen (New-Col $INK), 10
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $bar)
  $pen.Dispose(); $bar.Dispose()

  # 4) 类型页签（名字右侧，浅底好读图标）
  New-Slab $g $TAG_CX $TAG_CY $TAG_W $TAG_H $TAG_RAD $PARCH_L $PARCH_D $acc 24 $true

  # 5) 前缀页签（缎带下方）
  New-Slab $g $PRE_CX $PRE_CY $PRE_W $PRE_H $PRE_RAD $PARCH_L $PARCH_D $null 24 $true

  # 6) 底栏牌座 + 攻血名牌
  Fill-Round $g $BOT_L $BOT_T ($BOT_R - $BOT_L) ($BOT_B - $BOT_T) $BOT_RAD $PARCH_D
  Stroke-Round $g $BOT_L $BOT_T ($BOT_R - $BOT_L) ($BOT_B - $BOT_T) $BOT_RAD $INK 10
  Stroke-Round $g ($BOT_L + 14) ($BOT_T + 14) ($BOT_R - $BOT_L - 28) ($BOT_B - $BOT_T - 28) ([Math]::Max(2, $BOT_RAD - 10)) $PARCH_L 7
  foreach ($cx in @($HP_CX, $ATK_CX)) {
    New-Slab $g $cx $PLT_CY $PLT_W $PLT_H $PLT_RAD $PARCH_L $PARCH_D $null 32 $true
  }

  # 7) 立绘窗：切角窗形 → 擦透明 → 墨线 + 浅色内框线 → 窗内硬边投影
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

  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 0, 0, 0))), $wp)
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver

  $pen = New-Object System.Drawing.Pen (New-Col $INK), 40
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $wp); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen (New-Col $PARCH_L), 18
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $wp); $pen.Dispose()
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $INK 75)), ($ART_L + 22), ($ART_T + 10), ($ART_R - $ART_L - 44), 30)
  $wp.Dispose()

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
