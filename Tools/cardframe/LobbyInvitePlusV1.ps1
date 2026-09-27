# 好友行「邀请」那一格 v2（2026-09-27）—— 方形 -> **扁平长方形**
#
# 用户 2026-09-27：「要做的更像长方形，更"扁"一点」。
# v1 是 256x256 画布里的 168x168 圆角方（显示出来就是个正方），这版改成**横向长方形**：
#   · 出图口径换成与 LobbyRoomChipV1 完全同一套（屏幕 3 倍、贴图 3px = 屏幕 1px、平底竖渐变 + 左上亮楔
#     + 外墨边 9 + 等比内缩金线 3/18）—— 它俩以后要并排出现在同一个面板里，形体语言必须一致。
#   · 屏幕 **78x48**（1.625:1），与「踢出」子背景同高（48），金线重量 / 圆角 / α 全部照抄。
#     （v1 那种 256 画布内缩的画法，实际显示只有 39x39 见方，缩到 0.2 倍时线宽也不好核。）
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   Icon_InvitePlus.png       234x144 = 屏幕 78x48 —— 常态（金加号）
#   Icon_InvitePlusHover.png  234x144              —— 悬停（石面提亮 + 金线 GOLD -> GOLD_L）
#   Icon_InvitePlusCool.png   234x144              —— 倒计时那 10 秒：**同一块板、同一个金框，只少加号那一笔**
#                                                    （加号是烘进贴图的，藏不掉 -> 另出一张空板，数字叠在上面）
# 尺寸不是 2 的幂 -> TextureImportSettingsGuard 的 lobby-ui-v1 规则里已把 Icon_InvitePlus 前缀加进白名单。
#
# 预览：Tools/cardframe/preview/lobby-invite-plus.png（1:1 实尺 + 2x / 3x + 倒计时叠字）

Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$S       = 3                        # 出图倍率：贴图 3px = 屏幕 1px
$W       = 78 * $S                  # 234 贴图 px = 屏幕 78
$H       = 48 * $S                  # 144 贴图 px = 屏幕 48
$LW_INK  = 9                        # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3                        # 贴图 px：金细线（屏幕 1）
$INS     = 18                       # 贴图 px：金线内缩（屏幕 6，与 MatchWait 按钮底 / LobbyChip 同档）
$R       = 24                       # 贴图 px：圆角（屏幕 8）
$A_GOLD  = 150                      # 金线 α（常态）
$PLUS_ARM = 33                      # 贴图 px：加号半臂长（整条 66 = 屏幕 22）
$PLUS_LW  = 10                      # 贴图 px：加号线宽（屏幕 3.33）

# ── 与 LobbyRoomChipV1 / MatchWait 按钮底同一套：平底竖渐变 + 左上亮楔 + 外墨边 + 等比内缩金细线 ──
function New-InvitePlate($g, [bool]$hover) {
  $cTop = $BAR_T; $cBot = $BAR_B; $cGold = $GOLD; $aWedge = 64; $aG = $A_GOLD
  if ($hover) {
    $cTop = (Mix-Col $BAR_T $HILITE 0.12); $cBot = (Mix-Col $BAR_B $BAR_T 0.40)
    $cGold = $GOLD_L; $aWedge = 96; $aG = [Math]::Min(255, $A_GOLD + 86)
  }
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($W - $LW_INK) ($H - $LW_INK) $R
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $W $H $cTop $cBot 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($W * 0.55), 0)),
    (New-Object System.Drawing.PointF(0, ($H * 0.52))))) $cTop $aWedge
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $ix = $oi + $INS
  $inner = New-RoundPath $ix $ix ($W - 2 * $ix) ($H - 2 * $ix) ([Math]::Max(2.0, $R - $INS))
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aG)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  $outer.Dispose()
}

# 加号：居中、圆头（同关闭叉那条笔法）
function Add-PlusGlyph($g, [bool]$hover) {
  $col = $(if ($hover) { (Mix-Col $GOLD_L $HILITE 0.35) } else { $GOLD_L })
  $pen = New-Object System.Drawing.Pen ((New-Col $col 235)), $PLUS_LW
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $cx = $W / 2.0; $cy = $H / 2.0
  $g.DrawLine($pen, [single]($cx - $PLUS_ARM), [single]$cy, [single]($cx + $PLUS_ARM), [single]$cy)
  $g.DrawLine($pen, [single]$cx, [single]($cy - $PLUS_ARM), [single]$cx, [single]($cy + $PLUS_ARM))
  $pen.Dispose()
}

