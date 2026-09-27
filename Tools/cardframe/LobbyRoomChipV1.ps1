# 「房间」面板两个文字按钮的子背景 v1（2026-09-27）
#
# 用户 2026-09-27：「踢出和开始游戏是有个子背景的」。
# 配方与 MatchWait_CancelBg（Tools/cardframe/MatchWaitV1.ps1）**同一套**：平底竖渐变 + 左上亮楔
#   + 外墨边 + 等比内缩金细线；常态 / 悬停两张只差色调（形体尺寸完全一致 -> 切图不跳位）。
# 线宽按屏幕像素折算（本套统一 3 倍出图：贴图 3px = 屏幕 1px）。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyChip_Kick.png        288x144 = 屏幕  96x48  —— 「踢出」子背景 · 常态（2 字 / 字号 30，与 MatchWait 的取消键同档）
#   LobbyChip_KickHover.png   288x144                —— 悬停
#   LobbyChip_Start.png       636x192 = 屏幕 212x64  —— 「开始游戏」子背景 · 常态（4 字 / 字号 42）
#   LobbyChip_StartHover.png  636x192                —— 悬停
# 尺寸不是 2 的幂 -> TextureImportSettingsGuard 的 lobby-ui-v1 规则里已把 LobbyChip_ 前缀加进白名单。
#
# 预览：Tools/cardframe/preview/lobby-room-chips.png（1:1 实尺 + 2 倍放大）

Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$S       = 3                        # 出图倍率：贴图 3px = 屏幕 1px
$LW_INK  = 9                        # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3                        # 贴图 px：金细线（屏幕 1）
$INS     = 18                       # 贴图 px：金线内缩（屏幕 6，与 MatchWait 按钮底同档）
$R       = 22                       # 贴图 px：圆角
$A_GOLD  = 150                      # 金线 α（常态）

# ── 子背景：平底竖渐变 + 左上亮楔 + 外墨边 + 等比内缩金细线（同 New-MwPlate）──
function New-ChipPlate([string]$out, [int]$w, [int]$h, [switch]$hover) {
  $cTop = $BAR_T; $cBot = $BAR_B; $cGold = $GOLD; $aWedge = 64; $aG = $A_GOLD
  if ($hover) {
    $cTop = (Mix-Col $BAR_T $HILITE 0.12); $cBot = (Mix-Col $BAR_B $BAR_T 0.40)
    $cGold = $GOLD_L; $aWedge = 96; $aG = [Math]::Min(255, $A_GOLD + 86)
  }
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($w - $LW_INK) ($h - $LW_INK) $R
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $w $h $cTop $cBot 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($w * 0.55), 0)),
    (New-Object System.Drawing.PointF(0, ($h * 0.52))))) $cTop $aWedge
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $ix = $oi + $INS
  $inner = New-RoundPath $ix $ix ($w - 2 * $ix) ($h - 2 * $ix) ([Math]::Max(2.0, $R - $INS))
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aG)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

function Get-ChipFont([single]$px) {
  if ($script:CHFC -eq $null) {
    $script:CHFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:CHFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:CHFC.Families[0], $px, [System.Drawing.GraphicsUnit]::Pixel))
}

function Put-ChipTxt($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 200) {
  $f = Get-ChipFont $px; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L $a)
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}

# ── 预览：1:1 实尺 + 2 倍放大 ─────────────────────────────────────────────
function New-ChipSheet([string]$dir, [string]$out) {
  $CW = 1120; $CH = 470
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bgPath = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
  if (Test-Path $bgPath) { $img = [System.Drawing.Image]::FromFile($bgPath); $g.DrawImage($img, 0, 0, $CW, $CH); $img.Dispose() }
  else { Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255 }

  Put-ChipTxt $g '房间两个文字按钮的子背景（1:1 实尺 / 下面一行 2 倍放大）' 40 22 24 236

  # 1:1（屏幕尺寸）
  $pairs = @(
    @('LobbyChip_Kick.png',  'LobbyChip_KickHover.png',  96, 48,  '踢出'),
    @('LobbyChip_Start.png', 'LobbyChip_StartHover.png', 212, 64, '开始游戏')
  )
  $x = 60.0; $y = 74.0
  foreach ($p in $pairs) {
    foreach ($n in @($p[0], $p[1])) {
      $img = [System.Drawing.Image]::FromFile((Join-Path $dir $n))
      $g.DrawImage($img, [single]$x, [single]$y, [single]$p[2], [single]$p[3]); $img.Dispose()
      $f = Get-ChipFont $p[4].Length
      # 文字叠在子背景上，位置按屏幕字号（与场景里 TMP 的字号一致）
      $fsz = if ($p[4].Length -gt 2) { 42 } else { 30 }
      $font = Get-ChipFont $fsz
      $sf = $g.MeasureString($p[4], $font)
      $br = New-Object System.Drawing.SolidBrush (New-Col @(240, 232, 210) 245)
      $g.DrawString($p[4], $font, $br, [single]($x + ($p[2] - $sf.Width) / 2.0), [single]($y + ($p[3] - $sf.Height) / 2.0 + 2))
      $br.Dispose(); $font.Dispose(); $f.Dispose()
      $x += $p[2] + 34
    }
    $y += $p[3] + 26
  }

  # 2 倍放大
  $x = 60.0; $y = 232.0
  foreach ($p in $pairs) {
    $n = $p[0]
    $img = [System.Drawing.Image]::FromFile((Join-Path $dir $n))
    $g.DrawImage($img, [single]$x, [single]$y, [single]($p[2] * 2), [single]($p[3] * 2)); $img.Dispose()
    $f = Get-ChipFont 20
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 190)
    $g.DrawString(($p[4] + '  常态 · 2x'), $f, $br, [single]$x, [single]($y + $p[3] * 2 + 6)); $br.Dispose(); $f.Dispose()
    $x += $p[2] * 2 + 60
    $img = [System.Drawing.Image]::FromFile((Join-Path $dir $p[1]))
    $g.DrawImage($img, [single]$x, [single]$y, [single]($p[2] * 2), [single]($p[3] * 2)); $img.Dispose()
    $f = Get-ChipFont 20
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 190)
    $g.DrawString('悬停 · 2x', $f, $br, [single]$x, [single]($y + $p[3] * 2 + 6)); $br.Dispose(); $f.Dispose()
    $x += $p[2] * 2 + 60
  }

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
foreach ($c in @(@('LobbyChip_Kick', 96, 48), @('LobbyChip_Start', 212, 64))) {
  $made += (New-ChipPlate (Join-Path $GEN ($c[0] + '.png'))      ($c[1] * $S) ($c[2] * $S))
  $made += (New-ChipPlate (Join-Path $GEN ($c[0] + 'Hover.png')) ($c[1] * $S) ($c[2] * $S) -hover)
}
$sheet = New-ChipSheet $GEN (Join-Path $PREV 'lobby-room-chips.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
