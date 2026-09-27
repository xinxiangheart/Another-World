# 好友详情 · 好友列表的行 v1（2026-09-27）
#
# 用户 2026-09-27：「好友列表……每个好友有个独立的长矩形子背景，从左到右分别是头像，名称，id（这个字体小一点），
#   然后中间可以留空，右边分别是当前状态（在线/离线什么的），拉黑，删除，（仅在在线状态下）邀请，拉黑删除和邀请
#   都是小ui图案代替文字，每个好友之间是有一点间隔，和底框也有间隔，右下角是以类似23/50小字这种形式展示好友数量」。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyFriendRow.png        1376x112 —— 行底板（可见板身 1360x96 + 每边 8 透明边）
#   Icon_FriendActBlock.png   / Hover   256x256 —— 「拉黑」（人影 + 斜杠）
#   Icon_FriendActDelete.png  / Hover   256x256 —— 「删除」（垃圾桶）
#   （「邀请」复用已有的 Icon_FriendPlus*.png —— 这套里「+」就是邀请，与好友侧边栏同源）
#
# 两条口径：
#   1 **行底板存成 1:1（贴图 px = 屏幕 px），不是全族那个 x3**。理由：行宽 1360 屏幕 px，x3 就是 4080 ——
#     超过导入器默认的 maxTextureSize 2048，Unity 会**静默**缩到 2048（本族其它件的账都按「贴图/3」算，
#     这里一旦被缩就全错）。所以**在脚本里按 x3 画、再高质量降采样到 1:1**：抗锯齿照样有，贴图小、也不会被缩。
#     → 场景那边 RawImage 的 sizeDelta 直接 = 贴图尺寸（1376x112），**不除 3**。
#   2 徽章照抄 Icon_FriendPlus / Icon_Close 的「圆角方印 + 内缩金细线 + 圆头金笔画」，只换笔画。
#
# 导入白名单（Assets/_Game/Editor/TextureImportSettingsGuard.cs）：LobbyFriendRow 要 npotScale=None +
#   alphaIsTransparency；Icon_FriendAct* 要 alphaIsTransparency（256 是 2 的幂，不吃 npot 那条）。
#
# 预览：Tools/cardframe/preview/lobby-friend-row.png（底板 + 两枚徽章）
#       Tools/cardframe/preview/lobby-friend-row-mock.png（1920x1080 真坐标：右侧大格子里的好友列表）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
$CMN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 版式（屏幕 px · 1920x1080）—— C# 那边（LobbyUIBuilder 的 FdRow* / FdAct* 常量）照抄同一组数 ──
$PAD_X      = 64
$LINE_TOP_Y = 156
$BOX_L      = 428
$BOX_R      = 1920 - $PAD_X        # 1856
$BOX_W      = $BOX_R - $BOX_L      # 1428
$LIST_PADX  = 32
$LIST_TOP   = 124                  # 列表上沿距大格子上沿
$LIST_BOT   = 60                   # 列表下沿距大格子下沿（给右下角那行「n/50」留位）

$ROW_W      = 1360
$ROW_H      = 96
$ROW_GAP    = 12
$RING_X     = 14
$RING_SZ    = 64
$AVA_INSET  = 7
$AVA_SZ     = 50
$NAME_X     = 96
$NAME_W     = 260
$NAME_FS    = 26
$ID_X       = 364
$ID_W       = 336
$ID_FS      = 20
$STATUS_R   = 1172                 # 状态字右沿
$STATUS_W   = 260
$STATUS_FS  = 22
$ACT_SZ     = 44
$ACT_GAP    = 12
$ACT_INV_X  = $ROW_W - 20 - $ACT_SZ          # 1296
$ACT_DEL_X  = $ACT_INV_X - $ACT_SZ - $ACT_GAP   # 1240
$ACT_BLK_X  = $ACT_DEL_X - $ACT_SZ - $ACT_GAP   # 1184
$ACT_Y      = -26                  # 44 高的盒在 96 高的行里竖直居中
$COUNT_FS   = 22

# ── 出图尺度 ──
$S = 3
$LW_INK  = 9
$LW_GOLD = 3
$PAD     = 24                      # 贴图 px（x3 尺度下）：板身之外多留一圈
$ROW_R   = 30                      # 板角半径（贴图 px，x3）
$GLW     = 13                      # 徽章笔画宽（同 Icon_FriendTab* 那套）

function Get-PrevFont([single]$px) {
  if ($script:PVFC -eq $null) {
    $script:PVFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PVFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:PVFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel))
}

