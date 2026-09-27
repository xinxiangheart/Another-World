# 好友详情 · 「申请列表 / 黑名单」两格的动作徽章 v1（2026-09-27）
#
# 用户 2026-09-27：「申请列表和黑名单一起做，展示申请加好友的列表（均在右下角类似限制50）只在右边显示不同，
#   申请列表有一个勾和叉的ui图案用于同意和申请，黑名单则只有一个取消拉黑的」。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   Icon_FriendActAccept.png   / Hover   256x256 —— 「同意」（勾）
#   Icon_FriendActRefuse.png   / Hover   256x256 —— 「拒绝」（叉）
#   Icon_FriendActUnblock.png  / Hover   256x256 —— 「取消拉黑」（禁止符那个圆 + 勾）
# 配方与 Icon_FriendActBlock / Icon_FriendActDelete 同一支笔：圆角方印 + 外墨边 9 + 等比内缩金细线
#   （inset 140 / 线宽 6）+ 圆头金笔画（GLW 13），只换笔画。256 是 2 的幂，不吃 npot 那条；
#   三枚都要 alphaIsTransparency（TextureImportSettingsGuard）。
#
# 「取消拉黑」为什么用「圆 + 勾」：拉黑那颗是「圆 + 斜杠」，取消就是把那道斜杠换成勾 ——
#   同一枚圆环，正反对照，44px 下不会和垃圾桶 / 勾 / 叉混。
#
# 预览：preview/lobby-friend-request.png（三枚徽章 x 常态/悬停）
#       preview/lobby-friend-request-mock.png / lobby-friend-block-mock.png（1920x1080 真坐标）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
$CMN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 版式（屏幕 px · 1920x1080）—— C# 那边（LobbyUIBuilder 的 Fp* 常量）照抄同一组数 ──
$PAD_X      = 64
$LINE_TOP_Y = 156
$BOX_L      = 428
$BOX_R      = 1920 - $PAD_X          # 1856
$LIST_PADX  = 32
$LIST_TOP   = 124
$LIST_BOT   = 60
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
$STATUS_R   = 1172
$STATUS_W   = 260
$STATUS_FS  = 22
$ACT_SZ     = 44
$ACT_GAP    = 12
$ACT_MAIN_X = $ROW_W - 20 - $ACT_SZ            # 1296 —— 主动作（拒绝 / 取消拉黑）
$ACT_SUB_X  = $ACT_MAIN_X - $ACT_SZ - $ACT_GAP # 1240 —— 次动作（同意）
$ACT_Y      = -26
$COUNT_FS   = 22

$GLW = 13

function Get-PrevFont([single]$px) {
  if ($script:PVFC -eq $null) {
    $script:PVFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PVFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:PVFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel))
}

# ── 徽章：照抄 Icon_FriendActBlock / Delete 的方印配方，只换笔画 ──
$TONE_N = @{ top = $BAR_T; bot = $BAR_B; gold = $GOLD;   bright = $GOLD_L; boost = 0 }
$TONE_H = @{ top = (Mix-Col $BAR_T $HILITE 0.12); bot = (Mix-Col $BAR_B $BAR_T 0.40); gold = $GOLD_L; bright = (Mix-Col $GOLD_L $HILITE 0.35); boost = 40 }

function New-PanelActBadge([string]$out, [string]$kind, [bool]$hover) {
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

  if ($kind -eq 'accept') {
    # 勾：一笔折线（44px 下三段的折角比两段直勾清楚）
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddLine(92, 134, 118, 160)
    $p.AddLine(118, 160, 166, 96)
    $g.DrawPath($pen, $p); $p.Dispose()
  } elseif ($kind -eq 'refuse') {
    # 叉：两条对角线
    $g.DrawLine($pen, 96, 96, 160, 160)
    $g.DrawLine($pen, 160, 96, 96, 160)
  } elseif ($kind -eq 'unblock') {
    # 取消拉黑：拉黑那颗的圆环 + 圈里的勾（把斜杠换成勾）
    $penC = New-Object System.Drawing.Pen ((New-Col $t['gold'] $ga)), $GLW
    $penC.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penC.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawEllipse($penC, 128 - 42, 128 - 42, 84, 84); $penC.Dispose()
    $penT = New-Object System.Drawing.Pen ((New-Col $t['gold'] $ga)), 11
    $penT.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $penT.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penT.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddLine(108, 130, 122, 146)
    $p.AddLine(122, 146, 150, 110)
    $g.DrawPath($penT, $p); $p.Dispose(); $penT.Dispose()
  } else { throw "New-PanelActBadge: 没有这个 kind '$kind'" }

  $pen.Dispose(); $g.Dispose()
  return (Save-Bmp $b $g $out)
}

