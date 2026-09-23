# 图标套件 v1 —— 与 CardFrameV6 / SteelUi 同一材质语言：深青钢底 + 金边 + 属性色辉光
# 三种轮廓对应三类信息：六边形=属性前缀（与费用宝石同族）/ 方牌=特性与召唤物种类（与画框同族）/ 圆环=状态（与数值槽同族）
Add-Type -AssemblyName System.Drawing

$ICON = 511
$INK    = @(5, 8, 13)
$PL_T   = @(36, 49, 66)
$PL_B   = @(18, 26, 36)
$STEEL  = @(92, 124, 152)
$STEEL_H= @(150, 178, 202)
$IVORY  = @(232, 238, 246)
$GOLD   = @(200, 164, 74)
$GOLD_L = @(232, 209, 138)
$GOLD_D = @(128, 100, 42)

function New-Col([int[]]$rgb, [int]$a = 255) { return [System.Drawing.Color]::FromArgb($a, $rgb[0], $rgb[1], $rgb[2]) }
function Mix-Col([int[]]$a, [int[]]$b, [double]$t) { return @([int]($a[0]+($b[0]-$a[0])*$t), [int]($a[1]+($b[1]-$a[1])*$t), [int]($a[2]+($b[2]-$a[2])*$t)) }
function New-Acc([int[]]$rgb, [int]$d) { return @([int]([Math]::Max(0,$rgb[0]-$d)), [int]([Math]::Max(0,$rgb[1]-$d)), [int]([Math]::Max(0,$rgb[2]-$d))) }

function New-Pts($list) {
  $r = New-Object System.Drawing.PointF[] $list.Count
  for ($i = 0; $i -lt $list.Count; $i++) { $r[$i] = New-Object System.Drawing.PointF([single]$list[$i][0], [single]$list[$i][1]) }
  return ,$r
}

# 尖左右六边形（与费用宝石同构）
function New-HexPath([single]$cx, [single]$cy, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = @()
  for ($i = 0; $i -lt 6; $i++) {
    $a = [Math]::PI / 180 * (60 * $i)
    $pts += ,@(($cx + $r * [Math]::Cos($a)), ($cy + $r * [Math]::Sin($a)))
  }
  $p.AddPolygon((New-Pts $pts))
  return $p
}

# 硬角方牌（与卡面画框同构）
function New-ChamferPath([single]$cx, [single]$cy, [single]$h, [single]$ch) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = @(@(($cx-$h+$ch),($cy-$h)), @(($cx+$h-$ch),($cy-$h)), @(($cx+$h),($cy-$h+$ch)),
           @(($cx+$h),($cy+$h-$ch)), @(($cx+$h-$ch),($cy+$h)), @(($cx-$h+$ch),($cy+$h)),
           @(($cx-$h),($cy+$h-$ch)), @(($cx-$h),($cy-$h+$ch)))
  $p.AddPolygon((New-Pts $pts))
  return $p
}

function New-StarPath([single]$cx, [single]$cy, [single]$ro, [single]$ri, [int]$n, [single]$rotDeg) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = @()
  for ($i = 0; $i -lt ($n * 2); $i++) {
    $r = if ($i % 2 -eq 0) { $ro } else { $ri }
    $a = [Math]::PI / 180 * ($rotDeg + 180.0 * $i / $n)
    $pts += ,@(($cx + $r * [Math]::Cos($a)), ($cy + $r * [Math]::Sin($a)))
  }
  $p.AddPolygon((New-Pts $pts))
  return $p
}

function Add-Rect($p, [single]$x, [single]$y, [single]$w, [single]$h) { $p.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h))) }

function New-RoundPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  if ($r -le 0.5) { $p.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h))); return $p }
  $d = $r * 2
  if ($d -gt $w) { $d = $w }
  if ($d -gt $h) { $d = $h }
  $p.AddArc($x, $y, $d, $d, 180, 90)
  $p.AddArc(($x+$w-$d), $y, $d, $d, 270, 90)
  $p.AddArc(($x+$w-$d), ($y+$h-$d), $d, $d, 0, 90)
  $p.AddArc($x, ($y+$h-$d), $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

function Scale-Path($p, [single]$cx, [single]$cy, [single]$s) {
  $c = [System.Drawing.Drawing2D.GraphicsPath]$p.Clone()
  $m = New-Object System.Drawing.Drawing2D.Matrix
  $m.Translate($cx, $cy); $m.Scale($s, $s); $m.Translate(-$cx, -$cy)
  $c.Transform($m); $m.Dispose()
  return $c
}

function Offset-Path($p, [single]$dx, [single]$dy) {
  $c = [System.Drawing.Drawing2D.GraphicsPath]$p.Clone()
  $m = New-Object System.Drawing.Drawing2D.Matrix
  $m.Translate($dx, $dy)
  $c.Transform($m); $m.Dispose()
  return $c
}

function Fill-PathCol($g, $p, [int[]]$c, [int]$a = 255) {
  $b = New-Object System.Drawing.SolidBrush (New-Col $c $a)
  $g.FillPath($b, $p); $b.Dispose()
}

function Fill-PathGrad($g, $p, [int[]]$t, [int[]]$bt, [single]$x, [single]$y, [single]$w, [single]$h) {
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle([int]$x,[int]$y,[int]$w,[int]$h)), (New-Col $t), (New-Col $bt), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $sv = $g.Clip; $g.SetClip($p); $g.FillRectangle($lg, $x, $y, $w, $h); $g.Clip = $sv
  $lg.Dispose()
}

