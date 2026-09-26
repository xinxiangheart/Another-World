# 大厅背景 v1 —— 深蓝黑石殿底板 + 中央棋盘徽记（与 Game.unity 战场同源）
#
# 复用 BoardLayersV2.ps1 的调色板与画笔工具（它自己会带入 CardFrameV6.ps1），
# 所以色值与线宽与战场 L1/L3/L4 完全同一套，不另起配色。
#
# 产物
#   Assets/_Game/Art/Sprites/Generated/lobby-v1/LobbyBack.png        2048x1152 不透明底板
#       只有渐变 / 噪点 / 直线金框 —— 没有圆，所以全屏拉伸（非 16:9）也不会变形
#   Assets/_Game/Art/Sprites/Generated/lobby-v1/LobbyBackEmblem.png  1200x1200 透明徽记
#       所有圆 / 六芒星都在这张里，场景里按定尺 + preserveAspect 摆，避免被拉成椭圆
#   Tools/cardframe/preview/lobby-bg-v1-mockup.png                   1920x1080 实机摆位粗合成
#       按 Lobby.unity 实测坐标把菜单文字压上去，用来判断背景是否抢主体
#
# 回退：删掉 Generated/lobby-v1 目录即可，场景未接线前本脚本不改任何现有文件。
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/BoardLayersV2.ps1"

$ROOT = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 可调参数 ────────────────────────────────────────────────
$PLATE_W       = 2048
$PLATE_H       = 1152
$EMBLEM_BOX    = 1200      # 徽记画布边长（正方、透明）
$EMBLEM_SCREEN = 1020      # 徽记在屏上的目标边长（1920x1080 口径）
$FRAME_INSET   = 46        # 底板内缩金细框
$FRAME_ALPHA   = 46
$STAR_COUNT    = 70        # 星点（战场上 110，这里再压一档）
$STAR_A        = @(12, 46)
$HEX_ALPHA     = 38        # 六芒星透明度（战场上 38）
$POOL_A        = 118       # 中央冷光池强度（战场上 150）
$VIGNETTE_A    = 104       # 四角压深（战场上 96）
$MOCK_W        = 1920
$MOCK_H        = 1080

