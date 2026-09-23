# 卡面边框 v2 生成器（召唤物）—— 平涂赛璐璐 + 手绘装饰密度
# 守：2-3 级硬边色块、无渐变无浮雕、统一粗细的粗墨线。
# 做回：卷轴名条、珠串内边、四角卷花、骑缝线、底部徽记、火漆式种类座。
Add-Type -AssemblyName System.Drawing

$W = 1152
$H = 2016

# ── 几何 ──
$SIL_L = 50;  $SIL_T = 36;  $SIL_R = 1102; $SIL_B = 1983; $SIL_RAD = 60
$INK_M1  = 18
$ACC_T   = 42
$ROPE_T  = 22
$INK_M2  = 8
$B0      = 90                      # 18+42+22+8 -> 羊皮纸本体
$BAND_B  = 436                     # 名条下沿 ＝ 立绘窗口上沿
$ART_L = 140; $ART_T = 436; $ART_R = 1012; $ART_B = 1560; $ART_RAD = 22
$ROW_CY  = 205
$NAME_L  = 250
$SEAL_CX = 924; $SEAL_R = 86
$PLAQ_W  = 300; $PLAQ_H = 268; $PLAQ_CY = 1764
$HP_CX = 275; $ATK_CX = 877
$ROLL_W  = 92
$MED_CX = 576; $MED_CY = 1854; $MED_R = 36

# ── 配色 ──
$INK   = @(42, 23, 18)
$STEP  = @(176, 155, 120)
$PARCH = @(233, 222, 196)
$BANDC = @(214, 197, 164)
$SOCKC = @(203, 183, 148)
$ROLLC = @(240, 231, 210)

$COST_ACCENT = @{
  0 = @(140, 140, 140); 1 = @(245, 240, 228); 2 = @(60, 140, 60)
  3 = @(50, 90, 160);   4 = @(100, 60, 150);  5 = @(232, 200, 80)
}

function New-Col([int[]]$rgb, [int]$a = 255) {
  return [System.Drawing.Color]::FromArgb($a, $rgb[0], $rgb[1], $rgb[2])
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

function Stroke-Round($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, [int[]]$rgb, [single]$t) {
  $p = New-RoundPath $x $y $w $h $r
  $pen = New-Object System.Drawing.Pen (New-Col $rgb), $t
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p)
  $pen.Dispose(); $p.Dispose()
}

# 双描边：先按「填充色 + 2×墨边」铺墨，再用填充色盖回去 —— 得到统一粗细的手绘墨线轮廓
function Stroke-Double($g, $path, [int[]]$fill, [int[]]$ink, [single]$w, [single]$inkW) {
  $p1 = New-Object System.Drawing.Pen (New-Col $ink), ($w + 2 * $inkW)
  $p1.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $p1.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $p1.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawPath($p1, $path)
  $p1.Dispose()
  $p2 = New-Object System.Drawing.Pen (New-Col $fill), $w
  $p2.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $p2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $p2.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawPath($p2, $path)
  $p2.Dispose()
}

# 手绘感的抖动折线
function New-WobblePath([single]$x1, [single]$y1, [single]$x2, [single]$y2, [single]$amp, [int]$seed) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $len = [Math]::Sqrt(($x2 - $x1) * ($x2 - $x1) + ($y2 - $y1) * ($y2 - $y1))
  $n = [Math]::Max(4, [int]($len / 22))
  $nx = -($y2 - $y1) / $len
  $ny = ($x2 - $x1) / $len
  $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($i = 0; $i -le $n; $i++) {
    $t = $i / [single]$n
    $o = $amp * ([Math]::Sin($t * 6.28 * 1.5 + $seed) * 0.6 + [Math]::Sin($t * 6.28 * 3.7 + $seed * 2.3) * 0.4)
    if ($i -eq 0 -or $i -eq $n) { $o = 0 }
    $pts.Add((New-Object System.Drawing.PointF(($x1 + ($x2 - $x1) * $t + $nx * $o), ($y1 + ($y2 - $y1) * $t + $ny * $o))))
  }
  $p.AddLines($pts.ToArray())
  return $p
}

function Get-InsetBox([int]$n, [hashtable]$sil, [single]$sw, [single]$sh) {
  return @{
    x = $sil.L + $n; y = $sil.T + $n
    w = $sw - 2 * $n; h = $sh - 2 * $n
    rad = [Math]::Max(2, $sil.Rad - $n)
  }
}