function Stroke-Path($g, $p, [int[]]$c, [single]$t, [int]$a = 255) {
  $pen = New-Object System.Drawing.Pen ((New-Col $c $a)), $t
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose()
}

function Glow-Path($g, $p, [int[]]$c, [single]$base, [int]$steps) {
  for ($i = $steps; $i -ge 1; $i--) {
    $a = [int](80 / ($i * 1.2)); if ($a -lt 8) { $a = 8 }
    $pen = New-Object System.Drawing.Pen ((New-Col $c $a)), ($base * $i)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $p); $pen.Dispose()
  }
}

function Fill-SoftEllipse($g, [single]$cx, [single]$cy, [single]$rx, [single]$ry, [int[]]$c, [int]$aMax, [int]$steps) {
  for ($i = $steps; $i -ge 1; $i--) {
    $k = $i / $steps
    $a = [int]($aMax * (1 - $k) * 0.9)
    if ($a -lt 4) { continue }
    $b = New-Object System.Drawing.SolidBrush (New-Col $c $a)
    $g.FillEllipse($b, ($cx - $rx * $k), ($cy - $ry * $k), ($rx * 2 * $k), ($ry * 2 * $k))
    $b.Dispose()
  }
}
# ── 底座：深青钢渐变 + 金边（受光在上、背光在下）+ 内凹槽 + 属性辉光 ──
function Draw-BadgePlate($g, $path, [single]$cx, [single]$cy, [int[]]$acc) {
  $sp = Offset-Path $path 5 11
  $pen = New-Object System.Drawing.Pen ((New-Col @(0,0,0) 110)), 20
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $sp); $pen.Dispose(); $sp.Dispose()

  Glow-Path $g $path $acc 10 3

  Fill-PathGrad $g $path $PL_T $PL_B 0 0 $ICON $ICON

  $sv = $g.Clip; $g.SetClip($path)
  $hg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$ICON,[int]($ICON*0.5))), (New-Col @(255,255,255) 46), (New-Col @(255,255,255) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($hg, 0, 0, $ICON, $ICON); $hg.Dispose()
  $g.Clip = $sv

  $inner = Scale-Path $path $cx $cy 0.815
  Fill-PathCol $g $inner $INK 188
  $sv = $g.Clip; $g.SetClip($inner)
  $ig = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$ICON,[int]($ICON*0.35))), (New-Col @(0,0,0) 170), (New-Col @(0,0,0) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($ig, 0, 0, $ICON, $ICON); $ig.Dispose()
  $g.Clip = $sv
  Stroke-Path $g $inner $STEEL 2 95

  Stroke-Path $g $path $INK 6
  $rim = Scale-Path $path $cx $cy 0.962
  Stroke-Path $g $rim $GOLD 9
  $sv = $g.Clip; $g.SetClip((New-Object System.Drawing.RectangleF(0, 0, $ICON, $cy)))
  Stroke-Path $g $rim $GOLD_L 3.5 235
  $g.Clip = $sv
  $sv = $g.Clip; $g.SetClip((New-Object System.Drawing.RectangleF(0, $cy, $ICON, ($ICON - $cy))))
  Stroke-Path $g $rim $GOLD_D 3.5 235
  $g.Clip = $sv
}

