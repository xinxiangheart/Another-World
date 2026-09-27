# 好友详情 · 「添加好友」那一格的件 v1（2026-09-27）
#
# 用户 2026-09-27：「添加好友，首先是一个长的输入框（框的最右边有个类似于放大镜的ui），输入框可以之间输入
#   id或者昵称（加入右键粘贴id功能），然后会在下方列出所有相关玩家（一般是输入昵称时可以有多个玩家）（也是能滑动的）」。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyFriendAddInput.png   1376x92  —— 输入框那口井的底板（可见井身 1360x76 + 每边 8 透明边）
#   Icon_FriendSearch.png     / Hover   256x256 —— 井右端那枚放大镜徽章
#   （「添加」那颗动作徽章**不另出图**：复用已有的 Icon_FriendPlus*.png —— 这套里「+」就是加好友，
#     与好友侧边栏表头那颗「+」/ 好友行的邀请「+」同源）
#
# 三条口径：
#   1 **井底板存成 1:1（贴图 px = 屏幕 px），不是全族那个 x3** —— 与 LobbyFriendRow / LobbyConfirmPlate 同一条理由：
#     1360 屏幕 px 的 x3 是 4044，超过导入器默认 maxTextureSize 2048，Unity 会**静默**缩到 2048。
#     所以按 x3 画、再高质量降采样到 1:1：抗锯齿照样有，贴图小、也不会被缩。
#   2 井与「结果行底板」的区别只有两处：**底色更暗**（不用 BAR_T/BAR_B，用接近 Ink 的凹井色）+ **没有左上亮楔**。
#     外墨边 / 等比内缩金细线 / 圆角半径与行底板同一套 —— 于是「输入框」和「结果行」是一个族里的两档，
#     既不撞脸也不出戏。
#   3 放大镜徽章照抄 Icon_FriendAct* / Icon_FriendPlus 的「圆角方印 + 内缩金细线 + 圆头金笔画」，只换笔画。
#
# 导入白名单（Assets/_Game/Editor/TextureImportSettingsGuard.cs）：LobbyFriendAddInput 要 npotScale=None +
#   alphaIsTransparency（1376x92 既不是 2 的幂、高又小于 128，两道门都得进）；Icon_FriendSearch 进 alpha 那条。
#
# 预览：Tools/cardframe/preview/lobby-friend-add.png（井 + 徽章三档）
#       Tools/cardframe/preview/lobby-friend-add-mock.png（1920x1080 真坐标：右侧大格子里的「添加好友」）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
$CMN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 版式（屏幕 px · 1920x1080）—— C# 那边（LobbyUIBuilder 的 Fa* 常量）照抄同一组数 ──
$PAD_X      = 64
$LINE_TOP_Y = 156
$BOX_L      = 428
$BOX_R      = 1920 - $PAD_X          # 1856
$BOX_W      = $BOX_R - $BOX_L        # 1428
$BOX_H      = 1080 - $PAD_X - $LINE_TOP_Y   # 860

$IN_PADX    = 32
$IN_TOP     = 124
$IN_H       = 76
$IN_W       = 1360             # 与下面的结果行**同宽同列**（x32 .. x1392）
$MAG_SZ     = 44
$MAG_R      = 16
$LINE_Y     = 224
$LIST_TOP   = 248
$LIST_BOT   = 60
$LIST_PADX  = 32

$ROW_W      = 1360
$ROW_H      = 96
$ROW_GAP    = 12
$RING_X     = 14
$RING_SZ    = 64
$AVA_INSET  = 7
$AVA_SZ     = 50
$NAME_X     = 96
$NAME_W     = 340
$NAME_FS    = 26
$ID_X       = 456
$ID_W       = 480
$ID_FS      = 20
$ACT_SZ     = 44
$ACT_X      = $ROW_W - 20 - $ACT_SZ          # 1296（与好友行那颗「+」同位）
$STATUS_R   = $ACT_X - 24                    # 1272
$STATUS_W   = 240
$STATUS_FS  = 22
$COUNT_FS   = 22

$WELL_T = @(14, 19, 29)      # 凹井：比 BAR_T(30,41,56) 暗一档
$WELL_B = @(8, 12, 19)       # 与 BAR_B(12,17,26) 几乎同档，只压一点

# ── 出图尺度（井底板按 x3 画再降采样到 1:1）──
$S       = 3
$LW_INK  = 8                 # 外墨边（x3 px）= 2.7 屏幕 px
$LW_GOLD = 3                 # 金细线（x3 px）= 1 屏幕 px
$PAD     = 24                # 贴图 px（x3 尺度下）：板身之外多留一圈
$RAD     = 30                # 板角半径（x3 px）= 10 屏幕 px，与行底板同值
$INS     = 24                # 金细线等比内缩（x3 px）= 8 屏幕 px，与行底板同值
$GLW     = 13                # 徽章笔画宽

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

