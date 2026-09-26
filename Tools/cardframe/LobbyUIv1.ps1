# 大厅 UI 套装 v1 —— 占位件（2026-09-26）
#
# 用户 2026-09-26：好友 / 货币 / 商城 / 活动 / 教程「先做占位 ui 即可」。
# 语言与战场底板、顶栏（topbar-v1）、图标（icons-v1）同源：深蓝黑石面 + 金细线。
# 手法：整块平底 + 一条金细线 + 平板；**不倒角 / 不内阴影 / 不外发光 / 不双层面板**。
#
# 线宽按屏幕像素折算：本套统一 3 倍出图（贴图 3px = 屏幕 1px），见每组注释。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyPanelPlate.png        192x192  9-slice(border 48) 面板底（设置 / 弹窗 / 列表）
#   LobbyBtnPlate.png          192x192  9-slice(border 48) 按钮底 · 常态
#   LobbyBtnPlateHover.png     同尺寸 · 悬停
#   LobbyBtnPlatePressed.png   同尺寸 · 按下
#   LobbyBandRight.png         1614x285 右上横栏（屏幕 538x95，固定尺寸）—— 齐屏幕上沿+右沿，左端 45° 斜切，右端底边下沉一级
#   LobbyProfilePlate.png      1401x288 左上头像衬托板（屏幕 467x96，固定尺寸）—— 齐屏幕上沿+左沿，圆框=头像 / 右侧=名字，底边一级台阶
#   LobbyAvatarRing.png        264x264  头像金圆框（屏幕 88px）
#   Icon_Lobby*.png            256x256  7 个图标（屏幕 80px）：Gear/Friend/Shop/Gift/Tutorial/Coin/Ticket
#
# 2026-09-26 二次修正（用户看图后）：
#   ① 好友 / 商城 / 活动 / 教程 **不带底板**，图标直接显示在各自的位（去掉 LobbyFriendPlate）
#   ② 右上横栏里是**两个货币**（金币 + 点券）+ 数字，**底下没有名字**
#   ③ 设置齿轮压在横栏右端
#   ④ 活动图标：旗 → 礼盒；新增点券图标（菱形券，蓝色与金币区分）
#
# 2026-09-26 三次修正（用户：横栏形状不对 / 左上角形状也不对 —— 连读错两次，改成扫像素）：
#   两块异形板逐列逐行扫原稿量出来，原稿画布 1419x797 = 16:9 即屏幕本身：
#     左上：底边左段 y≈96（头像坐在这条边上）→ 台阶收上到 y≈47 → 直线 → 右端 45° 斜切上收到屏幕上沿（x413→467）
#     右上：底边 y≈56 直线到 x≈1861 → 台阶沉到 y≈95 → 走到屏幕右沿；左端 45° 斜切（x1382→1433）
#   两块互为镜像，都齐屏幕角；齿轮压在右上那段下沉台阶上。
#   教程图标：摊开的书 → 合起的书（竖长方形 + 靠右一道书脊线）。
#
# 2026-09-26 四次修正（用户：金线为什么距离边缘不一样 / 左边再收紧一点）：
#   ① 金线改等比内缩 Get-InsetPoly($outer, 24)：旧版左上板内顶点 (1368,29) 正好落在斜边上（间距 0），
#      斜边与直边的金线间距因此不一样；现在每条边一律垂直内缩 24 贴图 px。
#   ② 左上板左侧收紧：低段底边从 x≈182 收到 x≈127（刚好包住头像圆框 36..124），台阶改到 x≈127→162。
# 预览（Tools/cardframe/preview/）
#   lobby-ui-v1-sheet.png      全部件 1:1 贴纸式对照 + 尺寸标注
#   lobby-ui-v1-mockup.png     按草图版式（lobby-layout-v1）拼出的 1920x1080 效果
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$ROOT = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$BG   = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-v1/LobbyBack.png'
$EMB  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-v1/LobbyBackEmblem.png'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }

