# 好友行「邀请加号」v1（2026-09-27）
#
# 用户 2026-09-27：「好友栏中处于在线（非战斗状态和匹配状态）时其名字右边会出现一个加号，点击后发送邀请
# 并且加号变成 10 秒倒计时」。
#
# 与「通用关闭叉 / 合起的书 / 信封」同族（金线石印）：深蓝黑平底 + 一条金细线 + 内缩一圈金框当那「一处金饰」，
# 中间是金加号。常态 / 悬停两张只差色调（走同一套 Set-IconTone，形体尺寸完全一致 -> 切图不跳位）。
# 线宽按屏幕像素折算：本件 256 贴图、屏幕 52 显示（0.203 倍）→ 金细线 6 贴图 px ≈ 1.22 屏幕 px。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   Icon_InvitePlus.png       256x256 —— 好友行右端那个加号 · 常态
#   Icon_InvitePlusHover.png  256x256 —— 悬停（石面提亮 + 金线 GOLD -> GOLD_L）
#   Icon_InvitePlusCool.png   256x256 —— 倒计时那 10 秒：**同一块板、同一个金框，只是没有加号**
#                                        （加号是烘进贴图的，藏不掉；数字叠在它上面会被加号穿过，所以另出一张空板）
# 尺寸 2 的幂 -> TextureImportSettingsGuard 只需默认的「双线性 + mipmap」，不用进白名单。
#
# 预览：Tools/cardframe/preview/lobby-invite-plus.png（1:1 实尺 + 2x / 3x 放大 + 倒计时叠字）

Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$SIZE = 256

# ── 与 LobbyUIv1.ps1 的图标家族同一套色调 / 笔法 ─────────────────────────
$ICON_INK   = 9      # 贴图 px：形状外墨边
$ICON_GOLD  = 6      # 贴图 px：金细线
$ICON_TONE_N = @{ 'top' = $BAR_T; 'bot' = $BAR_B; 'gold' = $GOLD; 'bright' = $GOLD_L; 'boost' = 0 }
$ICON_TONE_H = @{ 'top' = (Mix-Col $BAR_T $HILITE 0.12); 'bot' = (Mix-Col $BAR_B $BAR_T 0.40); 'gold' = $GOLD_L; 'bright' = (Mix-Col $GOLD_L $HILITE 0.35); 'boost' = 40 }
$ICON_TOP = $ICON_TONE_N['top']; $ICON_BOT = $ICON_TONE_N['bot']
$ICON_GC = $ICON_TONE_N['gold']; $ICON_GB = $ICON_TONE_N['bright']; $ICON_GA = 0

function Set-IconTone([bool]$hover) {
  $tone = $(if ($hover) { $ICON_TONE_H } else { $ICON_TONE_N })
  $script:ICON_TOP = $tone['top']
  $script:ICON_BOT = $tone['bot']
  $script:ICON_GC = $tone['gold']
  $script:ICON_GB = $tone['bright']
  $script:ICON_GA = $tone['boost']
}
function New-IconGold([int]$a = 235) { return (New-Col $ICON_GC ([Math]::Min(255, $a + $ICON_GA))) }
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

# 空板：外板（圆角方）+ 内缩一圈金框 —— 倒计时叠数字用的那张（与加号那张逐像素同形，只少中间那一笔）
function New-PlusPlate($g) {
  $body = New-RoundPath 44 44 168 168 20
  Fill-GlyphBody $g $body
  Stroke-GlyphGold $g (New-RoundPath 58 58 140 140 14)
  $body.Dispose()
}

# 加号：空板 + 中间金加号（圆头，同关闭叉那条笔法）
function New-PlusGlyph($g) {
  New-PlusPlate $g
  $pen = New-Object System.Drawing.Pen (New-IconGold 235), 14
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($pen, 94, 128, 162, 128)
  $g.DrawLine($pen, 128, 94, 128, 162)
  $pen.Dispose()
}

