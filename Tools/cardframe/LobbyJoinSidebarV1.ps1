# 「加入房间」右侧侧边栏底板 v1（2026-09-27）
#
# 用户 2026-09-27：「加入房间是一个右侧侧边栏（到顶，但不需要完全到底），范围到右上角的左边，
#   不遮挡房间号，ui 等，再次点击（或者点击范围外）滑动回去」。
# 本条只管外形：板是**浮层**（贴在屏幕右上、不参与视差），形体与好友侧边栏 LobbyFriendPanel /
#   通用面板底同一套配方 —— 平底竖渐变（BAR_T -> BAR_B）+ 外墨边 + 等比内缩金细线；本体不另加装饰。
# 尺寸（2026-09-27 七改 —— 用户「缩到和右上角顶端一样位置不变作为其背景」「好友侧边栏不是一样的
#   要求吗，和右上角最左侧持平」「其它超出组件向右平移到内部」）
#   W538 x H920，3 倍出图 -> 1614x2760。板 = **右上横栏（LobbyBandRight，屏幕 538x95）那一块的背景**
#   —— 与好友侧边栏同一套口径：
#     · 上沿 = 屏幕顶（0，与横栏同顶；横栏画在 Layer_Hud_v1 ⇒ 永远压在板之上）
#     · 左沿 = 横栏左沿（1920 − 538 = 1382）⇒ 板宽 538、右沿贴屏幕右沿（= 横栏右沿）
#     · 下沿让开 160（「不需要完全到底」）
#   板顶开口：左右两条线跑到画面上沿，**只有下方两个圆角**（上封边会被横栏压着看不见，反而会在
#   横栏左端斜切的左侧露出一小截横线 —— 所以不画）。
#   板里的内容位置**没动** —— 场景侧那层 Content 仍钉在屏幕顶下来 126。
# 尺寸不是 2 的幂 -> TextureImportSettingsGuard 的 lobby-ui-v1 白名单已加 LobbyJoin 前缀。
#
# 预览：Tools/cardframe/preview/lobby-join-sidebar.png

Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$S       = 3
$LW_INK  = 9      # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3      # 贴图 px：金细线（屏幕 1）
$INS     = 30     # 贴图 px：金线内缩（屏幕 10）
$R       = 30     # 贴图 px：圆角（屏幕 10）
$A_GOLD  = 150

$SIDE_W = 538
$SIDE_H = 920

# 板顶开口的板形：左右两条竖线从画面上沿（-40）下来，只有**下方两个圆角**（上封边 / 上圆角不画）。
# 线心口径：外墨边线心 (4.5, 4.5)..(w-4.5, h-4.5)，金线心再内缩 INS。
function New-OpenTopPath([single]$x, [single]$x2, [single]$yBottom, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $p.AddLine($x, -40, $x, ($yBottom - $r))
  $p.AddArc($x, ($yBottom - $d), $d, $d, 180, -90)
  $p.AddLine(($x + $r), $yBottom, ($x2 - $r), $yBottom)
  $p.AddArc(($x2 - $d), ($yBottom - $d), $d, $d, 90, -90)
  $p.AddLine($x2, ($yBottom - $r), $x2, -40)
  return $p
}

function New-JoinSidePlate([string]$out, [int]$w, [int]$h) {
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-OpenTopPath $oi ($w - $oi) ($h - $oi) $R
  $fill = [System.Drawing.Drawing2D.GraphicsPath]$outer.Clone()
  $fill.CloseFigure()
  $st = $g.Save(); $g.SetClip($fill)
  Fill-VGrad $g 0 0 $w $h $BAR_T $BAR_B 255 255
  $g.Restore($st); $fill.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
  $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Flat
  $g.DrawPath($pen, $outer); $pen.Dispose()
  # 金细线：与外墨边**等比内缩** INS（线心），两条竖线同样跑到画面上沿。
  $ix = $oi + $INS
  $yIn = $h - $ix
  $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
  $inner.AddLine($ix, -40, $ix, $yIn)
  $inner.AddLine($ix, $yIn, ($w - $ix), $yIn)
  $inner.AddLine(($w - $ix), $yIn, ($w - $ix), -40)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $A_GOLD)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
  $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Flat
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