# ── 输入框那口井：配方同行底板（外墨边 + 等比内缩金线），只是底色更暗、没有左上亮楔 ──
function New-AddInputPlate([string]$out) {
  $wt = $IN_W * $S; $ht = $IN_H * $S
  $srcW = [int]($wt + 2 * $PAD); $srcH = [int]($ht + 2 * $PAD)
  $res = New-Bmp $srcW $srcH; $b = $res[0]; $g = $res[1]
  $g.TranslateTransform($PAD, $PAD)

  $outer = New-RoundPath 5 5 ($wt - 10) ($ht - 10) $RAD
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $WELL_T $WELL_B 248 248
  $g.Restore($st)

  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()

  $inner = New-RoundPath (5 + $INS) (5 + $INS) ($wt - 10 - 2 * $INS) ($ht - 10 - 2 * $INS) ($RAD - $INS)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $outer.Dispose()
  $g.Dispose()

  return (Save-BmpDown $b ($srcW / $S) ($srcH / $S) $out)
}

# ── 放大镜徽章：照抄 Icon_FriendAct* 的方印配方，笔画换成「圆 + 斜柄」 ──
$TONE_N = @{ top = $BAR_T; bot = $BAR_B; gold = $GOLD;   bright = $GOLD_L; boost = 0 }
$TONE_H = @{ top = (Mix-Col $BAR_T $HILITE 0.12); bot = (Mix-Col $BAR_B $BAR_T 0.40); gold = $GOLD_L; bright = (Mix-Col $GOLD_L $HILITE 0.35); boost = 40 }

function New-SearchBadge([string]$out, [bool]$hover) {
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

  # 放大镜：镜圈（圆心 114,114 半径 32）+ 右下斜柄（外沿到 174,174）
  $g.DrawEllipse($pen, 82, 82, 64, 64)
  $g.DrawLine($pen, 137, 137, 174, 174)

  $pen.Dispose(); $g.Dispose()
  return (Save-Bmp $b $g $out)
}

