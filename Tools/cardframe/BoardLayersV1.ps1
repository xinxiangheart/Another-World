# 战场底板 v1（分层版）——同一张 2048x1152 画布拆 7 层，每层独立可动
# 依赖 CardFrameV6.ps1 的 New-Col / Mix-Col / New-RoundPath
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

$BW = 2048; $BH = 1152
$SKY_T   = @(9, 14, 25)
$SKY_B   = @(20, 31, 49)
$GLOW_C  = @(40, 62, 92)
$INK3    = @(5, 8, 13)
$FL_C    = @(34, 48, 70)
$FL_M    = @(21, 30, 45)
$FL_E    = @(10, 15, 24)
$STEEL   = @(110, 147, 176)
$STEEL_D = @(48, 66, 86)
$GOLD    = @(200, 164, 74)
$GOLD_L  = @(232, 209, 138)
$GOLD_D  = @(120, 94, 40)
$ARC     = @(74, 132, 206)

function New-Layer([int]$w, [int]$h, [bool]$opaque, [int[]]$bg) {
  $b = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  if ($opaque) { $g.Clear((New-Col $bg)) }
  return @($b, $g)
}

function Fill-VGrad($g, [single]$x, [single]$y, [single]$w, [single]$h, [int[]]$top, [int[]]$bot, [int]$aT = 255, [int]$aB = 255) {
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle([int]$x,[int]$y,[int]$w,[int]$h)), (New-Col $top $aT), (New-Col $bot $aB), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $x, $y, $w, $h); $lg.Dispose()
}

function Fill-Radial($g, [single]$cx, [single]$cy, [single]$rx, [single]$ry, [int[]]$cIn, [int[]]$cOut, [single]$bias = 0.7, [int]$aIn = 255, [int]$aOut = 0) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddEllipse(($cx-$rx), ($cy-$ry), ($rx*2), ($ry*2))
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($p)
  $pg.CenterPoint = New-Object System.Drawing.PointF($cx, $cy)
  $pg.CenterColor = (New-Col $cIn $aIn)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $cOut $aOut))
  $pg.SetSigmaBellShape($bias)
  $g.FillPath($pg, $p); $pg.Dispose(); $p.Dispose()
}

function Stroke-Ell($g, [single]$cx, [single]$cy, [single]$r, [int[]]$c, [single]$w, [int]$a = 255) {
  $pen = New-Object System.Drawing.Pen ((New-Col $c $a)), $w
  $g.DrawEllipse($pen, ($cx-$r), ($cy-$r), ($r*2), ($r*2)); $pen.Dispose()
}

function New-Orn([single]$x, [single]$y, [single]$s, [single]$rot) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = @(@(0,-1), @(0.55,-0.25), @(0.35,0), @(1,0.35), @(1,1), @(0.3,0.55), @(0,1), @(-0.3,0.55), @(-1,1), @(-1,0.35), @(-0.35,0), @(-0.55,-0.25))
  $o = @()
  $ca = [Math]::Cos($rot); $sa = [Math]::Sin($rot)
  foreach ($q in $pts) { $xx = $q[0]*$s; $yy = $q[1]*$s; $o += ,@(($x + $xx*$ca - $yy*$sa), ($y + $xx*$sa + $yy*$ca)) }
  $pf = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  foreach ($q in $o) { $pf.Add((New-Object System.Drawing.PointF([single]$q[0], [single]$q[1]))) }
  $p.AddPolygon($pf.ToArray())
  return $p
}

