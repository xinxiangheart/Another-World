# 好友详情 · 左侧四个 tab（2026-09-27）
#
# 用户 2026-09-27：「左边有四个格子类似于大厅右下角那四个不过更扁平，分别是好友列表，添加好友，申请列表，
#   黑名单，初始进入位于好友列表格子，……四个格子每个格子的内容都将不一样」。
#
# 与右下角那四块（LobbyCornerPlate_*，屏幕 300x120）**同源**：同一个入口板配方 ——
#   平底竖渐变 + 左上亮楔 + 外墨边（LW_INK 9）+ 等比内缩金细线（LW_GOLD 3 / inset 24）+ 圆角。
# 只差两条：
#   1 **更扁平**：340x104（3.27:1），右下角那条是 2.5:1；
#   2 **徽记不烤进板**：徽记另出一张 256 图标（Icon_FriendTab*），运行时只改 alpha 提亮 ——
#     于是「常态 / 悬停 / 选中」三态只要三张板，四格共用；每格换的只是徽记与文字。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyFriendTab.png / LobbyFriendTabHover.png / LobbyFriendTabOn.png   1068x360（屏幕 340x104 + 每边 8 透明边）
#   Icon_FriendTabList / Add / Request / Block.png                        256x256（金线稿，透明底）
# 导入白名单（Assets/_Game/Editor/TextureImportSettingsGuard.cs）：
#   LobbyFriendTab* 要 npotScale=None + alphaIsTransparency；Icon_FriendTab* 要 alphaIsTransparency。
#
# 预览：Tools/cardframe/preview/lobby-friend-tabs.png（三态板 + 四枚徽记）
#       Tools/cardframe/preview/lobby-friend-detail-mock.png（按真场景坐标 1:1 拼的 1920x1080）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
$CMN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

# ── 版式（屏幕 px · 1920x1080）—— C# 那边（LobbyUIBuilder 的 Fd* 常量）照抄同一组数 ──
$PAD_X      = 64      # 左右让边（= SubPanelPadX）
$PAD_BOTTOM = 64
$LINE_TOP_Y = 156     # 横金线距屏幕上沿（叉占 66..126，这条在它下面）
$TAB_W      = 340
$TAB_H      = 104
$LINE_GAP   = 24      # 竖金线到 tab 右沿
$LINE_LEFT_X= $PAD_X + $TAB_W + $LINE_GAP        # 428
$RAIL_H     = 1080 - $LINE_TOP_Y - $PAD_BOTTOM   # 860
$SLOT_H     = $RAIL_H / 4.0                      # 215 —— 四格等分，板在格内居中
$EMBLEM_CX  = 0.755   # 徽记中心：与入口板同位（板宽 75.5% / 板高 50%）

# ── 出图尺度 ──
$S       = 3
$LW_INK  = 9
$LW_GOLD = 3
$PAD     = 24         # 贴图 px：板身之外多留一圈（外墨边 9 + 圆角外扩余量）
$TAB_R   = 26         # 板角半径（贴图）

$TONE = @{
  'n' = @{ top = $BAR_T
           bot = $BAR_B
           gold = $GOLD
           ga = 150
           accent = $false }
  'h' = @{ top = (Mix-Col $BAR_T $HILITE 0.12)
           bot = (Mix-Col $BAR_B $BAR_T 0.40)
           gold = $GOLD_L
           ga = 190
           accent = $false }
  'o' = @{ top = @(($BAR_T[0] + 8), ($BAR_T[1] + 8), ($BAR_T[2] + 8))
           bot = @(($BAR_B[0] + 8), ($BAR_B[1] + 8), ($BAR_B[2] + 8))
           gold = $GOLD_L
           ga = 240
           accent = $true }
}
$TONE_FILE = @{ 'n' = 'LobbyFriendTab.png'; 'h' = 'LobbyFriendTabHover.png'; 'o' = 'LobbyFriendTabOn.png' }

function New-FriendTabPlate([string]$out, [string]$toneKey) {
  $t = $TONE[$toneKey]
  $wt = $TAB_W * $S; $ht = $TAB_H * $S
  $res = New-Bmp ([int]($wt + 2 * $PAD)) ([int]($ht + 2 * $PAD)); $b = $res[0]; $g = $res[1]
  $g.TranslateTransform($PAD, $PAD)
  if ($null -eq $t) { throw "New-FriendTabPlate: 色调表里没有 '$toneKey'" }

  $outer = New-RoundPath 5 5 ($wt - 10) ($ht - 10) $TAB_R
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $t['top'] $t['bot'] 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($wt * 0.62), 0)),
    (New-Object System.Drawing.PointF(0, ($ht * 0.86))))) $t['top'] 74

  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()

  $INS = 24
  $inner = New-RoundPath (5 + $INS) (5 + $INS) ($wt - 10 - 2 * $INS) ($ht - 10 - 2 * $INS) ($TAB_R - $INS)
  $pen = New-Object System.Drawing.Pen ((New-Col $t['gold'] $t['ga'])), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()

  if ($t['accent']) {
    $br = New-Object System.Drawing.SolidBrush (New-Col $t['gold'] 215)
    $g.FillRectangle($br, (5 + $INS + 3), (5 + $INS + 8), 9, ($ht - 10 - 2 * $INS - 16))
    $br.Dispose()
  }

  $outer.Dispose(); $g.Dispose()
  return (Save-Bmp $b $g $out)
}

