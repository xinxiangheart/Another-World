# 「收到邀请」小窗 v1（2026-09-27）
#
# 用户 2026-09-27：「邀请是从屏幕中央上顶滑出一个小框，上面标题是收到邀请，下面一排是对应玩家头像和名称，
#   在下面是有子背景的同意和拒绝」。
#
# 语言与手法同全套 UI（与 MatchWait_Plate 同配方、同一支笔）：深蓝黑石面 + 一条金细线 + 平板；
# 禁倒角 / 内阴影 / 外发光 / 双层面板 / 材质贴图。色值取共享调色板（TopBarV2 -> CardFrameV6）。
#
# 产物（Assets/_Game/Art/Sprites/Generated/invite-v1/）
#   Invite_Plate.png   1080x720 = 屏幕 360x240 —— 小窗底板（固定尺寸，不切片）
#   ---- 尺寸不是 2 的幂：TextureImportSettingsGuard 的 NoNpotScaleFolders 里已含本目录 ----
#
# 版式（屏幕 px，锚屏幕顶中；场景侧在 LobbyUIBuilder.BuildInvitePanelMenu）：
#   上 18 留白 / 标题「收到邀请」38（金，30 号）/ 16 / 头像行 72（环 72 + 井 56 + 名字）/ 26 /
#   同意 + 拒绝两键 96x48（各带子背景 LobbyChip_Kick）/ 下 24 留白
#
# 预览：Tools/cardframe/preview/invite-panel.png（1:1 实尺 + 放大 + 版式标注）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/invite-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$S       = 3                       # 出图倍率：贴图 3px = 屏幕 1px（与 match-wait-v1 / lobby-ui-v1 同口径）
$PLATE_W = 360; $PLATE_H = 240     # 屏幕 px
$BTN_W   = 96;  $BTN_H   = 48      # 子背景两键 = LobbyChip_Kick 同规格

$LW_INK  = 9                       # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3                       # 贴图 px：金细线（屏幕 1）
$INS_P   = 24                      # 贴图 px：金线内缩（屏幕 8）
$R_P     = 30                      # 贴图 px：圆角
$A_P     = 168                     # 金线 alpha

# ── 底板：与 MatchWaitV1.ps1 的 New-MwPlate 逐行同配方（只换尺寸）────────────
function New-InvPlate([string]$out, [int]$w, [int]$h, [single]$rad, [single]$ins, [int]$aGold) {
  $cTop = $BAR_T; $cBot = $BAR_B; $cGold = $GOLD; $aWedge = 64; $aG = $aGold
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($w - $LW_INK) ($h - $LW_INK) $rad
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
  $ix = $oi + $ins
  $inner = New-RoundPath $ix $ix ($w - 2 * $ix) ($h - 2 * $ix) ([Math]::Max(2.0, $rad - $ins))
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aG)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

function Get-IvFont([single]$px) {
  if ($script:IVFC -eq $null) {
    $script:IVFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:IVFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:IVFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel))
}
function Put-IvC($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int]$a = 236) {
  $f = Get-IvFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}
function Put-IvGold($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int]$a = 236) {
  $f = Get-IvFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 200, 164, 74))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}
function Put-IvL($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 176) {
  $f = Get-IvFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}

# 把「一块底板 + 一屏内容」画到给定位置（预览与场景共用同一套相对坐标）
function Draw-IvWindow($g, [string]$dir, [single]$px, [single]$py, [single]$k) {
  $w = $PLATE_W * $k; $h = $PLATE_H * $k
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'Invite_Plate.png'))
  $g.DrawImage($im, $px, $py, $w, $h); $im.Dispose()
  $cx = $px + $w / 2.0
  Put-IvGold $g '收到邀请' $cx ($py + 37 * $k) (30 * $k) 255
  # 头像行：环 + 井 + 名字（场景里环是 LobbyAvatarRing.png，这里为预览画一对同心圆）
  $rx = $px + 48 * $k; $ry = $py + 70 * $k; $rr = 36 * $k
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 236)), (3 * $k)
  $g.DrawEllipse($pen, $rx, $ry, (2 * $rr), (2 * $rr)); $pen.Dispose()
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 60, 70, 86))
  $g.FillEllipse($bs, ($rx + 8 * $k), ($ry + 8 * $k), (2 * $rr - 16 * $k), (2 * $rr - 16 * $k)); $bs.Dispose()
  Put-IvL $g '心响' ($px + 136 * $k) ($py + 92 * $k) (28 * $k) 236
  # 两个子背景键
  foreach ($t in @(@('同意', 56), @('拒绝', 208))) {
    $chip = [System.Drawing.Image]::FromFile((Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/LobbyChip_Kick.png'))
    $g.DrawImage($chip, ($px + [single]$t[1] * $k), ($py + 168 * $k), ($BTN_W * $k), ($BTN_H * $k)); $chip.Dispose()
    Put-IvC $g $t[0] ($px + ([single]$t[1] + $BTN_W / 2.0) * $k) ($py + 192 * $k) (30 * $k) 255
  }
}

function New-IvSheet([string]$dir, [string]$out) {
  $CW = 1180; $CH = 620
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 9, 12, 18))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-IvL $g '收到邀请小窗 v1' 36 20 28 236
  Put-IvL $g '底板 360x240 屏幕 px（贴图 3x = 1080x720）· 锚屏幕顶中 · 深蓝黑石面 + 一条金细线' 36 58 18
  Put-IvL $g '标题「收到邀请」金 30 号 / 头像行（环 72 + 井 56 + 名字 28 号）/ 同意 + 拒绝（子背景 96x48、字 30 号）' 36 84 18
  Put-IvL $g '入场：从屏幕顶滑到位（LobbyInvitePanel），滑动 0.28s 快进慢出' 36 110 18

  Draw-IvWindow $g $dir 56 150 1.0
  Put-IvL $g '① 1:1 实尺（360x240）' 56 398 18 210

  Draw-IvWindow $g $dir 470 140 1.55
  Put-IvL $g '② 1.55x（看金线与留白）' 470 520 18 210

  Put-IvL $g '版式：上 18 / 标题 38 / 16 / 头像行 72 / 26 / 两键 96x48 / 下 24' 56 570 18
  Put-IvL $g '金细线内缩 24 贴图 px（屏幕 8）· 尺寸非 2 的幂 —— 已进 NoNpotScaleFolders' 56 596 18
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
$made += (New-InvPlate (Join-Path $GEN 'Invite_Plate.png') ($PLATE_W * $S) ($PLATE_H * $S) $R_P $INS_P $A_P)
$sheet = New-IvSheet $GEN (Join-Path $PREV 'invite-panel.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
