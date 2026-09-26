# 大厅背景 v2 —— 三层（远 / 中 + 可自转环带 / 近）：给视差与「变动态」预留
#
# 与 v1 的差别（用户 2026-09-26 定）
#   ① 删「铺石缝」—— 用户判「地缝太难看」；② 删「石面暗斑」（同一批抱怨里的暗斑点）；
#   ③ 不再走「石殿」路线：远景 = 深蓝黑星野（夜空底 + 冷光池 + 两档星点 + 四角压深 + 内缩金细框），
#      近景只用「光尘 + 金线弧带」，不做栏杆 / 台阶 / 门拱 / 地面那类结构。
#   ④ 拆成四张，供视差分层与动态；每层的视差倍率见下表。
#   ⑤ 可自转环带单独一张，交给 Assets/_Game/Art/Shaders/BoardRingSpin.shader
#      （战场 L3 Board_Sigil / L4 Board_Rune 用的就是这一支）；微尘闪烁 = BoardMotesTwinkle，
#      金线脉冲 = BoardTrimPulse。三支都是「一层一张全屏贴图」口径，不用改着色器。
#
# 分层与视差倍率（ParallaxMax = 0.030 画布宽 ≈ 58px @1920）
#   Bg_Far      ×0.25   夜空底 / 冷光池 / 星点 / 四角压深 / 内缩金细框   ← 只有渐变与直线，拉伸不变形
#   Ring_*      ×0.55   左侧星环：环骨架 + 12 颗点 + 点亮辉光（三张，点与辉光出成近白靠 tint 上色）
#   Star_Dipper ×0.55   左侧北斗七星 —— **2026-09-26 五次定：用户「不要北斗了」，已停用**（$DIPPER_ON = $false）
#   Bg_Near     ×1.00   光尘 + 金线弧带（四角留空，避开 UI 热点）
#
# 2026-09-26 二次定（用户）：
#   ① **删掉中央大圆环 + 六芒星** —— 不要和战斗场景撞同一套母题（当时中央一律留纯星野）；
#   ② 星象改到**左侧**，顶点将来「一个个亮起」→ 所以点 / 辉光**出成近白**，
#      点亮只改 tint（或换辉光叠加），不重出图；线稿与点分开，亮一颗不影响其它。
#   ③ 七芒星 → **北斗七星**（用户 2026-09-26：「其实我是想要北斗七星形状」）。
#
# 2026-09-26 四次定（用户）：「算了还是要圆环吧，不过不能和战斗场景的完全一样，要能够独立亮起部分点」
#   → 星环**回归**；与战场环同源不同形（双细环 / 24 根三档刻度 / 12 颗点 / 中央留空），
#      12 颗点各自独立可亮。差异表与点亮口径见下面「中央星环」一节。
#
# 2026-09-26 五次定（用户）：「不要北斗了，正中央的圆环移到左侧不被遮挡，同时大小缩小一点」
#   ① 北斗停用（$DIPPER_ON = $false）—— 代码与参数**留在脚本里**，改一个开关就能恢复；
#   ② 环心 (960, 540) → **(560, 540)**：右边缘 560 + 350 = 910 < UI 热点最左 1099，**不再被入口板压住**；
#   ③ 半径 410 → **350**（内环 378 → 318、轨道环 448 → 388）。
#
# 视差溢出：层要位移就得比屏幕大。这里远景 / 近景按 1920×1080 的 1.08 倍出图（2074×1166），
# 场景里把容器放大 1.08 即可 —— 位移 58px ≈ 屏宽 3%，四边各留 4% 够用。
#
# 产物
#   Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/Bg_Far.png             2074x1166  不透明
#   Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/Bg_Near.png            2074x1166  透明
#   Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/Ring_Base.png           1584x1584  透明（环骨架，中央留空）
#   （北斗三张 Star_Dipper* 已停用，不再生成；要恢复把 $DIPPER_ON 改成 $true）
#   Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/Ring_Node.png           104x104    透明（近白，可 tint）
#   Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/Ring_NodeGlow.png       160x160    透明（近白辉光，可 tint）
#   Tools/cardframe/preview/lobby-bg-v2-layers.png   分层分列 + 口径
#   Tools/cardframe/preview/lobby-bg-v2-mockup.png    1920x1080 合成 + UI 热点虚线（环全暗）
#   Tools/cardframe/preview/lobby-bg-v2-mockup-lit.png 同上，环 5/12 已亮
#   Tools/cardframe/preview/lobby-bg-v2-mockup-ui.png  同上，再铺 UI 热点遮挡色块
#
# 回退：删掉 Generated/lobby-bg-v2 目录即可，本脚本不改任何现有文件（v1 与场景都没碰）。
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/BoardLayersV2.ps1"

$ROOT = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-bg-v2'
$PREV = Join-Path $PSScriptRoot 'preview'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 可调参数 ────────────────────────────────────────────────
$OV            = 1.08               # 视差溢出倍率
$FAR_W         = 2074               # 1920 × $OV
$FAR_H         = 1166               # 1080 × $OV
$EMBLEM_BOX    = 1200
$EMBLEM_SCREEN = 1020
$MOCK_W        = 1920
$MOCK_H        = 1080
$ROT_RING      = 12                 # 预览里环带转过的角度
$FRAME_INSET   = [int](46 * $OV)
$FRAME_ALPHA   = 46
$STAR_COUNT    = 210                # 暗档星点（2026-09-26 六次定：82 → 210，且改成铺满整屏）
$STAR_A        = @(9, 36)
$STAR2_COUNT   = 30                 # 亮档星点（带极弱晕）
$STAR3_COUNT   = 10                 # 最亮档（晕更大、芯近白）

# 银河带（2026-09-26 六次定，治「太空」）：一条斜穿画面的极淡冷色带，两端跑到画外靠四角压深自然淡出
$MW_ON         = $true
$MW_X0         = -240.0             # 带中心线（贴图 px）：左下角外 -> 右上角外
$MW_Y0         = 1330.0
$MW_X1         = 2320.0
$MW_Y1         = -180.0
$MW_HALF       = 300.0              # 带半宽（贴图 px）
$MW_A          = 20                 # 带心峰值 alpha
$MW_C          = @(150, 178, 216)   # 带色（冷蓝白）
$MW_STAR_SHARE = 0.42               # 星点里落在带内的比例（带内更密）
$POOL_A        = 118                # 中央冷光池（战场 150）
$VIGNETTE_A    = 104                # 四角压深
$HEX_ALPHA     = 38                 # 六芒星（战场 L4 同值）
$NEAR_DUST     = 150                # 近景光尘颗数
$NEAR_ZONE_PAD = 30                 # UI 热点外扩（屏 px）