# ── 徽记（256x256 金线稿，透明底）──────────────────────────────────────────
$GLW = 13
function Add-FriendPerson($g, $pen, [single]$hx, [single]$hy, [single]$r) {
  $g.DrawEllipse($pen, ($hx - $r), ($hy - $r), ($r * 2), ($r * 2))
  $sw = $r * 3.9
  $g.DrawArc($pen, ($hx - $sw / 2), ($hy + $r * 1.15), $sw, ($r * 3.4), 180, 180)
}

function New-FriendTabEmblem([string]$out, [string]$kind) {
  $res = New-Bmp 256 256; $b = $res[0]; $g = $res[1]
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 255)), $GLW
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

  if ($kind -eq 'list') {
    for ($i = 0; $i -lt 3; $i++) {
      $yy = 76 + $i * 52
      $dm = New-Diamond 60 $yy 11
      $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255)
      $g.FillPath($br, $dm); $br.Dispose(); $dm.Dispose()
      $g.DrawLine($pen, 92, $yy, (200 - $i * 24), $yy)
    }
  } elseif ($kind -eq 'add') {
    Add-FriendPerson $g $pen 112 104 31
    $g.DrawLine($pen, (196 - 26), 72, (196 + 26), 72)
    $g.DrawLine($pen, 196, (72 - 26), 196, (72 + 26))
  } elseif ($kind -eq 'request') {
    Add-FriendPerson $g $pen 104 104 31
    $g.DrawLine($pen, 236, 104, 178, 104)
    $g.DrawLine($pen, 178, 104, 200, 84)
    $g.DrawLine($pen, 178, 104, 200, 124)
  } elseif ($kind -eq 'block') {
    # 禁止符：圆 + 斜杠。与好友列表行里那枚「拉黑」徽章**同一形体**（44px 下「人影 + 斜杠」会糊成一团）。
    $g.DrawEllipse($pen, 128 - 42, 128 - 42, 84, 84)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $punch = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0, 0, 0, 0)), ($GLW + 16)
    $punch.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $punch.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($punch, 94, 162, 162, 94); $punch.Dispose()
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.DrawLine($pen, 94, 162, 162, 94)
  }
  $pen.Dispose(); $g.Dispose()
  return (Save-Bmp $b $g $out)
}

$TAB_KINDS  = @('list', 'add', 'request', 'block')
$TAB_FILES  = @('Icon_FriendTabList.png', 'Icon_FriendTabAdd.png', 'Icon_FriendTabRequest.png', 'Icon_FriendTabBlock.png')
$TAB_LABELS = @('好友列表', '添加好友', '申请列表', '黑名单')

function Get-PrevFont([single]$px) {
  if ($script:PVFC -eq $null) {
    $script:PVFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PVFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:PVFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel))
}

