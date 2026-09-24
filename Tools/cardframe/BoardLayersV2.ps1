# 战场底板 v2（分层版）——同一张 2048x1152 画布拆 8 层，每层独立可动
# 依赖 CardFrameV6.ps1 的 New-Col / Mix-Col / New-RoundPath
#
# 设计口径（2026-09-22 定）
#  A. 可见区 ≈ 整张画布：透视相机 FOV50、机位 (0,1,-16.22)、板面前脸 z=-5.5，
#     板面 19.2x10.8 世界单位 = 2048x1152 px（106.67 px/单位）。距板面 10.72u，
#     可见 10.0x17.78 单位 = 1066x1896 px → 左右各裁 ~76px、上下各裁 ~43px。
#     即：整幅都看得见，四边安全边 ~4%（80px），细节铺满，别做「画框外没人看」的假设。
#  B. 亮度纪律（用户 2026-09-22：「这个基础上改啊草」+「不要太多闪光，一般是暗细节，
#     不能抢到卡牌主体」）。口径：
#       ① 明度结构一律照 v1（用户认可那张）——夜空底 + 面板内托 + 冷光池 + 四角压深，
#          由 L1 用 v1 的 sky/floor/glow 三份配方原样烘焙（glow 按 v1 的 0.7 叠回）；
#       ② 「改」的部分只允许是**暗细节**：板缝、放射刻线、座圈、磨蚀、裂痕、口袋刻花，
#          alpha 压在 100 上下、线宽 2-6px，读作「刻在黑石头上」；
#       ③ 禁止：大面积亮块、密集亮点、暖色光斑（壁灯已随柱子一并删除）。
#  C. 一个板：整幅就是一块石板，不做任何出屏的建筑 / 柱子（用户 2026-09-22 定）。
#     卡位禁区：战场卡占据 x 609..1439 / y 92..1057（列 704/1024/1344，
#     行 y=192/437/711/957，卡 1.35x2.4 世界单位）。这一竖带只放「暗细节」
#     （凹槽、板缝、磨损），视觉重心放在左右口袋区（x<600 / x>1440）。
#  D. 动态基础：8 层同画布同锚点（中心对齐），可直接叠在同一次排序里：
#     L1 Board_Plate      静态   整块底板：夜空底 + 面板内托 + 中心冷光池 + 四角压深（v1 配方原样烘焙）
#     L2 Board_Surface    静态   板面刻纹：板缝 / 镶嵌 / 放射线 / 磨蚀 / 裂痕 / 口袋内嵌凹板
#     L3 Board_Sigil      慢转   (建议 +3~5 度/秒)
#     L4 Board_Rune       反转   (建议 -6~-8 度/秒)
#     L5 Board_Ornament   静态   金框 / 角铁 / 铆钉 / 框上刻记
#     L6 Board_Glow       脉冲   Additive 材质，亮度 0.6~1.4 呼吸
#     L7 Board_Motes      漂移   可无缝平铺；数量/间隔见 BoardMotes.cs
#     L8 Board_Foreground 视差   暗角 + 底沿，镜头抖动时反向轻微位移
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

$BW = 2048; $BH = 1152

# ── 调色板：石面一律低明度，金只做细线 ──
$INK0    = @(6, 9, 14)
$INK1    = @(10, 14, 21)
$INK2    = @(14, 19, 29)
$INK3    = @(20, 27, 40)
$STONE0  = @(16, 22, 33)
$STONE1  = @(22, 30, 44)
$STONE2  = @(31, 42, 61)
$STONE3  = @(44, 58, 82)
$STEEL   = @(110, 147, 176)
$STEEL_D = @(64, 86, 112)
$GOLD    = @(200, 164, 74)
$GOLD_L  = @(232, 209, 138)
$GOLD_D  = @(120, 94, 40)
$WARM    = @(122, 88, 42)
$ARC     = @(74, 132, 206)
# ── v1（用户认可的那张）的底色配方，L1 原样沿用 ──
$SKY_T   = @(9, 14, 25)
$SKY_B   = @(20, 31, 49)
$GLOW_C  = @(40, 62, 92)
$FL_C    = @(34, 48, 70)
$FL_E    = @(10, 15, 24)

# 法阵中心（= 卡位禁区中心）：卡位 x 704/1024/1344，行 y 192/437/711/957
$SGX = 1024.0; $SGY = 576.0

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

function New-Diamond([single]$cx, [single]$cy, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($cx, ($cy-$r))), (New-Object System.Drawing.PointF(($cx+$r), $cy)),
    (New-Object System.Drawing.PointF($cx, ($cy+$r))), (New-Object System.Drawing.PointF(($cx-$r), $cy))))
  return $p
}

function Add-Pits($g, $rng, [int]$n, [int]$x0, [int]$y0, [int]$w, [int]$h, [int[]]$col, [int]$aMin, [int]$aMax, [single]$rMin, [single]$rMax) {
  for ($i = 0; $i -lt $n; $i++) {
    $x = $x0 + $rng.NextDouble() * $w
    $y = $y0 + $rng.NextDouble() * $h
    $rx = $rMin + $rng.NextDouble() * ($rMax - $rMin)
    $ky = 0.45 + $rng.NextDouble() * 0.75
    $ry = $rx * $ky
    $aa = $aMin + $rng.Next(0, ($aMax - $aMin + 1))
    $br = New-Object System.Drawing.SolidBrush (New-Col $col $aa)
    $g.FillEllipse($br, [single]($x - $rx), [single]($y - $ry), [single]($rx * 2), [single]($ry * 2))
    $br.Dispose()
  }
}

# ══════════════════════════════════════════════════════════
# L1 远景：墙体 + 双排列柱 + 拱窗 + 悬幡 + 垂链   [静态]
#     只占上半带 y 0..330；下半被 L2 石面完全盖住，留作兜底
# ══════════════════════════════════════════════════════════