function New-JoinSideSheet([string]$dir, [string]$out) {
  $CW = 1500; $CH = 1160
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bgPath = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1/CommonBack_A_clean.png'
  if (Test-Path $bgPath) { $img = [System.Drawing.Image]::FromFile($bgPath); $g.DrawImage($img, 0, 0, $CW, $CH); $img.Dispose() }
  else { Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255 }

  $f = New-Object System.Drawing.Font('Microsoft YaHei', 26, [System.Drawing.GraphicsUnit]::Pixel)
  $br = New-Object System.Drawing.SolidBrush (New-Col @(232, 209, 138) 236)
  $g.DrawString('「加入房间」右侧侧边栏底板（1:1 实尺 · 右图画上金细线与文字位置示意）', $f, $br, 40, 24)
  $br.Dispose(); $f.Dispose()

  # 1:1
  $img = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyJoinSidebar.png'))
  $g.DrawImage($img, [single]60, [single]80, [single]$SIDE_W, [single]$SIDE_H); $img.Dispose()

  # 1:1 + 内容示意（标题 / 金线 / 输入框 / 预览）
  $x = 780.0; $y = 80.0
  $img = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyJoinSidebar.png'))
  $g.DrawImage($img, [single]$x, [single]$y, [single]$SIDE_W, [single]$SIDE_H); $img.Dispose()
  # 板顶 = 屏幕顶（0）⇒ 板内偏移 = 屏幕偏移；内容仍钉在屏幕顶下来 126（场景侧那层 Content），一个位置都没动。
  $cy = $y

  $ft = New-Object System.Drawing.Font('Microsoft YaHei', 36, [System.Drawing.GraphicsUnit]::Pixel)
  $bt = New-Object System.Drawing.SolidBrush (New-Col @(240, 232, 210) 236)
  $g.DrawString('加入房间', $ft, $bt, [single]($x + 48), [single]($cy + 166)); $bt.Dispose(); $ft.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 132)), 2
  $g.DrawLine($pen, [single]($x + 48), [single]($cy + 244), [single]($x + $SIDE_W - 48), [single]($cy + 244)); $pen.Dispose()

  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 2
  $g.DrawRectangle($pen, [single]($x + 48), [single]($cy + 284), [single]($SIDE_W - 96), [single]76); $pen.Dispose()
  $fs = New-Object System.Drawing.Font('Microsoft YaHei', 30, [System.Drawing.GraphicsUnit]::Pixel)
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(142, 162, 180) 235)
  $g.DrawString('输入 6 位房间号', $fs, $bs, [single]($x + 68), [single]($cy + 306)); $bs.Dispose(); $fs.Dispose()

  $ring = Join-Path $dir 'LobbyAvatarRing.png'
  if (Test-Path $ring) { $img = [System.Drawing.Image]::FromFile($ring); $g.DrawImage($img, [single]($x + 48), [single]($cy + 406), 96, 96); $img.Dispose() }

  $fn = New-Object System.Drawing.Font('Microsoft YaHei', 32, [System.Drawing.GraphicsUnit]::Pixel)
  $bn = New-Object System.Drawing.SolidBrush (New-Col @(240, 232, 210) 236)
  $g.DrawString('心响', $fn, $bn, [single]($x + 172), [single]($cy + 420)); $bn.Dispose()
  $fc = New-Object System.Drawing.Font('Microsoft YaHei', 30, [System.Drawing.GraphicsUnit]::Pixel)
  $bc = New-Object System.Drawing.SolidBrush (New-Col @(240, 232, 210) 236)
  $g.DrawString('1/2', $fc, $bc, [single]($x + 172), [single]($cy + 470)); $bc.Dispose()

  $chip = Join-Path $dir 'LobbyChip_Kick.png'
  if (Test-Path $chip) {
    $img = [System.Drawing.Image]::FromFile($chip); $g.DrawImage($img, [single]($x + 328), [single]($cy + 460), 96, 48); $img.Dispose()
    $fj = New-Object System.Drawing.Font('Microsoft YaHei', 30, [System.Drawing.GraphicsUnit]::Pixel)
    $bj = New-Object System.Drawing.SolidBrush (New-Col @(240, 232, 210) 245)
    $mj = $g.MeasureString('加入', $fj)
    $g.DrawString('加入', $fj, $bj, [single]($x + 328 + (96 - $mj.Width) / 2.0), [single]($cy + 460 + (48 - $mj.Height) / 2.0 + 2))
    $bj.Dispose(); $fj.Dispose()
  }
  $fn.Dispose(); $fc.Dispose()

  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
$made += (New-JoinSidePlate (Join-Path $GEN 'LobbyJoinSidebar.png') ($SIDE_W * $S) ($SIDE_H * $S))
$made += (New-JoinSideSheet $GEN (Join-Path $PREV 'lobby-join-sidebar.png'))
$made | ForEach-Object { "sprite : $_" }