# 预览一：三态板 1:1 + 2x、四枚徽记 2x
function New-FriendTabSheet([string]$dir, [string]$out) {
  $CW = 1560; $CH = 700
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255
  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友详情左侧四个 tab：板 1068x360 贴图 = 屏幕 340x104（比右下角那四块 300x120 更扁）', $f, $br, 40, 22)
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('配方与右下角入口条同源：平底竖渐变 + 左上亮楔 + 外墨边 9 + 等比内缩金线（inset 24 / 线宽 3）；徽记另出 256 图标，运行时只改 alpha', $f2, $br2, 40, 56)
  $br2.Dispose(); $f2.Dispose()

  $x = 40.0; $y = 120.0
  foreach ($k in @('n', 'h', 'o')) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $TONE_FILE[$k]))
    $g.DrawImage($im, [single]$x, [single]$y, [single]($im.Width / 3.0), [single]($im.Height / 3.0)); $im.Dispose()
    $z = Get-PrevFont 18; $brz = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 200)
    $tag = $(if ($k -eq 'n') { '常态' } elseif ($k -eq 'h') { '悬停' } else { '选中（提亮一级 + 金线加亮 + 左金条）' })
    $g.DrawString($tag, $z, $brz, [single]$x, [single]($y + 124)); $brz.Dispose(); $z.Dispose()
    $x += 375
  }

  $x = 40.0; $y = 300.0
  for ($i = 0; $i -lt 4; $i++) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $TAB_FILES[$i]))
    $g.DrawImage($im, [single]$x, [single]$y, 128, 128); $im.Dispose()
    $z = Get-PrevFont 18; $brz = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 200)
    $g.DrawString($TAB_LABELS[$i], $z, $brz, [single]$x, [single]($y + 138)); $brz.Dispose(); $z.Dispose()
    $x += 170
  }

  $f3 = Get-PrevFont 18; $br3 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('场景坐标（1920x1080）：四格等分竖栏 64..404，格高 215、板 104 居中；竖金线 x=428 从上到下；横金线 y=156 横跨 64..1856', $f3, $br3, 40, 480)
  $g.DrawString('面板里板身 x=64、格心 y = 156 + 215*i + 107.5；文字距板身左沿 58、徽记中心 = 板宽 75.5% / 板高 50%', $f3, $br3, 40, 512)
  $g.DrawString('徽记 64px 显示（256 贴图）；四枚分别 = 列表 / 人影+加号 / 人影+来向箭头 / 人影+斜杠（斜杠先 SourceCopy 冲掉一条缝再画金线）', $f3, $br3, 40, 544)
  $br3.Dispose(); $f3.Dispose()
  $br.Dispose(); $f.Dispose()
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# 预览二：按真场景坐标 1:1 拼一张 1920x1080
function New-FriendDetailMock([string]$dir, [string]$out) {
  $W = 1920; $H = 1080
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $bg = [System.Drawing.Image]::FromFile($CMN)
  $g.DrawImage($bg, 0, 0, $W, $H); $bg.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawLine($pen, $PAD_X, $LINE_TOP_Y, ($W - $PAD_X), $LINE_TOP_Y)
  $g.DrawLine($pen, $LINE_LEFT_X, $LINE_TOP_Y, $LINE_LEFT_X, (1080 - $PAD_BOTTOM))
  $pen.Dispose()

  $PADS = $PAD / $S
  for ($i = 0; $i -lt 4; $i++) {
    $top = $LINE_TOP_Y + $SLOT_H * $i + ($SLOT_H - $TAB_H) / 2.0
    $file = $(if ($i -eq 0) { $TONE_FILE['o'] } elseif ($i -eq 2) { $TONE_FILE['h'] } else { $TONE_FILE['n'] })
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $file))
    $g.DrawImage($im, [single]($PAD_X - $PADS), [single]($top - $PADS), [single]($TAB_W + 2 * $PADS), [single]($TAB_H + 2 * $PADS)); $im.Dispose()

    $em = [System.Drawing.Image]::FromFile((Join-Path $dir $TAB_FILES[$i]))
    $ec = $TAB_W * $EMBLEM_CX
    $g.DrawImage($em, [single]($PAD_X + $ec - 32), [single]($top + $TAB_H * 0.5 - 32), 64, 64); $em.Dispose()

    $on = ($i -eq 0)
    $f = Get-PrevFont 28
    $col = $(if ($on -or $i -eq 2) { $GOLD_L } else { @(240,232,210) })
    $al  = $(if ($on -or $i -eq 2) { 255 } else { 236 })
    $br = New-Object System.Drawing.SolidBrush (New-Col $col $al)
    $g.DrawString($TAB_LABELS[$i], $f, $br, [single]($PAD_X + 58), [single]($top + $TAB_H * 0.5 - 19))
    $br.Dispose(); $f.Dispose()
  }

  $f = Get-PrevFont 44
  $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255)
  $g.DrawString($TAB_LABELS[0], $f, $br, ($LINE_LEFT_X + 40), ($LINE_TOP_Y + 40))
  $br.Dispose(); $f.Dispose()
  $f = Get-PrevFont 26
  $br = New-Object System.Drawing.SolidBrush (New-Col @(142,162,180) 190)
  $g.DrawString('占位 · 内容待接入（右侧大格子里只有当前选中那一格的内容可见）', $f, $br, ($LINE_LEFT_X + 40), ($LINE_TOP_Y + 116))
  $br.Dispose(); $f.Dispose()

  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'Icon_Close.png'))
  $g.DrawImage($im, ($W - 168), 66, 60, 60); $im.Dispose()

  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
foreach ($k in @('n', 'h', 'o')) {
  $made += (New-FriendTabPlate (Join-Path $GEN $TONE_FILE[$k]) $k)
}
for ($i = 0; $i -lt 4; $i++) {
  $made += (New-FriendTabEmblem (Join-Path $GEN $TAB_FILES[$i]) $TAB_KINDS[$i])
}
$sheet = New-FriendTabSheet $GEN (Join-Path $PREV 'lobby-friend-tabs.png')
$mock  = New-FriendDetailMock $GEN (Join-Path $PREV 'lobby-friend-detail-mock.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
"mock   : $mock"
