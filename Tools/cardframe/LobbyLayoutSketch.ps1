# 大厅版式图 v1 —— 把用户手绘草图翻译成 1920x1080 的摆位图（用来对齐理解，不是成品美术）
# 产物：Tools/cardframe/preview/lobby-layout-v1.png
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/BoardLayersV2.ps1"

$ROOT = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$PREV = Join-Path $PSScriptRoot 'preview'
$BG   = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-v1/LobbyBack.png'
$EMB  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-v1/LobbyBackEmblem.png'
$OUT  = Join-Path $PREV 'lobby-layout-v1.png'
$W = 1920; $H = 1080
$TILT = 6.0   # 草图上四块斜板实测约 6 度（顺时针）

$script:PFC = $null
function Get-F([single]$px) {
  if ($script:PFC -eq $null) {
    $script:PFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PFC.AddFontFile((Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'))
  }
  return New-Object System.Drawing.Font($script:PFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}

function New-Plate($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$rot, [int[]]$line, [int]$la, [int]$fl) {
  $st = $g.Save()
  $g.TranslateTransform(($x + $w/2), ($y + $h/2))
  if ($rot -ne 0) { $g.RotateTransform($rot) }
  $g.TranslateTransform(-$w/2, -$h/2)
  $path = New-RoundPath 0 0 $w $h 10
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(30,41,56) $fl)
  $g.FillPath($bs, $path); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $line $la)), 2
  $g.DrawPath($pen, $path); $pen.Dispose(); $path.Dispose()
  $g.Restore($st)
}

function Draw-Label($g, [string]$main, [string]$sub, [single]$cx, [single]$cy, [single]$px = 26) {
  $f1 = Get-F $px
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $b1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(242, 240, 228, 196))
  $g.DrawString($main, $f1, $b1, (New-Object System.Drawing.RectangleF(($cx-300), ($cy-16), 600, 34)), $fmt)
  $b1.Dispose(); $f1.Dispose()
  if ($sub -ne '') {
    $f2 = Get-F ($px * 0.62)
    $b2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 158, 178, 200))
    $g.DrawString($sub, $f2, $b2, (New-Object System.Drawing.RectangleF(($cx-300), ($cy+16), 600, 30)), $fmt)
    $b2.Dispose(); $f2.Dispose()
  }
  $fmt.Dispose()
}

function New-LayoutSketch([string]$out) {
  $b = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

  # 背景压到 35%，只当底纹
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $cm = New-Object System.Drawing.Imaging.ColorMatrix; $cm.Matrix33 = 0.35; $ia.SetColorMatrix($cm)
  foreach ($f in @($BG, $EMB)) {
    $im = [System.Drawing.Image]::FromFile($f)
    $dw = $W; $dh = $H; $dx = 0; $dy = 0
    if ($f -eq $EMB) { $dw = 1020; $dh = 1020; $dx = ($W-$dw)/2; $dy = ($H-$dh)/2 }
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle([int]$dx,[int]$dy,[int]$dw,[int]$dh)), 0, 0, $im.Width, $im.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
    $im.Dispose()
  }
  $ia.Dispose()
  $ov = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 6, 9, 14))
  $g.FillRectangle($ov, 0, 0, $W, $H); $ov.Dispose()

  $GOLD = @(200,164,74); $CYAN = @(110,200,235); $PINK = @(220,130,150); $STEEL = @(142,162,180)

  # ── 左上：头像 + 名字（衬托底板，右端收尖）──
  $tl = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  foreach ($q in @(@(8,14), @(392,14), @(452,62), @(392,110), @(8,110))) { $pts.Add((New-Object System.Drawing.PointF($q[0], $q[1]))) }
  $tl.AddPolygon($pts.ToArray())
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(30,41,56) 150); $g.FillPath($bs, $tl); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 170)), 2; $g.DrawPath($pen, $tl); $pen.Dispose(); $tl.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 200)), 3
  $g.DrawEllipse($pen, 22, 20, 88, 88); $pen.Dispose()
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(60,80,110) 190); $g.FillEllipse($bs, 24, 22, 84, 84); $bs.Dispose()
  Draw-Label $g '头像 + 名字' '衬托底板 · 已有 PlayerProfilePanel' 240 62 24

  # ── 头像下：好友 ──
  New-Plate $g 150 128 96 84 0 $GOLD 170 150
  Draw-Label $g '好友' '' 198 170 22

  # ── 右上：货币 + 商城 / 活动 / 教程 + 设置（左端斜切）──
  $band = New-Object System.Drawing.Drawing2D.GraphicsPath
  $pts2 = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  foreach ($q in @(@(1424,0), @(1912,0), @(1912,104), @(1392,104))) { $pts2.Add((New-Object System.Drawing.PointF($q[0], $q[1]))) }
  $band.AddPolygon($pts2.ToArray())
  $bs = New-Object System.Drawing.SolidBrush (New-Col @(30,41,56) 150); $g.FillPath($bs, $band); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 170)), 2; $g.DrawPath($pen, $band); $pen.Dispose(); $band.Dispose()
  Draw-Label $g '货币栏？' '' 1326 56 24
  for ($i = 0; $i -lt 3; $i++) {
    $x = 1560 + $i * 104
    New-Plate $g ($x-40) 40 80 80 12 $CYAN 180 140
    $names = @('商城', '活动', '教程')
    $f = Get-F 20
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 200, 228, 240))
    $g.DrawString($names[$i], $f, $br, (New-Object System.Drawing.RectangleF(($x-50), 118, 100, 28)), $fmt)
    $br.Dispose(); $fmt.Dispose(); $f.Dispose()
  }
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 200)), 3
  $g.DrawEllipse($pen, 1824, 6, 92, 92); $pen.Dispose()
  Draw-Label $g '设置' '' 1870 52 20

  # ── 四块斜板 ──
  $main = @(
    @(1138, 225, 558, 145, '战斗',   '随机匹配 + AI 对战'),
    @(1114, 456, 553, 149, '卡牌总览', '已有 CardCollectionPanel'),
    @(1038, 725, 287, 130, '房间',   '创建房间 + 加入房间'),
    @(1343, 782, 295, 129, '其它',   '介绍 / 战绩 / 设置 / 结束游戏 / 返回主界面'))
  foreach ($m in $main) {
    New-Plate $g $m[0] $m[1] $m[2] $m[3] $TILT $GOLD 190 170
    Draw-Label $g $m[4] $m[5] ($m[0]+$m[2]/2+14) ($m[1]+$m[3]/2+10) 30
  }

  # ── 说明 ──
  $f = Get-F 24
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 210, 120))
  $g.DrawString('大厅版式 v1 · 按你草图换算（斜板实测约 6° 顺时针）· 左半屏留空', $f, $br, 24, $H - 108)
  $g.DrawString('库里现状：无货币 / 无商城 / 无活动系统；教程只有 PLACEHOLDER；好友只有 Steam 头像 API，没有列表 UI', $f, $br, 24, $H - 76)
  $g.DrawString('设置 = 已有 Setting 按钮 + SettingsLauncher；头像+名字 = 已有 PlayerProfilePanel（现挂在左上 anchor(0,1)）', $f, $br, 24, $H - 44)
  $br.Dispose(); $f.Dispose()

  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

New-LayoutSketch $OUT
"layout : $OUT"