# 把徽记画到独立图层（局部坐标：中心 0,0，半幅约 130），再带落影合成到底座上
function New-GlyphLayer([scriptblock]$body, [int[]]$acc, [single]$k = 1.0) {
  $b = New-Object System.Drawing.Bitmap($ICON, $ICON, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $m = New-Object System.Drawing.Drawing2D.Matrix
  $m.Translate(($ICON/2.0 + 1), ($ICON/2.0 + 2)); $m.Scale($k, $k)
  $g.Transform = $m
  $null = & $body $g $acc
  $g.Dispose()
  return $b
}

function Compose-Glyph($g, $layer, [single]$dx, [single]$dy, [int]$shA) {
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = 0; $cm.Matrix11 = 0; $cm.Matrix22 = 0
  $cm.Matrix40 = $INK[0]/255.0; $cm.Matrix41 = $INK[1]/255.0; $cm.Matrix42 = $INK[2]/255.0
  $cm.Matrix33 = $shA / 255.0
  $ia.SetColorMatrix($cm)
  $dst = New-Object System.Drawing.Rectangle([int]$dx, [int]$dy, $ICON, $ICON)
  $g.DrawImage($layer, $dst, 0, 0, $ICON, $ICON, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
  $g.DrawImage($layer, 0, 0, $ICON, $ICON)
}

function New-IconArt {
  param([string]$Shape, [int[]]$Accent, [scriptblock]$Glyph, [string]$Out, [single]$GlyphScale = 1.0)
  $bmp = New-Object System.Drawing.Bitmap($ICON, $ICON, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $cx = $ICON / 2.0; $cy = $ICON / 2.0 + 4
  if ($Shape -eq 'hex')    { $path = New-HexPath $cx ($cy - 12) 232 }
  elseif ($Shape -eq 'square') { $path = New-ChamferPath $cx $cy 226 50 }
  else                     { $path = New-HexPath 0 0 1; $path.Dispose()
                             $path = New-Object System.Drawing.Drawing2D.GraphicsPath
                             $path.AddEllipse(($cx - 230), ($cy - 230), 460, 460) }
  Draw-BadgePlate $g $path $cx $cy $Accent
  Fill-SoftEllipse $g $cx ($cy - 6) 126 118 (Mix-Col $Accent $IVORY 0.3) 46 8
  $layer = New-GlyphLayer $Glyph $Accent $GlyphScale
  Compose-Glyph $g $layer 4 8 130
  $layer.Dispose()
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose(); $path.Dispose()
  return $Out
}

# ══════════ 徽记库（局部坐标：中心 0,0，半幅约 130） ══════════

function New-BoltPath([single]$cx, [single]$cy, [single]$s) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = @(@(18,-85), @(52,-85), @(10,-14), @(44,-14), @(-16,88), @(-2,10), @(-34,10))
  $out = @()
  foreach ($q in $pts) { $out += ,@(($cx + $q[0]*$s), ($cy + $q[1]*$s)) }
  $p.AddPolygon((New-Pts $out))
  return $p
}

function New-DoorPath([single]$w, [single]$h, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc((-$w), (-$h), ($w*2), ($r*2), 180, 180)
  $p.AddLine($w, (-$h + $r), $w, $h)
  $p.AddLine($w, $h, (-$w), $h)
  $p.AddLine((-$w), $h, (-$w), (-$h + $r))
  $p.CloseFigure()
  return $p
}

function New-DropPath([single]$k) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.StartFigure()
  $p.AddBezier(0, (-124*$k), (42*$k), (-58*$k), (84*$k), (-20*$k), (84*$k), (34*$k))
  $p.AddBezier((84*$k), (34*$k), (84*$k), (86*$k), (46*$k), (120*$k), 0, (120*$k))
  $p.AddBezier(0, (120*$k), (-46*$k), (120*$k), (-84*$k), (86*$k), (-84*$k), (34*$k))
  $p.AddBezier((-84*$k), (34*$k), (-84*$k), (-20*$k), (-42*$k), (-58*$k), 0, (-124*$k))
  $p.CloseFigure()
  return $p
}

# 渊：涡旋 + 注视的眼
$GLYPH_ABYSS = {
  param($g, $acc)
  foreach ($sp in @(@(128, 130, 196, 344, 13), @(94, 84, 206, 334, 10))) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc((-$sp[0]), (-$sp[0]), ($sp[0]*2), ($sp[0]*2), $sp[2], ($sp[3]-$sp[2]))
    Stroke-Path $g $p (Mix-Col $acc $IVORY 0.55) $sp[4] $sp[1]
    $p.Dispose()
  }
  $eye = New-Object System.Drawing.Drawing2D.GraphicsPath
  $eye.StartFigure()
  $eye.AddBezier((-112), 6, (-58), (-66), 58, (-66), 112, 6)
  $eye.AddBezier(112, 6, 58, 78, -58, 78, -112, 6)
  $eye.CloseFigure()
  Fill-PathCol $g $eye $IVORY 245
  Stroke-Path $g $eye $INK 7
  $ir = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ir.AddEllipse(-40, (-28), 80, 80)
  Fill-PathCol $g $ir $acc 255
  Stroke-Path $g $ir $INK 6
  $pw = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pw.AddEllipse(-13, (-26), 26, 56)
  Fill-PathCol $g $pw $INK 255
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl.AddEllipse(-30, (-20), 20, 20)
  Fill-PathCol $g $hl $IVORY 235
  $ir.Dispose(); $pw.Dispose(); $hl.Dispose(); $eye.Dispose()
}

# 血歌：血滴 + 声波
$GLYPH_BLOOD = {
  param($g, $acc)
  foreach ($sp in @(@(104, 88, 24, 156, 13), @(74, 62, 26, 154, 10))) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc((-$sp[0]), (-$sp[0]), ($sp[0]*2), ($sp[0]*2), $sp[2], ($sp[3]-$sp[2]))
    Stroke-Path $g $p (Mix-Col $acc $IVORY 0.78) $sp[4] ($sp[1] + 40)
    $p.Dispose()
  }
  $g.TranslateTransform(-46, 0); $g.ScaleTransform(0.92, 0.92)
  $d = New-DropPath 1.0
  Fill-PathGrad $g $d (Mix-Col $acc $IVORY 0.28) (New-Acc $acc 46) (-92) (-128) 184 256
  Stroke-Path $g $d $INK 8
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl.StartFigure()
  $hl.AddBezier((-40), 28, (-52), 56, (-34), 84, (-12), 92)
  Stroke-Path $g $hl $IVORY 12 190
  $hl.Dispose(); $d.Dispose()
}