# 预览一：井（1:1 可见区）+ 徽章三档
$IN_FILE = 'LobbyFriendAddInput.png'
function New-AddSheet([string]$dir, [string]$out) {
  $CW = 1500; $CH = 460
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  Fill-VGrad $g 0 0 $CW $CH (Mix-Col $INK $BAR_B 0.35) (Mix-Col $INK $BAR_B 0.35) 255 255

  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友详情 · 「添加好友」：输入框那口井 + 右端放大镜徽章', $f, $br, 40, 20)
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('井底板 1376x92（可见井身 1360x76 + 每边 8 透明边）—— 外墨边 8 / 等比内缩金线 3 / 圆角 30（都是 x3 px），存成 1:1', $f2, $br2, 40, 54)
  $g.DrawString('与结果行底板同族：只两处不同 —— 底色更暗（凹井）+ 没有左上亮楔', $f2, $br2, 40, 82)
  $br2.Dispose(); $f2.Dispose()

  $im = [System.Drawing.Image]::FromFile((Join-Path $dir $IN_FILE))
  $g.DrawImage($im, [System.Drawing.Rectangle]::new(50, 124, 1360, 76), 8, 8, 1360, 76, [System.Drawing.GraphicsUnit]::Pixel)
  $im.Dispose()
  $z = Get-PrevFont 18; $brz = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 200)
  $g.DrawString('1:1（1360x76 可见区）', $z, $brz, 50, 216)
  $brz.Dispose(); $z.Dispose()

  $n = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendSearch.png'))
  $h = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendSearchHover.png'))
  $pl = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendPlus.png'))
  $g.DrawImage($n, 50, 258, 128, 128)
  $g.DrawImage($h, 200, 258, 128, 128)
  $g.DrawImage($pl, 350, 258, 128, 128)
  $f3 = Get-PrevFont 16; $br3 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  $g.DrawString('Icon_FriendSearch 常态 256', $f3, $br3, 50, 396)
  $g.DrawString('悬停档', $f3, $br3, 200, 396)
  $g.DrawString('「添加」复用 Icon_FriendPlus（与好友行那颗 + 同源）', $f3, $br3, 350, 396)
  $br3.Dispose(); $f3.Dispose(); $n.Dispose(); $h.Dispose(); $pl.Dispose()

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# 预览二：1920x1080 真坐标 —— 右侧大格子里的「添加好友」（井 + 放大镜 + 三行结果 + 右下角计数）
function New-AddMock([string]$dir, [string]$out) {
  $W = 1920; $H = 1080
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $bg = [System.Drawing.Image]::FromFile($CMN)
  $g.DrawImage($bg, 0, 0, $W, $H); $bg.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawLine($pen, $PAD_X, $LINE_TOP_Y, ($W - $PAD_X), $LINE_TOP_Y)
  $g.DrawLine($pen, $BOX_L, $LINE_TOP_Y, $BOX_L, (1080 - $PAD_X))
  $pen.Dispose()

  $f = Get-PrevFont 44; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255)
  $g.DrawString('添加好友', $f, $br, ($BOX_L + 40), ($LINE_TOP_Y + 40)); $br.Dispose(); $f.Dispose()

  $inL = $BOX_L + $IN_PADX; $inT = $LINE_TOP_Y + $IN_TOP
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir $IN_FILE))
  $g.DrawImage($im, [System.Drawing.Rectangle]::new($inL, $inT, $IN_W, $IN_H), 8, 8, $IN_W, $IN_H, [System.Drawing.GraphicsUnit]::Pixel)
  $im.Dispose()
  $fp = Get-PrevFont 30; $brp = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 205)
  $g.DrawString('输入异界号或昵称', $fp, $brp, ($inL + 24), ($inT + ($IN_H - 40) / 2)); $brp.Dispose(); $fp.Dispose()

  $mg = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendSearch.png'))
  $g.DrawImage($mg, [single]($inL + $IN_W - $MAG_R - $MAG_SZ), [single]($inT + ($IN_H - $MAG_SZ) / 2), $MAG_SZ, $MAG_SZ)
  $mg.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawLine($pen, $inL, ($LINE_TOP_Y + $LINE_Y), ($inL + $IN_W), ($LINE_TOP_Y + $LINE_Y))
  $pen.Dispose()

  $plate = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyFriendRow.png'))
  $plus  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendPlus.png'))

  $names  = @('星野', '雾岛听风', 'Kite', '未知玩家')
  $ids    = @('P00-20260927-4-K3M79QX-8', 'P00-20260927-9-08QWPLX-C', '未绑定异界号', 'P00-20260927-8-00004CT-5')
  $stats  = @(@('可添加', 228, 203, 132), @('已是好友', 142, 162, 180), @('Steam 好友', 142, 162, 180), @('可添加', 228, 203, 132))
  $addable = @($true, $false, $false, $true)

  for ($i = 0; $i -lt 4; $i++) {
    $rowTop = $LINE_TOP_Y + $LIST_TOP + ($ROW_H + $ROW_GAP) * $i
    $rowL   = $BOX_L + $LIST_PADX
    $g.DrawImage($plate, [System.Drawing.Rectangle]::new($rowL, $rowTop, $ROW_W, $ROW_H), 8, 8, $ROW_W, $ROW_H, [System.Drawing.GraphicsUnit]::Pixel)

    $cx = $rowL + $RING_X + $RING_SZ / 2.0; $cy = $rowTop + $ROW_H / 2.0
    $brp = New-Object System.Drawing.SolidBrush (New-Col @(58, 70, 88) 255)
    $g.FillEllipse($brp, [single]($cx - $AVA_SZ / 2.0), [single]($cy - $AVA_SZ / 2.0), $AVA_SZ, $AVA_SZ); $brp.Dispose()
    $penr = New-Object System.Drawing.Pen ((New-Col $GOLD 220)), 2
    $g.DrawEllipse($penr, [single]($cx - $RING_SZ / 2.0), [single]($cy - $RING_SZ / 2.0), $RING_SZ, $RING_SZ); $penr.Dispose()

    $fn = Get-PrevFont $NAME_FS; $brn = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 236)
    $g.DrawString($names[$i], $fn, $brn, [single]($rowL + $NAME_X), [single]($cy - 14)); $brn.Dispose(); $fn.Dispose()

    if ($ids[$i] -ne '') {
      $fi = Get-PrevFont $ID_FS; $bri = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 190)
      $g.DrawString($ids[$i], $fi, $bri, [single]($rowL + $ID_X), [single]($cy - 11)); $bri.Dispose(); $fi.Dispose()
    }

    $fs = Get-PrevFont $STATUS_FS; $brs = New-Object System.Drawing.SolidBrush (New-Col @($stats[$i][1], $stats[$i][2], $stats[$i][3]) 236)
    $sz = $g.MeasureString($stats[$i][0], $fs)
    $g.DrawString($stats[$i][0], $fs, $brs, [single]($rowL + $STATUS_R - $sz.Width), [single]($cy - 14)); $brs.Dispose(); $fs.Dispose()

    if ($addable[$i]) {
      $ay = $cy - $ACT_SZ / 2.0
      $g.DrawImage($plus, [single]($rowL + $ACT_X), [single]$ay, $ACT_SZ, $ACT_SZ)
    }
  }
  $plate.Dispose(); $plus.Dispose()

  $fc = Get-PrevFont $COUNT_FS; $brc = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 200)
  $t = '4 位相关玩家'
  $tw = $g.MeasureString($t, $fc)
  $g.DrawString($t, $fc, $brc, [single]($BOX_R - $LIST_PADX - $tw.Width), [single](1080 - $PAD_X - 48))
  $brc.Dispose(); $fc.Dispose()

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
$made += (New-AddInputPlate (Join-Path $GEN 'LobbyFriendAddInput.png'))
$made += (New-SearchBadge  (Join-Path $GEN 'Icon_FriendSearch.png')      $false)
$made += (New-SearchBadge  (Join-Path $GEN 'Icon_FriendSearchHover.png') $true)
$sheet = New-AddSheet $GEN (Join-Path $PREV 'lobby-friend-add.png')
$mock  = New-AddMock  $GEN (Join-Path $PREV 'lobby-friend-add-mock.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
"mock   : $mock"