function New-BannerPath([single]$x, [single]$yTop, [single]$w, [single]$h) {
  $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  $pts.Add((New-Object System.Drawing.PointF($x, $yTop)))
  $pts.Add((New-Object System.Drawing.PointF(($x + $w), $yTop)))
  $pts.Add((New-Object System.Drawing.PointF(($x + $w), ($yTop + $h * 0.80))))
  $pts.Add((New-Object System.Drawing.PointF(($x + $w * 0.5), ($yTop + $h))))
  $pts.Add((New-Object System.Drawing.PointF($x, ($yTop + $h * 0.80))))
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon($pts.ToArray())
  return $p
}
function Draw-Pillar($g, [single]$x, [single]$w, [single]$yTop, [single]$yBase, [int[]]$col, [int]$a, [int[]]$trim, [int]$ta, [bool]$capital) {
  $br = New-Object System.Drawing.SolidBrush (New-Col $col $a)
  $g.FillRectangle($br, $x, $yTop, $w, ($yBase - $yTop))
  if ($capital) {
    $g.FillRectangle($br, ($x - 13), $yTop, ($w + 26), 16)
    $g.FillRectangle($br, ($x - 20), ($yBase - 22), ($w + 40), 22)
  }
  $br.Dispose()
  if ($ta -gt 0) {
    $pen = New-Object System.Drawing.Pen ((New-Col $trim $ta)), 2
    $g.DrawLine($pen, ($x + 4), ($yTop + 22), ($x + 4), ($yBase - 26)); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 150)), 2
    $g.DrawLine($pen, ($x + $w - 4), ($yTop + 22), ($x + $w - 4), ($yBase - 26)); $pen.Dispose()
  }
}

# ══════════════════════════════════════════════════════════
# L1 底板：整幅就是一块板   [静态·不透明]
#     不出屏的建筑一律不做（用户 2026-09-22：不要柱子，整体就一个板）
#     这层只管「整块板」的大明度结构：中间托起、四角压深、大块色斑
# ══════════════════════════════════════════════════════════
# ══════════════════════════════════════════════════════════
# L1 底板：v1 的底子（深蓝夜色 + 圆角面板 + 中心柔托）   [静态·不透明]
#     严格沿 v1 的构图：面板 (132,140,1784,872) r300、法阵中心 (1024,576)
#     只把中心柔光压弱一档（用户 2026-09-22：不要太多闪光）
# ══════════════════════════════════════════════════════════
function New-LayerPlate([string]$out) {
  $r = New-Layer $BW $BH $true $SKY_T; $bmp = $r[0]; $g = $r[1]
  # ── v1 的夜空底：上暗下亮 + 中心冷雾（照 BoardLayersV1 的 New-LayerSky）──
  Fill-VGrad $g 0 0 $BW $BH $SKY_T $SKY_B
  Fill-Radial $g ($BW/2.0) ($BH*0.66) 1180 620 $GLOW_C @(12,18,30) 0.85 150 0
  # v1 的星点：数量与强度各压一档（用户要求「不要太多闪光」）
  $rng = New-Object System.Random 20260922
  for ($i = 0; $i -lt 110; $i++) {
    $x = $rng.Next(0, $BW); $y = $rng.Next(0, [int]($BH*0.62))
    $rr = 1 + $rng.NextDouble() * 1.5
    $a = 16 + $rng.Next(0, 56)
    $br = New-Object System.Drawing.SolidBrush (New-Col @(210,224,244) $a)
    $g.FillEllipse($br, [single]($x-$rr), [single]($y-$rr), [single]($rr*2), [single]($rr*2)); $br.Dispose()
  }
  # ── 战场面板：v1 的 FL_C / FL_E 结构（中心托起、四周压深）──
  $ap = New-ArenaPath 0
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($ap)
  $pg.CenterPoint = New-Object System.Drawing.PointF($AR_CX, ($AR_CY - 40))
  $pg.CenterColor = (New-Col $FL_C 242)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $FL_E 232))
  $pg.SetSigmaBellShape(0.78)
  $g.FillPath($pg, $ap); $pg.Dispose()
  # ── v1 的辉光层原样叠回（0.7）—— 明度结构就是它给的：三圈冷光池 + 法阵环暖灰 +
  #    左右两池，四角压深也来自它（黑 * 0.7）。把它烘焙进 L1 而不是留在 L6，
  #    是为了让静态底先说清「光在哪」，L6 只负责之后那口气（呼吸 / 脉冲）。──
  $gr = New-Layer $BW $BH $true @(0,0,0); $gb = $gr[0]; $gg = $gr[1]
  $cx = $BW/2.0; $cy = $BH/2.0
  Fill-Radial $gg $cx $cy 900 520 @(44,74,120) @(0,0,0) 0.9 210 0
  Fill-Radial $gg $cx $cy 520 320 @(60,100,158) @(0,0,0) 0.85 190 0
  Fill-Radial $gg $cx $cy 210 150 @(96,148,214) @(0,0,0) 0.8 170 0
  foreach ($i in 0..5) {
    $rr = 430.0 + $i * 9
    $pen = New-Object System.Drawing.Pen ((New-Col @(58,48,22) (16 - $i*2)), 16)
    $gg.DrawEllipse($pen, [single]($cx-$rr), [single]($cy-$rr), [single]($rr*2), [single]($rr*2)); $pen.Dispose()
  }
  Fill-Radial $gg 430 300 260 150 @(36,58,96) @(0,0,0) 0.9 120 0
  Fill-Radial $gg 1618 300 260 150 @(36,58,96) @(0,0,0) 0.9 120 0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix33 = 0.7; $ia.SetColorMatrix($cm)
  $g.DrawImage($gb, (New-Object System.Drawing.Rectangle(0,0,$BW,$BH)), 0, 0, $BW, $BH, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose(); $gg.Dispose(); $gb.Dispose()
  # ── 面板外一圈压深 + 四角再压一档（把面板「坐」进底色里）──
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 110)), 26
  $g.DrawPath($pen, $ap); $pen.Dispose()
  foreach ($c in @(@(0,0,700,470), @($BW,0,700,470), @(0,$BH,700,470), @($BW,$BH,700,470))) {
    Fill-Radial $g $c[0] $c[1] $c[2] $c[3] $INK0 $INK0 0.92 96 0
  }
  $ap.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}