# 机械：齿轮
$GLYPH_MECH = {
  param($g, $acc)
  $gear = New-StarPath 0 0 122 92 8 22.5
  Fill-PathGrad $g $gear (Mix-Col $acc $IVORY 0.22) (New-Acc $acc 52) (-126) (-126) 252 252
  Stroke-Path $g $gear $INK 8
  $hub = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hub.AddEllipse(-84, -84, 168, 168)
  Stroke-Path $g $hub (Mix-Col $acc $IVORY 0.5) 7 220
  $hole = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hole.AddEllipse(-50, -50, 100, 100)
  Fill-PathCol $g $hole $INK 240
  Stroke-Path $g $hole $GOLD 8
  $key = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $key -12  -32  24  64
  Add-Rect $key -32  -12  64  24
  Fill-PathCol $g $key (Mix-Col $acc $IVORY 0.35) 255
  $hub.Dispose(); $hole.Dispose(); $key.Dispose(); $gear.Dispose()
}

# 灵能：晶体 + 环绕弧
$GLYPH_PSYCHIC = {
  param($g, $acc)
  foreach ($sp in @(@(136, 150, 194, 346, 11), @(136, 96, 14, 166, 11))) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc((-$sp[0]), (-$sp[0]), ($sp[0]*2), ($sp[0]*2), $sp[2], ($sp[3]-$sp[2]))
    Stroke-Path $g $p (Mix-Col $acc $IVORY 0.55) $sp[4] $sp[1]
    $p.Dispose()
  }
  $cr = New-Object System.Drawing.Drawing2D.GraphicsPath
  $cr.AddPolygon((New-Pts @(@(0,-132), @(62,0), @(0,132), @(-62,0))))
  Fill-PathGrad $g $cr (Mix-Col $acc $IVORY 0.5) (New-Acc $acc 34) -64 -132 128 264
  Stroke-Path $g $cr $INK 7
  $facet = New-Object System.Drawing.Drawing2D.GraphicsPath
  $facet.AddPolygon((New-Pts @(@(0,-132), @(0,132), @(-62,0))))
  Fill-PathCol $g $facet $IVORY 78
  $core = New-Object System.Drawing.Drawing2D.GraphicsPath
  $core.AddEllipse(-19, -19, 38, 38)
  Fill-PathCol $g $core $IVORY 240
  $facet.Dispose(); $core.Dispose(); $cr.Dispose()
}
# 神灵画卷：卷轴 + 星点
$GLYPH_SCROLL = {
  param($g, $acc)
  $body = New-RoundPath -96 -66 192 132 14
  Fill-PathCol $g $body $IVORY 248
  Stroke-Path $g $body $INK 7
  foreach ($sy in @(-86, 56)) {
    $rod = New-RoundPath -114 $sy 228 32 15
    Fill-PathGrad $g $rod (Mix-Col $acc $IVORY 0.35) (New-Acc $acc 50) -114 $sy 228 32
    Stroke-Path $g $rod $INK 6
    $rod.Dispose()
  }
  $ln = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $ln -66  -34  132  9
  Add-Rect $ln -66  -6  96  9
  Add-Rect $ln -66  22  112  9
  Fill-PathCol $g $ln (Mix-Col $INK $acc 0.35) 190
  foreach ($sp in @(@(-40, -46, 17), @(6, -34, 23), @(48, -48, 15))) {
    $st = New-StarPath $sp[0] $sp[1] $sp[2] ($sp[2]*0.36) 4 0
    Fill-PathCol $g $st $GOLD_L 250
    Stroke-Path $g $st $INK 4
    $st.Dispose()
  }
  $ln.Dispose(); $body.Dispose()
}

# 先手：三道闪电
$GLYPH_FIRST = {
  param($g, $acc)
  foreach ($spec in @(@(0, 0, 0.88), @(-84, 24, 0.58), @(84, 24, 0.58))) {
    $b = New-BoltPath $spec[0] $spec[1] $spec[2]
    Fill-PathGrad $g $b (Mix-Col $acc $IVORY 0.5) (New-Acc $acc 46) ($spec[0]-44) ($spec[1]-80) 88 170
    Stroke-Path $g $b $INK 6
    $b.Dispose()
  }
}# 进场：门框 + 向内箭头
$GLYPH_ENTER = {
  param($g, $acc)
  $d = New-DoorPath 92 104 92
  Fill-PathCol $g $d $INK 150
  Stroke-Path $g $d (Mix-Col $GOLD $IVORY 0.25) 13
  $a = New-Object System.Drawing.Drawing2D.GraphicsPath
  $a.AddPolygon((New-Pts @(@(-60,-44), @(20,-44), @(20,-78), @(72,0), @(20,78), @(20,44), @(-60,44))))
  Fill-PathGrad $g $a (Mix-Col $acc $IVORY 0.45) (New-Acc $acc 34) -70 -80 150 160
  Stroke-Path $g $a $INK 7
  $a.Dispose(); $d.Dispose()
}