function New-InviteIcon([string]$out, [switch]$hover) {
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  New-InvitePlate $g $hover.IsPresent
  Add-PlusGlyph $g $hover.IsPresent
  return (Save-Bmp $b $g $out)
}

function New-InviteCool([string]$out) {
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  New-InvitePlate $g $false
  return (Save-Bmp $b $g $out)
}

# ── 预览 ────────────────────────────────────────────────────────────────
function Get-PrevFont([single]$px) {
  if ($script:PVFC -eq $null) {
    $script:PVFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PVFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:PVFC.Families[0], $px, [System.Drawing.GraphicsUnit]::Pixel))
}

function New-InviteSheet([string]$dir, [string]$out) {
  $CW = 1080; $CH = 520
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bgPath = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
  if (Test-Path $bgPath) { $img = [System.Drawing.Image]::FromFile($bgPath); $g.DrawImage($img, 0, 0, $CW, $CH); $img.Dispose() }
  else { Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255 }

  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友行「邀请」那一格（屏幕 78x48 扁平长方形；1:1 实尺 + 2x / 3x 放大）', $f, $br, 40, 20)
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('配方与「踢出 / 开始游戏」子背景同一套（3 倍出图 · 外墨边 3 · 等比内缩金线 1/6 · 左上亮楔）；倒计时 = 同一块空板 + 金数字', $f2, $br2, 40, 50)
  $br2.Dispose(); $f2.Dispose(); $br.Dispose(); $f.Dispose()

  # 1:1 实尺：常态 · 悬停 · 倒计时（空板 + 叠字）
  $x = 60.0; $y = 110.0
  $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlus.png'))
  $g.DrawImage($a, [single]$x, [single]$y, 78, 48); $a.Dispose()
  $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('常态 78x48', $z, $br, [single]$x, [single]($y + 56)); $br.Dispose(); $z.Dispose()
  $x += 130
  $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlusHover.png'))
  $g.DrawImage($a, [single]$x, [single]$y, 78, 48); $a.Dispose()
  $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('悬停', $z, $br, [single]$x, [single]($y + 56)); $br.Dispose(); $z.Dispose()
  $x += 130
  # 倒计时叠字（画在空板上，字号 = 场景里那个 30）
  $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlusCool.png'))
  $g.DrawImage($a, [single]$x, [single]$y, 78, 48)
  $cf = Get-PrevFont 30; $sf = $g.MeasureString('10', $cf)
  $cb = New-Object System.Drawing.SolidBrush (New-Col @(228,203,132) 245)
  $g.DrawString('10', $cf, $cb, [single]($x + (78 - $sf.Width) / 2.0), [single]($y + (48 - $sf.Height) / 2.0 + 2))
  $cb.Dispose(); $cf.Dispose(); $a.Dispose()
  $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('倒计时（叠字）', $z, $br, [single]$x, [single]($y + 56)); $br.Dispose(); $z.Dispose()

  # 2x / 3x
  $x = 60.0; $y = 240.0
  foreach ($k in @(2, 3)) {
    $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlus.png'))
    $g.DrawImage($a, [single]$x, [single]$y, [single](78 * $k), [single](48 * $k)); $a.Dispose()
    $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 190)
    $g.DrawString(('常态 ' + $k + 'x'), $z, $br, [single]$x, [single]($y + 48 * $k + 8)); $br.Dispose(); $z.Dispose()
    $x += 78 * $k + 60
    $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlusHover.png'))
    $g.DrawImage($a, [single]$x, [single]$y, [single](78 * $k), [single](48 * $k)); $a.Dispose()
    $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 190)
    $g.DrawString(('悬停 ' + $k + 'x'), $z, $br, [single]$x, [single]($y + 48 * $k + 8)); $br.Dispose(); $z.Dispose()
    $x += 78 * $k + 60
  }

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
$made += (New-InviteIcon (Join-Path $GEN 'Icon_InvitePlus.png'))
$made += (New-InviteIcon (Join-Path $GEN 'Icon_InvitePlusHover.png') -hover)
$made += (New-InviteCool (Join-Path $GEN 'Icon_InvitePlusCool.png'))
$sheet = New-InviteSheet $GEN (Join-Path $PREV 'lobby-invite-plus.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"