# ── L1 远景：夜空 + 拱廊剪影 + 悬垂幡 + 星点 ──
function New-LayerSky([string]$out) {
  $r = New-Layer $BW $BH $true $SKY_T; $bmp = $r[0]; $g = $r[1]
  Fill-VGrad $g 0 0 $BW $BH $SKY_T $SKY_B
  Fill-Radial $g ($BW/2.0) ($BH*0.66) 1180 620 $GLOW_C @(12,18,30) 0.85 150 0
  $rng = New-Object System.Random 20260922
  # 星点
  for ($i = 0; $i -lt 170; $i++) {
    $x = $rng.Next(0, $BW); $y = $rng.Next(0, [int]($BH*0.62))
    $rr = 1 + $rng.NextDouble() * 1.8
    $a = 26 + $rng.Next(0, 90)
    $br = New-Object System.Drawing.SolidBrush (New-Col @(210,224,244) $a)
    $g.FillEllipse($br, [single]($x-$rr), [single]($y-$rr), [single]($rr*2), [single]($rr*2)); $br.Dispose()
  }
  # 横向雾带
  foreach ($spec in @(@(0.30, 26), @(0.42, 20), @(0.55, 16))) {
    $yy = [int]($BH * $spec[0])
    Fill-VGrad $g 0 $yy $BW 130 @(150,180,215) @(150,180,215) $spec[1] 0
  }
  # 远处拱廊（左右两组，中间留空给战场）
  $base = [single]($BH * 0.395)
  foreach ($grp in @(@(-40, 7), @(1180, 7))) {
    for ($i = 0; $i -lt $grp[1]; $i++) {
      $x = $grp[0] + $i * 148.0
      $h = 210 + (($i * 37) % 90)
      $aw = 96.0
      $pa = New-Object System.Drawing.Drawing2D.GraphicsPath
      $pa.AddArc(($x), ($base - $h - 30), $aw, $aw, 180, 180)
      $pa.AddLine(($x + $aw), ($base - $h - 30 + $aw/2), ($x + $aw), $base)
      $pa.AddLine(($x + $aw), $base, $x, $base)
      $pa.AddLine($x, $base, $x, ($base - $h - 30 + $aw/2))
      $pa.CloseFigure()
      $bs = New-Object System.Drawing.SolidBrush (New-Col $INK3 150)
      $g.FillPath($bs, $pa); $bs.Dispose()
      $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 34)), 2.5
      $g.DrawPath($pen, $pa); $pen.Dispose(); $pa.Dispose()
      $pen = New-Object System.Drawing.Pen ((New-Col $INK3 170)), 26
      $g.DrawLine($pen, ($x - 26), $base, ($x + $aw + 26), $base); $pen.Dispose()
    }
  }
  # 悬垂幡
  foreach ($spec in @(@(300, 250, 34), @(1620, 210, 30), @(980, 150, 24))) {
    $bx = [single]$spec[0]; $bh2 = [single]$spec[1]; $bwd = [single]$spec[2]
    $pb = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pb.StartFigure()
    $pb.AddBezier(($bx-$bwd), 0, ($bx-$bwd-8), ($bh2*0.7), ($bx-$bwd*0.6), ($bh2*0.92), ($bx-$bwd*0.3), $bh2)
    $pb.AddLine(($bx-$bwd*0.3), $bh2, ($bx+$bwd*0.3), $bh2)
    $pb.AddBezier(($bx+$bwd*0.6), ($bh2*0.92), ($bx+$bwd+8), ($bh2*0.7), ($bx+$bwd), 0, ($bx+$bwd), 0)
    $pb.CloseFigure()
    $bs = New-Object System.Drawing.SolidBrush (New-Col @(20,16,26) 190)
    $g.FillPath($bs, $pb); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 46)), 3
    $g.DrawPath($pen, $pb); $pen.Dispose(); $pb.Dispose()
  }
  # 垂链
  foreach ($spec in @(@(760, 96, 300), @(1300, 70, 250))) {
    $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D 120)), 4
    $g.DrawBezier($pen, $spec[0], 0, ($spec[0]-26), $spec[1], ($spec[0]+$spec[2]+26), $spec[1], ($spec[0]+$spec[2]), 0); $pen.Dispose()
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}
$AR_L = 132; $AR_T = 140; $AR_W = 1784; $AR_H = 872; $AR_R = 300
$AR_CX = $AR_L + $AR_W/2.0; $AR_CY = $AR_T + $AR_H/2.0

function New-ArenaPath([single]$i = 0) {
  return (New-RoundPath ($AR_L+$i) ($AR_T+$i) ($AR_W-$i*2) ($AR_H-$i*2) ([Math]::Max(4,$AR_R-$i)))
}