# ── 预览一：三枚徽章 x 常态/悬停 ──
function New-ActSheet([string]$dir, [string]$out) {
  $CW = 1200; $CH = 460
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  Fill-VGrad $g 0 0 $CW $CH (Mix-Col $INK $BAR_B 0.35) (Mix-Col $INK $BAR_B 0.35) 255 255

  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友详情 · 申请列表 / 黑名单 的动作徽章（256 · 屏幕 44x44）', $f, $br, 40, 20); $br.Dispose(); $f.Dispose()
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('同意（勾） / 拒绝（叉） / 取消拉黑（圆 + 勾，与拉黑那颗圆 + 斜杠正反对照）—— 每枚都是「圆角方印 + 外墨边 + 内缩金细线 + 圆头金笔画」', $f2, $br2, 40, 54)
  $g.DrawString('四格动作右对齐：主动作（拒绝 / 取消拉黑）x1296，次动作（同意）x1240；常态暗金 -> 悬停亮金', $f2, $br2, 40, 82)
  $br2.Dispose(); $f2.Dispose()

  $names = @(@('Icon_FriendActAccept.png',  '同意 常态'), @('Icon_FriendActAcceptHover.png',  '同意 悬停'),
             @('Icon_FriendActRefuse.png',  '拒绝 常态'), @('Icon_FriendActRefuseHover.png', '拒绝 悬停'),
             @('Icon_FriendActUnblock.png', '取消拉黑 常态'), @('Icon_FriendActUnblockHover.png','取消拉黑 悬停'))
  $x = 60; $f3 = Get-PrevFont 16; $br3 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
  foreach ($n in $names) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $n[0]))
    $g.DrawImage($im, $x, 150, 152, 152); $im.Dispose()
    $sz = $g.MeasureString($n[1], $f3)
    $g.DrawString($n[1], $f3, $br3, ($x + (152 - $sz.Width) / 2), 316)
    $x += 190
  }
  $br3.Dispose(); $f3.Dispose()
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 预览二：两个面板的真坐标 mock（右侧大格子） ──
function New-PanelMock([string]$dir, [string]$out, [string]$mode) {
  $W = 1920; $H = 1080
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $bg = [System.Drawing.Image]::FromFile($CMN)
  $g.DrawImage($bg, 0, 0, $W, $H); $bg.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawLine($pen, $PAD_X, $LINE_TOP_Y, ($W - $PAD_X), $LINE_TOP_Y)
  $g.DrawLine($pen, $BOX_L, $LINE_TOP_Y, $BOX_L, (1080 - $PAD_X))
  $pen.Dispose()

  $title = $(if ($mode -eq 'block') { '黑名单' } else { '申请列表' })
  $f = Get-PrevFont 44; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255)
  $g.DrawString($title, $f, $br, ($BOX_L + 40), ($LINE_TOP_Y + 40)); $br.Dispose(); $f.Dispose()

  if ($mode -eq 'block') {
    $names  = @('夜行人', '离线丁')
    $ids    = @('P00-20260927-5-8QWPLX-C', 'P00-20260927-6-9EADF7S-Z')
    $stats  = @(@('离线', 142, 162, 180), @('离线', 142, 162, 180))
  } else {
    $names  = @('星野', '雾岛听风', '未知玩家')
    $ids    = @('P00-20260927-4-K3M79QX-8', 'P00-20260927-9-08QWPLX-C', 'P00-20260927-8-00004CT-5')
    $stats  = @(@('在线', 116, 176, 138), @('离线', 142, 162, 180), @('离线', 142, 162, 180))
  }
  $n = $names.Count

  $plate = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyFriendRow.png'))
  $acc   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendActAccept.png'))
  $ref   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendActRefuse.png'))
  $unb   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_FriendActUnblock.png'))

  for ($i = 0; $i -lt $n; $i++) {
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

    $fi = Get-PrevFont $ID_FS; $bri = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 190)
    $g.DrawString($ids[$i], $fi, $bri, [single]($rowL + $ID_X), [single]($cy - 11)); $bri.Dispose(); $fi.Dispose()

    $fs = Get-PrevFont $STATUS_FS; $brs = New-Object System.Drawing.SolidBrush (New-Col @($stats[$i][1], $stats[$i][2], $stats[$i][3]) 236)
    $sz = $g.MeasureString($stats[$i][0], $fs)
    $g.DrawString($stats[$i][0], $fs, $brs, [single]($rowL + $STATUS_R - $sz.Width), [single]($cy - 14)); $brs.Dispose(); $fs.Dispose()

    $ay = $cy - $ACT_SZ / 2.0
    if ($mode -eq 'block') {
      $g.DrawImage($unb, [single]($rowL + $ACT_MAIN_X), [single]$ay, $ACT_SZ, $ACT_SZ)
    } else {
      $g.DrawImage($acc, [single]($rowL + $ACT_SUB_X),  [single]$ay, $ACT_SZ, $ACT_SZ)
      $g.DrawImage($ref, [single]($rowL + $ACT_MAIN_X), [single]$ay, $ACT_SZ, $ACT_SZ)
    }
  }
  $plate.Dispose(); $acc.Dispose(); $ref.Dispose(); $unb.Dispose()

  $fc = Get-PrevFont $COUNT_FS; $brc = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 200)
  $t = "$n/50"
  $tw = $g.MeasureString($t, $fc)
  $g.DrawString($t, $fc, $brc, [single]($BOX_R - $LIST_PADX - $tw.Width), [single](1080 - $PAD_X - 48))
  $brc.Dispose(); $fc.Dispose()

  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
$made += (New-PanelActBadge (Join-Path $GEN 'Icon_FriendActAccept.png')   'accept'  $false)
$made += (New-PanelActBadge (Join-Path $GEN 'Icon_FriendActAcceptHover.png') 'accept' $true)
$made += (New-PanelActBadge (Join-Path $GEN 'Icon_FriendActRefuse.png')   'refuse'  $false)
$made += (New-PanelActBadge (Join-Path $GEN 'Icon_FriendActRefuseHover.png') 'refuse' $true)
$made += (New-PanelActBadge (Join-Path $GEN 'Icon_FriendActUnblock.png')  'unblock' $false)
$made += (New-PanelActBadge (Join-Path $GEN 'Icon_FriendActUnblockHover.png') 'unblock' $true)
$sheet = New-ActSheet $GEN (Join-Path $PREV 'lobby-friend-request.png')
$m1 = New-PanelMock $GEN (Join-Path $PREV 'lobby-friend-request-mock.png') 'request'
$m2 = New-PanelMock $GEN (Join-Path $PREV 'lobby-friend-block-mock.png')   'block'
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
"mock   : $m1"
"mock   : $m2"
