# 「加入房间」图标 v1（2026-09-27）
#
# 用户 2026-09-27：「在叉ui左边做一个大小一样的加入房间的简单ui，button」。
# 与 LobbyUIv1.ps1 的「金线石印族」（好友 / 商城 / 礼盒 / 合起的书 / 信封 / 叉）**同一套配方**：
#   深蓝黑平底（竖渐变 $BAR_T -> $BAR_B）+ 外墨边 + 等比内缩金细线 + 一处金饰（菱形铆钉）；
#   常态 / 悬停两张只差色调（走同一套 Set-IconTone：石面提亮 + 金线 GOLD->GOLD_L 且 α +40）——
#   形体尺寸完全一致，切图不跳位。
# 线宽按屏幕像素折算（本套 3 倍出图：贴图 3px = 屏幕 1px；图标是 256 贴图 -> 屏幕 60，1 贴图 px ≈ 0.234 屏 px）。
#
# 形体：**门框 + 一支向右进入的箭头**（「进房间」）——
# 门 = 竖长方形（比教程那本书瘦一点）+ 内缩金线；铆钉当门把手；箭头在门的左侧指向门内。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   Icon_LobbyJoin.png        256x256 = 屏幕 60x60  —— 房间面板右上角「加入房间」· 常态
#   Icon_LobbyJoinHover.png   256x256               —— 悬停
# 预览：Tools/cardframe/preview/lobby-icon-join.png（同背景上：与教程 / 叉 并排，1:1 实尺 + 3 倍放大）

Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad / New-Diamond

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 图标层常量（照 LobbyUIv1.ps1 的图标段抄）────────────────────────────────
$ICON_INK   = 9      # 贴图 px：形状外墨边（屏幕 60px 时 ≈ 2.1）
$ICON_GOLD  = 6      # 贴图 px：金细线（屏幕 60px 时 ≈ 1.4）
$ICON_TONE_N = @{ 'top' = $BAR_T; 'bot' = $BAR_B; 'gold' = $GOLD; 'bright' = $GOLD_L; 'boost' = 0 }
$ICON_TONE_H = @{ 'top' = (Mix-Col $BAR_T $HILITE 0.12); 'bot' = (Mix-Col $BAR_B $BAR_T 0.40); 'gold' = $GOLD_L; 'bright' = (Mix-Col $GOLD_L $HILITE 0.35); 'boost' = 40 }
$ICON_TOP = $ICON_TONE_N['top']; $ICON_BOT = $ICON_TONE_N['bot']
$ICON_GC  = $ICON_TONE_N['gold']; $ICON_GB = $ICON_TONE_N['bright']; $ICON_GA = 0

function Set-IconTone([bool]$hover) {
  $tone = $(if ($hover) { $ICON_TONE_H } else { $ICON_TONE_N })
  $script:ICON_TOP = $tone['top']; $script:ICON_BOT = $tone['bot']
  $script:ICON_GC = $tone['gold']; $script:ICON_GB = $tone['bright']; $script:ICON_GA = $tone['boost']
}
function New-IconGold([int]$a = 235) { return (New-Col $ICON_GC ([Math]::Min(255, $a + $ICON_GA))) }
function New-IconGoldBright([int]$a = 245) { return (New-Col $ICON_GB ([Math]::Min(255, $a + $ICON_GA))) }

function Fill-GlyphBody($g, $path, [single]$ink = 0) {
  if ($ink -le 0) { $ink = $ICON_INK }
  $st = $g.Save(); $g.SetClip($path)
  $bd = $path.GetBounds()
  Fill-VGrad $g $bd.X $bd.Y $bd.Width $bd.Height $ICON_TOP $ICON_BOT 255 255
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 205)), $ink
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose()
}
function Stroke-GlyphGold($g, $path, [int]$a = 235, [single]$w = 0) {
  if ($w -le 0) { $w = $ICON_GOLD }
  $pen = New-Object System.Drawing.Pen (New-IconGold $a), $w
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose()
}