function Save-BmpDown([System.Drawing.Bitmap]$big, [int]$dw, [int]$dh, [string]$out) {
  $res = New-Bmp $dw $dh; $b = $res[0]; $g = $res[1]
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingMode   = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  $g.DrawImage($big, (New-Object System.Drawing.Rectangle 0, 0, $dw, $dh))
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 行底板：配方同入口板（平底竖渐变 + 左上亮楔 + 外墨边 + 等比内缩金线），按 x3 画再降到 1:1 ──
function New-FriendRowPlate([string]$out) {
  $wt = $ROW_W * $S; $ht = $ROW_H * $S
  $srcW = [int]($wt + 2 * $PAD); $srcH = [int]($ht + 2 * $PAD)
  $res = New-Bmp $srcW $srcH; $b = $res[0]; $g = $res[1]
  $g.TranslateTransform($PAD, $PAD)

  $outer = New-RoundPath 5 5 ($wt - 10) ($ht - 10) $ROW_R
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $BAR_T $BAR_B 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($wt * 0.62), 0)),
    (New-Object System.Drawing.PointF(0, ($ht * 0.86))))) $BAR_T 74

  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()

  $INS = 24
  $inner = New-RoundPath (5 + $INS) (5 + $INS) ($wt - 10 - 2 * $INS) ($ht - 10 - 2 * $INS) ($ROW_R - $INS)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $outer.Dispose()
  $g.Dispose()

  return (Save-BmpDown $b ($srcW / $S) ($srcH / $S) $out)
}

# ── 徽章：照抄 Icon_FriendPlus 的方印配方，只换笔画 ──
$TONE_N = @{ top = $BAR_T; bot = $BAR_B; gold = $GOLD;   bright = $GOLD_L; boost = 0 }
$TONE_H = @{ top = (Mix-Col $BAR_T $HILITE 0.12); bot = (Mix-Col $BAR_B $BAR_T 0.40); gold = $GOLD_L; bright = (Mix-Col $GOLD_L $HILITE 0.35); boost = 40 }

function New-FriendActBadge([string]$out, [string]$kind, [bool]$hover) {
  $t = $(if ($hover) { $TONE_H } else { $TONE_N })
  $res = New-Bmp 256 256; $b = $res[0]; $g = $res[1]
  $ga = [Math]::Min(255, 235 + $t['boost'])

  $body = New-RoundPath 44 44 168 168 20
  $st = $g.Save(); $g.SetClip($body)
  $bd = $body.GetBounds()
  Fill-VGrad $g $bd.X $bd.Y $bd.Width $bd.Height $t['top'] $t['bot'] 255 255
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 205)), 9
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $body); $pen.Dispose()

  $inner = New-RoundPath 58 58 140 140 14
  $pen = New-Object System.Drawing.Pen ((New-Col $t['gold'] $ga)), 6
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $body.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $t['gold'] $ga)), $GLW
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

  if ($kind -eq 'block') {
    # 禁止符：圆 + 斜杠。44px 下「人影 + 斜杠」会糊成一团（实测），圆环斜杠这个尺寸唯一无歧义。
    # 斜杠先用「板面中间色」冲掉一条缝，再画金线 —— 斜杠与圆环交界处才有那道标准缺口。
    $g.DrawEllipse($pen, 128 - 42, 128 - 42, 84, 84)
    $gap = New-Object System.Drawing.Pen ((New-Col (Mix-Col $BAR_T $BAR_B 0.5) 255)), ($GLW + 16)
    $gap.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $gap.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($gap, 94, 162, 162, 94); $gap.Dispose()
    $g.DrawLine($pen, 94, 162, 162, 94)
  } elseif ($kind -eq 'delete') {
    # 垃圾桶：盖 + 提手 + 桶身（上宽下窄的三段线）+ 两条内竖线
    $g.DrawLine($pen, 86, 86, 170, 86)
    $g.DrawLine($pen, 114, 70, 142, 70)
    $bodyPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bodyPath.AddLine(98, 102, 106, 176)
    $bodyPath.AddLine(106, 176, 146, 176)
    $bodyPath.AddLine(146, 176, 154, 102)
    $g.DrawPath($pen, $bodyPath); $bodyPath.Dispose()
    $g.DrawLine($pen, 120, 120, 120, 160)
    $g.DrawLine($pen, 136, 120, 136, 160)
  } else { throw "New-FriendActBadge: 没有这个 kind '$kind'" }

  $pen.Dispose(); $g.Dispose()
  return (Save-Bmp $b $g $out)
}