# ══════════════════════════════════════════════════════════
# L2 石面：台地 + 板缝 + 战场镶嵌 + 刻线 + 磨蚀 + 裂痕   [静态]
#     只从 y=330（墙脚）往下铺，上半留给 L1 远景
# ══════════════════════════════════════════════════════════
$AR_L = 132.0; $AR_T = 140.0; $AR_W = 1784.0; $AR_H = 872.0; $AR_R = 300.0
$AR_CX = $AR_L + ($AR_W / 2.0); $AR_CY = $AR_T + ($AR_H / 2.0)

function New-ArenaPath([single]$i = 0) {
  return (New-RoundPath ($AR_L+$i) ($AR_T+$i) ($AR_W-$i*2) ($AR_H-$i*2) ([Math]::Max(6,$AR_R-$i)))
}
# ══════════════════════════════════════════════════════════
# L2 板面：刻纹 / 镶嵌 / 磨蚀 / 裂痕   [静态·透明叠加在 L1 上]
#     整幅铺满，卡位竖带（x 609..1439）只放凹槽与板缝，不放亮点
# ══════════════════════════════════════════════════════════
# ══════════════════════════════════════════════════════════
# L2 板面：v1 的缝 / 磨损 / 裂痕 / 镶嵌，全部加密加深   [静态·透明]
#     几何一律沿 v1：面板 (132,140,1784,872) r300、法阵中心 (1024,576)
#     新增：法阵座圈、外圈刻线与短齿、口袋区刻花、放射刻线、更多磨蚀
# ══════════════════════════════════════════════════════════
function New-LayerSurface([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $rng = New-Object System.Random 77123
  $ap = New-ArenaPath 0
  $sv = $g.Clip; $g.SetClip($ap)

  # ── 面板内石板缝（v1 做法：横线 + 带透视的竖线），加密一档 ──
  for ($k = 1; $k -lt 11; $k++) {
    $yy = $AR_T + $AR_H / 11.0 * $k
    $d = [Math]::Abs($yy - $AR_CY) / ($AR_H / 2.0)
    $aa = [int](92 * (1 - $d * 0.66))
    if ($aa -lt 12) { continue }
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 $aa)), 3
    $g.DrawLine($pen, $AR_L, $yy, ($AR_L + $AR_W), $yy); $pen.Dispose()
  }
  for ($x0 = $AR_L + 112.0; $x0 -lt ($AR_L + $AR_W); $x0 += 112.0) {
    $xa = $x0 + ($AR_T - $AR_CY) * 0.055
    $xb = $x0 + ($AR_T + $AR_H - $AR_CY) * 0.055
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 56)), 3
    $g.DrawLine($pen, $xa, $AR_T, $xb, ($AR_T + $AR_H)); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(150,178,208) 22)), 2
    $g.DrawLine($pen, ($xa + 3), $AR_T, ($xb + 3), ($AR_T + $AR_H)); $pen.Dispose()
  }
  # 放射刻线：从法阵中心往外
  for ($k = 0; $k -lt 12; $k++) {
    $a = [Math]::PI * 2 / 12.0 * $k + 0.13
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 34)), 2
    $g.DrawLine($pen, [single]($SGX + 150 * [Math]::Cos($a)), [single]($SGY + 150 * [Math]::Sin($a)), [single]($SGX + 560 * [Math]::Cos($a)), [single]($SGY + 560 * [Math]::Sin($a)))
    $pen.Dispose()
  }
  # ── 磨损斑：v1 的斑保留，但尺寸与强度各压一档 —— 只做「石头脏了」，不做「泥点子」──
  for ($i = 0; $i -lt 20; $i++) {
    $ex = $rng.Next([int]$AR_L, [int]($AR_L + $AR_W)); $ey = $rng.Next([int]$AR_T, [int]($AR_T + $AR_H))
    $rx = 34 + $rng.Next(0, 68); $ry = 20 + $rng.Next(0, 44)
    $br = New-Object System.Drawing.SolidBrush (New-Col @(6,10,18) (12 + $rng.Next(0, 14)))
    $g.FillEllipse($br, [single]($ex-$rx), [single]($ey-$ry), [single]($rx*2), [single]($ry*2)); $br.Dispose()
    $br = New-Object System.Drawing.SolidBrush (New-Col @(130,160,196) (4 + $rng.Next(0, 6)))
    $g.FillEllipse($br, [single]($ex-$rx*0.5), [single]($ey-$ry*1.4), [single]($rx*1.1), [single]($ry*0.8)); $br.Dispose()
  }
  Add-Pits $g $rng 620 ([int]$AR_L) ([int]$AR_T) ([int]$AR_W) ([int]$AR_H) $INK0 12 30 3 12
  Add-Pits $g $rng 280 ([int]$AR_L) ([int]$AR_T) ([int]$AR_W) ([int]$AR_H) @(150,178,208) 6 16 2 8
  # ── 裂痕：v1 那三条保留，再补三条；不做满（多了就成了花纹）──
  $cracks = @(@(560,300,1), @(1500,420,-1), @(880,860,1), @(1300,690,-1), @(300,380,1), @(1620,760,-1))
  foreach ($cs in $cracks) {
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.StartFigure()
    $cx0 = [single]$cs[0]; $cy0 = [single]$cs[1]; $dir = [single]$cs[2]
    $pp.AddLine($cx0, $cy0, ($cx0 + 42*$dir), ($cy0 + 34))
    $pp.AddLine(($cx0 + 42*$dir), ($cy0 + 34), ($cx0 + 30*$dir), ($cy0 + 78))
    $pp.AddLine(($cx0 + 30*$dir), ($cy0 + 78), ($cx0 + 96*$dir), ($cy0 + 104))
    $pp.AddLine(($cx0 + 96*$dir), ($cy0 + 104), ($cx0 + 120*$dir), ($cy0 + 168))
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 142)), 3.2
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $pp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(140,170,205) 24)), 1.4
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }
  $g.Clip = $sv
  # ── 面板外描边（v1 双层）──
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 220)), 8
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ap); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D 120)), 3
  $g.DrawPath($pen, $ap); $pen.Dispose()
  # ── 金线镶嵌 + 钢线（v1 的 84 / 100 内缩）──
  $ip = New-ArenaPath 84
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 76)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ip); $pen.Dispose()
  $ip2 = New-ArenaPath 100
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 30)), 2.5
  $g.DrawPath($pen, $ip2); $pen.Dispose()
  $ip3 = New-ArenaPath 62
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 50)), 2
  $g.DrawPath($pen, $ip3); $pen.Dispose()
  # 金线上的菱形铆点
  for ($k = 0; $k -lt 40; $k++) {
    $t4 = $k / 40.0 * 4.0
    $x = $AR_L + 84.0; $y = $AR_T + 84.0
    if ($t4 -lt 1.0) { $x = $AR_L + 84 + ($AR_W - 168) * $t4; $y = $AR_T + 84 }
    elseif ($t4 -lt 2.0) { $x = $AR_L + $AR_W - 84; $y = $AR_T + 84 + ($AR_H - 168) * ($t4 - 1.0) }
    elseif ($t4 -lt 3.0) { $x = $AR_L + $AR_W - 84 - ($AR_W - 168) * ($t4 - 2.0); $y = $AR_T + $AR_H - 84 }
    else { $x = $AR_L + 84; $y = $AR_T + $AR_H - 84 - ($AR_H - 168) * ($t4 - 3.0) }
    $dp = New-Diamond $x $y 6
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD 70)
    $g.FillPath($br, $dp); $br.Dispose(); $dp.Dispose()
  }
  # ── 四角斜切金线（v1 特征，保留并加一档）──
  $cx1 = $AR_L + 168; $cx2 = $AR_L + $AR_W - 168; $cy1 = $AR_T + 168; $cy2 = $AR_T + $AR_H - 168
  foreach ($c in @(@($cx1,$cy1,1,1), @($cx2,$cy1,-1,1), @($cx1,$cy2,1,-1), @($cx2,$cy2,-1,-1))) {
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 86)), 5
    $g.DrawLine($pen, $c[0], $c[1], ($c[0] + 104*$c[2]), ($c[1] + 104*$c[3])); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 60)), 4
    $g.DrawLine($pen, ($c[0] + 22*$c[2]), ($c[1] + 22*$c[3]), ($c[0] + 132*$c[2]), ($c[1] + 132*$c[3])); $pen.Dispose()
  }
  $ip.Dispose(); $ip2.Dispose(); $ip3.Dispose(); $ap.Dispose()

  # ── 法阵座圈：把 L3/L4 的环坐进板里 ──
  foreach ($ring in @(@(430, 4, 44), @(466, 2, 26), @(330, 3, 38), @(318, 2, 20), @(248, 2, 14))) {
    Stroke-Ell $g $SGX $SGY $ring[0] $INK0 $ring[1] $ring[2]
  }
  Stroke-Ell $g $SGX $SGY 430 $GOLD 2 34
  # 外圈场地刻线 + 短齿
  Stroke-Ell $g $SGX $SGY 566 $INK0 3 48
  Stroke-Ell $g $SGX $SGY 578 $INK0 2 28
  for ($k = 0; $k -lt 48; $k++) {
    $a = [Math]::PI * 2 / 48.0 * $k
    $rr = 566.0
    $r1 = 584.0
    if ($k % 4 -eq 0) { $r1 = 600.0 }
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 42)), 2.6
    $g.DrawLine($pen, [single]($SGX + $rr*[Math]::Cos($a)), [single]($SGY + $rr*[Math]::Sin($a)), [single]($SGX + $r1*[Math]::Cos($a)), [single]($SGY + $r1*[Math]::Sin($a)))
    $pen.Dispose()
  }
  # ── 口袋区刻花（v1 那几条斜线所在的区域）──
  foreach ($q in @(@(276, 452), @(1772, 452), @(276, 934), @(1772, 934))) {
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 52)), 3
    $qp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $qp.AddLine(($q[0] - 74), $q[1], ($q[0] + 74), $q[1])
    $qp.AddLine(($q[0] + 74), $q[1], ($q[0] + 74), ($q[1] + 74))
    $g.DrawPath($pen, $qp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 36)), 2.4
    $qp2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $qp2.AddLine(($q[0] - 52), ($q[1] + 22), ($q[0] + 52), ($q[1] + 22))
    $qp2.AddLine(($q[0] + 52), ($q[1] + 22), ($q[0] + 52), ($q[1] + 96))
    $g.DrawPath($pen, $qp2); $pen.Dispose(); $qp.Dispose(); $qp2.Dispose()
    $dd = New-Diamond ($q[0] + 30) ($q[1] + 48) 11
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 40)), 2.4
    $g.DrawPath($pen, $dd); $pen.Dispose(); $dd.Dispose()
  }
  # ── 左右口袋区：内嵌凹板 —— 把空场做成「下沉的石台」（纯暗结构，不发光）──
  #    卡位竖带 x 609..1439 不放这块，只落在左右口袋里。
  foreach ($side in @(0, 1)) {
    $sx = 208.0
    if ($side -eq 1) { $sx = $BW - 560.0 }
    $ipA = New-RoundPath $sx 246 352 660 52
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 62)), 24
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $ipA); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 104)), 3
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $ipA); $pen.Dispose()
    $ipB = New-RoundPath ($sx + 6) 252 352 660 52
    $pen = New-Object System.Drawing.Pen ((New-Col @(150,178,208) 24)), 2
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $ipB); $pen.Dispose()
    $ipC = New-RoundPath ($sx + 22) 268 308 616 44
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 34)), 2
    $g.DrawPath($pen, $ipC); $pen.Dispose()
    # 凹板内的刻线
    for ($k = 1; $k -lt 4; $k++) {
      $yy = 268 + 616 / 4.0 * $k
      $pen = New-Object System.Drawing.Pen ((New-Col $INK0 42)), 2.4
      $g.DrawLine($pen, ($sx + 22), $yy, ($sx + 330), $yy); $pen.Dispose()
    }
    for ($k = 1; $k -lt 3; $k++) {
      $xx = $sx + 22 + 308 / 3.0 * $k
      $pen = New-Object System.Drawing.Pen ((New-Col $INK0 30)), 2.2
      $g.DrawLine($pen, $xx, 268, $xx, 884); $pen.Dispose()
    }
    # 凹板四角的小三角刻记
    foreach ($cn in @(@(1,1), @(-1,1), @(1,-1), @(-1,-1))) {
      $ax = $sx + 22; if ($cn[0] -lt 0) { $ax = $sx + 330 }
      $ay = 268;     if ($cn[1] -lt 0) { $ay = 884 }
      $tp2 = New-Object System.Drawing.Drawing2D.GraphicsPath
      $tp2.AddLine($ax, ($ay + 26 * $cn[1]), ($ax + 26 * $cn[0]), ($ay + 26 * $cn[1]))
      $tp2.AddLine(($ax + 26 * $cn[0]), ($ay + 26 * $cn[1]), $ax, $ay)
      $pen = New-Object System.Drawing.Pen ((New-Col $INK0 44)), 2.6
      $g.DrawPath($pen, $tp2); $pen.Dispose(); $tp2.Dispose()
    }
    $ipA.Dispose(); $ipB.Dispose(); $ipC.Dispose()
  }
  # ── 底沿下沉 ──
  Fill-VGrad $g 0 1090 $BW 62 @(18,25,37) @(10,14,21) 0 120
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 150)), 5
  $g.DrawLine($pen, 0, 1090, $BW, 1090); $pen.Dispose()

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}