# ── L2 战场地面：石台 + 板缝 + 裂痕 + 金线镶嵌 ──
function New-LayerFloor([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $ap = New-ArenaPath 0
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($ap)
  $pg.CenterPoint = New-Object System.Drawing.PointF($AR_CX, ($AR_CY - 40))
  $pg.CenterColor = (New-Col $FL_C 242)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $FL_E 232))
  $pg.SetSigmaBellShape(0.78)
  $g.FillPath($pg, $ap); $pg.Dispose()

  $sv = $g.Clip; $g.SetClip($ap)
  # 顶部sheen
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($AR_L, $AR_T, $AR_W, 200)), (New-Col @(160,190,220) 44), (New-Col @(160,190,220) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $AR_L, $AR_T, $AR_W, 200); $lg.Dispose()
  # 石缝：横线 + 带透视的竖线
  for ($k = 1; $k -lt 9; $k++) {
    $yy = $AR_T + $AR_H / 9.0 * $k
    $d = [Math]::Abs($yy - $AR_CY) / ($AR_H/2.0)
    $a = [int](96 * (1 - $d * 0.72))
    if ($a -lt 12) { continue }
    $pen = New-Object System.Drawing.Pen ((New-Col $INK3 $a)), 3
    $g.DrawLine($pen, $AR_L, $yy, ($AR_L + $AR_W), $yy); $pen.Dispose()
  }
  for ($x0 = $AR_L + 120; $x0 -lt ($AR_L + $AR_W); $x0 += 118) {
    $xa = $x0 + ($AR_T - $AR_CY) * 0.055
    $xb = $x0 + ($AR_T + $AR_H - $AR_CY) * 0.055
    $pen = New-Object System.Drawing.Pen ((New-Col $INK3 54)), 3
    $g.DrawLine($pen, $xa, $AR_T, $xb, ($AR_T + $AR_H)); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(150,178,208) 20)), 2
    $g.DrawLine($pen, ($xa+2), $AR_T, ($xb+2), ($AR_T + $AR_H)); $pen.Dispose()
  }
  # 磨损斑
  $rng = New-Object System.Random 9182734
  for ($i = 0; $i -lt 22; $i++) {
    $ex = $rng.Next($AR_L, ($AR_L + $AR_W)); $ey = $rng.Next($AR_T, ($AR_T + $AR_H))
    $rx = 40 + $rng.Next(0, 150); $ry = 24 + $rng.Next(0, 80)
    $br = New-Object System.Drawing.SolidBrush (New-Col @(6,10,18) (18 + $rng.Next(0, 22)))
    $g.FillEllipse($br, ($ex-$rx), ($ey-$ry), ($rx*2), ($ry*2)); $br.Dispose()
    $br = New-Object System.Drawing.SolidBrush (New-Col @(120,150,185) (8 + $rng.Next(0, 12)))
    $g.FillEllipse($br, ($ex-$rx*0.5), ($ey-$ry*1.4), ($rx*1.1), ($ry*0.8)); $br.Dispose()
  }
  # 裂痕
  foreach ($cs in @(@(560, 300, 1), @(1500, 420, -1), @(880, 860, 1))) {
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.StartFigure()
    $cx0 = [single]$cs[0]; $cy0 = [single]$cs[1]; $dir = [single]$cs[2]
    $pp.AddLine($cx0, $cy0, ($cx0 + 42*$dir), ($cy0 + 34))
    $pp.AddLine(($cx0 + 42*$dir), ($cy0 + 34), ($cx0 + 30*$dir), ($cy0 + 78))
    $pp.AddLine(($cx0 + 30*$dir), ($cy0 + 78), ($cx0 + 96*$dir), ($cy0 + 104))
    $pp.AddLine(($cx0 + 96*$dir), ($cy0 + 104), ($cx0 + 120*$dir), ($cy0 + 168))
    $pen = New-Object System.Drawing.Pen ((New-Col $INK3 165)), 3.5
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $pp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(140,170,205) 26)), 1.6
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }
  # 金线镶嵌
  $ip = New-ArenaPath 84
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 86)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ip); $pen.Dispose()
  $ip2 = New-ArenaPath 100
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 40)), 2.5
  $g.DrawPath($pen, $ip2); $pen.Dispose(); $ip.Dispose(); $ip2.Dispose()
  # 四角斜切金线
  $cx1 = $AR_L + 168; $cx2 = $AR_L + $AR_W - 168; $cy1 = $AR_T + 168; $cy2 = $AR_T + $AR_H - 168
  foreach ($c in @(@($cx1,$cy1,1,1), @($cx2,$cy1,-1,1), @($cx1,$cy2,1,-1), @($cx2,$cy2,-1,-1))) {
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 96)), 5
    $g.DrawLine($pen, $c[0], $c[1], ($c[0] + 104*$c[2]), ($c[1] + 104*$c[3])); $pen.Dispose()
  }
  $g.Clip = $sv
  # 外描边（双层）
  $pen = New-Object System.Drawing.Pen ((New-Col $INK3 220)), 10
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ap); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D 120)), 3
  $g.DrawPath($pen, $ap); $pen.Dispose(); $ap.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

