# 战斗模式卡 v1 —— 「匹配」「排位」两张卡（2026-09-27）
#
# 用户 2026-09-27：出一张类似于卡片的匹配图和排位图，上面是文字下面是对应的适配图案；
#   进入战斗场景时会从最右边滑入到最左边，匹配在最左边，排位在其次，二者之间以及和边框之间
#   有一定距离，位置在屏幕中心线上，大小较大。
#
# 语言：与战场底板 / 顶栏 / 大厅同一套 —— 深蓝黑石面 + 一条金细线。
# 手法：**整块平底 + 一条金细线 + 平板**；禁倒角 / 内阴影 / 外发光 / 双层面板 / 材质贴图。
# 色值一律取自共享调色板（TopBarV2 -> CardFrameV6）：$INK / $BAR_T / $BAR_B / $GOLD / $HILITE。
#
# 产物（Assets/_Game/Art/Sprites/Generated/battle-mode-v1/）
#   BattleModeCard_Match.png        900x1260 贴图 = 屏幕 300x420（$S=3）
#   BattleModeCard_Ranked.png       同上
#   BattleModeCard_Offline.png      同上（**其它子弹窗**里那张「离线模式」）
#   BattleModeCard_MatchHover.png   悬停态；同上尺寸
#   BattleModeCard_RankedHover.png  悬停态；同上尺寸
#   BattleModeCard_OfflineHover.png 悬停态；同上尺寸
#   ---- 三张只差徽记：文字在场景里是 TMP（不进贴图），贴图只出「标题槽 + 分隔线 + 徽记」----
#   ---- 悬停态只差色调（2026-09-27）：石面提亮 + 金 GOLD->GOLD_L + 金饰 α 上调；形体不动 -> 切换不跳位 ----
#
# 卡面版式（贴图 px，画布 900x1260）：
#   外墨边 9（屏幕 3）-> 平底竖渐变 BAR_T->BAR_B -> 三角亮楔（左上，α64）
#   内缩金细线 3（屏幕 1），自外框等比内缩 24（屏幕 8）
#   标题槽    : 上沿到 y=0.345h，两块之间一道金细线（两端各一枚菱形铆钉，同入口板的母题）
#   徽记      : 圆心 (w/2, 0.655h)，半径 0.195w = 175.5 贴图 px（屏幕 58.5）—— 金线，α225
#   收尾细线  : y=0.885h，居中短横线（呼应入口板标题下的那道短线）
#
# 三个徽记（金线勾形，圆角接头）：
#   match  「匹配」    = 两个相对的箭头 + 中央一枚菱形铆钉  —— 撮合 / 相遇
#   ranked 「排位」    = 三级上升台阶连成一条折线 + 顶端一枚菱形 —— 段位晋升
#   offline「离线模式」= 左右各留一道缺口的圆环 + 中央菱形铆钉 —— 没连线 / 不在网里
#   （刻意避开库里已有的：战斗=双三角 / 成就=奖章 / 赛季=盾徽 / 战绩=三根分离柱+基线）
#
# 预览（Tools/cardframe/preview/）：
#   battle-mode-v1-cards.png   四张卡 1:1（上排常态 / 下排悬停，含 TMP 文字的真实版式）+ 徽记放大 + 尺寸标注
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $HILITE / $GOLD 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / New-Diamond / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/battle-mode-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }

$S       = 3                       # 出图倍率：贴图 3px = 屏幕 1px（与 lobby-ui-v1 同口径）
$CARD_W  = 300                     # 屏幕 px
$CARD_H  = 420
$LW_INK  = 9                       # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3                       # 贴图 px：金细线（屏幕 1）
$INS     = 24                      # 贴图 px：金线自外框内缩（屏幕 8）
$RADIUS  = 30                      # 贴图 px：板角半径（与 New-PlateBmp 同值）
$GOLD_A  = 150                     # 内缩金线不透明度（同入口板）
$SPLIT_Y = 0.345                   # 标题槽 / 徽记区 的分界线（占卡高）
$EMB_CY  = 0.620                   # 徽记圆心 y（占卡高）
$EMB_R   = 0.225                   # 徽记半径（占卡宽）
$TAIL_Y  = 0.885                   # 收尾细线 y（占卡高）

function Get-BMFont([single]$px) {
  if ($script:BMFC -eq $null) {
    $script:BMFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:BMFC.AddFontFile($FONT)
  }
  return New-Object System.Drawing.Font($script:BMFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
function Put-Text($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 236) {
  $f = Get-BMFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function Put-TextC($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int]$a = 236, [int[]]$rgb = $null) {
  $f = Get-BMFont $px
  if ($rgb -eq $null) { $rgb = @(240, 232, 210) }
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $rgb[0], $rgb[1], $rgb[2]))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}