# ══════════════════════════════════════════════════════════
# L3 法阵外环   [可整体慢转：建议 +3~5 度/秒]
# ══════════════════════════════════════════════════════════
function New-LayerSigil([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $SGX; $cy = $SGY
  Stroke-Ell $g $cx $cy 430 $GOLD 4 92
  Stroke-Ell $g $cx $cy 466 $GOLD 2 36
  Stroke-Ell $g $cx $cy 414 $STEEL 2 24
  for ($i = 0; $i -lt 24; $i++) {
    $a = [Math]::PI / 12.0 * $i
    $r0 = 430.0
    $r1 = 448.0
    $ta = 56; $tw = 3
    if ($i % 2 -eq 0) { $r1 = 468.0; $ta = 96; $tw = 5 }
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $ta)), $tw
    $g.DrawLine($pen, [single]($cx + $r0*[Math]::Cos($a)), [single]($cy + $r0*[Math]::Sin($a)), [single]($cx + $r1*[Math]::Cos($a)), [single]($cy + $r1*[Math]::Sin($a)))
    $pen.Dispose()
  }
  for ($i = 0; $i -lt 12; $i++) {
    $a = [Math]::PI / 6.0 * $i + 0.26
    $rx = $cx + 500*[Math]::Cos($a); $ry = $cy + 500*[Math]::Sin($a)
    $s2 = 14.0
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 60)), 3.4
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
    $dp = New-Diamond $dx $dy 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 112)
    $g.FillPath($bs, $dp); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 190)), 3.4
    $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ══════════════════════════════════════════════════════════