# 退场：墓碑 + 上升的灵魂点
$GLYPH_DEATH = {
  param($g, $acc)
  $t = New-Object System.Drawing.Drawing2D.GraphicsPath
  $t.AddArc(-96, -28, 192, 192, 180, 180)
  $t.AddLine(96, 68, 96, 124)
  $t.AddLine(96, 124, -96, 124)
  $t.AddLine(-96, 124, -96, 68)
  $t.CloseFigure()
  Fill-PathGrad $g $t (Mix-Col $acc $IVORY 0.46) (New-Acc $acc 34) -96 -30 192 158
  Stroke-Path $g $t $INK 8
  $cr = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $cr -11 34 22 62
  Add-Rect $cr -40 52 80 22
  Fill-PathCol $g $cr $INK 175
  foreach ($sp in @(@(0,-96,15), @(-52,-74,11), @(52,-74,11))) {
    $dot = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dot.AddEllipse(($sp[0]-$sp[2]), ($sp[1]-$sp[2]), ($sp[2]*2), ($sp[2]*2))
    Fill-PathCol $g $dot (Mix-Col $acc $IVORY 0.72) 240
    Stroke-Path $g $dot $INK 4
    $dot.Dispose()
  }
  $cr.Dispose(); $t.Dispose()
}
# 主动退场：门框 + 向外箭头 + 外散运动线
$GLYPH_AEXIT = {
  param($g, $acc)
  $d = New-DoorPath 92 104 92
  Fill-PathCol $g $d $INK 150
  Stroke-Path $g $d (Mix-Col $GOLD $IVORY 0.25) 13
  $a = New-Object System.Drawing.Drawing2D.GraphicsPath
  $a.AddPolygon((New-Pts @(@(60,44), @(-20,44), @(-20,78), @(-72,0), @(-20,-78), @(-20,-44), @(60,-44))))
  Fill-PathGrad $g $a (Mix-Col $acc $IVORY 0.45) (New-Acc $acc 34) -70 -80 150 160
  Stroke-Path $g $a $INK 7
  foreach ($ly in @(-52, 0, 52)) {
    $ml = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-Rect $ml -118 ($ly-8) 34 16
    Fill-PathCol $g $ml (Mix-Col $acc $IVORY 0.5) 210
    $ml.Dispose()
  }
  $a.Dispose(); $d.Dispose()
}

# 反击：盾牌 + 回弹箭
$GLYPH_REVENGE = {
  param($g, $acc)
  $r = 116
  $a0 = 300.0; $sweep = 300.0
  $arc = New-Object System.Drawing.Drawing2D.GraphicsPath
  $arc.AddArc(-$r, -$r, ($r*2), ($r*2), $a0, $sweep)
  Stroke-Path $g $arc $INK 40
  Stroke-Path $g $arc (Mix-Col $acc $IVORY 0.1) 25
  $rad = [Math]::PI / 180.0 * $a0
  $px = $r * [Math]::Cos($rad); $py = $r * [Math]::Sin($rad)
  $dx = [Math]::Sin($rad); $dy = -[Math]::Cos($rad)
  $nx = -$dy; $ny = $dx
  $tip = @(($px + $dx*56), ($py + $dy*56))
  $b1 = @(($px - $dx*18 + $nx*44), ($py - $dy*18 + $ny*44))
  $b2 = @(($px - $dx*18 - $nx*44), ($py - $dy*18 - $ny*44))
  $hd = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hd.AddPolygon((New-Pts @($tip, $b1, $b2)))
  Fill-PathCol $g $hd (Mix-Col $acc $IVORY 0.15) 255
  Stroke-Path $g $hd $INK 7
  $star = New-StarPath 0 0 62 20 4 0
  Fill-PathCol $g $star $IVORY 245
  Stroke-Path $g $star $INK 6
  $star.Dispose(); $hd.Dispose(); $arc.Dispose()
}
# 抛置：信笺被抛出 + 运动线
$GLYPH_DISCARD = {
  param($g, $acc)
  foreach ($spec in @(@(-112, 40, 52, -34), @(-112, -18, 34, -34), @(-112, -70, 44, -34))) {
    $ml = New-RoundPath $spec[0] $spec[1] $spec[2] 15 7
    Fill-PathCol $g $ml (Mix-Col $acc $IVORY 0.45) 210
    $ml.Dispose()
  }
  $g.TranslateTransform(26, 0); $g.RotateTransform(-13)
  $pg = New-RoundPath -78 -96 156 192 8
  Fill-PathGrad $g $pg $IVORY (Mix-Col $IVORY $STEEL 0.32) -78 -96 156 192
  Stroke-Path $g $pg $INK 7
  $fl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $fl.AddPolygon((New-Pts @(@(78,-96), @(78,-46), @(28,-96))))
  Fill-PathCol $g $fl (Mix-Col $STEEL $INK 0.25) 255
  Stroke-Path $g $fl $INK 6
  $ln = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $ln -54  -34  108  10
  Add-Rect $ln -54  -4  108  10
  Add-Rect $ln -54  26  76  10
  Add-Rect $ln -54  56  92  10
  Fill-PathCol $g $ln (Mix-Col $INK $acc 0.3) 165
  $ln.Dispose(); $fl.Dispose(); $pg.Dispose()
}