# ── 徽记：金线勾形，圆角接头（与入口板同一支笔的粗细比例 r*0.13） ──────────────
function New-ModeEmblem($g, [string]$kind, [single]$cx, [single]$cy, [single]$r, [int]$a = 225) {
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $a)), ([single]($r * 0.13))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  if ($kind -eq 'match') {
    # 两个相对的箭头（撮合 / 相遇）+ 中央菱形铆钉
    foreach ($sgn in @(-1, 1)) {
      $pt = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(($cx + $sgn * $r * 0.98), ($cy - $r * 0.56))),
        (New-Object System.Drawing.PointF(($cx + $sgn * $r * 0.38), $cy)),
        (New-Object System.Drawing.PointF(($cx + $sgn * $r * 0.98), ($cy + $r * 0.56))))
      $g.DrawLines($pen, $pt)
    }
    $dm = New-Diamond $cx $cy ($r * 0.26)
    $g.DrawPath($pen, $dm); $dm.Dispose()
  } elseif ($kind -eq 'ranked') {
    # 三级上升台阶连成一条折线（**不封底、不给基线** —— 与「战绩」那三根分离柱 + 基线刻意拉开）
    $bw   = $r * 0.46
    $base = $cy + $r * 0.64
    $hs   = @(0.46, 0.82, 1.18)
    $x0   = $cx - $r * 0.86
    $pt = @()
    for ($i = 0; $i -lt 3; $i++) {
      $xa = $x0 + $i * $bw
      $ya = $base - $hs[$i] * $r
      $pt += (New-Object System.Drawing.PointF($xa, $base))
      $pt += (New-Object System.Drawing.PointF($xa, $ya))
      $pt += (New-Object System.Drawing.PointF(($xa + $bw), $ya))
    }
    $pt += (New-Object System.Drawing.PointF(($x0 + 3 * $bw), $base))
    $g.DrawLines($pen, [System.Drawing.PointF[]]$pt)
    # 顶端（最高一级）上方一枚菱形
    $dm = New-Diamond ($x0 + 2.5 * $bw) ($base - $hs[2] * $r - $r * 0.34) ($r * 0.20)
    $g.DrawPath($pen, $dm); $dm.Dispose()
  } elseif ($kind -eq 'offline') {
    # 左右各留一道缺口的圆环（= 没连线 / 不在网里）+ 中央菱形铆钉
    $rr = $r * 0.82
    $rect = New-Object System.Drawing.RectangleF(($cx - $rr), ($cy - $rr), ($rr * 2), ($rr * 2))
    foreach ($a0 in @(38, 218)) { $g.DrawArc($pen, $rect, $a0, 124) }
    $dm = New-Diamond $cx $cy ($r * 0.26)
    $g.DrawPath($pen, $dm); $dm.Dispose()
  } else {
    throw "New-ModeEmblem: 没有 '$kind' 这个徽记"
  }
  $pen.Dispose()
}

function New-BattleModeCard([string]$out, [string]$kind, [switch]$hover) {
  $w = $CARD_W * $S; $h = $CARD_H * $S
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  # 悬停态（2026-09-27）：照 LobbyBtnPlateHover / 图标悬停的同一配方 —— 石面提亮 + 金线 GOLD->GOLD_L、
  # 各档金饰 α 上调。**形体 / 尺寸 / 版式一律不动**，所以切换贴图不会跳位。
  $cTop = $BAR_T; $cBot = $BAR_B; $cGold = $GOLD; $cWedge = $BAR_T
  $aWedge = 64; $aLn = $GOLD_A; $aDiv = 110; $aRiv = 205; $aEmb = 225; $aTail = 96
  if ($hover) {
    $cTop = (Mix-Col $BAR_T $HILITE 0.12); $cBot = (Mix-Col $BAR_B $BAR_T 0.40)
    $cGold = $GOLD_L; $cWedge = (Mix-Col $BAR_T $HILITE 0.18)
    $aWedge = 96; $aLn = 236; $aDiv = 190; $aRiv = 245; $aEmb = 250; $aTail = 170
  }
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($w - $LW_INK) ($h - $LW_INK) $RADIUS
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $w $h $cTop $cBot 255 255
  $g.Restore($st)
  # 左上亮楔（同入口板，但卡是竖的，楔更短）
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($w * 0.62), 0)),
    (New-Object System.Drawing.PointF(0, ($h * 0.40))))) $cWedge $aWedge
  # 外墨边
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  # 内缩金细线
  $ix = $oi + $INS; $iy = $oi + $INS
  $inner = New-RoundPath $ix $iy ($w - 2 * $ix) ($h - 2 * $iy) ($RADIUS - $INS)
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aLn)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  # 标题槽 / 徽记区 的分界线 + 两端菱形铆钉
  $dy = [single]($h * $SPLIT_Y)
  $dx0 = [single]($ix + 42); $dx1 = [single]($w - $ix - 42)
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aDiv)), $LW_GOLD
  $g.DrawLine($pen, $dx0, $dy, $dx1, $dy); $pen.Dispose()
  $dp = New-Object System.Drawing.SolidBrush (New-Col $cGold $aRiv)
  foreach ($dx in @($dx0, $dx1)) {
    $dm = New-Diamond $dx $dy 11.0
    $g.FillPath($dp, $dm); $dm.Dispose()
  }
  $dp.Dispose()
  # 徽记
  New-ModeEmblem $g $kind ($w / 2.0) ($h * $EMB_CY) ($w * $EMB_R) $aEmb
  # 收尾细线
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aTail)), $LW_GOLD
  $g.DrawLine($pen, ($w / 2.0 - $w * 0.14), ($h * $TAIL_Y), ($w / 2.0 + $w * 0.14), ($h * $TAIL_Y))
  $pen.Dispose()
  $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