$S = 3                     # 出图倍率：贴图 3px = 屏幕 1px
$LW_INK  = 9               # 外墨边（屏幕 3px）
$LW_GOLD = 3               # 金细线（屏幕 1px）
$SLICE   = 48              # 9-slice 边（屏幕 16px）
$PLATE_R = 30              # 板角半径（贴图）

function New-PlateBmp([int]$w, [int]$h, [single]$r, [int]$tone) {
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $ct = $BAR_T; $cb = $BAR_B; $ga = 168
  if ($tone -eq 1) { $ct = (Mix-Col $BAR_T $HILITE 0.12); $cb = (Mix-Col $BAR_B $BAR_T 0.40); $ga = 236 }
  elseif ($tone -eq 2) { $ct = (Mix-Col $BAR_T $INK 0.44); $cb = (Mix-Col $BAR_B $INK 0.44); $ga = 120 }
  $in = $LW_INK / 2.0
  $path = New-RoundPath $in $in ($w - $LW_INK) ($h - $LW_INK) $r
  $st = $g.Save(); $g.SetClip($path)
  Fill-VGrad $g 0 0 $w $h $ct $cb 255 255
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose()
  $o2 = $in + $LW_INK
  $inner = New-RoundPath $o2 $o2 ($w - $o2*2) ($h - $o2*2) ($r - $LW_INK + 2)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $ga)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $path.Dispose()
  return @($b, $g)
}
function New-PolyPlate([int]$w, [int]$h, [System.Drawing.PointF[]]$pts, [int]$tone) {
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $ct = $BAR_T; $cb = $BAR_B; $ga = 168
  if ($tone -eq 1) { $ct = (Mix-Col $BAR_T $HILITE 0.12); $cb = (Mix-Col $BAR_B $BAR_T 0.40); $ga = 236 }
  elseif ($tone -eq 2) { $ct = (Mix-Col $BAR_T $INK 0.44); $cb = (Mix-Col $BAR_B $INK 0.44); $ga = 120 }
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddPolygon($pts)
  $st = $g.Save(); $g.SetClip($path)
  Fill-VGrad $g 0 0 $w $h $ct $cb 255 255
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $ga)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose(); $path.Dispose()
  return @($b, $g)
}