# L4 法阵内环   [与 L3 反向转：建议 -6~-8 度/秒]
# ══════════════════════════════════════════════════════════
function New-LayerRune([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $SGX; $cy = $SGY
  Stroke-Ell $g $cx $cy 330 $STEEL 3 52
  Stroke-Ell $g $cx $cy 318 $STEEL 2 26
  for ($i = 0; $i -lt 16; $i++) {
    $a = [Math]::PI / 8.0 * $i
    $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 30)), 3
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
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 38)), 3
    $g.DrawPath($pen, $tp); $pen.Dispose(); $tp.Dispose()
  }
  $cp = New-Diamond $cx $cy 92
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 54)), 4
  $g.DrawPath($pen, $cp); $pen.Dispose(); $cp.Dispose()
  $cp2 = New-Diamond $cx $cy 24
  $bs = New-Object System.Drawing.SolidBrush (New-Col $ARC 92)
  $g.FillPath($bs, $cp2); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 180)), 3.4
  $g.DrawPath($pen, $cp2); $pen.Dispose(); $cp2.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ══════════════════════════════════════════════════════════
# L5 静态饰件：金框 / 角铁 / 铆钉 / 边符文 / 壁灯座   [静态]
# ══════════════════════════════════════════════════════════
function New-LayerOrnament([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $f1 = 64; $f2 = 92; $f3 = 118
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 108)), 5
  $g.DrawRectangle($pen, $f1, $f1, ($BW-$f1*2), ($BH-$f1*2)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 44)), 2
  $g.DrawRectangle($pen, $f2, $f2, ($BW-$f2*2), ($BH-$f2*2)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 78)), 2
  $g.DrawRectangle($pen, $f3, $f3, ($BW-$f3*2), ($BH-$f3*2)); $pen.Dispose()
  # 内框上的符文刻记（细密一排，读作「刻的」不是「贴的」）
  for ($x = $f3 + 40; $x -lt ($BW - $f3 - 40); $x += 74) {
    foreach ($yy in @($f3, ($BH - $f3))) {
      $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 54)), 2
      $g.DrawLine($pen, $x, ($yy - 7), ($x + 16), ($yy - 7)); $pen.Dispose()
      $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 40)), 2
      $g.DrawLine($pen, $x, ($yy + 7), ($x + 22), ($yy + 7)); $pen.Dispose()
    }
  }
  for ($y = $f3 + 40; $y -lt ($BH - $f3 - 40); $y += 74) {
    foreach ($xx in @($f3, ($BW - $f3))) {
      $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 54)), 2
      $g.DrawLine($pen, ($xx - 7), $y, ($xx - 7), ($y + 16)); $pen.Dispose()
      $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 40)), 2
      $g.DrawLine($pen, ($xx + 7), $y, ($xx + 7), ($y + 22)); $pen.Dispose()
    }
  }
  # 四角角铁
  $arm = 162; $t = 15
  foreach ($pair in @(@(0,0),@(1,0),@(0,1),@(1,1))) {
    $sx = $pair[0]; $sy = $pair[1]
    $x0 = 46
    if ($sx -ne 0) { $x0 = $BW - 46 }
    $y0 = 46
    if ($sy -ne 0) { $y0 = $BH - 46 }
    $dx = 1
    if ($sx -ne 0) { $dx = -1 }
    $dy = 1
    if ($sy -ne 0) { $dy = -1 }
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddLines([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(($x0+$dx*$arm), $y0)),
      (New-Object System.Drawing.PointF($x0, $y0)),
      (New-Object System.Drawing.PointF($x0, ($y0+$dy*$arm)))))
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 210)), ($t+11)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 208)), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 130)), 3
    $g.DrawPath($pen, $pp); $pen.Dispose()
    # 角上的菱形饰
    $dd = New-Diamond ($x0 + $dx*20) ($y0 + $dy*20) 15
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 200)
    $g.FillPath($bs, $dd); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 120)), 2
    $g.DrawPath($pen, $dd); $pen.Dispose(); $dd.Dispose()
    $pp.Dispose()
  }
  # 铆钉
  for ($x = $f1 + 200; $x -lt ($BW - $f1 - 190); $x += 184) {
    foreach ($yy in @($f1, ($BH-$f1))) {
      $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 195)
      $g.FillEllipse($bs, [single]($x-7), [single]($yy-7), 14, 14); $bs.Dispose()
      $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 140)
      $g.FillEllipse($bs, [single]($x-5), [single]($yy-5), 8, 8); $bs.Dispose()
    }
  }
  for ($y = $f1 + 200; $y -lt ($BH - $f1 - 190); $y += 184) {
    foreach ($xx in @($f1, ($BW-$f1))) {
      $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 195)
      $g.FillEllipse($bs, [single]($xx-7), [single]($y-7), 14, 14); $bs.Dispose()
      $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 140)
      $g.FillEllipse($bs, [single]($xx-5), [single]($y-5), 8, 8); $bs.Dispose()
    }
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ══════════════════════════════════════════════════════════
# L6 微光层（透明底；引擎里配 Additive 材质做呼吸/脉冲，普通混合也成立）
#     刻意压到极弱：叠在石面上只抬几档明度，不形成「亮块」
#     RGB = 想加多少，A = 强度；空处 RGB=0，Additive 下不贡献
# ══════════════════════════════════════════════════════════
function New-LayerGlow([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $SGX; $cy = $SGY
  Fill-Radial $g $cx $cy 880 520 @(30,46,74) @(0,0,0) 0.95 46 0
  Fill-Radial $g $cx $cy 430 300 @(34,54,86) @(0,0,0) 0.9 38 0
  foreach ($i in 0..4) {
    $rr = 430.0 + $i * 9
    $aa = 26 - $i * 4
    $pen = New-Object System.Drawing.Pen ((New-Col @(78,64,30) $aa)), 15
    $g.DrawEllipse($pen, [single]($cx-$rr), [single]($cy-$rr), [single]($rr*2), [single]($rr*2)); $pen.Dispose()
  }
  # 左右两侧原来那两块暖色壁灯光斑已删除（柱子一并不要了）
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ══════════════════════════════════════════════════════════
# L7 微尘层（透明；上下左右均无缝，可循环漂移）
#     数量 = $Count（烤进贴图的颗数，运行时不再增减）；运行时闪烁走 Board_Motes.mat
#     → 换 shader AnotherWorld/BoardMotesTwinkle：每颗按自己格子里的随机相位慢慢明灭，
#       同一时间只有一部分亮着（_Visible 控制比例）。改这个图仍会改变颗数与位置。
# ══════════════════════════════════════════════════════════
function New-LayerMotes([string]$out, [int]$Count = 220, [int]$Seed = 5150243) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $rng = New-Object System.Random $Seed
  for ($i = 0; $i -lt $Count; $i++) {
    $x = $rng.NextDouble() * $BW; $y = $rng.NextDouble() * $BH
    $rr = 0.8 + $rng.NextDouble() * 2.1
    $a = 18 + $rng.Next(0, 92)
    $tint = @(186,204,232)
    if ($i % 9 -eq 0) { $tint = @(224,200,132) }
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

# ══════════════════════════════════════════════════════════
# L8 前景层：暗角 + 底沿   [可做视差/镜头抖动反向位移]
# ══════════════════════════════════════════════════════════
function New-LayerForeground([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  foreach ($c in @(@(0,0,820,560), @($BW,0,820,560), @(0,$BH,820,560), @($BW,$BH,820,560))) {
    Fill-Radial $g $c[0] $c[1] $c[2] $c[3] @(0,0,0) @(0,0,0) 0.92 90 0
  }
  Fill-VGrad $g 0 0 $BW 168 @(0,0,0) @(0,0,0) 86 0
  Fill-VGrad $g 0 ($BH-190) $BW 190 @(0,0,0) @(0,0,0) 0 106
  # 底沿石台前景（压在手牌后方，做视差用）
  Fill-VGrad $g 0 1104 $BW 48 @(0,0,0) @(0,0,0) 44 152
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 220)), 4
  $g.DrawLine($pen, 0, 1104, $BW, 1104); $pen.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ══════════════════════════════════════════════════════════
# 辅助：单颗微尘贴图（给 BoardMotes.cs / ParticleSystem 当粒子贴图）
# ══════════════════════════════════════════════════════════
function New-MoteDot([string]$out, [int]$size = 64) {
  $r = New-Layer $size $size $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $c = $size / 2.0
  for ($i = 24; $i -ge 0; $i--) {
    $rr = $c * ($i / 24.0)
    $a = [int](255 * [Math]::Pow(1.0 - ($i / 24.0), 2.6))
    $br = New-Object System.Drawing.SolidBrush (New-Col @(255,255,255) $a)
    $g.FillEllipse($br, [single]($c - $rr), [single]($c - $rr), [single]($rr * 2), [single]($rr * 2))
    $br.Dispose()
  }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}
$LAYERS_V2 = @('Board_Plate','Board_Surface','Board_Sigil','Board_Rune','Board_Ornament','Board_Glow','Board_Motes','Board_Foreground')

function New-BoardLayersV2([string]$OutDir, [int]$MoteCount = 220) {
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
  $made = @()
  $made += New-LayerPlate      (Join-Path $OutDir 'Board_Plate.png')
  $made += New-LayerSurface    (Join-Path $OutDir 'Board_Surface.png')
  $made += New-LayerSigil      (Join-Path $OutDir 'Board_Sigil.png')
  $made += New-LayerRune       (Join-Path $OutDir 'Board_Rune.png')
  $made += New-LayerOrnament   (Join-Path $OutDir 'Board_Ornament.png')
  $made += New-LayerGlow       (Join-Path $OutDir 'Board_Glow.png')
  $made += New-LayerMotes      (Join-Path $OutDir 'Board_Motes.png') $MoteCount
  $made += New-LayerForeground (Join-Path $OutDir 'Board_Foreground.png')
  $made += New-MoteDot          (Join-Path $OutDir 'Board_MoteDot.png')
  return $made
}

function Load-BoardSetV2([string]$Dir) {
  return @{
    plate   = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Plate.png'))
    surface = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Surface.png'))
    sigil = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Sigil.png'))
    rune  = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Rune.png'))
    orn   = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Ornament.png'))
    glow  = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Glow.png'))
    motes = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Motes.png'))
    fore  = [System.Drawing.Image]::FromFile((Join-Path $Dir 'Board_Foreground.png'))
  }
}

function Draw-BoardLayerInto($g, $img, [int]$dw, [int]$dh, [single]$rot, [single]$alpha) {
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

function New-BoardCompositeV2([int]$dw, [int]$dh, $imgs, [single]$rotSigil, [single]$rotRune, [single]$glowA, [single]$motesA, [single]$motOffX, [single]$motOffY, [single]$foreA = 1.0, [single]$foreOffY = 0.0) {
  $b = New-Object System.Drawing.Bitmap($dw, $dh, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  Draw-BoardLayerInto $g $imgs.plate $dw $dh 0 1.0
  Draw-BoardLayerInto $g $imgs.surface $dw $dh 0 1.0
  Draw-BoardLayerInto $g $imgs.sigil $dw $dh $rotSigil 1.0
  Draw-BoardLayerInto $g $imgs.rune $dw $dh $rotRune 1.0
  # 该层是透明底、RGB=加多少，普通混合即近似 Additive
  Draw-BoardLayerInto $g $imgs.glow $dw $dh 0 $glowA
  Draw-BoardLayerInto $g $imgs.orn $dw $dh 0 1.0
  $st = $g.Save()
  $g.TranslateTransform($motOffX, $motOffY)
  Draw-BoardLayerInto $g $imgs.motes $dw $dh 0 $motesA
  $g.TranslateTransform((0 - $dw), (0 - $dh))
  Draw-BoardLayerInto $g $imgs.motes $dw $dh 0 $motesA
  $g.Restore($st)
  $st2 = $g.Save()
  $g.TranslateTransform(0, $foreOffY)
  Draw-BoardLayerInto $g $imgs.fore $dw $dh 0 $foreA
  $g.Restore($st2)
  $g.Dispose()
  return $b
}

# ══════════════════════════════════════════════════════════
# 预览：分层总览表
# ══════════════════════════════════════════════════════════
function New-BoardSheetV2([string]$Dir, [string]$Out) {
  $S = Load-BoardSetV2 $Dir
  $W = 1520; $H = 855
  $bmp = New-Object System.Drawing.Bitmap(1600, ($H + 400), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(10, 13, 19))
  $font = New-Object System.Drawing.Font('Microsoft YaHei', 13)
  $fontS = New-Object System.Drawing.Font('Microsoft YaHei', 11)
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
  $br = [System.Drawing.Brushes]::White
  $g.DrawString('合成结果（16:9 可见区 ≈ 整幅；辉光层按 0.62 透明近似 Additive）', $font, $gold, 24, 14)
  $flat = New-BoardCompositeV2 $W $H $S 0 0 1.0 1.0 0 0
  $g.DrawImage($flat, 40, 46, $W, $H); $flat.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col @(200,164,74) 110)), 2
  $g.DrawRectangle($pen, 40, 46, $W, $H); $pen.Dispose()
  # 卡位禁区标注
  $pen = New-Object System.Drawing.Pen ((New-Col @(255,120,90) 120)), 2
  $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
  $zx = 40 + 609.0 * $W / 2048.0; $zy = 46 + 92.0 * $H / 1152.0
  $zw = (1439.0 - 609.0) * $W / 2048.0; $zh = (1057.0 - 92.0) * $H / 1152.0
  $g.DrawRectangle($pen, $zx, $zy, $zw, $zh); $pen.Dispose()
  $fontX = New-Object System.Drawing.Font('Microsoft YaHei', 10)
  $g.DrawString('卡位禁区（此处只留暗细节）', $fontX, $gold, $zx + 6, $zy + 4)
  $y = $H + 92
  $g.DrawString('分层（每层 2048x1152，同画布同锚点，中心对齐）', $font, $gold, 24, $y - 30)
  $cell = 186; $x = 22
  $names = @(
    @('Board_Plate','L1 底板 整块石板'),
    @('Board_Surface','L2 板面 板缝/镶嵌/磨蚀'),
    @('Board_Sigil','L3 外环 [慢转]'),
    @('Board_Rune','L4 内环 [反转]'),
    @('Board_Ornament','L5 饰件 框/角铁/壁灯'),
    @('Board_Glow','L6 微光 [Additive]'),
    @('Board_Motes','L7 微尘 [漂移/可调]'),
    @('Board_Foreground','L8 前景 [视差]'))
  foreach ($n in $names) {
    $img = [System.Drawing.Image]::FromFile((Join-Path $Dir ($n[0] + '.png')))
    $hh = [int]($cell * $BH / $BW)
    $g.DrawImage($img, $x, $y, $cell, $hh); $img.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(70,84,110) 160)), 1
    $g.DrawRectangle($pen, $x, $y, $cell, $hh); $pen.Dispose()
    $g.DrawString($n[0], $fontS, $br, $x, ($y + $hh + 6))
    $g.DrawString($n[1], $fontS, $gold, $x, ($y + $hh + 24))
    $x += ($cell + 12)
  }
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  foreach ($k in $S.Keys) { $S[$k].Dispose() }
  return $Out
}

# ══════════════════════════════════════════════════════════
# 预览：三种动态状态（只动 L3/L4 旋转、L6 辉光、L7 微尘、L8 视差）
# ══════════════════════════════════════════════════════════
function New-BoardAnimSheetV2([string]$Dir, [string]$Out) {
  $S = Load-BoardSetV2 $Dir
  $fw = 512; $fh = 288
  $bmp = New-Object System.Drawing.Bitmap(($fw*3 + 64), ($fh + 118), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(10, 13, 19))
  $font = New-Object System.Drawing.Font('Microsoft YaHei', 12)
  $fontS = New-Object System.Drawing.Font('Microsoft YaHei', 10)
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
  $g.DrawString('动态基础验证：同一套分层，三种状态（只动 L3/L4 旋转、L6 辉光、L7 微尘、L8 视差）', $font, $gold, 20, 12)
  $frames = @(
    @(0.0, 0.0, 0.55, 0.5, 0, 0, 0, 'A 待机：环静止 / 微光最弱'),
    @(12.0, -19.0, 1.30, 0.8, 62, 24, -6, 'B 施法：正反转动 / 微光亮起'),
    @(-9.0, 24.0, 0.4, 0.35, 148, 58, 8, 'C 低潮：环回摆 / 微尘漂移 / 前景视差'))
  $x = 20
  foreach ($f in $frames) {
    $img = New-BoardCompositeV2 $fw $fh $S $f[0] $f[1] $f[2] $f[3] $f[4] $f[5] 1.0 $f[6]
    $g.DrawImage($img, $x, 42, $fw, $fh); $img.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col @(200,164,74) 120)), 2
    $g.DrawRectangle($pen, $x, 42, $fw, $fh); $pen.Dispose()
    $g.DrawString($f[7], $fontS, $gold, $x, ($fh + 50))
    $x += ($fw + 12)
  }
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  foreach ($k in $S.Keys) { $S[$k].Dispose() }
  return $Out
}