# 预览一：底板（1:1 可见区 + 2x）+ 两枚徽章三档
$ROW_FILE = 'LobbyFriendRow.png'
function New-RowSheet([string]$dir, [string]$out) {
  $CW = 1500; $CH = 620
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  Fill-VGrad $g 0 0 $CW $CH (Mix-Col $INK $BAR_B 0.35) (Mix-Col $INK $BAR_B 0.35) 255 255

  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友详情 · 好友列表的行底板 + 拉黑 / 删除徽章（邀请复用已有的「+」）', $f, $br, 40, 20)
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('底板 1376x112（可见板身 1360x96 + 每边 8 透明边）—— 配方同入口板：平底竖渐变 + 左上亮楔 + 外墨边 9 + 等比内缩金线', $f2, $br2, 40, 54)
  $g.DrawString('底板存成 1:1（不是全族那个 x3）：x3 会到 4080，超过导入器默认 maxTextureSize 2048 被静默缩掉；脚本内按 x3 画再降采样', $f2, $br2, 40, 82)
  $br2.Dispose(); $f2.Dispose()

  $im = [System.Drawing.Image]::FromFile((Join-Path $dir $ROW_FILE))
  $g.DrawImage($im, [System.Drawing.Rectangle]::new(50, 130, 1360, 96), 8, 8, 1360, 96, [System.Drawing.GraphicsUnit]::Pixel)
  $im.Dispose()
  $z = Get-PrevFont 18; $brz = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 200)
  $g.DrawString('1:1（1360x96 可见区）', $z, $brz, 50, 234)
  $brz.Dispose(); $z.Dispose()

  $x = 60.0; $y = 300.0
  foreach ($p in @(@('Icon_FriendActBlock.png','拉黑 常态'), @('Icon_FriendActBlockHover.png','拉黑 悬停'), @('Icon_FriendActDelete.png','删除 常态'), @('Icon_FriendActDeleteHover.png','删除 悬停'), @('Icon_FriendPlus.png','邀请 常态'), @('Icon_FriendPlusHover.png','邀请 悬停'))) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $p[0]))
    $g.DrawImage($im, [single]$x, [single]$y, 66, 66); $im.Dispose()
    $z = Get-PrevFont 17; $brz = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 200)
    $g.DrawString($p[1], $z, $brz, [single]$x, [single]($y + 72)); $brz.Dispose(); $z.Dispose()
    $x += 96
  }
  $f3 = Get-PrevFont 18; $br3 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('徽章 256 贴图 = 屏幕 44x44（与好友侧边栏那颗「+」同尺寸、同配方）', $f3, $br3, 40, 420)
  $g.DrawString('场景：行 1360x96、行距 12；头像环 64 @(14,-16)（井里头像 50 @(21,-23)）；名字 @96 字号 26；id @364 字号 20', $f3, $br3, 40, 452)
  $g.DrawString('状态字右沿 1172 字号 22；三格动作 44x44 @ y-26，x = 1184 / 1240 / 1296（间距 12，右让 20）', $f3, $br3, 40, 484)
  $br3.Dispose(); $f3.Dispose(); $br.Dispose(); $f.Dispose()
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# 预览二：1920x1080 真坐标 —— 右侧大格子里的好友列表（含头像占位 / 名称 / id / 状态 / 三格动作 / n/50）
function New-RowMock([string]$dir, [string]$out) {
  $W = 1920; $H = 1080
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $bg = [System.Drawing.Image]::FromFile($CMN)
  $g.DrawImage($bg, 0, 0, $W, $H); $bg.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawLine($pen, $PAD_X, $LINE_TOP_Y, ($W - $PAD_X), $LINE_TOP_Y)
  $g.DrawLine($pen, $BOX_L, $LINE_TOP_Y, $BOX_L, (1080 - $PAD_X))
  $pen.Dispose()

  $f = Get-PrevFont 44; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255)
  $g.DrawString('好友列表', $f, $br, ($BOX_L + 40), ($LINE_TOP_Y + 40)); $br.Dispose(); $f.Dispose()

  $plate = [System.Drawing.Image]::FromFile((Join-Path $dir $ROW_FILE))
  $blk   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendActBlock.png'))
  $del   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendActDelete.png'))
  $inv   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendPlus.png'))

  $names = @('星野', 'Kite', '阿澜', '雾岛听风', 'Rook', '长夜白')
  $ids   = @('P00-20260927-8-255JHAS-F', 'P00-20260927-8-71KBQQ2-A', 'P00-20260927-9-4MZ0TRD-K', 'P00-20260927-9-08QWPLX-C', 'P00-20261001-1-5HUV3NB-M', 'P00-20261002-2-9EADF7S-Z')
  $stat  = @(@('在线', 116, 176, 138), @('对局中', 228, 203, 132), @('匹配中', 200, 164, 74), @('离线', 142, 162, 180), @('离线', 142, 162, 180), @('离线', 142, 162, 180))
  $online = @($true, $true, $true, $false, $false, $false)

  for ($i = 0; $i -lt 6; $i++) {
    $rowTop = $LINE_TOP_Y + $LIST_TOP + ($ROW_H + $ROW_GAP) * $i
    $rowL   = $BOX_L + $LIST_PADX
    $g.DrawImage($plate, [System.Drawing.Rectangle]::new($rowL, $rowTop, 1360, 96), 8, 8, 1360, 96, [System.Drawing.GraphicsUnit]::Pixel)

    # 头像环 + 灰盘
    $cx = $rowL + $RING_X + $RING_SZ / 2.0; $cy = $rowTop + $ROW_H / 2.0
    $brp = New-Object System.Drawing.SolidBrush (New-Col @(58, 70, 88) 255)
    $g.FillEllipse($brp, [single]($cx - $AVA_SZ / 2.0), [single]($cy - $AVA_SZ / 2.0), $AVA_SZ, $AVA_SZ); $brp.Dispose()
    $penr = New-Object System.Drawing.Pen ((New-Col $GOLD 220)), 2
    $g.DrawEllipse($penr, [single]($cx - $RING_SZ / 2.0), [single]($cy - $RING_SZ / 2.0), $RING_SZ, $RING_SZ); $penr.Dispose()

    $fn = Get-PrevFont $NAME_FS; $brn = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 236)
    $g.DrawString($names[$i], $fn, $brn, [single]($rowL + $NAME_X), [single]($cy - 14))
    $brn.Dispose(); $fn.Dispose()
    $fi = Get-PrevFont $ID_FS; $bri = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 190)
    $g.DrawString($ids[$i], $fi, $bri, [single]($rowL + $ID_X), [single]($cy - 11))
    $bri.Dispose(); $fi.Dispose()

    $fs = Get-PrevFont $STATUS_FS; $brs = New-Object System.Drawing.SolidBrush (New-Col @($stat[$i][1], $stat[$i][2], $stat[$i][3]) 236)
    $sz = $g.MeasureString($stat[$i][0], $fs)
    $g.DrawString($stat[$i][0], $fs, $brs, [single]($rowL + $STATUS_R - $sz.Width), [single]($cy - 14))
    $brs.Dispose(); $fs.Dispose()

    $ay = $cy - $ACT_SZ / 2.0
    $g.DrawImage($blk, [single]($rowL + $ACT_BLK_X), [single]$ay, $ACT_SZ, $ACT_SZ)
    $g.DrawImage($del, [single]($rowL + $ACT_DEL_X), [single]$ay, $ACT_SZ, $ACT_SZ)
    if ($online[$i]) { $g.DrawImage($inv, [single]($rowL + $ACT_INV_X), [single]$ay, $ACT_SZ, $ACT_SZ) }
  }
  $plate.Dispose(); $blk.Dispose(); $del.Dispose(); $inv.Dispose()

  $fc = Get-PrevFont $COUNT_FS; $brc = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 200)
  $t = '23/50'
  $tw = $g.MeasureString($t, $fc)
  $g.DrawString($t, $fc, $brc, [single]($BOX_R - $LIST_PADX - $tw.Width), [single](1080 - $PAD_X - 48))
  $brc.Dispose(); $fc.Dispose()

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
$made += (New-FriendRowPlate (Join-Path $GEN $ROW_FILE))
$made += (New-FriendActBadge (Join-Path $GEN 'Icon_FriendActBlock.png')  'block'  $false)
$made += (New-FriendActBadge (Join-Path $GEN 'Icon_FriendActBlockHover.png')  'block'  $true)
$made += (New-FriendActBadge (Join-Path $GEN 'Icon_FriendActDelete.png') 'delete' $false)
$made += (New-FriendActBadge (Join-Path $GEN 'Icon_FriendActDeleteHover.png') 'delete' $true)
$sheet = New-RowSheet $GEN (Join-Path $PREV 'lobby-friend-row.png')
$mock  = New-RowMock  $GEN (Join-Path $PREV 'lobby-friend-row-mock.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
"mock   : $mock"