# 左侧北斗七星（2026-09-26 三次定）：位置 / 大小是屏口径，改这里重出图；场景里再按需微调
$DIPPER_ON = $false                 # 2026-09-26 五次定：用户「不要北斗了」—— 停用只影响生成与合成，代码/参数全留着
$DIP_CX   = 300                     # 星象中心 x（1920 口径）
$DIP_CY   = 560                     # 星象中心 y（1080 口径）
$DIP_W    = 400                     # 星象宽（屏 px，斗柄方向）—— 缩到 400 让开中央星环（斗柄末星到环心 477 > 环半径 410 + 67 余量）
$DIP_ROT  = -8                      # 整体倾角（度，正 = 顺时针；只影响预览摆位，贴图里不烤）
$DIP_LW   = 6                       # 连线线宽（贴图 px；3 倍出图 → 2 屏幕 px）
$DIP_S    = 3                       # 出图倍率（与 lobby-ui-v1 同一档 3 倍）
$DIP_STAR_R = 26                    # 星点菱形半径（贴图 px → 8.7 屏幕 px）
$DIP_PAD  = 48                      # 线稿四周留白（贴图 px，要装得下星点）
$DIP_UNIT = 0.0                     # 画布宽 / 星象宽 的比例换算用不到，留 0

# 中央星环（2026-09-26 四次定）—— 位置 / 尺寸都是屏口径；场景里是独立一层，可整体挪
$RING_CX      = 560                 # 环心 x（1920 口径）—— 2026-09-26 五次定：挪到左侧，右边缘 560+350 < UI 热点最左 1099
$RING_CY      = 540                 # 环心 y（1080 口径）
$RING_R       = 350                 # 外环半径（屏 px）—— 410 → 350
$RING_R2      = 318                 # 内环半径（双细环；战场是一条环带）
$RING_R3      = 388                 # 环外极淡轨道环（战场的光晕在环内侧、更亮）
$RING_TICK    = 36                  # 刻度槽位总数（每 10°；战场 24 根等长）
$RING_NODE    = 12                  # 可独立点亮的点数（每 30° 一颗；战场是 4 颗铆钉）
$RING_A0      = -90                 # 槽位 0 的角度（度，-90 = 正上方；顺时针为正）
$RING_NODE_R  = 17                  # 一颗点的菱形半径（屏 px）
$RING_NODE_AT = 52                  # 一颗点在合成图里的显示边长（屏 px）= 贴图 104 / 2
$RING_GLOW_AT = 112                 # 点亮辉光在合成图里的显示边长（屏 px）
$RING_S       = 2                   # 出图倍率（贴图 1584² < 2048 上限，不会被降采样）
$RING_PAD     = 46                  # 四周留白（屏 px，装得下轨道环 + 线宽）
$RING_LW_R    = 3.0                 # 外环线宽（屏 px）
$RING_LW_R2   = 2.0                 # 内环
$RING_LW_T1   = 2.6                 # 长刻度（4 根，跨过双环）
$RING_LW_T2   = 2.0                 # 中刻度（8 根，夹在双环之间）
$RING_LW_T3   = 1.6                 # 短刻度（12 根，只挂在外环内侧）
$RING_LW_OR   = 1.4                 # 轨道环
$RING_T1_IN   = 46                  # 长刻度向内长出
$RING_T1_OUT  = 22                  # 长刻度向外长出
$RING_T3_IN   = 14                  # 短刻度向内长出

# UI 热点（1920×1080 屏口径）：近景的光尘与弧带要避开这些框
$UI_ZONES = @(
  @(0, 0, 467, 96),                 # 左上头像板 Plate_Profile
  @(1382, 0, 538, 95),              # 右上横栏 Plate_TopBand
  @(1125, 265, 682, 170),           # 入口板 战斗
  @(1099, 455, 682, 170),           # 入口板 卡牌总览
  @(1133, 668, 666, 145)            # 下排透明大框（房间 / 其它）
)

function In-UiZone([single]$sx, [single]$sy) {
  foreach ($z in $UI_ZONES) {
    if ($sx -ge ($z[0] - $NEAR_ZONE_PAD) -and $sx -le ($z[0] + $z[2] + $NEAR_ZONE_PAD) -and
        $sy -ge ($z[1] - $NEAR_ZONE_PAD) -and $sy -le ($z[1] + $z[3] + $NEAR_ZONE_PAD)) { return $true }
  }
  return $false
}

# 三点平滑曲线：起终点 + 中段往法线推 $sag（正 = 往左手法线方向鼓）
function New-ShallowArc([single]$x0, [single]$y0, [single]$x1, [single]$y1, [single]$sag) {
  $mx = ($x0 + $x1) / 2.0; $my = ($y0 + $y1) / 2.0
  $dx = $x1 - $x0; $dy = $y1 - $y0
  $len = [Math]::Sqrt($dx * $dx + $dy * $dy)
  if ($len -lt 0.001) { $len = 1.0 }
  $px = $mx + (-$dy / $len) * $sag
  $py = $my + ($dx / $len) * $sag
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddCurve([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($x0, $y0)),
    (New-Object System.Drawing.PointF($px, $py)),
    (New-Object System.Drawing.PointF($x1, $y1))))
  return $p
}