# ══════════════════════════════════════════════════════════
# 预览：实战叠卡（验证「不抢主体」）——按真实世界坐标换算
#   板面 19.2x10.8 单位 = 2048x1152 px；16:9 视口 = 整幅 x0.9375
#   列 x = 704/1024/1344 → 660/960/1260；行 y = 192/437/711/957 → 180/410/667/897
#   战场卡 1.126x2.228 单位 = 113x223；手牌卡 2.16x3.85 单位 = 216x385
# ══════════════════════════════════════════════════════════
function Draw-CardV6($g, $frame, $art, [single]$cx, [single]$cy, [single]$w, [single]$h, [bool]$shadow) {
  if ($shadow) {
    $br = New-Object System.Drawing.SolidBrush (New-Col @(0,0,0) 110)
    $g.FillEllipse($br, [single]($cx - $w*0.52), [single]($cy - $h*0.02), [single]($w*1.04), [single]($h*0.16))
    $br.Dispose()
  }
  if ($art -ne $null) {
    $ax = $cx - $w/2.0 + 0.11601 * $w
    $ay = $cy - $h/2.0 + 0.21637 * $h
    $g.DrawImage($art, $ax, $ay, (0.76797 * $w), (0.57599 * $h))
  }
  $g.DrawImage($frame, ($cx - $w/2.0), ($cy - $h/2.0), $w, $h)
}