# ── 底板：石殿的「底」────────────────────────────────────────
function New-LobbyPlate([string]$out) {
  $r = New-Layer $PLATE_W $PLATE_H $true $SKY_T; $bmp = $r[0]; $g = $r[1]
  $cx = $PLATE_W / 2.0; $cy = $PLATE_H / 2.0

  # ① 夜空底（与战场 L1 同一配方：上暗下亮）
  Fill-VGrad $g 0 0 $PLATE_W $PLATE_H $SKY_T $SKY_B

  # ② 中央冷光池：透明外圈，只加光不压暗（避免出现一圈可见的暗边）
  Fill-Radial $g $cx ($cy + 40) 1560 860 $GLOW_C @(11,17,29) 0.92 $POOL_A 0

  # ③ 星点：克制，只铺上 70%
  $rng = New-Object System.Random 20260926
  for ($i = 0; $i -lt $STAR_COUNT; $i++) {
    $x = $rng.Next(0, $PLATE_W); $y = $rng.Next(0, [int]($PLATE_H * 0.70))
    $rr = 1 + $rng.NextDouble() * 1.6
    $a = $STAR_A[0] + $rng.Next(0, ($STAR_A[1] - $STAR_A[0]))
    $br = New-Object System.Drawing.SolidBrush (New-Col @(210,224,244) $a)
    $g.FillEllipse($br, [single]($x-$rr), [single]($y-$rr), [single]($rr*2), [single]($rr*2)); $br.Dispose()
  }

  # ④ 铺石：三条横缝 + 错缝竖缝，低 alpha，只读作「殿内的地面分块」
  $rows = 3
  $bandH = $PLATE_H / $rows
  $penH  = New-Object System.Drawing.Pen ((New-Col $INK0 40)), 2
  $penHs = New-Object System.Drawing.Pen ((New-Col $STEEL_D 20)), 2
  $penV  = New-Object System.Drawing.Pen ((New-Col $INK0 30)), 2
  for ($ri = 0; $ri -lt $rows; $ri++) {
    $y0 = [single]($bandH * $ri)
    if ($ri -gt 0) {
      $g.DrawLine($penH,  0, $y0, $PLATE_W, $y0)
      $g.DrawLine($penHs, 0, [single]($y0 + 5), $PLATE_W, [single]($y0 + 5))
    }
    for ($k = 0; $k -lt 4; $k++) {
      $fx = (($k + 0.5 + ($ri % 2) * 0.5) % 4) / 4.0
      $x  = [single]($PLATE_W * $fx)
      $g.DrawLine($penV, $x, $y0, $x, [single]($y0 + $bandH))
    }
  }
  $penH.Dispose(); $penHs.Dispose(); $penV.Dispose()

  # ⑤ 石面微斑：只用极低 alpha 的暗斑，不做云状亮斑
  Add-Pits $g $rng 1100 90 90 ($PLATE_W-180) ($PLATE_H-180) $INK0 7 20 4 18
  Add-Pits $g $rng 520 90 90 ($PLATE_W-180) ($PLATE_H-180) $STONE1 6 15 5 20

  # ⑥ 四角压深（照战场 L1）
  foreach ($c in @(@(0,0,760,520), @($PLATE_W,0,760,520), @(0,$PLATE_H,760,520), @($PLATE_W,$PLATE_H,760,520))) {
    Fill-Radial $g $c[0] $c[1] $c[2] $c[3] $INK0 $INK0 0.92 $VIGNETTE_A 0
  }

  # ⑦ 内缩金细框 + 四角菱形铆钉（顶栏的阶梯角在这里简化成直角 + 菱形）
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $FRAME_ALPHA)), 2
  $g.DrawRectangle($pen, $FRAME_INSET, $FRAME_INSET, ($PLATE_W - $FRAME_INSET*2), ($PLATE_H - $FRAME_INSET*2))
  $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D 30)), 2
  $g.DrawRectangle($pen, ($FRAME_INSET+7), ($FRAME_INSET+7), ($PLATE_W - ($FRAME_INSET+7)*2), ($PLATE_H - ($FRAME_INSET+7)*2))
  $pen.Dispose()
  $fp = @(
    @(($FRAME_INSET + 30), ($FRAME_INSET + 30)),
    @(($PLATE_W - $FRAME_INSET - 30), ($FRAME_INSET + 30)),
    @(($FRAME_INSET + 30), ($PLATE_H - $FRAME_INSET - 30)),
    @(($PLATE_W - $FRAME_INSET - 30), ($PLATE_H - $FRAME_INSET - 30)))
  foreach ($q in $fp) {
    $dp = New-Diamond $q[0] $q[1] 13
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 96)
    $g.FillPath($bs, $dp); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 190)), 2.4
    $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  }

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}
# ── 徽记：棋盘母题（金主环 + 内环 + 六芒星），几何照抄 L3 Sigil / L4 Rune ──
function New-LobbyEmblem([string]$out) {
  $r = New-Layer $EMBLEM_BOX $EMBLEM_BOX $false @(0,0,0); $bmp = $r[0]; $g = $r[1]
  $cx = $EMBLEM_BOX / 2.0; $cy = $EMBLEM_BOX / 2.0

  # ── ① 金主环：4px 主环 + 2px 外圈 + 钢色内衬（战场 L3 同值）──
  Stroke-Ell $g $cx $cy 430 $GOLD 4 92
  Stroke-Ell $g $cx $cy 466 $GOLD 2 36
  Stroke-Ell $g $cx $cy 414 $STEEL 2 24

  # 环带刻度：24 根，偶数根更长更亮
  for ($i = 0; $i -lt 24; $i++) {
    $a  = [Math]::PI / 12.0 * $i
    $r0 = 430.0; $r1 = 448.0; $ta = 56; $tw = 3
    if ($i % 2 -eq 0) { $r1 = 468.0; $ta = 96; $tw = 5 }
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $ta)), $tw
    $g.DrawLine($pen, [single]($cx + $r0*[Math]::Cos($a)), [single]($cy + $r0*[Math]::Sin($a)), [single]($cx + $r1*[Math]::Cos($a)), [single]($cy + $r1*[Math]::Sin($a)))
    $pen.Dispose()
  }

  # 外圈 12 个标记（每三个一循环换朝向）
  for ($i = 0; $i -lt 12; $i++) {
    $a  = [Math]::PI / 6.0 * $i + 0.26
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

  # 主环上 4 颗菱形铆钉（斜 45°）
  foreach ($i in 0..3) {
    $a  = [Math]::PI / 2.0 * $i + [Math]::PI / 4.0
    $dx = $cx + 430*[Math]::Cos($a); $dy = $cy + 430*[Math]::Sin($a)
    $dp = New-Diamond $dx $dy 22
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 112)
    $g.FillPath($bs, $dp); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $INK0 190)), 3.4
    $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  }

  # ── ② 内环 + 刻度（战场 L4 同值）──
  Stroke-Ell $g $cx $cy 330 $STEEL 3 52
  Stroke-Ell $g $cx $cy 318 $STEEL 2 26
  for ($i = 0; $i -lt 16; $i++) {
    $a = [Math]::PI / 8.0 * $i
    $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 30)), 3
    $g.DrawLine($pen, [single]($cx + 296*[Math]::Cos($a)), [single]($cy + 296*[Math]::Sin($a)), [single]($cx + 322*[Math]::Cos($a)), [single]($cy + 322*[Math]::Sin($a)))
    $pen.Dispose()
  }

  # ── ③ 六芒星：两个正三角，r 与透明度照抄战场 L4（alpha 38）──
  foreach ($rot in @(0.0, 1.0472)) {
    $tp  = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @()
    for ($i = 0; $i -lt 3; $i++) {
      $a = $rot + [Math]::PI * 2 / 3 * $i - [Math]::PI/2
      $pts += ,@(($cx + 248*[Math]::Cos($a)), ($cy + 248*[Math]::Sin($a)))
    }
    $pf2 = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    foreach ($q in $pts) { $pf2.Add((New-Object System.Drawing.PointF([single]$q[0], [single]$q[1]))) }
    $tp.AddPolygon($pf2.ToArray())
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $HEX_ALPHA)), 3
    $g.DrawPath($pen, $tp); $pen.Dispose(); $tp.Dispose()
  }

  # 中心：金菱形 + 蓝芯（战场 L4 同值）
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
# ── 实机摆位粗合成：用来判断「背景有没有抢主体」────────────────
$script:PFC = $null
function Get-LobbyFont([single]$px) {
  if ($script:PFC -eq $null) {
    $script:PFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PFC.AddFontFile((Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'))
  }
  return New-Object System.Drawing.Font($script:PFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}

function New-LobbyMockup([string]$plate, [string]$emblem, [string]$out) {
  $b = New-Object System.Drawing.Bitmap($MOCK_W, $MOCK_H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

  $pi = [System.Drawing.Image]::FromFile($plate)
  $g.DrawImage($pi, 0, 0, $MOCK_W, $MOCK_H); $pi.Dispose()

  $ei = [System.Drawing.Image]::FromFile($emblem)
  $g.DrawImage($ei, [single](($MOCK_W - $EMBLEM_SCREEN) / 2.0), [single](($MOCK_H - $EMBLEM_SCREEN) / 2.0), [single]$EMBLEM_SCREEN, [single]$EMBLEM_SCREEN)
  $ei.Dispose()

  # 面板底板样例（CreateRoomPanel 720x480）：检查背景会不会跟面板打架
  $pw = 720.0; $ph = 480.0
  $px = ($MOCK_W - $pw) / 2.0; $py = ($MOCK_H - $ph) / 2.0
  $rpath = New-RoundPath $px $py $pw $ph 18
  Fill-RoundGrad $g $px $py $pw $ph 18 @(30,41,56) @(12,17,26)
  Stroke-RoundCol $g $px $py $pw $ph 18 $GOLD 2 120
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 60)), 2
  $g.DrawPath($pen, $rpath); $pen.Dispose(); $rpath.Dispose()

  # 菜单文字：坐标 = Lobby.unity 实测（anchor 居中，pos 换到左上角原点）
  $font = Get-LobbyFont 26
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $items = @(
    @('随机匹配', 0, 199), @('创建房间', 0, 137.7), @('加入房间', 0, 60),
    @('卡牌总览', 0, -9), @('游戏介绍', 0, -85), @('设置', 0, -152),
    @('结束游戏', 0, -217), @('返回主界面', 0, -284))
  foreach ($it in $items) {
    $sx = [single]($MOCK_W / 2.0 + $it[1] - 80)
    $sy = [single]($MOCK_H / 2.0 - $it[2] - 15)
    $rect = New-Object System.Drawing.RectangleF($sx, $sy, 160, 30)
    $sh = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(190, 0, 0, 0))
    $g.DrawString($it[0], $font, $sh, (New-Object System.Drawing.RectangleF(($sx+1.5), ($sy+1.5), 160, 30)), $fmt)
    $sh.Dispose()
    $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(238, 236, 240, 246))
    $g.DrawString($it[0], $font, $br, $rect, $fmt); $br.Dispose()
  }
  $font.Dispose()

  # 角标说明
  $cap = New-Object System.Drawing.Font('Microsoft YaHei', 13)
  $cbr = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 210, 120))
  $g.DrawString(('大厅背景 v1 粗合成（{0}x{1}）· 底板 {2}x{3} + 徽记 {4}px · 文字按 Lobby.unity 实测坐标 · 中央方块是面板底板样例' -f $MOCK_W, $MOCK_H, $PLATE_W, $PLATE_H, $EMBLEM_SCREEN), $cap, $cbr, 24, 16)
  $cbr.Dispose(); $cap.Dispose()

  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ─────────────────────────────────────────────────