function New-PlusIcon([string]$out, [switch]$hover) {
  $res = New-Bmp $SIZE $SIZE; $b = $res[0]; $g = $res[1]
  Set-IconTone($hover.IsPresent)
  New-PlusGlyph $g
  return (Save-Bmp $b $g $out)
}

function New-CoolIcon([string]$out) {
  $res = New-Bmp $SIZE $SIZE; $b = $res[0]; $g = $res[1]
  Set-IconTone($false)
  New-PlusPlate $g
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

function New-PlusSheet([string]$dir, [string]$out) {
  $CW = 1080; $CH = 560
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bgPath = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
  if (Test-Path $bgPath) { $img = [System.Drawing.Image]::FromFile($bgPath); $g.DrawImage($img, 0, 0, $CW, $CH); $img.Dispose() }
  else { Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255 }

  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友行「邀请加号」（1:1 实尺 = 屏幕 60px；下排 2x / 3x 放大）', $f, $br, 40, 20)
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('常态 = 加号 / 悬停 = 石面提亮 / 倒计时那 10 秒 = 同一块空板 + 金数字（Icon_InvitePlusCool）', $f2, $br2, 40, 50)
  $br2.Dispose(); $f2.Dispose()
  $br.Dispose(); $f.Dispose()

  # 1:1 实尺：常态 · 悬停 · 倒计时（空板 + 叠字）
  $x = 60.0; $y = 110.0
  $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlus.png'))
  $g.DrawImage($a, [single]$x, [single]$y, 60, 60); $a.Dispose()
  $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('常态 60px', $z, $br, [single]$x, [single]($y + 66)); $br.Dispose(); $z.Dispose()
  $x += 130
  $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlusHover.png'))
  $g.DrawImage($a, [single]$x, [single]$y, 60, 60); $a.Dispose()
  $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('悬停 60px', $z, $br, [single]$x, [single]($y + 66)); $br.Dispose(); $z.Dispose()
  $x += 130
  # 倒计时叠字（画在空板上，字号 = 场景里那个 26）
  $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlusCool.png'))
  $g.DrawImage($a, [single]$x, [single]$y, 60, 60)
  $cf = Get-PrevFont 26; $sf = $g.MeasureString('10', $cf)
  $cb = New-Object System.Drawing.SolidBrush (New-Col @(228,203,132) 245)
  $g.DrawString('10', $cf, $cb, [single]($x + (60 - $sf.Width) / 2.0), [single]($y + (60 - $sf.Height) / 2.0 + 1))
  $cb.Dispose(); $cf.Dispose(); $a.Dispose()
  $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('倒计时（叠字）', $z, $br, [single]$x, [single]($y + 66)); $br.Dispose(); $z.Dispose()

  # 2x / 3x
  $x = 60.0; $y = 250.0
  foreach ($k in @(2, 3)) {
    $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlus.png'))
    $g.DrawImage($a, [single]$x, [single]$y, [single](60 * $k), [single](60 * $k)); $a.Dispose()
    $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 190)
    $g.DrawString(('常态 ' + $k + 'x'), $z, $br, [single]$x, [single]($y + 60 * $k + 6)); $br.Dispose(); $z.Dispose()
    $x += 60 * $k + 70
    $a = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_InvitePlusHover.png'))
    $g.DrawImage($a, [single]$x, [single]$y, [single](60 * $k), [single](60 * $k)); $a.Dispose()
    $z = Get-PrevFont 20; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 190)
    $g.DrawString(('悬停 ' + $k + 'x'), $z, $br, [single]$x, [single]($y + 60 * $k + 6)); $br.Dispose(); $z.Dispose()
    $x += 60 * $k + 70
  }

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
$made += (New-PlusIcon (Join-Path $GEN 'Icon_InvitePlus.png'))
$made += (New-PlusIcon (Join-Path $GEN 'Icon_InvitePlusHover.png') -hover)
$made += (New-CoolIcon (Join-Path $GEN 'Icon_InvitePlusCool.png'))
$sheet = New-PlusSheet $GEN (Join-Path $PREV 'lobby-invite-plus.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