function New-Diamond([single]$cx, [single]$cy, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($cx, ($cy-$r))), (New-Object System.Drawing.PointF(($cx+$r), $cy)),
    (New-Object System.Drawing.PointF($cx, ($cy+$r))), (New-Object System.Drawing.PointF(($cx-$r), $cy))))
  return $p
}

# ── L3 法阵外环（可整体慢转）──
function New-LayerSigil([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $BW/2.0; $cy = $BH/2.0
  Stroke-Ell $g $cx $cy 430 $GOLD 8 120
  Stroke-Ell $g $cx $cy 466 $GOLD 3 52
  Stroke-Ell $g $cx $cy 414 $STEEL 2.5 44
  for ($i = 0; $i -lt 24; $i++) {
    $a = [Math]::PI / 12.0 * $i
    $r0 = 430.0; $r1 = if ($i % 2 -eq 0) { 430.0 + 38 } else { 430.0 + 20 }
    $ta = 74; $tw = 5
    if ($i % 2 -eq 0) { $ta = 118; $tw = 8 }
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $ta)), $tw
    $g.DrawLine($pen, [single]($cx + $r0*[Math]::Cos($a)), [single]($cy + $r0*[Math]::Sin($a)), [single]($cx + $r1*[Math]::Cos($a)), [single]($cy + $r1*[Math]::Sin($a)))
    $pen.Dispose()
  }
  # 12 枚符文刻记
  for ($i = 0; $i -lt 12; $i++) {
    $a = [Math]::PI / 6.0 * $i + 0.26
    $rx = $cx + 500*[Math]::Cos($a); $ry = $cy + 500*[Math]::Sin($a)
    $s2 = 15.0
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 92)), 4
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $gp.AddLine(($rx-$s2), ($ry-$s2), ($rx+$s2), ($ry-$s2))
    $gp.AddLine(($rx+$s2), ($ry-$s2), ($rx), ($ry+$s2))
    $gp.AddLine($rx, ($ry+$s2), ($rx-$s2), ($ry-$s2))
    if ($i % 3 -eq 0) { $gp.AddLine($rx, ($ry-$s2), $rx, ($ry+$s2)) }
    if ($i % 3 -eq 1) { $gp.AddLine(($rx-$s2), $ry, ($rx+$s2), $ry) }
    $g.DrawPath($pen, $gp); $pen.Dispose(); $gp.Dispose()
  }
  foreach ($i in 0..3) {
    $a = [Math]::PI / 2.0 * $i + [Math]::PI / 4.0
    $dx = $cx + 430*[Math]::Cos($a); $dy = $cy + 430*[Math]::Sin($a)
    $dp = New-Diamond $dx $dy 26
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 150)
    $g.FillPath($bs, $dp); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK3 200)), 4
    $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ── L4 法阵内环（与 L3 反向转）──
function New-LayerRune([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $BW/2.0; $cy = $BH/2.0
  Stroke-Ell $g $cx $cy 330 $STEEL 4 66
  Stroke-Ell $g $cx $cy 318 $STEEL 2 34
  for ($i = 0; $i -lt 16; $i++) {
    $a = [Math]::PI / 8.0 * $i
    $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 40)), 4
    $g.DrawLine($pen, [single]($cx + 296*[Math]::Cos($a)), [single]($cy + 296*[Math]::Sin($a)), [single]($cx + 322*[Math]::Cos($a)), [single]($cy + 322*[Math]::Sin($a)))
    $pen.Dispose()
  }
  foreach ($rot in @(0.0, 1.0472)) {
    $tp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @()
    for ($i = 0; $i -lt 3; $i++) {
      $a = $rot + [Math]::PI * 2 / 3 * $i - [Math]::PI/2
      $pts += ,@(($cx + 248*[Math]::Cos($a)), ($cy + 248*[Math]::Sin($a)))
    }
    $pf2 = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    foreach ($q in $pts) { $pf2.Add((New-Object System.Drawing.PointF([single]$q[0], [single]$q[1]))) }
    $tp.AddPolygon($pf2.ToArray())
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 48)), 4
    $g.DrawPath($pen, $tp); $pen.Dispose(); $tp.Dispose()
  }
  $cp = New-Diamond $cx $cy 92
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 66)), 5
  $g.DrawPath($pen, $cp); $pen.Dispose(); $cp.Dispose()
  $cp2 = New-Diamond $cx $cy 26
  $bs = New-Object System.Drawing.SolidBrush (New-Col $ARC 150)
  $g.FillPath($bs, $cp2); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK3 190)), 4
  $g.DrawPath($pen, $cp2); $pen.Dispose(); $cp2.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ── L5 静态饰件：外框 + 角铁 + 铆钉 + 暗角 ──