$HEX_ALPHA = 38
$plateOut  = Join-Path $GEN  'LobbyBack.png'
$emblemOut = Join-Path $GEN  'LobbyBackEmblem.png'
$mockOut   = Join-Path $PREV 'lobby-bg-v1-mockup.png'
$cleanOut  = Join-Path $PREV 'lobby-bg-v1-clean.png'

# 纯背景对照：底板 + 徽记，无文字无面板
function New-LobbyClean([string]$plate, [string]$emblem, [string]$out) {
  $b = New-Object System.Drawing.Bitmap($MOCK_W, $MOCK_H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $pi = [System.Drawing.Image]::FromFile($plate)
  $g.DrawImage($pi, 0, 0, $MOCK_W, $MOCK_H); $pi.Dispose()
  $ei = [System.Drawing.Image]::FromFile($emblem)
  $g.DrawImage($ei, [single](($MOCK_W - $EMBLEM_SCREEN) / 2.0), [single](($MOCK_H - $EMBLEM_SCREEN) / 2.0), [single]$EMBLEM_SCREEN, [single]$EMBLEM_SCREEN)
  $ei.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

New-LobbyPlate  $plateOut  | Out-Null
New-LobbyEmblem $emblemOut | Out-Null
New-LobbyClean  $plateOut $emblemOut $cleanOut | Out-Null
New-LobbyMockup $plateOut $emblemOut $mockOut  | Out-Null

"plate  : $plateOut"
"emblem : $emblemOut"
"clean  : $cleanOut"
"mockup : $mockOut"