# 正确的径向发光：同色多圈叠加，alpha 按 (1-r)^power 逐圈解出来，中心不会变暗、边缘无暗环
# （BoardLayersV2.ps1 的 Fill-Radial 走 GDI+ PathGradientBrush，实测中心不亮、外圈发暗，这里不用它）
function Fill-BgGlow($g, [single]$cx, [single]$cy, [single]$rx, [single]$ry, [int[]]$col, [int]$aMax, [single]$power = 1.6, [int]$steps = 64) {
  $prev = 0.0
  for ($k = 2; $k -le $steps; $k++) {
    $rf = 1.0 - ($k - 1) / [double]$steps
    $target = $aMax * [Math]::Pow(1.0 - $rf, $power)
    if ($target -le 0.0) { continue }
    $a = (1.0 - (1.0 - $target / 255.0) / (1.0 - $prev / 255.0)) * 255.0
    $prev = $target
    if ($a -lt 1.0) { continue }
    $br = New-Object System.Drawing.SolidBrush (New-Col $col ([int][Math]::Round($a)))
    $g.FillEllipse($br, [single]($cx - $rx * $rf), [single]($cy - $ry * $rf), [single]($rx * $rf * 2), [single]($ry * $rf * 2))
    $br.Dispose()
  }
}
# 沿一条直线的「带」状柔光：横截面走 LinearGradientBrush 透明→峰值→透明。
# 不能像星点那样叠几十个 Fill-BgGlow —— 叠加会把带心烧成一块实色（alpha 会复利）。
function Fill-BandGlow($g, [single]$x0, [single]$y0, [single]$x1, [single]$y1, [single]$halfW, [int[]]$col, [int]$aMax) {
  $dx = $x1 - $x0; $dy = $y1 - $y0
  $len = [Math]::Sqrt($dx * $dx + $dy * $dy)
  if ($len -lt 1.0) { return }
  $nx = -$dy / $len; $ny = $dx / $len
  $pA = New-Object System.Drawing.PointF(($x0 - $nx * $halfW), ($y0 - $ny * $halfW))
  $pB = New-Object System.Drawing.PointF(($x0 + $nx * $halfW), ($y0 + $ny * $halfW))
  $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush($pA, $pB, (New-Col $col 0), (New-Col $col $aMax))
  $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
  $blend.Colors = @((New-Col $col 0), (New-Col $col $aMax), (New-Col $col 0))
  $blend.Positions = @(0.0, 0.5, 1.0)
  $br.InterpolationColors = $blend
  $pts = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(($x0 - $nx * $halfW), ($y0 - $ny * $halfW))),
    (New-Object System.Drawing.PointF(($x1 - $nx * $halfW), ($y1 - $ny * $halfW))),
    (New-Object System.Drawing.PointF(($x1 + $nx * $halfW), ($y1 + $ny * $halfW))),
    (New-Object System.Drawing.PointF(($x0 + $nx * $halfW), ($y0 + $ny * $halfW))))
  $g.FillPolygon($br, $pts)
  $br.Dispose()
}

# 星点落点：$MW_STAR_SHARE 的比例落在银河带内（三个均匀量相加 = 越靠带心越密的钟形分布），其余全屏均匀。
# 注意 y 是**全高** —— 旧版按 0.72/0.62 只撒画面上半部分，下半屏一颗星都没有，这就是「太空」的主因。
function New-StarPoint($rng) {
  if ($MW_ON -and $rng.NextDouble() -lt $MW_STAR_SHARE) {
    $dx = $MW_X1 - $MW_X0; $dy = $MW_Y1 - $MW_Y0
    $len = [Math]::Sqrt($dx * $dx + $dy * $dy)
    $t = $rng.NextDouble() * 1.16 - 0.08
    $px = $MW_X0 + $dx * $t; $py = $MW_Y0 + $dy * $t
    $o = (($rng.NextDouble() + $rng.NextDouble() + $rng.NextDouble()) - 1.5) / 1.5 * $MW_HALF * 1.35
    return @(($px - $dy / $len * $o), ($py + $dx / $len * $o))
  }
  return @($rng.Next(0, $FAR_W), $rng.Next(0, $FAR_H))
}

# ── 远景：夜空底 + 冷光池 + 两档星点 + 四角压深 + 内缩金细框（无铺石缝 / 无暗斑）──
function New-BgFar([string]$out) {
  $r = New-Layer $FAR_W $FAR_H $true $SKY_T; $bmp = $r[0]; $g = $r[1]
  $cx = $FAR_W / 2.0; $cy = $FAR_H / 2.0

  Fill-VGrad $g 0 0 $FAR_W $FAR_H $SKY_T $SKY_B
  Fill-BgGlow $g $cx ($cy + (40 * $OV)) (1560 * $OV) (860 * $OV) $GLOW_C $POOL_A 1.5 64

  # 银河带：一条宽的淡雾 + 一条偏上、更窄更亮的核心带（错开一点，免得像一条直尺）
  if ($MW_ON) {
    Fill-BandGlow $g $MW_X0 $MW_Y0 $MW_X1 $MW_Y1 $MW_HALF $MW_C $MW_A
    Fill-BandGlow $g ($MW_X0 + 170) ($MW_Y0 - 300) ($MW_X1 + 170) ($MW_Y1 - 300) ($MW_HALF * 0.42) $MW_C ([int][Math]::Round($MW_A * 0.75))
  }

  $rng = New-Object System.Random 20260927
  for ($i = 0; $i -lt $STAR_COUNT; $i++) {
    $p = New-StarPoint $rng
    $rr = 1 + $rng.NextDouble() * 1.8
    $a = $STAR_A[0] + $rng.Next(0, ($STAR_A[1] - $STAR_A[0]))
    $br = New-Object System.Drawing.SolidBrush (New-Col @(210, 224, 244) $a)
    $g.FillEllipse($br, [single]($p[0] - $rr), [single]($p[1] - $rr), [single]($rr * 2), [single]($rr * 2)); $br.Dispose()
  }
  for ($i = 0; $i -lt $STAR2_COUNT; $i++) {
    $p = New-StarPoint $rng
    $rr = 1.8 + $rng.NextDouble() * 1.6
    Fill-BgGlow $g $p[0] $p[1] ($rr * 7) ($rr * 7) @(208, 224, 248) 22 2.0 20
    $br = New-Object System.Drawing.SolidBrush (New-Col @(232, 240, 255) 92)
    $g.FillEllipse($br, [single]($p[0] - $rr), [single]($p[1] - $rr), [single]($rr * 2), [single]($rr * 2)); $br.Dispose()
  }
  for ($i = 0; $i -lt $STAR3_COUNT; $i++) {
    $p = New-StarPoint $rng
    $rr = 2.6 + $rng.NextDouble() * 1.4
    Fill-BgGlow $g $p[0] $p[1] ($rr * 12) ($rr * 12) @(206, 222, 250) 30 1.9 24
    $br = New-Object System.Drawing.SolidBrush (New-Col @(246, 250, 255) 128)
    $g.FillEllipse($br, [single]($p[0] - $rr), [single]($p[1] - $rr), [single]($rr * 2), [single]($rr * 2)); $br.Dispose()
  }

  foreach ($c in @(@(0, 0), @($FAR_W, 0), @(0, $FAR_H), @($FAR_W, $FAR_H))) {
    Fill-BgGlow $g $c[0] $c[1] (760 * $OV) (520 * $OV) $INK0 $VIGNETTE_A 1.4 48
  }

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $FRAME_ALPHA)), 2
  $g.DrawRectangle($pen, $FRAME_INSET, $FRAME_INSET, ($FAR_W - $FRAME_INSET * 2), ($FAR_H - $FRAME_INSET * 2)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D 30)), 2
  $g.DrawRectangle($pen, ($FRAME_INSET + 7), ($FRAME_INSET + 7), ($FAR_W - ($FRAME_INSET + 7) * 2), ($FAR_H - ($FRAME_INSET + 7) * 2)); $pen.Dispose()

  foreach ($q in @(@(($FRAME_INSET + 30), ($FRAME_INSET + 30)),
                   @(($FAR_W - $FRAME_INSET - 30), ($FRAME_INSET + 30)),
                   @(($FRAME_INSET + 30), ($FAR_H - $FRAME_INSET - 30)),
                   @(($FAR_W - $FRAME_INSET - 30), ($FAR_H - $FRAME_INSET - 30)))) {
    $dp = New-Diamond $q[0] $q[1] 13
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 96)
    $g.FillPath($bs, $dp); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 190)), 2.4
    $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  }

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