# 附着：两枚相扣的环
$GLYPH_ATTACH = {
  param($g, $acc)
  $pl = New-RoundPath -108 -50 116 100 34
  $pr = New-RoundPath -8 -50 116 100 34
  Stroke-Path $g $pl $INK 30
  Stroke-Path $g $pr $INK 30
  Stroke-Path $g $pl (Mix-Col $STEEL_H $IVORY 0.3) 19
  Stroke-Path $g $pr (Mix-Col $acc $IVORY 0.3) 19
  $sv = $g.Clip
  $g.SetClip((New-Object System.Drawing.RectangleF(-120, -120, 108, 240)))
  Stroke-Path $g $pl (Mix-Col $STEEL_H $IVORY 0.3) 19
  $g.Clip = $sv
  $sv = $g.Clip
  $g.SetClip((New-Object System.Drawing.RectangleF(-20, -120, 116, 240)))
  Stroke-Path $g $pr (Mix-Col $acc $IVORY 0.3) 19
  $g.Clip = $sv
  $pl.Dispose(); $pr.Dispose()
}

# ══════════ 状态（圆环底座） ══════════

$GLYPH_SHIELD = {
  param($g, $acc)
  $sh = New-Object System.Drawing.Drawing2D.GraphicsPath
  $sh.StartFigure()
  $sh.AddLine(-104, -110, 104, -110)
  $sh.AddBezier(104, -110, 108, 14, 66, 80, 0, 124)
  $sh.AddBezier(-66, 80, -108, 14, -104, -110, -104, -110)
  $sh.CloseFigure()
  Fill-PathGrad $g $sh (Mix-Col $acc $IVORY 0.4) (New-Acc $acc 56) -108 -112 216 240
  Stroke-Path $g $sh $INK 10
  Stroke-Path $g $sh $GOLD 6
  $ridge = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ridge.AddLine(0, -104, 0, 108)
  Stroke-Path $g $ridge (New-Acc $acc 30) 6 155
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl.AddPolygon((New-Pts @(@(-92,-104), @(-18,-104), @(-44,60), @(-92,-14))))
  Fill-PathCol $g $hl $IVORY 78
  $sh2 = New-Object System.Drawing.Drawing2D.GraphicsPath
  $sh2.AddPolygon((New-Pts @(@(92,-104), @(34,-104), @(58,48), @(92,-12))))
  Fill-PathCol $g $sh2 $INK 55
  $sh2.Dispose(); $hl.Dispose(); $ridge.Dispose(); $sh.Dispose()
}
$GLYPH_BUFF = {
  param($g, $acc)
  $a = New-Object System.Drawing.Drawing2D.GraphicsPath
  $a.AddPolygon((New-Pts @(@(-58,10), @(-58,-34), @(0,-126), @(58,-34), @(58,10))))
  $st = New-RoundPath -30 -30 60 138 14
  $all = New-Object System.Drawing.Drawing2D.GraphicsPath
  $all.AddPath($a, $false); $all.AddPath($st, $false)
  Fill-PathGrad $g $all (Mix-Col $acc $IVORY 0.42) (New-Acc $acc 42) -60 -128 120 238
  Stroke-Path $g $all $INK 8
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $hl -40  -14  16  104
  Fill-PathCol $g $hl $IVORY 96
  $hl.Dispose(); $all.Dispose(); $st.Dispose(); $a.Dispose()
}

$GLYPH_DEBUFF = {
  param($g, $acc)
  $a = New-Object System.Drawing.Drawing2D.GraphicsPath
  $a.AddPolygon((New-Pts @(@(-58,-10), @(-58,34), @(0,126), @(58,34), @(58,-10))))
  $st = New-RoundPath -30 -108 60 138 14
  $all = New-Object System.Drawing.Drawing2D.GraphicsPath
  $all.AddPath($a, $false); $all.AddPath($st, $false)
  Fill-PathGrad $g $all (Mix-Col $acc $IVORY 0.42) (New-Acc $acc 42) -60 -110 120 238
  Stroke-Path $g $all $INK 8
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $hl -40  -96  16  104
  Fill-PathCol $g $hl $IVORY 96
  $hl.Dispose(); $all.Dispose(); $st.Dispose(); $a.Dispose()
}

# ══════════ 角标（无底座，直接落在卡框的金环凹槽里） ══════════