function New-CardFrame([int]$cost, [string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
  $acc = $COST_ACCENT[$cost]

  $sil = @{ L = $SIL_L; T = $SIL_T; R = $SIL_R; B = $SIL_B; Rad = $SIL_RAD }
  $sw = $SIL_R - $SIL_L
  $sh = $SIL_B - $SIL_T

  # 1~5) 同心平涂环：墨边 / 费用色 / 珠串底 / 内墨线 / 羊皮纸
  $b1 = Get-InsetBox 0 $sil $sw $sh;                                 Fill-Round $g $b1.x $b1.y $b1.w $b1.h $b1.rad $INK
  $b2 = Get-InsetBox $INK_M1 $sil $sw $sh;                           Fill-Round $g $b2.x $b2.y $b2.w $b2.h $b2.rad $acc
  $b3 = Get-InsetBox ($INK_M1 + $ACC_T) $sil $sw $sh;                Fill-Round $g $b3.x $b3.y $b3.w $b3.h $b3.rad $STEP
  $b4 = Get-InsetBox ($B0 - $INK_M2) $sil $sw $sh;                   Fill-Round $g $b4.x $b4.y $b4.w $b4.h $b4.rad $INK
  $b5 = Get-InsetBox $B0 $sil $sw $sh;                               Fill-Round $g $b5.x $b5.y $b5.w $b5.h $b5.rad $PARCH

  $bodyL = $b5.x; $bodyT = $b5.y; $bodyR = ($b5.x + $b5.w); $bodyB = ($b5.y + $b5.h); $bodyW = $b5.w

  # 6) 珠串内边：沿四点中点走一圈平涂小菱珠（四角交给角花）
  $midInset = $INK_M1 + $ACC_T + [int]($ROPE_T / 2)
  $midY1 = $SIL_T + $midInset; $midY2 = $SIL_B - $midInset
  $midX1 = $SIL_L + $midInset; $midX2 = $SIL_R - $midInset
  $bead = 13; $stepB = 46
  for ($x = ($midX1 + 130); $x -lt ($midX2 - 130); $x += $stepB) {
    foreach ($yy in @($midY1, $midY2)) {
      $p = New-Object System.Drawing.Drawing2D.GraphicsPath
      $pts = [System.Drawing.PointF[]]@(
          (New-Object System.Drawing.PointF($x, ($yy - $bead))),
          (New-Object System.Drawing.PointF(($x + $bead), $yy)),
          (New-Object System.Drawing.PointF($x, ($yy + $bead))),
          (New-Object System.Drawing.PointF(($x - $bead), $yy)))
      $p.AddPolygon($pts)
      $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $INK)), $p)
      $p.Dispose()
    }
  }
  for ($y = ($midY1 + 130); $y -lt ($midY2 - 130); $y += $stepB) {
    foreach ($xx in @($midX1, $midX2)) {
      $p = New-Object System.Drawing.Drawing2D.GraphicsPath
      $pts = [System.Drawing.PointF[]]@(
          (New-Object System.Drawing.PointF($xx, ($y - $bead))),
          (New-Object System.Drawing.PointF(($xx + $bead), $y)),
          (New-Object System.Drawing.PointF($xx, ($y + $bead))),
          (New-Object System.Drawing.PointF(($xx - $bead), $y)))
      $p.AddPolygon($pts)
      $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $INK)), $p)
      $p.Dispose()
    }
  }

  # 7) 四角角花：珠串的「大珠」——平涂圆牌 + 双层墨线，四角各一
  $cr = 44
  foreach ($px in @(($SIL_L + $midInset), ($SIL_R - $midInset))) {
    foreach ($py in @(($SIL_T + $midInset), ($SIL_B - $midInset))) {
      Fill-Round $g ($px - $cr) ($py - $cr) (2 * $cr) (2 * $cr) $cr $acc
      Stroke-Round $g ($px - $cr) ($py - $cr) (2 * $cr) (2 * $cr) $cr $INK 10
      Stroke-Round $g ($px - $cr + 14) ($py - $cr + 14) (2 * ($cr - 14)) (2 * ($cr - 14)) ($cr - 14) $INK 4
      $dot = New-Object System.Drawing.Drawing2D.GraphicsPath
      $dot.AddEllipse(($px - 9), ($py - 9), 18, 18)
      $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $INK)), $dot)
      $dot.Dispose()
    }
  }

  # 8) 名条面板
  Fill-Round $g $bodyL $bodyT $bodyW ($BAND_B - $bodyT) 18 $BANDC
  # 名条上下骑缝线（手绘抖动 + 短针脚）
  $seamTop = New-WobblePath ($bodyL + 8) ($bodyT + 26) ($bodyR - 8) ($bodyT + 26) 3 1
  Stroke-Double $g $seamTop $BANDC $INK 6 3
  $seamTop.Dispose()
  $rule = New-WobblePath ($bodyL + 4) ($BAND_B - 12) ($bodyR - 4) ($BAND_B - 12) 4 2
  Stroke-Double $g $rule $INK $INK 14 0
  $rule.Dispose()
  for ($x = ($bodyL + 40); $x -lt ($bodyR - 40); $x += 34) {
    $st = New-WobblePath $x ($BAND_B - 34) ($x + 16) ($BAND_B - 34) 1 3
    Stroke-Double $g $st $BANDC $INK 5 2
    $st.Dispose()
  }

  # 9) 名条两端的卷轴卷（左端压在费用宝石下，右端托种类火漆）
  foreach ($rx in @(($bodyL + 4), ($bodyR - $ROLL_W - 4))) {
    $ry = $bodyT + 8; $rh = ($BAND_B - $bodyT) - 16
    Fill-Round $g $rx $ry $ROLL_W $rh 40 $ROLLC
    Stroke-Round $g ($rx + 5) ($ry + 5) ($ROLL_W - 10) ($rh - 10) 34 $INK 10
    # 卷纸的硬边暗档 + 两道卷缝
    $inner = if ($rx -lt 576) { $rx + $ROLL_W - 22 } else { $rx + 4 }
    Fill-Round $g $inner ($ry + 10) 18 ($rh - 20) 9 $STEP
    foreach ($sy in @(($ry + 46), ($ry + $rh - 46))) {
      $sl = New-WobblePath ($rx + 12) $sy ($rx + $ROLL_W - 12) $sy 2 4
      Stroke-Double $g $sl $ROLLC $INK 5 2
      $sl.Dispose()
    }
  }

  # 10) 名条中央小牌（前缀图标落座）
  $oy = 352
  $tag = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = [System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(524, 308)),
      (New-Object System.Drawing.PointF(628, 308)),
      (New-Object System.Drawing.PointF(628, 372)),
      (New-Object System.Drawing.PointF(576, 410)),
      (New-Object System.Drawing.PointF(524, 372)))
  $tag.AddPolygon($pts)
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $acc)), $tag)
  $pen = New-Object System.Drawing.Pen (New-Col $INK), 9
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $tag)
  $pen.Dispose(); $tag.Dispose()

  # 11) 种类火漆座（名条右端）
  $sr = $SEAL_R
  Fill-Round $g ($SEAL_CX - $sr) ($ROW_CY - $sr) (2 * $sr) (2 * $sr) $sr $acc
  Stroke-Round $g ($SEAL_CX - $sr) ($ROW_CY - $sr) (2 * $sr) (2 * $sr) $sr $INK 13
  Stroke-Round $g ($SEAL_CX - $sr + 16) ($ROW_CY - $sr + 16) (2 * ($sr - 16)) (2 * ($sr - 16)) ($sr - 16) $INK 5

  # 12) 攻 / 血 通用牌座（同形同尺寸）+ 角钉
  foreach ($px in @($HP_CX, $ATK_CX)) {
    $x0 = $px - $PLAQ_W / 2; $y0 = $PLAQ_CY - $PLAQ_H / 2
    Fill-Round $g $x0 $y0 $PLAQ_W $PLAQ_H 36 $SOCKC
    Stroke-Round $g $x0 $y0 $PLAQ_W $PLAQ_H 36 $INK 12
    Stroke-Round $g ($x0 + 14) ($y0 + 14) ($PLAQ_W - 28) ($PLAQ_H - 28) 24 $INK 4
    foreach ($dx in @(($x0 + 26), ($x0 + $PLAQ_W - 26))) {
      foreach ($dy in @(($y0 + 26), ($y0 + $PLAQ_H - 26))) {
        $d = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d.AddEllipse(($dx - 7), ($dy - 7), 14, 14)
        $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $INK)), $d)
        $d.Dispose()
      }
    }
  }

  # 13) 底部徽记（阶位位）
  Fill-Round $g ($MED_CX - $MED_R) ($MED_CY - $MED_R) (2 * $MED_R) (2 * $MED_R) $MED_R $STEP
  Stroke-Round $g ($MED_CX - $MED_R) ($MED_CY - $MED_R) (2 * $MED_R) (2 * $MED_R) $MED_R $INK 11
  Stroke-Round $g ($MED_CX - $MED_R + 14) ($MED_CY - $MED_R + 14) (2 * ($MED_R - 14)) (2 * ($MED_R - 14)) ($MED_R - 14) $INK 4
  $md = New-Object System.Drawing.Drawing2D.GraphicsPath
  $md.AddPolygon([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF($MED_CX, ($MED_CY - 18))),
      (New-Object System.Drawing.PointF(($MED_CX + 18), $MED_CY)),
      (New-Object System.Drawing.PointF($MED_CX, ($MED_CY + 18))),
      (New-Object System.Drawing.PointF(($MED_CX - 18), $MED_CY))))
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $acc)), $md)
  $md.Dispose()

  # 14) 立绘窗口：擦透明 + 粗墨线
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  Fill-Round $g $ART_L $ART_T ($ART_R - $ART_L) ($ART_B - $ART_T) $ART_RAD @(0, 0, 0) 0
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
  Stroke-Round $g ($ART_L + 6) ($ART_T + 6) ($ART_R - $ART_L - 12) ($ART_B - $ART_T - 12) ($ART_RAD - 6) $INK 12

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