# ── 近景：光尘 + 金线弧带（无结构、无地面；四角留空，避开 UI 热点）──
function New-BgNear([string]$out) {
  $r = New-Layer $FAR_W $FAR_H $false @(0, 0, 0); $bmp = $r[0]; $g = $r[1]

  # 底部冷光晕：让下沿「有底」，不是一片纯黑（2026-09-26 六次定 900x430/a26 -> 1240x580/a48）
  Fill-BgGlow $g ($FAR_W / 2.0) ($FAR_H + 30) 1240 580 $GLOW_C 48 1.4 48

  $arcs = @(
    @(30, 980, 900, 1150, -34, 38, 'gold'),
    @(2044, 980, 1174, 1150, 34, 38, 'gold'),
    @(700, -30, 1300, 58, 26, 28, 'gold'),
    @(60, 1010, 880, 1170, -44, 20, 'steel'),
    @(2014, 1010, 1194, 1170, 44, 20, 'steel'))
  foreach ($aa in $arcs) {
    $path = New-ShallowArc $aa[0] $aa[1] $aa[2] $aa[3] $aa[4]
    if ($aa[6] -eq 'steel') { $col = New-Col $STEEL $aa[5] } else { $col = New-Col $GOLD $aa[5] }
    $pen = New-Object System.Drawing.Pen ($col), 2
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path); $pen.Dispose(); $path.Dispose()
  }

  $rng = New-Object System.Random 20260928
  $placed = 0; $guard = 0
  while ($placed -lt $NEAR_DUST -and $guard -lt 8000) {
    $guard++
    $x = $rng.Next(0, $FAR_W); $y = $rng.Next(0, $FAR_H)
    if (In-UiZone ($x / $OV) ($y / $OV)) { continue }
    $rr = 0.8 + $rng.NextDouble() * 1.0
    $a  = 14 + $rng.Next(0, 22)
    if ($rng.NextDouble() -lt 0.34) { $rr = 1.6 + $rng.NextDouble() * 1.8; $a = 34 + $rng.Next(0, 46) }
    $col = @(224, 234, 248)
    if ($rng.Next(0, 4) -eq 0) { $col = $GOLD_L }
    $br = New-Object System.Drawing.SolidBrush (New-Col $col $a)
    $g.FillEllipse($br, [single]($x - $rr), [single]($y - $rr), [single]($rr * 2), [single]($rr * 2)); $br.Dispose()
    $placed++
  }

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}
# ── 左侧北斗七星（斗 + 柄）──────────────────────────────────
# 星位是**真实赤经赤纬投影**（x = RA° × cos(dec)，y = Dec，J2000），不是手摆的：
#   天枢 α Dubhe  ( 0.27, 12.44)   斗口上沿；天枢 / 天璇 = 指极星
#   天璇 β Merak  ( 0.00,  7.07)   斗口下沿
#   天玑 γ Phecda ( 7.46,  4.38)   斗底
#   天权 δ Megrez (10.56,  7.72)   斗与柄的交接
#   玉衡 ε Alioth (16.10,  6.65)
#   开阳 ζ Mizar  (20.34,  5.62)
#   摇光 η Alkaid (23.74,  0.00)   斗柄末端
# 连线：斗闭合 α-β-γ-δ-α，柄 δ-ε-ζ-η（共 7 段）
# 归一化坐标 x 向右、y 向上；画进贴图时 y 翻转
$DIP_NORM  = @(
  @(0.27, 12.44), @(0.00, 7.07), @(7.46, 4.38), @(10.56, 7.72),
  @(16.10, 6.65), @(20.34, 5.62), @(23.74, 0.00))
$DIP_NAMES = @('天枢', '天璇', '天玑', '天权', '玉衡', '开阳', '摇光')
$DIP_SEG   = @(@(0, 1), @(1, 2), @(2, 3), @(3, 0), @(3, 4), @(4, 5), @(5, 6))
$DIP_SPANX = 23.74
$DIP_SPANY = 12.44

# 归一化包围盒 [x, y, w]（屏幕口径）→ 7 个星点的屏幕坐标（y 轴翻转）
function Get-DipLayout([single]$x, [single]$y, [single]$w) {
  $h = $w * $DIP_SPANY / $DIP_SPANX
  $pts = @()
  foreach ($q in $DIP_NORM) {
    $pts += , @(($x + $q[0] / $DIP_SPANX * $w), ($y + ($DIP_SPANY - $q[1]) / $DIP_SPANY * $h))
  }
  return @{ w = $w; h = $h; pts = $pts }
}

# 绕 (ccx,ccy) 转 $deg 度（正 = 顺时针，屏幕 y 向下）
function Rot-Pt([single]$x, [single]$y, [single]$ccx, [single]$ccy, [single]$deg) {
  $r = $deg * [Math]::PI / 180.0
  $dx = $x - $ccx; $dy = $y - $ccy
  return @(($ccx + $dx * [Math]::Cos($r) - $dy * [Math]::Sin($r)),
           ($ccy + $dx * [Math]::Sin($r) + $dy * [Math]::Cos($r)))
}

