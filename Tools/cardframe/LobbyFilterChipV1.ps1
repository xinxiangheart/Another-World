# 卡牌总览 · 左侧筛选格的三态子背景 v1（2026-09-28）
#
# 用户 2026-09-28：「做卡牌总览……左边一栏默认选中全部（这些选择格子都是一个 ui + 文字，有子背景以及
#   选中变化）」。配方与 LobbyChip_Kick / LobbyChip_Start（Tools/cardframe/LobbyRoomChipV1.ps1）
#   **同一套**：平底竖渐变 + 左上亮楔 + 外墨边 + 等比内缩金细线；三态只差色调，形体尺寸完全一致
#   -> 切图不跳位。
#
# 尺寸口径（屏幕 px）：108x52。左栏宽 349（= 好友侧边栏 465 x 3/4，取整），一栏放 3 格：
#   3 x 108 + 2 x 12 = 348 <= 349。最长的标签是「主动退场」（4 字），字号 26 -> 约 104px，塞得下。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyChip_Filter.png       324x156 = 屏幕 108x52 —— 常态
#   LobbyChip_FilterHover.png  324x156                —— 悬停
#   LobbyChip_FilterOn.png     324x156                —— 选中
# 前缀 LobbyChip_ 已在 TextureImportSettingsGuard 的白名单里（npotScale=None + alphaIsTransparency），
# 不需要改那个文件。
#
# 预览：Tools/cardframe/preview/lobby-filter-chips.png（三态 + 一行实尺样例）

Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 版式（屏幕 px）—— C# 那边 LobbyUIBuilder 的 Lc* 常量照抄同一组数 ──
$CHIP_W = 108
$CHIP_H = 52

$S       = 3
$LW_INK  = 9
$LW_GOLD = 3
$INS     = 18
$R       = 20
$A_GOLD  = 150

$TONE = @{
  'n' = @{ top = $BAR_T
           bot = $BAR_B
           gold = $GOLD
           aWedge = 64
           aG = 150 }
  'h' = @{ top = (Mix-Col $BAR_T $HILITE 0.12)
           bot = (Mix-Col $BAR_B $BAR_T 0.40)
           gold = $GOLD_L
           aWedge = 96
           aG = 236 }
  'o' = @{ top = @(($BAR_T[0] + 9), ($BAR_T[1] + 9), ($BAR_T[2] + 9))
           bot = @(($BAR_B[0] + 9), ($BAR_B[1] + 9), ($BAR_B[2] + 9))
           gold = $GOLD_L
           aWedge = 112
           aG = 255 }
}
$TONE_FILE = @{ 'n' = 'LobbyChip_Filter.png'; 'h' = 'LobbyChip_FilterHover.png'; 'o' = 'LobbyChip_FilterOn.png' }

function New-FilterChip([string]$out, [string]$toneKey) {
  $t = $TONE[$toneKey]
  $w = $CHIP_W * $S; $h = $CHIP_H * $S
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($w - $LW_INK) ($h - $LW_INK) $R
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $w $h $t['top'] $t['bot'] 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($w * 0.55), 0)),
    (New-Object System.Drawing.PointF(0, ($h * 0.52))))) $t['top'] $t['aWedge']
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()

  $ix = $oi + $INS
  $inner = New-RoundPath $ix $ix ($w - 2 * $ix) ($h - 2 * $ix) ([Math]::Max(2.0, $R - $INS))
  $pen = New-Object System.Drawing.Pen ((New-Col $t['gold'] $t['aG'])), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose()

  if ($toneKey -eq 'o') {
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 215)
    $g.FillRectangle($br, [single]($ix + 2), [single]($ix + 3), [single]6, [single]($h - 2 * $ix - 6))
    $br.Dispose()
  }

  $outer.Dispose(); $inner.Dispose(); $g.Dispose()
  return (Save-Bmp $b $g $out)
}

function Get-ChipFont([single]$px) {
  if ($script:CHFC -eq $null) {
    $script:CHFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:CHFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:CHFC.Families[0], $px, [System.Drawing.GraphicsUnit]::Pixel))
}

# ── 预览：三态实尺 + 一行真实标签 ─────────────────────────────────────────
function New-FilterChipSheet([string]$dir, [string]$out) {
  $CW = 760; $CH = 330
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bgPath = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
  if (Test-Path $bgPath) { $img = [System.Drawing.Image]::FromFile($bgPath); $g.DrawImage($img, 0, 0, $CW, $CH); $img.Dispose() }
  else { Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255 }

  $f0 = Get-ChipFont 22
  $br0 = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('卡牌总览 · 左侧筛选格三态（常态 / 悬停 / 选中）+ 一行真实标签', $f0, $br0, 32, 20)
  $br0.Dispose(); $f0.Dispose()

  # 三态 2 倍放大
  $x = 32.0; $y = 66.0
  foreach ($n in @('LobbyChip_Filter.png', 'LobbyChip_FilterHover.png', 'LobbyChip_FilterOn.png')) {
    $img = [System.Drawing.Image]::FromFile((Join-Path $dir $n))
    $g.DrawImage($img, [single]$x, [single]$y, [single]($CHIP_W * 2), [single]($CHIP_H * 2)); $img.Dispose()
    $x += $CHIP_W * 2 + 30
  }

  # 真实标签 1:1（一行 3 格，与场景排布同口径）
  $labels = @('全部', '召唤物', '主动退场')
  $x = 32.0; $y = 196.0
  for ($i = 0; $i -lt 3; $i++) {
    $n = if ($i -eq 2) { 'LobbyChip_FilterOn.png' } else { 'LobbyChip_Filter.png' }
    $img = [System.Drawing.Image]::FromFile((Join-Path $dir $n))
    $g.DrawImage($img, [single]$x, [single]$y, [single]$CHIP_W, [single]$CHIP_H); $img.Dispose()
    $font = Get-ChipFont 26
    $sf = $g.MeasureString($labels[$i], $font)
    $col = if ($i -eq 2) { $GOLD_L } else { @(240, 232, 210) }
    $tbr = New-Object System.Drawing.SolidBrush (New-Col $col 245)
    $g.DrawString($labels[$i], $font, $tbr, [single]($x + ($CHIP_W - $sf.Width) / 2.0), [single]($y + ($CHIP_H - $sf.Height) / 2.0 + 2))
    $tbr.Dispose(); $font.Dispose()
    $x += $CHIP_W + 12
  }

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
foreach ($k in @('n', 'h', 'o')) {
  $made += (New-FilterChip (Join-Path $GEN $TONE_FILE[$k]) $k)
}
$sheet = New-FilterChipSheet $GEN (Join-Path $PREV 'lobby-filter-chips.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