function New-LayerOrnament([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  # 暗角
  foreach ($c in @(@(0,0,760,520), @($BW,0,760,520), @(0,$BH,760,520), @($BW,$BH,760,520))) {
    Fill-Radial $g $c[0] $c[1] $c[2] $c[3] @(0,0,0) @(0,0,0) 0.9 150 0
  }
  Fill-VGrad $g 0 0 $BW 190 @(0,0,0) @(0,0,0) 150 0
  Fill-VGrad $g 0 ($BH-210) $BW 210 @(0,0,0) @(0,0,0) 0 160
  # 框线
  $f1 = 64; $f2 = 92; $f3 = 118
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 118)), 6
  $g.DrawRectangle($pen, $f1, $f1, ($BW-$f1*2), ($BH-$f1*2)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 58)), 3
  $g.DrawRectangle($pen, $f2, $f2, ($BW-$f2*2), ($BH-$f2*2)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 96)), 2
  $g.DrawRectangle($pen, $f3, $f3, ($BW-$f3*2), ($BH-$f3*2)); $pen.Dispose()
  # 角铁
  $arm = 168; $t = 18
  foreach ($pair in @(@(0,0),@(1,0),@(0,1),@(1,1))) {
    $sx = $pair[0]; $sy = $pair[1]
    $x0 = if ($sx -eq 0) { 44 } else { ($BW-44) }
    $y0 = if ($sy -eq 0) { 44 } else { ($BH-44) }
    $dx = if ($sx -eq 0) { 1 } else { -1 }
    $dy = if ($sy -eq 0) { 1 } else { -1 }
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddLines([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(($x0+$dx*$arm), $y0)),
      (New-Object System.Drawing.PointF($x0, $y0)),
      (New-Object System.Drawing.PointF($x0, ($y0+$dy*$arm)))))
    $pen = New-Object System.Drawing.Pen ((New-Col $INK3 200)), ($t+12)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 225)), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 150)), 4
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }
  # 铆钉
  for ($x = $f1 + 210; $x -lt ($BW - $f1 - 200); $x += 190) {
    foreach ($yy in @($f1, ($BH-$f1))) {
      $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 210)
      $g.FillEllipse($bs, [single]($x-8), [single]($yy-8), 16, 16); $bs.Dispose()
      $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 170)
      $g.FillEllipse($bs, [single]($x-6), [single]($yy-6), 9, 9); $bs.Dispose()
    }
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ── L6 辉光层（黑底，配 Additive 材质；做呼吸/脉冲）──
function New-LayerGlow([string]$out) {
  $r = New-Layer $BW $BH $true @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $BW/2.0; $cy = $BH/2.0
  Fill-Radial $g $cx $cy 900 520 @(44,74,120) @(0,0,0) 0.9 210 0
  Fill-Radial $g $cx $cy 520 320 @(60,100,158) @(0,0,0) 0.85 190 0
  Fill-Radial $g $cx $cy 210 150 @(96,148,214) @(0,0,0) 0.8 170 0
  foreach ($i in 0..5) {
    $rr = 430.0 + $i * 9
    $pen = New-Object System.Drawing.Pen ((New-Col @(58,48,22) (16 - $i*2)), 16)
    $g.DrawEllipse($pen, [single]($cx-$rr), [single]($cy-$rr), [single]($rr*2), [single]($rr*2)); $pen.Dispose()
  }
  Fill-Radial $g 430 300 260 150 @(36,58,96) @(0,0,0) 0.9 120 0
  Fill-Radial $g 1618 300 260 150 @(36,58,96) @(0,0,0) 0.9 120 0
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ── L7 浮尘层（透明、可无缝循环漂移）──
function New-LayerMotes([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $rng = New-Object System.Random 5150243
  for ($i = 0; $i -lt 260; $i++) {
    $x = $rng.NextDouble() * $BW; $y = $rng.NextDouble() * $BH
    $rr = 0.9 + $rng.NextDouble() * 2.4
    $a = 22 + $rng.Next(0, 118)
    $tint = if ($i % 7 -eq 0) { @(232,209,138) } elseif ($i % 3 -eq 0) { @(150,184,216) } else { @(214,226,242) }
    $br = New-Object System.Drawing.SolidBrush (New-Col $tint $a)
    foreach ($ox in @(0, (0 - $BW), $BW)) {
      foreach ($oy in @(0, (0 - $BH), $BH)) {
        $xx = $x + $ox; $yy = $y + $oy
        if ($xx -gt -8 -and $xx -lt ($BW+8) -and $yy -gt -8 -and $yy -lt ($BH+8)) {
          $g.FillEllipse($br, [single]($xx-$rr), [single]($yy-$rr), [single]($rr*2), [single]($rr*2))
        }
      }
    }
    $br.Dispose()
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

$LAYERS = @('Board_Sky','Board_Floor','Board_Sigil','Board_Rune','Board_Ornament','Board_Glow','Board_Motes')

function New-BoardLayers([string]$OutDir) {
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
  $made = @()
  $made += New-LayerSky      (Join-Path $OutDir 'Board_Sky.png')
  $made += New-LayerFloor    (Join-Path $OutDir 'Board_Floor.png')
  $made += New-LayerSigil    (Join-Path $OutDir 'Board_Sigil.png')
  $made += New-LayerRune     (Join-Path $OutDir 'Board_Rune.png')
  $made += New-LayerOrnament (Join-Path $OutDir 'Board_Ornament.png')
  $made += New-LayerGlow     (Join-Path $OutDir 'Board_Glow.png')
  $made += New-LayerMotes    (Join-Path $OutDir 'Board_Motes.png')
  return $made
}

function Draw-LayerInto($g, $img, [int]$dw, [int]$dh, [single]$rot, [single]$alpha) {
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  if ($alpha -lt 0.999) {
    $cm = New-Object System.Drawing.Imaging.ColorMatrix
    $cm.Matrix33 = $alpha
    $ia.SetColorMatrix($cm)
  }
  if ([Math]::Abs($rot) -gt 0.01) {
    $st = $g.Save()
    $g.TranslateTransform(($dw/2.0), ($dh/2.0)); $g.RotateTransform($rot); $g.TranslateTransform((-$dw/2.0), (-$dh/2.0))
    $g.DrawImage($img, (New-Object System.Drawing.Rectangle(0,0,$dw,$dh)), 0, 0, $BW, $BH, [System.Drawing.GraphicsUnit]::Pixel, $ia)
    $g.Restore($st)
  } else {
    $g.DrawImage($img, (New-Object System.Drawing.Rectangle(0,0,$dw,$dh)), 0, 0, $BW, $BH, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  }
  $ia.Dispose()
}

function New-BoardComposite([int]$dw, [int]$dh, $imgs, [single]$rotSigil, [single]$rotRune, [single]$glowA, [single]$motesA, [single]$motOffX, [single]$motOffY) {
  $b = New-Object System.Drawing.Bitmap($dw, $dh, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  Draw-LayerInto $g $imgs.sky $dw $dh 0 1.0
  Draw-LayerInto $g $imgs.floor $dw $dh 0 1.0
  Draw-LayerInto $g $imgs.sigil $dw $dh $rotSigil 1.0
  Draw-LayerInto $g $imgs.rune $dw $dh $rotRune 1.0
  # 辉光：GDI+ 无叠加混合，预览按 0.7 透明近似（引擎里用 Additive 材质）
  Draw-LayerInto $g $imgs.glow $dw $dh 0 ($glowA * 0.7)
  Draw-LayerInto $g $imgs.orn $dw $dh 0 1.0
  if ($motesA -gt 0.01) {
    $st = $g.Save()
    $g.TranslateTransform(($motOffX * $dw / $BW), ($motOffY * $dh / $BH))
    Draw-LayerInto $g $imgs.motes $dw $dh 0 $motesA
    $g.TranslateTransform(-$dw, -$dh); Draw-LayerInto $g $imgs.motes $dw $dh 0 $motesA
    $g.TranslateTransform($dw, 0); Draw-LayerInto $g $imgs.motes $dw $dh 0 $motesA
    $g.TranslateTransform(0, $dh); Draw-LayerInto $g $imgs.motes $dw $dh 0 $motesA
    $g.TranslateTransform(-$dw, -$dh)
    $g.Restore($st)
  }
  $g.Dispose()
  return $b
}

function Load-LayerSet([string]$Dir) {
  return @{
    sky   = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Sky.png'))
    floor = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Floor.png'))
    sigil = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Sigil.png'))
    rune  = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Rune.png'))
    orn   = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Ornament.png'))
    glow  = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Glow.png'))
    motes = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Motes.png'))
  }
}

function New-BoardLayerSheet([string]$Dir, [string]$Out) {
  $S = Load-LayerSet $Dir
  $W = 1560; $H = 878
  $bmp = New-Object System.Drawing.Bitmap(1640, ($H + 470), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(12, 16, 24))
  $font = New-Object System.Drawing.Font('Consolas', 12)
  $fontS = New-Object System.Drawing.Font('Consolas', 10)
  $br = [System.Drawing.Brushes]::White
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
  $g.DrawString('合成结果（含辉光，预览按 0.7 透明近似）', $font, $gold, 20, 12)
  $flat = New-BoardComposite $W $H $S 0 0 1.0 1.0 0 0
  $g.DrawImage($flat, 40, 40, $W, $H); $flat.Dispose()
  $y = $H + 70
  $g.DrawString('分层（每层 2048x1152，同画布同锚点，中心对齐）', $font, $gold, 20, $y)
  $y += 32
  $cell = 214; $x = 24
  $names = @(@('Board_Sky','L1 远景 夜空/拱廊/幡/星'), @('Board_Floor','L2 地面 石台/板缝/裂痕/金线'), @('Board_Sigil','L3 法阵外环 [可转]'), @('Board_Rune','L4 法阵内环 [反向转]'), @('Board_Ornament','L5 静态饰件 框/角铁/铆钉/暗角'), @('Board_Glow','L6 辉光 [Additive·脉冲]'), @('Board_Motes','L7 浮尘 [无缝漂移]'))
  foreach ($n in $names) {
    $img = [System.Drawing.Image]::FromFile((Join-Path $Dir ($n[0] + '.png')))
    $g.DrawImage($img, $x, $y, $cell, [int]($cell * $BH / $BW)); $img.Dispose()
    $g.DrawString($n[0], $fontS, $br, $x, ($y + $cell * $BH / $BW + 4))
    $g.DrawString($n[1], $fontS, $gold, $x, ($y + $cell * $BH / $BW + 20))
    $x += ($cell + 12)
  }
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  foreach ($k in $S.Keys) { $S[$k].Dispose() }
  return $Out
}

function New-BoardAnimSheet([string]$Dir, [string]$Out) {
  $S = Load-LayerSet $Dir
  $fw = 520; $fh = 292
  $bmp = New-Object System.Drawing.Bitmap(($fw*3 + 60), ($fh + 96), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(12, 16, 24))
  $font = New-Object System.Drawing.Font('Consolas', 11)
  $fontS = New-Object System.Drawing.Font('Consolas', 10)
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
  $g.DrawString('动态基础验证：同一套分层，三种状态（只动 L3/L4 旋转、L6 辉光、L7 浮尘）', $font, $gold, 18, 12)
  $frames = @(
    @(0.0, 0.0, 0.60, 0.55, 0, 0, 'A 待机 环静止 / 辉光弱'),
    @(14.0, -22.0, 1.35, 0.85, 64, 26, 'B 施法 环正反转动 / 辉光强'),
    @(-10.0, 26.0, 0.45, 0.40, 148, 62, 'C 低潮 环回摆 / 辉光弱 / 尘埃漂移')
  )
  $x = 18
  foreach ($f in $frames) {
    $img = New-BoardComposite $fw $fh $S $f[0] $f[1] $f[2] $f[3] $f[4] $f[5]
    $g.DrawImage($img, $x, 40, $fw, $fh); $img.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(200,164,74) 120)), 2
    $g.DrawRectangle($pen, $x, 40, $fw, $fh); $pen.Dispose()
    $g.DrawString($f[6], $fontS, $gold, $x, ($fh + 48))
    $x += ($fw + 12)
  }
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  foreach ($k in $S.Keys) { $S[$k].Dispose() }
  return $Out
}