$GLYPH_COST = {
  param($g, $acc)
  $hex = New-HexPath 0 0 228
  Fill-PathGrad $g $hex (Mix-Col $STEEL_H $IVORY 0.3) (New-Acc $PL_B 8) -230 -230 460 460
  Stroke-Path $g $hex $INK 12
  $rim = Scale-Path $hex 0 0 0.885
  Stroke-Path $g $rim $GOLD 13
  $core = Scale-Path $hex 0 0 0.56
  Fill-PathGrad $g $core (Mix-Col $STEEL_H $IVORY 0.52) (New-Acc $STEEL 30) -130 -130 260 260
  Stroke-Path $g $core $INK 7 140
  $facet = New-Object System.Drawing.Drawing2D.GraphicsPath
  $facet.AddPolygon((New-Pts @(@(0,-128), @(111,-64), @(0,0), @(-111,-64))))
  Fill-PathCol $g $facet $IVORY 70
  $gloss = New-Object System.Drawing.Drawing2D.GraphicsPath
  $gloss.AddPolygon((New-Pts @(@(0,-120), @(76,-82), @(0,-44), @(-76,-82))))
  Fill-PathCol $g $gloss $IVORY 120
  $gloss.Dispose(); $facet.Dispose(); $core.Dispose(); $rim.Dispose(); $hex.Dispose()
}
$GLYPH_ATTACK = {
  param($g, $acc)
  foreach ($spec in @(@(38, 26), @(-38, -26))) {
    $st = $g.Save()
    $g.RotateTransform($spec[0])
    $g.TranslateTransform($spec[1], 6)
    $blade = New-Object System.Drawing.Drawing2D.GraphicsPath
    $blade.AddPolygon((New-Pts @(@(0,-198), @(17,-142), @(17,26), @(-17,26), @(-17,-142))))
    Fill-PathGrad $g $blade $IVORY (Mix-Col $STEEL $INK 0.2) -20 -200 40 240
    Stroke-Path $g $blade $INK 8
    $guard = New-RoundPath -54 24 108 26 9
    Fill-PathCol $g $guard $GOLD 255
    Stroke-Path $g $guard $INK 6
    $grip = New-RoundPath -13 50 26 76 11
    Fill-PathCol $g $grip (Mix-Col $INK $STEEL 0.22) 255
    Stroke-Path $g $grip $INK 6
    $pom = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pom.AddEllipse(-21, 124, 42, 42)
    Fill-PathCol $g $pom $GOLD 255
    Stroke-Path $g $pom $INK 6
    $pom.Dispose(); $grip.Dispose(); $guard.Dispose(); $blade.Dispose()
    $g.Restore($st)
  }
}

$GLYPH_HEALTH = {
  param($g, $acc)
  $d = New-DropPath 1.46
  Fill-PathGrad $g $d (Mix-Col $acc $IVORY 0.3) (New-Acc $acc 58) -124 -184 248 372
  Stroke-Path $g $d $INK 10
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl.AddEllipse(-64, -66, 34, 68)
  Fill-PathCol $g $hl $IVORY 150
  $hl2 = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl2.AddEllipse(-56, -54, 34, 62)
  Fill-PathCol $g $hl2 (New-Acc $acc 30) 90
  $hl.Dispose(); $hl2.Dispose(); $d.Dispose()
}
$GLYPH_HERO = {
  param($g, $acc)
  foreach ($m in @(1, -1)) {
    $horn = New-Object System.Drawing.Drawing2D.GraphicsPath
    $horn.StartFigure()
    $horn.AddBezier((-84*$m), 10, (-184*$m), (-44), (-196*$m), (-142), (-112*$m), (-190))
    $horn.AddBezier((-112*$m), (-190), (-142*$m), (-142), (-142*$m), (-74), (-62*$m), (-14))
    $horn.CloseFigure()
    Fill-PathGrad $g $horn (Mix-Col $acc $IVORY 0.35) (New-Acc $acc 60) -200 -196 400 210
    Stroke-Path $g $horn $INK 8
    $horn.Dispose()
  }
  $helm = New-Object System.Drawing.Drawing2D.GraphicsPath
  $helm.AddArc(-96, -100, 192, 192, 180, 180)
  $helm.AddLine(96, -4, 78, 96)
  $helm.AddLine(78, 96, -78, 96)
  $helm.AddLine(-78, 96, -96, -4)
  $helm.CloseFigure()
  Fill-PathGrad $g $helm (Mix-Col $acc $IVORY 0.45) (New-Acc $acc 66) -96 -100 192 196
  Stroke-Path $g $helm $INK 8
  $slit = New-Object System.Drawing.Drawing2D.GraphicsPath
  Add-Rect $slit -13  -36  26  108
  Add-Rect $slit -62  -30  124  28
  Fill-PathCol $g $slit $INK 225
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl.AddPolygon((New-Pts @(@(-74,-52), @(-24,-78), @(-30,-30), @(-72,-8))))
  Fill-PathCol $g $hl $IVORY 92
  $hl.Dispose(); $slit.Dispose(); $helm.Dispose()
}

$GLYPH_CHOSEN = {
  param($g, $acc)
  $ring = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ring.AddEllipse(-176, -176, 352, 352)
  Stroke-Path $g $ring (Mix-Col $acc $GOLD_D 0.35) 24 205
  $st = New-StarPath 0 0 226 66 8 0
  Fill-PathGrad $g $st (Mix-Col $acc $IVORY 0.5) (New-Acc $acc 40) -230 -230 460 460
  Stroke-Path $g $st $INK 9
  $in = New-StarPath 0 0 118 30 4 0
  Fill-PathCol $g $in $IVORY 210
  $dot = New-Object System.Drawing.Drawing2D.GraphicsPath
  $dot.AddEllipse(-26, -26, 52, 52)
  Fill-PathCol $g $dot (Mix-Col $acc $GOLD_D 0.2) 255
  Stroke-Path $g $dot $INK 6
  $dot.Dispose(); $in.Dispose(); $st.Dispose(); $ring.Dispose()
}