# ── 图标（256x256，屏幕 80px；墨边 20 与 icons-v1 / topbar-v1 一致）────
function New-FriendGlyph($g) {
  $head = New-Object System.Drawing.Drawing2D.GraphicsPath
  $head.AddEllipse(86, 46, 84, 84)
  $body = New-Object System.Drawing.Drawing2D.GraphicsPath
  $body.AddArc(48, 142, 160, 150, 180, 180); $body.CloseFigure()
  foreach ($p in @($head, $body)) {
    Add-Outline $g $p 20
    $bs = New-Object System.Drawing.SolidBrush (New-Col $METAL 255)
    $g.FillPath($bs, $p); $bs.Dispose()
    Add-Shade $g $p $METAL_D 196 168
  }
  Add-Hilite $g $head 100 58 40 30 110
  Add-Hilite $g $body 76 152 54 34 70
  $head.Dispose(); $body.Dispose()
}
function New-ShopGlyph($g) {
  $basket = New-Object System.Drawing.Drawing2D.GraphicsPath
  $basket.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(74, 98)), (New-Object System.Drawing.PointF(240, 98)),
    (New-Object System.Drawing.PointF(214, 176)), (New-Object System.Drawing.PointF(100, 176))))
  $basket.CloseFigure()
  Add-Outline $g $basket 20
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 255)
  $g.FillPath($bs, $basket); $bs.Dispose()
  Add-Shade $g $basket $GOLD_D 150 132
  Add-Wedge $g $basket ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(78, 102)), (New-Object System.Drawing.PointF(236, 102)),
    (New-Object System.Drawing.PointF(230, 120)), (New-Object System.Drawing.PointF(84, 120)))) $GOLD_L 110
  $basket.Dispose()
  $bar = New-Object System.Drawing.Drawing2D.GraphicsPath
  $bar.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(18, 34)), (New-Object System.Drawing.PointF(70, 34)),
    (New-Object System.Drawing.PointF(88, 104)), (New-Object System.Drawing.PointF(66, 104)),
    (New-Object System.Drawing.PointF(52, 56)), (New-Object System.Drawing.PointF(18, 56))))
  $bar.CloseFigure()
  Add-Outline $g $bar 18
  $bs = New-Object System.Drawing.SolidBrush (New-Col $METAL 255); $g.FillPath($bs, $bar); $bs.Dispose()
  Add-Shade $g $bar $METAL_D 70 56
  $bar.Dispose()
  foreach ($cx in @(116, 198)) {
    $w = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w.AddEllipse(($cx - 20), 194, 40, 40)
    Add-Outline $g $w 18
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 255); $g.FillPath($bs, $w); $bs.Dispose()
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 255)
    $g.FillEllipse($bs, ($cx - 9), 205, 18, 18); $bs.Dispose()
    $w.Dispose()
  }
}
function New-GiftGlyph($g) {
  # 盒身
  $body = New-Object System.Drawing.Drawing2D.GraphicsPath
  $body.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(58, 118)), (New-Object System.Drawing.PointF(198, 118)),
    (New-Object System.Drawing.PointF(190, 224)), (New-Object System.Drawing.PointF(66, 224))))
  $body.CloseFigure()
  Add-Outline $g $body 20
  $bs = New-Object System.Drawing.SolidBrush (New-Col $CRIM 255); $g.FillPath($bs, $body); $bs.Dispose()
  Add-Shade $g $body $CRIM_D 168 200
  $body.Dispose()
  # 盒盖
  $lid = New-RoundPath 38 74 180 46 8
  Add-Outline $g $lid 20
  $bs = New-Object System.Drawing.SolidBrush (New-Col (Mix-Col $CRIM $HILITE 0.22) 255)
  $g.FillPath($bs, $lid); $bs.Dispose()
  Add-Shade $g $lid (Mix-Col $CRIM_D $CRIM 0.25) 100 88
  $lid.Dispose()
  # 缎带（竖）
  $rib = New-Object System.Drawing.Drawing2D.GraphicsPath
  $rib.AddRectangle((New-Object System.Drawing.RectangleF(114, 74, 28, 150)))
  Add-Outline $g $rib 12
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255); $g.FillPath($bs, $rib); $bs.Dispose()
  $rib.Dispose()
  # 蝴蝶结
  foreach ($cx in @(94, 162)) {
    $lp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $lp.AddEllipse(($cx - 26), 30, 52, 44)
    Add-Outline $g $lp 12
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255); $g.FillPath($bs, $lp); $bs.Dispose()
    $lp.Dispose()
  }
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 230)), 12
  $bs = New-Object System.Drawing.SolidBrush (New-Col (Mix-Col $GOLD_L $GOLD 0.4) 255)
  $g.FillEllipse($bs, 114, 42, 28, 28)
  $g.DrawEllipse($pen, 114, 42, 28, 28)
  $bs.Dispose(); $pen.Dispose()
}
function New-TicketGlyph($g) {
  # 点券：菱形券（与金币区分色相：金 vs 蓝）
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(128, 22)), (New-Object System.Drawing.PointF(234, 128)),
    (New-Object System.Drawing.PointF(128, 234)), (New-Object System.Drawing.PointF(22, 128))))
  $p.CloseFigure()
  Add-Outline $g $p 20
  $bs = New-Object System.Drawing.SolidBrush (New-Col $BOLT 255); $g.FillPath($bs, $p); $bs.Dispose()
  Add-Shade $g $p $BOLT_D 150 140
  Add-Wedge $g $p ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(128, 30)), (New-Object System.Drawing.PointF(30, 128)),
    (New-Object System.Drawing.PointF(128, 128)))) $HILITE 76
  $pen = New-Object System.Drawing.Pen ((New-Col $HILITE 170)), 8
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
  $inner.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(128, 58)), (New-Object System.Drawing.PointF(198, 128)),
    (New-Object System.Drawing.PointF(128, 198)), (New-Object System.Drawing.PointF(58, 128))))
  $inner.CloseFigure()
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $p.Dispose()
}
function New-BookGlyph($g) {
  # 合起的书：竖长方形 + 靠近右沿的一道书脊线（照草图那个形状）
  $body = New-RoundPath 60 32 136 192 16
  Add-Outline $g $body 20
  $bs = New-Object System.Drawing.SolidBrush (New-Col $SAND 255); $g.FillPath($bs, $body); $bs.Dispose()
  Add-Shade $g $body (Mix-Col $SAND_D $SAND 0.35) 90 108
  $body.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $SAND_D 235)), 14
  $g.DrawLine($pen, 166, 46, 166, 210); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $SAND_D 120)), 8
  foreach ($k in 0..2) {
    $y = 84 + $k * 40
    $g.DrawLine($pen, 86, $y, 140, $y)
  }
  $pen.Dispose()
}
function New-CoinGlyph($g) {
  Add-Outline $g (New-RoundPath 32 32 192 192 96) 20
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 255)
  $g.FillEllipse($bs, 32, 32, 192, 192); $bs.Dispose()
  $st = $g.Save()
  $cp = New-Object System.Drawing.Drawing2D.GraphicsPath; $cp.AddEllipse(32, 32, 192, 192)
  $g.SetClip($cp)
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_D 170)
  $g.FillEllipse($bs, 32, 138, 192, 120); $bs.Dispose()
  $g.Restore($st); $cp.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 190)), 8
  $g.DrawEllipse($pen, 62, 62, 132, 132); $pen.Dispose()
  $dd = New-Diamond 128 128 38
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 200)), 14
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dd); $pen.Dispose()
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 255); $g.FillPath($bs, $dd); $bs.Dispose()
  $dd.Dispose()
}
function New-LobbyIcon([string]$kind, [string]$out) {
  $res = New-Bmp 256 256; $b = $res[0]; $g = $res[1]
  switch ($kind) {
    'friend'   { New-FriendGlyph $g }
    'shop'     { New-ShopGlyph $g }
    'event'    { New-GiftGlyph $g }
    'ticket'   { New-TicketGlyph $g }
    'tutorial' { New-BookGlyph $g }
    'coin'     { New-CoinGlyph $g }
    'gear'     {
      $gp = New-GearPath
      $gp.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
      Add-Outline $g $gp 20
      $bs = New-Object System.Drawing.SolidBrush (New-Col $METAL 255); $g.FillPath($bs, $gp); $bs.Dispose()
      Add-Shade $g $gp $METAL_D 196 168
      Add-Hilite $g $gp 44 34 92 70 96
      $gp.Dispose()
    }
  }
  return (Save-Bmp $b $g $out)
}