# 三张：连线稿（最亮档金线）/ 星点（近白，tint 上色）/ 点亮辉光（近白）
function New-DipperSet([string]$dir) {
  $scale = [single]($DIP_W * $DIP_S / $DIP_SPANX)
  $bw = [int]($DIP_SPANX * $scale + 2 * $DIP_PAD)
  $bh = [int]($DIP_SPANY * $scale + 2 * $DIP_PAD)
  $pt = @()
  foreach ($q in $DIP_NORM) {
    $pt += , @(($DIP_PAD + $q[0] * $scale), ($bh - $DIP_PAD - $q[1] * $scale))
  }
  $made = @()

  # ① 连线稿
  $lay = New-Layer $bw $bh $false @(0, 0, 0); $bmp = $lay[0]; $g = $lay[1]
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 236)), $DIP_LW
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  foreach ($sg in $DIP_SEG) {
    $g.DrawLine($pen, [single]$pt[$sg[0]][0], [single]$pt[$sg[0]][1], [single]$pt[$sg[1]][0], [single]$pt[$sg[1]][1])
  }
  $pen.Dispose()
  $g.Dispose()
  $bmp.Save((Join-Path $dir 'Star_Dipper.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $made += (Join-Path $dir 'Star_Dipper.png')

  # ② 星点（近白）：外菱形 + 内芯 —— 一个星点一份，各自可 tint / 点亮
  $vb = $DIP_STAR_R * 2 + 36
  $lay = New-Layer $vb $vb $false @(0, 0, 0); $bmp = $lay[0]; $g = $lay[1]
  $vc = $vb / 2.0
  $dp = New-Diamond $vc $vc $DIP_STAR_R
  $pen = New-Object System.Drawing.Pen ((New-Col @(230, 234, 242) 225)), 6
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  $dp2 = New-Diamond $vc $vc ($DIP_STAR_R * 0.34)
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(240, 244, 252) 255)
  $g.FillPath($bs, $dp2); $bs.Dispose(); $dp2.Dispose()
  $g.Dispose()
  $bmp.Save((Join-Path $dir 'Star_DipperStar.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $made += (Join-Path $dir 'Star_DipperStar.png')

  # ③ 点亮辉光（近白，径向柔光）：亮态 = 辉光 + 星点
  $gb = 160
  $lay = New-Layer $gb $gb $false @(0, 0, 0); $bmp = $lay[0]; $g = $lay[1]
  Fill-BgGlow $g ($gb / 2.0) ($gb / 2.0) 76 76 @(244, 246, 252) 210 2.2 40
  $g.Dispose()
  $bmp.Save((Join-Path $dir 'Star_DipperGlow.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $made += (Join-Path $dir 'Star_DipperGlow.png')

  return $made
}

# ── 中央星环（罗盘环）：与战场环同源不同形 ───────────────────
# 用户 2026-09-26 四次定：「算了还是要圆环吧，不过不能和战斗场景的完全一样，要能够独立亮起部分点」。
#
# 战场环（Assets/_Game/Art/Sprites/Generated/board-layers-v2/Board_Sigil.png + Board_Rune.png）实测半径：
#   L3 主环带 412–418 + 24 根等长刻度 + 4 颗菱形铆钉 464–471 + 8 个外侧空心三角 477–521 + 环内一圈钢色亮线
#   L4 蓝环 292–336 + 六芒星 50–252 + 中央金菱形 0–31 + 蓝芯 24
#   一句话：**单条环带 + 等长刻度 + 环外三角 + 中央星形**。
#
# 大厅环的差异（每一条都是「同源不同形」：不换色、不换线法、不换手法）：
#   ① 环 —— **两条独立细金环**（R430 / R398），不是一条夹在双线里的环带
#   ② 刻度 —— **36 个槽位**分三档：长 4 根（跨过双环，落正上下左右）/ 中 8 根（夹在双环之间）
#      / 短 12 根（只挂在外环内侧）；战场是 24 根等长
#   ③ 环外标记 —— **不用三角**；改成 **12 颗菱形点**，且专走**空槽**（12 个槽位不画刻度），
#      所以点永远不压刻度
#   ④ 环外 R468 一圈**极淡钢色轨道环**（战场的光晕在环内侧、且更亮）
#   ⑤ **中央留空** —— 不放六芒星 / 七芒星，星野直接透出来（战场中央是六芒星 + 蓝芯）
#
# 「独立亮起部分点」怎么落地：
#   12 颗点 = 12 个独立槽位（$RING_A0 + i×10°，i % 3 == 1 → -80 / -50 / -20 / 10 / … / 250，顺时针）。
#   点的贴图与辉光**出成近白**（与北斗同一套口径），场景里**每颗一个实例、运行时只改 color**：
#     暗 = tint (0.40,0.33,0.16) α0.85；亮 = tint (0.91,0.82,0.54) α1.0 + 叠一张 Ring_NodeGlow
#   → 想亮几颗亮几颗、想亮哪颗亮哪颗，不重出图；槽位坐标见 Get-RingNodePoints。
function Get-RingNodePoints([single]$cx, [single]$cy, [single]$rr) {
  $pts = @()
  $step = 360.0 / $RING_TICK
  for ($i = 0; $i -lt $RING_TICK; $i++) {
    if (($i % 3) -ne 1) { continue }          # 只有 i % 3 == 1 的槽位留给点
    $th = ($RING_A0 + $i * $step) * [Math]::PI / 180.0
    $pts += , @([single]($cx + $rr * [Math]::Cos($th)), [single]($cy + $rr * [Math]::Sin($th)))
  }
  return $pts
}

# 一张层的「屏 px 显示边长」：贴图边长 = ($RING_R + $RING_PAD) × 2 × $RING_S，除以 $RING_S 后正好是 (R + PAD) × 2。
# 预览与场景都用这一个口径，别再手算（一次踩过：漏乘 / 多除 $RING_S 导致环只有一半大、点跑到环外）。
function Get-RingScreenSide() { return (($RING_R + $RING_PAD) * 2) }

# 一根刻度：从半径 $r0 画到 $r1（屏 px），角度 $deg；函数自己乘 $RING_S 并加中心偏移
function New-RingTickPath([single]$cx, [single]$cy, [single]$r0, [single]$r1, [single]$deg) {
  $th = $deg * [Math]::PI / 180.0
  return @(
    [single]($cx + $r0 * $RING_S * [Math]::Cos($th)), [single]($cy + $r0 * $RING_S * [Math]::Sin($th)),
    [single]($cx + $r1 * $RING_S * [Math]::Cos($th)), [single]($cy + $r1 * $RING_S * [Math]::Sin($th)))
}

# 三张：环骨架（双细环 + 三档刻度 + 轨道环）/ 一颗点（近白，tint 上色）/ 点亮辉光（近白）
function New-RingSet([string]$dir) {
  $half = [int](($RING_R + $RING_PAD) * $RING_S)      # 488 × 2 = 976（贴图半边）
  $side = $half * 2
  $cx = [single]$half; $cy = [single]$half
  $made = @()

  # ① 环骨架：轨道环 + 内环 + 外环 + 24 根三档刻度（中央留空）
  $lay = New-Layer $side $side $false @(0, 0, 0); $bmp = $lay[0]; $g = $lay[1]
  $ringDefs = @()
  $ringDefs += , @($RING_R3, $STEEL, 26,  $RING_LW_OR)      # 轨道环（最外，极淡）
  $ringDefs += , @($RING_R2, $GOLD,  140, $RING_LW_R2)      # 内环
  $ringDefs += , @($RING_R,  $GOLD,  210, $RING_LW_R)       # 外环
  foreach ($e in $ringDefs) {
    $rr = [single]($e[0] * $RING_S)
    $pen = New-Object System.Drawing.Pen ((New-Col ([int[]]$e[1]) $e[2])), ([single]($e[3] * $RING_S))
    $g.DrawEllipse($pen, [single]($cx - $rr), [single]($cy - $rr), [single]($rr * 2), [single]($rr * 2))
    $pen.Dispose()
  }
  $step = 360.0 / $RING_TICK
  for ($i = 0; $i -lt $RING_TICK; $i++) {
    if (($i % 3) -eq 1) { continue }                   # 空槽 → 留给点
    if     (($i % 9) -eq 0) { $r0 = $RING_R - $RING_T1_IN; $r1 = $RING_R + $RING_T1_OUT; $col = $GOLD_L; $a = 195; $lw = $RING_LW_T1 }
    elseif (($i % 3) -eq 0) { $r0 = $RING_R2;             $r1 = $RING_R;                $col = $GOLD;   $a = 150; $lw = $RING_LW_T2 }
    else                    { $r0 = $RING_R - $RING_T3_IN; $r1 = $RING_R;                $col = $GOLD;   $a = 105; $lw = $RING_LW_T3 }
    $q = New-RingTickPath $cx $cy $r0 $r1 ($RING_A0 + $i * $step)
    $pen = New-Object System.Drawing.Pen ((New-Col ([int[]]$col) $a)), ([single]($lw * $RING_S))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Flat
    $g.DrawLine($pen, $q[0], $q[1], $q[2], $q[3]); $pen.Dispose()
  }
  $g.Dispose()
  $bmp.Save((Join-Path $dir 'Ring_Base.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $made += (Join-Path $dir 'Ring_Base.png')

  # ② 一颗点（近白，可 tint）：外菱形描边 + 内芯 —— 12 颗各一份实例，亮哪颗只改这颗的 color
  $rb = [int]($RING_NODE_R * $RING_S * 2 + 36)
  $lay = New-Layer $rb $rb $false @(0, 0, 0); $bmp = $lay[0]; $g = $lay[1]
  $vc = $rb / 2.0
  $dp = New-Diamond $vc $vc ([single]($RING_NODE_R * $RING_S))
  $pen = New-Object System.Drawing.Pen ((New-Col @(230, 234, 242) 225)), ([single](5 * $RING_S))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  $dp2 = New-Diamond $vc $vc ([single]($RING_NODE_R * $RING_S * 0.34))
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(240, 244, 252) 255)
  $g.FillPath($bs, $dp2); $bs.Dispose(); $dp2.Dispose()
  $g.Dispose()
  $bmp.Save((Join-Path $dir 'Ring_Node.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $made += (Join-Path $dir 'Ring_Node.png')

  # ③ 点亮辉光（近白，径向柔光）：亮态 = 辉光 + 点
  $gb = 160
  $lay = New-Layer $gb $gb $false @(0, 0, 0); $bmp = $lay[0]; $g = $lay[1]
  Fill-BgGlow $g ($gb / 2.0) ($gb / 2.0) 76 76 @(244, 246, 252) 210 2.2 40
  $g.Dispose()
  $bmp.Save((Join-Path $dir 'Ring_NodeGlow.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $made += (Join-Path $dir 'Ring_NodeGlow.png')

  return $made
}

# 把 12 颗点画在环上：$litCount 颗亮（顺时针，从正上方偏右 10° 起），其余暗
function Draw-RingNodes($g, $node, $glow, [single]$cx, [single]$cy, [int]$litCount, [single]$size, [single]$glowSize) {
  $pts = Get-RingNodePoints $cx $cy $RING_R
  $half = $size / 2.0
  $gh = $glowSize / 2.0
  for ($i = 0; $i -lt $pts.Count; $i++) {
    if ($i -lt $litCount) {
      Draw-BgTint $g $glow ($pts[$i][0] - $gh) ($pts[$i][1] - $gh) $glowSize $glowSize 0.91 0.82 0.54 0.62
      Draw-BgTint $g $node ($pts[$i][0] - $half) ($pts[$i][1] - $half) $size $size 0.91 0.82 0.54 1.0
    } else {
      Draw-BgTint $g $node ($pts[$i][0] - $half) ($pts[$i][1] - $half) $size $size 0.40 0.33 0.16 0.85
    }
  }
}

# ── 预览用的小工具 ──────────────────────────────────────────
$script:PFC = $null
function Get-BgFont([single]$px) {
  if ($script:PFC -eq $null) {
    $script:PFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PFC.AddFontFile((Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'))
  }
  return New-Object System.Drawing.Font($script:PFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
function Put-BgText($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 236) {
  Put-BgTextCol $g $s $x $y $px $a 240 232 210
}
function Put-BgTextCol($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a, [int]$r, [int]$gg, [int]$bb) {
  $f = Get-BgFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $r, $gg, $bb))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function Draw-BgImg($g, $img, [single]$x, [single]$y, [single]$w, [single]$h, [single]$alpha = 1.0) {
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  if ($alpha -lt 0.999) {
    $cm = New-Object System.Drawing.Imaging.ColorMatrix; $cm.Matrix33 = $alpha; $ia.SetColorMatrix($cm)
  }
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]$x, [int]$y, [int]$w, [int]$h)), 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
}
# tint（RGB 乘系数）+ alpha —— 模拟运行时 Image.color
function Draw-BgTint($g, $img, [single]$x, [single]$y, [single]$w, [single]$h, [single]$tr, [single]$tg, [single]$tb, [single]$alpha) {
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = $tr; $cm.Matrix11 = $tg; $cm.Matrix22 = $tb; $cm.Matrix33 = $alpha
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle([int]$x, [int]$y, [int]$w, [int]$h)), 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
}
# 把星点画在 7 个星位上：$litCount 个亮（按 天枢→摇光 顺序），其余暗
function Draw-DipStars($g, $star, $glow, $pts, [single]$ccx, [single]$ccy, [single]$rot, [int]$litCount, [single]$size) {
  for ($i = 0; $i -lt $pts.Count; $i++) {
    $q = Rot-Pt $pts[$i][0] $pts[$i][1] $ccx $ccy $rot
    $half = $size / 2.0
    if ($i -lt $litCount) {
      Draw-BgTint $g $glow ($q[0] - $half * 1.6) ($q[1] - $half * 1.6) ($half * 3.2) ($half * 3.2) 0.91 0.82 0.54 0.62
      Draw-BgTint $g $star ($q[0] - $half) ($q[1] - $half) $size $size 0.91 0.82 0.54 1.0
    } else {
      Draw-BgTint $g $star ($q[0] - $half) ($q[1] - $half) $size $size 0.55 0.45 0.20 0.85
    }
  }
}

# ── 预览 1：拆图 + 口径（三栏版式：col1 x24 宽480 / col2 x544 宽620 / col3 x1210）──
function New-BgLayersPreview([string]$dir, [string]$out) {
  $CW = 1800; $CH = 900
  $b = New-Object System.Drawing.Bitmap($CW, $CH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 13, 19))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-BgText $g '大厅背景 v2（第六稿）—— 纯星野 + 左侧星环（12 颗点可独立亮）' 24 14 30
  Put-BgTextCol $g '环挪到左侧避开 UI 热点、半径 410 → 350；与战场环同源不同形；北斗停用（$DIPPER_ON = $false，代码与参数仍在脚本里）' 24 56 19 170 214 224 244

  $far   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Bg_Far.png'))
  $near  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Bg_Near.png'))
  $ring  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Ring_Base.png'))
  $rnode = [System.Drawing.Image]::FromFile((Join-Path $dir 'Ring_Node.png'))
  $rglow = [System.Drawing.Image]::FromFile((Join-Path $dir 'Ring_NodeGlow.png'))

  # ── col1：远景 / 近景（宽 480）──
  Draw-BgImg $g $far  24 110 470 264 1.0
  Put-BgText $g 'Bg_Far · 2074×1166 不透明 · ×0.25' 24 384 20
  Put-BgTextCol $g '夜空底 + 冷光池 + 两档星点 + 四角压深 + 内缩金细框' 24 410 17 175 158 172 190
  Draw-BgImg $g $near 24 440 470 264 1.0
  Put-BgText $g 'Bg_Near · 2074×1166 透明 · ×1.00' 24 714 20
  Put-BgTextCol $g '光尘两档 + 3 条金线弧 + 底部冷光晕' 24 740 17 175 158 172 190
  Put-BgTextCol $g '四角留空、避开 UI 热点' 24 764 17 175 158 172 190

  # ── col2：星环骨架缩略 + 一颗点的两态（宽 620）──
  $side  = [single](Get-RingScreenSide)
  $thumb = [single]($side * 0.58)
  Draw-BgImg $g $ring 544 110 $thumb $thumb 0.96
  Put-BgText $g ('Ring_Base · 1584×1584 透明 · ×0.55 · 场景显示 {0}px' -f $side) 544 580 20
  Put-BgTextCol $g '双细环（R350 / R318）+ 24 根三档刻度' 544 608 17 175 158 172 190
  Put-BgTextCol $g '（长 4 / 中 8 / 短 12）+ 环外 R388 极淡轨道环；中央留空' 544 632 17 175 158 172 190

  Put-BgText $g '环上的点（每 30° 一颗，共 12 颗，各自独立可亮）' 544 676 20
  Draw-BgTint $g $rnode 560 710 104 104 0.40 0.33 0.16 0.85
  Draw-BgTint $g $rglow 840 686 160 160 0.91 0.82 0.54 0.62
  Draw-BgTint $g $rnode 872 710 104 104 0.91 0.82 0.54 1.0
  Put-BgTextCol $g '暗态（tint 压暗）' 560 826 17 175 158 172 190
  Put-BgTextCol $g '亮态 = 辉光 + 点' 840 826 17 175 158 172 190
  Put-BgTextCol $g '点的贴图 104×104（= 屏幕 52px）· 辉光显示 112px，每颗一个实例' 544 852 17 175 158 172 190

  # ── col3：口径 ──
  $rx = 1210
  Put-BgText $g '口径' $rx 110 22
  $lines = @(
    '拆图      远景 Bg_Far    2074×1166   ×0.25',
    '          近景 Bg_Near   2074×1166   ×1.00',
    '          星环骨架 + 12 颗点 + 辉光      ×0.55',
    '',
    '星环摆位  环心 (560, 540) 外环 R350 内环 R318',
    '          轨道环 R388；场景显示边长 792px',
    '          右边缘 560+350 = 910 < 1099 → 不压 UI',
    '          36 槽位：长刻度 4 / 中 8 / 短 12 / 空 12',
    '          空槽 = 12 颗点，每 30° 一颗（i % 3 == 1）',
    '          点显示 52px、辉光 112px，每颗一个实例',
    '          中央留空；差异清单见脚本「中央星环」一节',
    '',
    '溢出口径  远景 / 近景 = 屏的 1.08 倍',
    '          位移 58px（屏宽 3%），四边留 4%',
    '',
    '视差      鼠标偏移 × 每层 depth × 平滑 6',
    '          照 MenuLightCurtain.cs（.030）',
    '',
    '变动态    ① 每层能各自动',
    '          ② 每层能被数据换（Tint / 贴图槽）',
    '          ③ 每颗点一个实例，点亮只改 color',
    '          → 换主题只改 Inspector',
    '',
    '北斗      已停用（$DIPPER_ON = $false）',
    '          代码 / 参数留在脚本里，改开关即恢复')
  $ly = 142
  foreach ($ln in $lines) {
    Put-BgTextCol $g $ln $rx $ly 16 200 214 224 244
    $ly += 23
  }

  $far.Dispose(); $near.Dispose(); $ring.Dispose(); $rnode.Dispose(); $rglow.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}
# ── 预览 2：1920×1080 合成（$ringLit = 已亮的环上点数，顺时针自正上方偏右 10° 起）────
function New-BgMockup([string]$dir, [string]$out, [bool]$guide, [int]$ringLit = 0, [bool]$uiMock = $false) {
  $b = New-Object System.Drawing.Bitmap($MOCK_W, $MOCK_H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

  $far   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Bg_Far.png'))
  $near  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Bg_Near.png'))
  $ring  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Ring_Base.png'))
  $rnode = [System.Drawing.Image]::FromFile((Join-Path $dir 'Ring_Node.png'))
  $rglow = [System.Drawing.Image]::FromFile((Join-Path $dir 'Ring_NodeGlow.png'))

  Draw-BgImg $g $far 0 0 $MOCK_W $MOCK_H 1.0

  # 左侧星环：骨架 + 12 颗点（$ringLit 颗已亮）—— 在近景之下、远景之上
  $rSide = [single](Get-RingScreenSide)
  Draw-BgImg $g $ring ($RING_CX - $rSide / 2.0) ($RING_CY - $rSide / 2.0) $rSide $rSide 0.96
  Draw-RingNodes $g $rnode $rglow $RING_CX $RING_CY $ringLit $RING_NODE_AT $RING_GLOW_AT

  # 北斗（已停用）：把 $DIPPER_ON 改成 $true 即可恢复，需要 Star_Dipper*.png 三张同时在
  if ($DIPPER_ON) {
    $dip  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Star_Dipper.png'))
    $star = [System.Drawing.Image]::FromFile((Join-Path $dir 'Star_DipperStar.png'))
    $glow = [System.Drawing.Image]::FromFile((Join-Path $dir 'Star_DipperGlow.png'))
    $dh = $DIP_W * $DIP_SPANY / $DIP_SPANX
    $bx = $DIP_CX - $DIP_W / 2.0
    $by = $DIP_CY - $dh / 2.0
    $L = Get-DipLayout $bx $by $DIP_W
    $padS = [single]($DIP_PAD / $DIP_S)
    $st = $g.Save()
    $g.TranslateTransform($DIP_CX, $DIP_CY); $g.RotateTransform($DIP_ROT); $g.TranslateTransform(-$DIP_CX, -$DIP_CY)
    Draw-BgImg $g $dip ($bx - $padS) ($by - $padS) ($DIP_W + 2 * $padS) ($L.h + 2 * $padS) 0.95
    $g.Restore($st)
    Draw-DipStars $g $star $glow $L.pts $DIP_CX $DIP_CY $DIP_ROT 4 40
    $dip.Dispose(); $star.Dispose(); $glow.Dispose()
  }

  Draw-BgImg $g $near 0 0 $MOCK_W $MOCK_H 1.0

  # 模拟入口板的遮挡：真实板是深蓝黑近不透明（贴图见 lobby-ui-v1/LobbyEntryPlate_*.png），
  # 这里只按 UI 热点铺同色矩形 + 一条金细线，用来核对「环是否真的躲开了」。
  if ($uiMock) {
    foreach ($z in $UI_ZONES) {
      $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(240, 12, 17, 26))
      $g.FillRectangle($br, $z[0], $z[1], $z[2], $z[3]); $br.Dispose()
      $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 70)), 2
      $g.DrawRectangle($pen, $z[0], $z[1], $z[2], $z[3]); $pen.Dispose()
    }
    Put-BgTextCol $g '色块 = UI 热点（模拟入口板 / 顶栏遮挡，不是真实贴图）' 24 1000 22 210 120 210 236
  }

  if ($guide) {
    $names = @('头像板 467×96', '横栏 538×95', '战斗板', '卡牌总览板', '下排大框 666×145')
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(140, 120, 210, 236)), 2
    $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    for ($i = 0; $i -lt $UI_ZONES.Count; $i++) {
      $z = $UI_ZONES[$i]
      $g.DrawRectangle($pen, $z[0], $z[1], $z[2], $z[3])
      Put-BgTextCol $g $names[$i] ($z[0] + 8) ($z[1] + 6) 20 170 120 210 236
    }
    $pen.Dispose()
    Put-BgTextCol $g ('合成（1920×1080）：Bg_Far + 左侧星环（{0} / 12 已亮，环心 560 半径 350）+ Bg_Near；蓝虚线 = UI 热点' -f $ringLit) 24 1036 22 210 120 210 236
  }

  $far.Dispose(); $near.Dispose(); $ring.Dispose(); $rnode.Dispose(); $rglow.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}
# ── 主流程 ──────────────────────────────────────────────────
$farOut  = Join-Path $GEN 'Bg_Far.png'
$nearOut = Join-Path $GEN 'Bg_Near.png'

New-BgFar  $farOut  | Out-Null
New-BgNear $nearOut | Out-Null
$dipMade  = @()
if ($DIPPER_ON) { $dipMade = New-DipperSet $GEN }
$ringMade = New-RingSet $GEN

$layersP = New-BgLayersPreview $GEN (Join-Path $PREV 'lobby-bg-v2-layers.png')
$mockP   = New-BgMockup $GEN (Join-Path $PREV 'lobby-bg-v2-mockup.png')     $true  0
$mockLit = New-BgMockup $GEN (Join-Path $PREV 'lobby-bg-v2-mockup-lit.png') $false 5
$mockUi  = New-BgMockup $GEN (Join-Path $PREV 'lobby-bg-v2-mockup-ui.png')  $false 5 $true

"far    : $farOut"
"near   : $nearOut"
foreach ($m in $dipMade)  { "dipper : $m" }
foreach ($m in $ringMade) { "ring   : $m" }
"layers : $layersP"
"mockup : $mockP"
"lit    : $mockLit"
"ui     : $mockUi"