function New-BoardMockupV2([string]$Dir, [string]$Out) {
  $S = Load-BoardSetV2 $Dir
  $W = 1920; $H = 1080
  $b = New-BoardCompositeV2 $W $H $S 6 0 0.9 1.0 40 14 1.0 0
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

  $root = Split-Path $PSScriptRoot -Parent
  $root = Split-Path $root -Parent
  $frame = [System.Drawing.Image]::FromFile((Join-Path $root 'Assets/_Game/Art/Sprites/Generated/CardFrameV6_cost3.png'))
  $artA = [System.Drawing.Image]::FromFile((Join-Path $root 'Assets/_Game/Resources/Cards/Summon/Hero/3/SummonCard_{01335}.png'))
  $artB = [System.Drawing.Image]::FromFile((Join-Path $root 'Assets/_Game/Resources/Cards/Summon/Hero/3/SummonCard_{01323}.png'))
  # 敌方两排（y=2.3 行）
  Draw-CardV6 $g $frame $artA 960 410 113 223 $true
  Draw-CardV6 $g $frame $artB 1260 410 113 223 $true
  # 手牌三张
  Draw-CardV6 $g $frame $artA 760 842 216 385 $true
  Draw-CardV6 $g $frame $artB 960 842 216 385 $true
  Draw-CardV6 $g $frame $artA 1160 842 216 385 $true
  $frame.Dispose(); $artA.Dispose(); $artB.Dispose()

  # HUD 粗摆位（只为判断背景是否抢主体）
  $uiDir = Join-Path $root 'Assets/_Game/Art/Sprites/Generated/ui'
  if (Test-Path (Join-Path $uiDir 'TopBorder.png')) {
    $tb = [System.Drawing.Image]::FromFile((Join-Path $uiDir 'TopBorder.png'))
    $g.DrawImage($tb, 0, 0, $W, 120); $tb.Dispose()
  }
  foreach ($ci in @(@('HealthCircle.png', 118, 618, 176), @('EnergyCircle.png', 118, 470, 120))) {
    $p = Join-Path $uiDir $ci[0]
    if (Test-Path $p) {
      $im = [System.Drawing.Image]::FromFile($p)
      $g.DrawImage($im, [single]($ci[1] - $ci[3]/2.0), [single]($ci[2] - $ci[3]/2.0), [single]$ci[3], [single]$ci[3])
      $im.Dispose()
    }
  }
  if (Test-Path (Join-Path $uiDir 'EndTurnPlate.png')) {
    $et = [System.Drawing.Image]::FromFile((Join-Path $uiDir 'EndTurnPlate.png'))
    $g.DrawImage($et, 1600, 640, 300, 200); $et.Dispose()
  }
  $g.Dispose()
  $b.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $b.Dispose()
  foreach ($k in $S.Keys) { $S[$k].Dispose() }
  return $Out
}