# ── 固定尺寸件 ──────────────────────────────────────────────
# ── 两块「齐屏幕角」的异形板：都是逐列/逐行扫原稿量出来的（见 README）──
# 外轮廓等比内缩：每条边沿自身法线内推 $t，再取相邻两条内推线的交点。
# 手摆内顶点会让斜边的间距和直边不一样（2026-09-26 用户：金线为什么距离边缘不一样）。
# 只对「y 向下的顺时针多边形」成立，本文件两块异形板都按这个方向排点。
function Get-InsetPoly([System.Drawing.PointF[]]$pts, [single]$t) {
  $n = $pts.Count
  $lines = @()
  for ($i = 0; $i -lt $n; $i++) {
    $a = $pts[$i]; $b = $pts[($i + 1) % $n]
    $dx = $b.X - $a.X; $dy = $b.Y - $a.Y
    $len = [Math]::Sqrt($dx * $dx + $dy * $dy)
    $ux = $dx / $len; $uy = $dy / $len
    $lines += ,@(($a.X - $uy * $t), ($a.Y + $ux * $t), $ux, $uy)
  }
  $out = @()
  for ($i = 0; $i -lt $n; $i++) {
    $l1 = $lines[($i - 1 + $n) % $n]; $l2 = $lines[$i]
    $den = $l1[2] * $l2[3] - $l1[3] * $l2[2]
    if ([Math]::Abs($den) -lt 1e-9) { $out += [System.Drawing.PointF]::new([single]$l2[0], [single]$l2[1]); continue }
    $u = (($l2[0] - $l1[0]) * $l2[3] - ($l2[1] - $l1[1]) * $l2[2]) / $den
    $out += [System.Drawing.PointF]::new([single]($l1[0] + $l1[2] * $u), [single]($l1[1] + $l1[3] * $u))
  }
  return ([System.Drawing.PointF[]]$out)
}
function New-ShapedPlate([string]$out, [int]$w, [int]$h, [System.Drawing.PointF[]]$outer, [System.Drawing.PointF[]]$inner) {
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddPolygon($outer)
  $st = $g.Save(); $g.SetClip($path)
  Fill-VGrad $g 0 0 $w $h $BAR_T $BAR_B 255 255
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose(); $path.Dispose()
  $ip = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ip.AddPolygon($inner)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 168)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ip); $pen.Dispose(); $ip.Dispose()
  return (Save-Bmp $b $g $out)
}
function New-LobbyBand([string]$out) {
  # 右上横栏 · 屏幕上 538x95（贴图 1614x285）· 与屏幕上沿 / 右沿齐平
  #   左端 45° 斜切（上沿比下沿靠左 51 / 高 56）· 底边一条直线到 x≈1861
  #   右端底边下沉一级（斜 23x39）→ 更低的一条边走到屏幕右沿（齿轮就压在这段下沉上）
  $outer = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(5, 5)),    (New-Object System.Drawing.PointF(1609, 5)),
    (New-Object System.Drawing.PointF(1609, 280)), (New-Object System.Drawing.PointF(1506, 280)),
    (New-Object System.Drawing.PointF(1437, 168)), (New-Object System.Drawing.PointF(153, 168)))
  return (New-ShapedPlate $out 1614 285 $outer (Get-InsetPoly $outer 24))
}
function New-LobbyProfilePlate([string]$out) {
  # 左上衬托板 · 屏幕上 467x96（贴图 1401x288）· 与屏幕上沿 / 左沿齐平
  #   圆框 = 头像位（贴屏幕左沿）· 圆框右侧那一段 = 名字位
  #   底边：左段 y≈95 只留够头像（到 x≈127）→ 台阶收到 y≈47 → 直线 → 右端 45° 斜切上收到屏幕上沿
  $outer = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(5, 5)),     (New-Object System.Drawing.PointF(1396, 5)),
    (New-Object System.Drawing.PointF(1239, 141)), (New-Object System.Drawing.PointF(486, 141)),
    (New-Object System.Drawing.PointF(372, 283)), (New-Object System.Drawing.PointF(5, 283)))
  return (New-ShapedPlate $out 1401 288 $outer (Get-InsetPoly $outer 24))
}
function New-LobbyAvatarRing([string]$out) {
  $res = New-Bmp 264 264; $b = $res[0]; $g = $res[1]
  Fill-Disc $g 132 132 129 $INK 255
  Fill-Disc $g 132 132 120 $GOLD 255
  Fill-Disc $g 132 132 108 $INK 255
  Fill-Disc $g 132 132 102 $BAR_T 255
  $st = $g.Save()
  $well = New-Object System.Drawing.Drawing2D.GraphicsPath; $well.AddEllipse(30, 30, 204, 204)
  $g.SetClip($well)
  Fill-VGrad $g 30 30 204 204 $BAR_T $BAR_B 255 255
  $g.Restore($st); $well.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_L 92)), 5
  $g.DrawArc($pen, 38, 38, 188, 188, 196, 78); $pen.Dispose()
  return (Save-Bmp $b $g $out)
}