function New-JoinGlyph($g) {
  # 加入房间：门框（竖长方形 + 内缩金线 + 一枚菱形门把手）+ 左侧一支指向门内的金箭头
  $door = New-RoundPath 104 34 116 188 14
  Fill-GlyphBody $g $door
  Stroke-GlyphGold $g (New-RoundPath 118 48 88 160 8)

  # 门把手：菱形铆钉（与教程 / 信封那枚同配方：亮金填充 + 墨线勾边）
  $dd = New-Diamond 186 128 13
  $bs = New-Object System.Drawing.SolidBrush (New-IconGoldBright 240); $g.FillPath($bs, $dd); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 200)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dd); $pen.Dispose(); $dd.Dispose()

  # 进入箭头：横线 + 箭头头部（比金细线粗半档 = 这一族「主元素」的线宽）
  $pen = New-Object System.Drawing.Pen (New-IconGold 235), ($ICON_GOLD * 1.5)
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($pen, 34, 128, 88, 128)
  $g.DrawLine($pen, 66, 106, 88, 128)
  $g.DrawLine($pen, 66, 150, 88, 128)
  $pen.Dispose()
  $door.Dispose()
}

function New-JoinIcon([string]$out, [switch]$hover) {
  $res = New-Bmp 256 256; $b = $res[0]; $g = $res[1]
  Set-IconTone($hover.IsPresent)
  New-JoinGlyph $g
  return (Save-Bmp $b $g $out)
}

# ── 出图 ────────────────────────────────────────────────────────────────────
$made = @()
$made += (New-JoinIcon (Join-Path $GEN 'Icon_LobbyJoin.png'))
$made += (New-JoinIcon (Join-Path $GEN 'Icon_LobbyJoinHover.png') -hover)

# ── 预览：与教程 / 叉并排（1:1 实尺 60px + 3 倍放大 180px）───────────────────
function New-JoinSheet([string]$dir, [string]$out) {
  $W = 900; $H = 700
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $bgPath = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
  if (Test-Path $bgPath) { $img = [System.Drawing.Image]::FromFile($bgPath); $g.DrawImage($img, 0, 0, $W, $H); $img.Dispose() }
  else { Fill-VGrad $g 0 0 $W $H $BAR_T $BAR_B 255 255 }

  $fBig = New-Object System.Drawing.Font('Consolas', 20, [System.Drawing.GraphicsUnit]::Pixel)
  $fSm  = New-Object System.Drawing.Font('Consolas', 15, [System.Drawing.GraphicsUnit]::Pixel)
  $brT  = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $brB  = New-Object System.Drawing.SolidBrush (New-Col @(240, 232, 210) 205)
  $g.DrawString('join icon v1  —  上：1:1 (60x60 与教程 / 叉同排)   下：3x 放大 180px（左常态 / 右悬停）', $fSm, $brT, 24, 14)

  $row = @(
    @('Icon_LobbyTutorial.png', 'Icon_LobbyTutorialHover.png', '教程 · 参考'),
    @('Icon_LobbyJoin.png',     'Icon_LobbyJoinHover.png',     '加入房间 · 新'),
    @('Icon_Close.png',         'Icon_CloseHover.png',         '叉 · 参考')
  )

  # 1:1 实尺一行（三组并排，每组 常态 + 悬停）
  $x = 40.0; $y = 48.0
  foreach ($r in $row) {
    foreach ($n in @($r[0], $r[1])) {
      if (Test-Path (Join-Path $dir $n)) {
        $img = [System.Drawing.Image]::FromFile((Join-Path $dir $n))
        $g.DrawImage($img, [single]$x, [single]$y, 60, 60); $img.Dispose()
      }
      $x += 72
    }
    $g.DrawString($r[2], $fSm, $brB, [single]$x + 8, [single]$y + 20)
    $x += 190
  }

  # 3 倍放大：一行一组
  $y = 150.0
  foreach ($r in $row) {
    $g.DrawString($r[2], $fSm, $brB, 40, [single]($y + 82))
    $x = 300.0
    foreach ($n in @($r[0], $r[1])) {
      if (Test-Path (Join-Path $dir $n)) {
        $img = [System.Drawing.Image]::FromFile((Join-Path $dir $n))
        $g.DrawImage($img, [single]$x, [single]$y, 180, 180); $img.Dispose()
      }
      $x += 200
    }
    $y += 182
  }

  $fBig.Dispose(); $fSm.Dispose(); $brT.Dispose(); $brB.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}
$sheet = New-JoinSheet $GEN (Join-Path $PREV 'lobby-icon-join.png')

$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