$GLYPH_SPECIAL = {
  param($g, $acc)
  $st = New-StarPath 0 0 228 52 4 0
  Fill-PathGrad $g $st (Mix-Col $acc $IVORY 0.5) (New-Acc $acc 46) -230 -230 460 460
  Stroke-Path $g $st $INK 9
  $hole = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hole.AddEllipse(-52, -52, 104, 104)
  Fill-PathCol $g $hole $INK 235
  Stroke-Path $g $hole $GOLD 9
  $hl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hl.AddPolygon((New-Pts @(@(0,-222), @(30,-66), @(0,-14))))
  Fill-PathCol $g $hl $IVORY 130
  $dot = New-Object System.Drawing.Drawing2D.GraphicsPath
  $dot.AddEllipse(-15, -15, 30, 30)
  Fill-PathCol $g $dot (Mix-Col $acc $IVORY 0.7) 255
  $dot.Dispose(); $hl.Dispose(); $hole.Dispose(); $st.Dispose()
}
# ══════════ 角标渲染（无底座：透明底 + 属性辉光 + 落影） ══════════

function New-CornerArt {
  param([scriptblock]$Glyph, [int[]]$Accent, [string]$Out, [single]$GlyphScale = 1.0, [int]$GlowA = 74, [single]$GlowR = 172)
  $bmp = New-Object System.Drawing.Bitmap($ICON, $ICON, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  Fill-SoftEllipse $g ($ICON/2) ($ICON/2 + 4) $GlowR $GlowR $Accent $GlowA 5
  $layer = New-GlyphLayer $Glyph $Accent $GlyphScale
  Compose-Glyph $g $layer 4 9 140
  $layer.Dispose()
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  return $Out
}

# ══════════ 套件清单 ══════════

$ICONSET = @(
  @{ n='UI/Cost';            kind='corner'; a=@(150,178,202); g=$GLYPH_COST },
  @{ n='UI/Attack';          kind='corner'; a=@(150,178,202); g=$GLYPH_ATTACK },
  @{ n='UI/Health';          kind='corner'; a=@(216,54,78);   g=$GLYPH_HEALTH },
  @{ n='UI/Hero';            kind='corner'; a=@(200,164,74);  g=$GLYPH_HERO },
  @{ n='UI/Chosen';          kind='corner'; a=@(240,220,160); g=$GLYPH_CHOSEN },
  @{ n='UI/Special';         kind='corner'; a=@(154,180,216); g=$GLYPH_SPECIAL },
  @{ n='Icons/Prefixes/Abyss';   kind='hex';    a=@(160,90,232); s=1.24; g=$GLYPH_ABYSS },
  @{ n='Icons/Prefixes/Blood';   kind='hex';    a=@(216,54,78); s=1.24; g=$GLYPH_BLOOD },
  @{ n='Icons/Prefixes/Mech';    kind='hex';    a=@(217,135,60); s=1.24; g=$GLYPH_MECH },
  @{ n='Icons/Prefixes/Psychic'; kind='hex';    a=@(69,198,232); s=1.24; g=$GLYPH_PSYCHIC },
  @{ n='Icons/Prefixes/Scroll';  kind='hex';    a=@(92,192,126); s=1.24; g=$GLYPH_SCROLL },
  @{ n='UI/First';               kind='square'; a=@(232,194,74);  s=1.24; g=$GLYPH_FIRST },
  @{ n='UI/Enter';               kind='square'; a=@(95,180,232);  s=1.24; g=$GLYPH_ENTER },
  @{ n='UI/Leave';               kind='square'; a=@(147,164,184); s=1.24; g=$GLYPH_DEATH },
  @{ n='UI/Exit';                kind='square'; a=@(232,144,60);  s=1.24; g=$GLYPH_AEXIT },
  @{ n='UI/Reverge';             kind='square'; a=@(216,72,60);   s=1.24; g=$GLYPH_REVENGE },
  @{ n='UI/Discard';             kind='square'; a=@(216,184,120); s=1.24; g=$GLYPH_DISCARD },
  @{ n='UI/Attach';              kind='square'; a=@(111,191,138); s=1.24; g=$GLYPH_ATTACH },
  @{ n='Icons/Buffs/Shield';     kind='circle'; a=@(92,140,200); s=1.34; g=$GLYPH_SHIELD },
  @{ n='Icons/Buffs/Buff';       kind='circle'; a=@(79,200,120); s=1.34; g=$GLYPH_BUFF },
  @{ n='Icons/Buffs/DeBuff';     kind='circle'; a=@(216,72,92); s=1.34; g=$GLYPH_DEBUFF }
)

function New-IconSetV1([string]$OutDir, [string[]]$Only = $null) {
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
  $made = @()
  foreach ($it in $ICONSET) {
    $flat = ($it.n -replace '/', '__')
    if ($Only -and ($Only -notcontains $flat)) { continue }
    $out = Join-Path $OutDir ($it.n + '.png')
    $od = Split-Path $out -Parent
    if (-not (Test-Path $od)) { New-Item -ItemType Directory -Path $od -Force | Out-Null }
    if (-not $it.ContainsKey('s')) { $it['s'] = 1.0 }
    if ($it.kind -eq 'corner') { New-CornerArt -Glyph $it.g -Accent $it.a -Out $out -GlyphScale $it.s | Out-Null }
    else { New-IconArt -Shape $it.kind -Accent $it.a -Glyph $it.g -Out $out -GlyphScale $it.s | Out-Null }
    $made += $out
  }
  return $made
}