# ── 预览：两张卡 1:1（含 TMP 文字的真实版式）+ 徽记放大 + 尺寸标注 ────────────
$MODE_META = @(
  @{ kind = 'match';   file = 'BattleModeCard_Match.png';   hover = 'BattleModeCard_MatchHover.png';   label = '匹配';     tip = '两个相对的箭头 + 中央菱形铆钉 —— 撮合 / 相遇' },
  @{ kind = 'ranked';  file = 'BattleModeCard_Ranked.png';  hover = 'BattleModeCard_RankedHover.png';  label = '排位';     tip = '三级上升台阶 + 顶端菱形 —— 段位晋升' },
  @{ kind = 'offline'; file = 'BattleModeCard_Offline.png'; hover = 'BattleModeCard_OfflineHover.png'; label = '离线模式'; tip = '缺口圆环 + 中央菱形 —— 没连线 / 不在网里' }
)
function New-BattleModeSheet([string]$dir, [string]$out) {
  $CW = 1900; $CH = 1340
  $b = New-Object System.Drawing.Bitmap($CW, $CH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 9, 12, 18))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-Text $g '战斗模式卡 v1 —— 「匹配」「排位」「离线模式」（上排常态 / 下排悬停态）' 40 26 32
  Put-Text $g '卡 300x420 屏幕 px（贴图 3x = 900x1260）· 深蓝黑石面 + 一条金细线 · 母题：菱形铆钉 / 平板' 40 70 20 176
  Put-Text $g '文字是场景里的 TMP，不进贴图 —— 这里按最终版式画上去，给你看落点与字号；三张只差徽记' 40 100 20 176
  Put-Text $g '悬停态 = 石面提亮 + 金线 GOLD->GOLD_L（同一配方）；形体不动 -> 不跳位。标题变金由场景里的 TMP 负责。' 40 130 19 196

  # 上排 = 常态，下排 = 悬停态；卡间距 60 = 场景里两卡之间的净距（见 BattleModeCards.gap）
  $cyA = 190.0
  $cyB = 190.0 + $CARD_H + 96
  $x = 110.0
  foreach ($m in $MODE_META) {
    foreach ($pair in @(@($m.file, $cyA, 236, $null, '常态'), @($m.hover, $cyB, 255, @(232, 209, 138), '悬停'))) {
      $im = [System.Drawing.Image]::FromFile((Join-Path $dir $pair[0]))
      $g.DrawImage($im, $x, $pair[1], $CARD_W, $CARD_H); $im.Dispose()
      # 场景里那行 TMP 的落点：标题槽正中（悬停行按悬停色画金）
      Put-TextC $g $m.label ($x + $CARD_W / 2.0) ($pair[1] + $CARD_H * $SPLIT_Y / 2.0) 40 $pair[2] $pair[3]
      Put-Text $g ($m.label + ' · ' + $pair[4]) $x ($pair[1] + $CARD_H + 12) 18 170
    }
    $x += $CARD_W + 60
  }
  # 间距标注（前两张之间 = 场景里的 gap）
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(150, 200, 164, 74)), 1
  $g.DrawLine($pen, 110, ($cyA - 22), 470, ($cyA - 22))
  $g.DrawLine($pen, 410, ($cyA - 28), 410, ($cyA - 16))
  $g.DrawLine($pen, 470, ($cyA - 28), 470, ($cyA - 16))
  $pen.Dispose()
  Put-TextC $g '净距 60' 440 ($cyA - 40) 18 200
  # 右栏：三个徽记放大（单独看图案）
  $ex = 1500.0
  $er = 120.0
  Put-TextC $g '徽记 · 放大看图案' $ex 150 22 210
  $ey = 280.0
  foreach ($m in $MODE_META) {
    New-ModeEmblem $g $m.kind $ex $ey $er
    Put-TextC $g $m.label $ex ($ey + 160) 30
    Put-TextC $g $m.tip $ex ($ey + 192) 17 170
    $ey += 330
  }
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
foreach ($m in $MODE_META) {
  $made += (New-BattleModeCard (Join-Path $GEN $m.file) $m.kind)
  $made += (New-BattleModeCard (Join-Path $GEN $m.hover) $m.kind -hover)
}
$sheet = New-BattleModeSheet $GEN (Join-Path $PREV 'battle-mode-v1-cards.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