# ── 预览：贴纸式对照 ────────────────────────────────────────
$script:PFC = $null
function Get-F([single]$px) {
  if ($script:PFC -eq $null) {
    $script:PFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PFC.AddFontFile((Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'))
  }
  return New-Object System.Drawing.Font($script:PFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
function Put-Text($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 236) {
  $f = Get-F $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function New-LobbyUiSheet([string]$dir, [string]$out) {
  $res = New-Bmp 1720 1200; $b = $res[0]; $g = $res[1]
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 12, 15, 21))
  $g.FillRectangle($bs, 0, 0, 1720, 1200); $bs.Dispose()
  $brd = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 27, 38))
  $g.FillRectangle($brd, 24, 24, 1672, 300); $brd.Dispose()
  Put-Text $g '面板 / 按钮（192x192 贴图，9-slice border 48）· 屏幕 1px = 贴图 3px' 44 40 24
  $x = 44
  foreach ($n in @(@('LobbyPanelPlate.png', '面板底'), @('LobbyBtnPlate.png', '按钮 · 常态'), @('LobbyBtnPlateHover.png', '按钮 · 悬停'), @('LobbyBtnPlatePressed.png', '按钮 · 按下'))) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $n[0]))
    $g.DrawImage($im, $x, 84, 180, 180); $im.Dispose()
    Put-Text $g $n[1] $x 272 22 210
    $x += 200
  }
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyAvatarRing.png'))
  $g.DrawImage($im, 1180, 84, 180, 180); $im.Dispose()
  Put-Text $g '头像金圆框 264x264' 1180 272 22 210

  $brd = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 27, 38))
  $g.FillRectangle($brd, 24, 344, 1672, 224); $brd.Dispose()
  Put-Text $g '图标 256x256（屏幕 80px）· 墨边 20 / 与 icons-v1 同规' 44 360 24
  $x = 44
  foreach ($n in @(@('Icon_LobbyGear.png', '设置 · 齿轮'), @('Icon_LobbyFriend.png', '好友 · 人影'), @('Icon_LobbyShop.png', '商城 · 购物车'), @('Icon_LobbyEvent.png', '活动 · 礼盒'), @('Icon_LobbyTutorial.png', '教程 · 书'), @('Icon_LobbyCoin.png', '金币 · 币'), @('Icon_LobbyTicket.png', '点券 · 菱形券'))) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $n[0]))
    $g.DrawImage($im, $x, 400, 160, 160); $im.Dispose()
    Put-Text $g $n[1] $x 560 22 210
    $x += 222
  }

  $brd = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 27, 38))
  $g.FillRectangle($brd, 24, 596, 1672, 578); $brd.Dispose()
  Put-Text $g '固定尺寸件（左：右上横栏 · 右：左上头像衬托底板）' 44 612 24
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyBandRight.png'))
  $g.DrawImage($im, 44, 660, 1000, 206); $im.Dispose()
  Put-Text $g 'LobbyBandRight.png  1614x285（屏幕 538x95）· 与屏幕上沿/右沿齐平 · 左端 45° 斜切 · 右端底边下沉一级' 44 874 22 210
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyProfilePlate.png'))
  $g.DrawImage($im, 44, 926, 1000, 221); $im.Dispose()
  Put-Text $g 'LobbyProfilePlate.png  1401x288（屏幕 467x96）· 齐屏幕上沿/左沿 · 圆框=头像 / 右侧=名字 · 金线等比内缩 24' 44 1154 22 210
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# ── 预览：按版式拼装 ────────────────────────────────────────
function Draw-Sprite($g, [string]$file, [single]$cx, [single]$cy, [single]$w, [single]$h, [single]$rot) {
  $im = [System.Drawing.Image]::FromFile($file)
  $st = $g.Save()
  $g.TranslateTransform($cx, $cy)
  if ($rot -ne 0) { $g.RotateTransform($rot) }
  $g.DrawImage($im, (-$w/2), (-$h/2), $w, $h)
  $g.Restore($st); $im.Dispose()
}
function New-LobbyUiMockup([string]$dir, [string]$out) {
  $W = 1920; $H = 1080
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 8, 11, 17))
  $g.FillRectangle($bs, 0, 0, $W, $H); $bs.Dispose()
  foreach ($f in @(@($BG, 0, 0, $W, $H), @($EMB, 450, 30, 1020, 1020))) {
    $im = [System.Drawing.Image]::FromFile($f[0])
    $ia = New-Object System.Drawing.Imaging.ImageAttributes
    $cm = New-Object System.Drawing.Imaging.ColorMatrix; $cm.Matrix33 = 0.60; $ia.SetColorMatrix($cm)
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle([int]$f[1], [int]$f[2], [int]$f[3], [int]$f[4])), 0, 0, $im.Width, $im.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
    $im.Dispose(); $ia.Dispose()
  }
  $TILT = 6.0
  # 四块斜板（先用按钮底贴图，按版式旋转 6°）
  $main = @(
    @(1138, 225, 558, 145, '战斗',   '随机匹配 + AI 对战'),
    @(1114, 456, 553, 149, '卡牌总览', '已有 CardCollectionPanel'),
    @(1038, 725, 287, 130, '房间',   '创建房间 + 加入房间'),
    @(1343, 782, 295, 129, '其它',   '介绍 / 战绩 / 设置 / 结束游戏 / 返回主界面'))
  foreach ($m in $main) {
    Draw-Sprite $g (Join-Path $dir 'LobbyBtnPlate.png') ($m[0] + $m[2]/2) ($m[1] + $m[3]/2) $m[2] $m[3] $TILT
  }
  # 右上：横栏（与屏幕上沿 / 右沿齐平，左端 45° 斜切，右端底边下沉一级）
  Draw-Sprite $g (Join-Path $dir 'LobbyBandRight.png') 1651 47.5 538 95 0
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyCoin.png') 1516 40 48 48 0
  Put-Text $g '1,280' 1546 24 28 238
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyTicket.png') 1706 40 48 48 0
  Put-Text $g '360' 1736 24 28 238
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyGear.png') 1874 48 92 92 0
  # 栏下：商城 / 活动 / 教程 的自身图标 —— 无底板、无名字
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyShop.png') 1521 96 60 60 0
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyEvent.png') 1644 96 60 60 0
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyTutorial.png') 1770 96 60 60 0
  # 左上：圆框 = 头像位 · 圆框右侧 = 名字位 · 好友图标挂在底板下沿
  Draw-Sprite $g (Join-Path $dir 'LobbyProfilePlate.png') 233.5 48 467 96 0
  Draw-Sprite $g (Join-Path $dir 'LobbyAvatarRing.png') 80 46 88 88 0
  Put-Text $g '名字' 184 12 26
  Draw-Sprite $g (Join-Path $dir 'Icon_LobbyFriend.png') 183 80 46 46 0
  foreach ($m in $main) {
    Put-Text $g $m[4] ($m[0] + 26) ($m[1] + 34) 30 236
    Put-Text $g $m[5] ($m[0] + 26) ($m[1] + 76) 18 190
  }
  Put-Text $g '大厅 UI 占位 v1 · 按草图版式（斜板 6°）· 左半屏留空' 24 ($H - 44) 24 220
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# ── 主流程 ─────────────────────────────────────────────────
$made = @()
foreach ($t in @(@('LobbyPanelPlate.png', 0), @('LobbyBtnPlate.png', 0), @('LobbyBtnPlateHover.png', 1), @('LobbyBtnPlatePressed.png', 2))) {
  $r = New-PlateBmp 192 192 $PLATE_R $t[1]
  $made += (Save-Bmp $r[0] $r[1] (Join-Path $GEN $t[0]))
}
$made += (New-LobbyBand (Join-Path $GEN 'LobbyBandRight.png'))
$made += (New-LobbyProfilePlate (Join-Path $GEN 'LobbyProfilePlate.png'))
$made += (New-LobbyAvatarRing (Join-Path $GEN 'LobbyAvatarRing.png'))
foreach ($k in @('gear', 'friend', 'shop', 'event', 'tutorial', 'coin', 'ticket')) {
  $nm = 'Icon_Lobby' + $k.Substring(0,1).ToUpper() + $k.Substring(1) + '.png'
  $made += (New-LobbyIcon $k (Join-Path $GEN $nm))
}
$sheet = Join-Path $PREV 'lobby-ui-v1-sheet.png'
$mock  = Join-Path $PREV 'lobby-ui-v1-mockup.png'
New-LobbyUiSheet $GEN $sheet
New-LobbyUiMockup $GEN $mock
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
"mockup : $mock"