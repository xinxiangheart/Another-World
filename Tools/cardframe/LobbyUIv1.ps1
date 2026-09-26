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
#   LobbyEntryPlate_*.png      定尺×3   四个**入口板**（战斗 666x145 / 卡牌总览 661x149 / 房间 287x130 / 其它 295x129），贴图比板身大一圈
#                                        —— 每块一张定尺贴图，**不能 9-slice**：端头斜切 + 远端收缩烤进贴图，倾斜由场景的 Rotation Z 承担（见「九次修正」）
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
#
# 2026-09-26 五次修正（用户：重出一批用于四个框的背景，现在的太简陋视觉表现太怪了）：
#   ① 病灶一：mockup 把 192x192 的 9-slice 直接按**屏幕像素**铺上去，贴图的线宽 / 圆角是 3 倍口径，
#      于是墨边 9px、金线 3px、圆角 30px 全按 1 贴图 px = 1 屏幕 px 画出来 —— 比该有的粗 3 倍。
#   ② 病灶二：四个入口板只有一条金线 + 一块平底，没有任何结构。
#   ③ 现在每块入口板单独出一张定尺贴图（屏幕尺寸 ×3）：平底渐变 + 左上角一级亮楔（色阶差 6–8 级）
#      + 右半低透明徽记（六芒星 / 三叠牌 / 拱门 / 环带刻度）+ 外墨边 + 等比内缩金细线
#      + 左端标题槽（竖线 + 上下菱形铆钉）+ 标题下短线；贴图按屏幕尺寸缩下去画，线宽圆角才对。
#
# 2026-09-26 六次修正（用户：只显示入口名、小字注释不要、文字斜着展示、字号按板大小分档）：
#   ① 板上只留入口名（战斗 / 卡牌总览 / 房间 / 其它），副标题一律删掉。
#   ② 文字**跟板同角度**（Put-TextRot，绕板心转），不再水平（角度随后调成 5°）。
#   ③ 字号分档：上面两块大（战斗 44 / 卡牌总览 46），下面两块小（房间 / 其它 30）。
#
# 2026-09-26 七次修正（用户：倾斜程度小一点点 / 各个格子之间距离缩短，只留出部分间隔）：
#   ① 倾斜 6° → **5°**（草图实测 6.1°，用户要再平一点）。
#   ③ 2026-09-26 后续：用户「我忘了说是向上倾斜了」→ **倾角改负（-5°，右端高）**，草图方向作废；
#      同时「上面两个可以增长一些，不要让下面的露出侧边」→ 战斗 558→666、卡牌总览 553→661（右端不动、往左长），
#      左端越过 房间 的最左点，下面两块的侧边被上面两块盖住。
#   ② 四块沿板法线的净距收到 **40px**（原来 战斗→卡牌 87.5、卡牌→房间 140.4）。
#      注意：斜板的外框（轴对齐包围盒）会互相咬 —— 量间距要沿板法线量，不能看外框。
#
# 2026-09-26 十次修正（用户：下面两个小框实际上就是在一个整体的大框下 / 那个大框是透明的只用于限位）：
#   下排**不出合并贴图** —— 房间 / 其它 仍是各自一张定尺贴图（各自的形体），位置由一个**不画的容器**定：
#   容器与上面两块同尺寸 666x145（屏幕 px），竖切成两格（各 324 宽、中缝 18），两块各占一格、左右贴齐容器外沿。
#   容器只有 145 高、板已 130 高，塞不下 57px 的错落 —— 两格同一行；原来那 287 / 295 的板宽跟着格子变成 324。
#   版式与取值见下方 $BOX_* / $ENTRY_CELL；场景里对应 Canvas 下一个空的 Entry_BottomRow（无 Image）。
#
# 2026-09-26 九次修正（用户：想做类似的「透视」效果，**不是所有板都朝一个方向倾斜**，而且**倾斜程度很低**）：
#   ① 形体改成**每块板自己的微透视**，三个数一块（顶部 $ENTRY_SHAPE）：倾斜度（场景 Rotation Z）/ 端头斜切度 / 远端收缩比。
#      实测明日方舟大厅：上/下边接近水平（±1~2°）、四条边互不平行（右端矮 1~3% = 梯形）、每块方向不同。
#   ② 板体照旧在局部坐标里画好，再用**两片三角形仿射**映射成梯形（Get-EntryQuad / Get-EntryPlateMap / New-QuadWarpDraw），
#      装饰（徽记 / 标题槽 / 金线）跟着形体一起走，不用逐件改坐标。
#   ③ 修掉「$W 与 $w 是同一个变量」造成的两次乘 3 倍（贴图像素尺寸改叫 $wt/$ht），以及返回嵌套数组被拍平（改哈希表返回）。
# 预览（Tools/cardframe/preview/）
#   lobby-ui-v1-sheet.png      全部件 1:1 贴纸式对照 + 尺寸标注
#   lobby-ui-v1-entryplates.png 四个入口板 ×4（屏幕 1.55 倍）+ 图层说明
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
# 入口板形体（2026-09-26 九次修正）：每块板**自己的微透视** —— 幅度很小（1~2°）、方向不统一，
# 且左右端高度不同（真梯形 = 真透视），不是四块统一旋转。每项 = @(倾斜度, 端头斜切度, 远端收缩比)。
#   倾斜度    ：场景里 Entry_* 的 Rotation Z（Unity 口径，+ = 右端高）—— 不烤进贴图，方便在 Scene 里拖
#               用户 2026-09-26 定的方向：**中间那块（卡牌总览）不倾**（0°）、**上面那块朝左下**（右端高，+）、
#               **下面两块朝左上**（右端低，−）—— 整簇像绕屏幕右中一个灭点扇开。
#   端头斜切度：端头相对板面再斜多少（GDI 口径，负 = 顶点相对底点右移）—— 烤进贴图
#   远端收缩比：右端比左端矮多少（0.03 = 矮 3%）—— 纯旋转做不到这一条，它是「透视」的主要来源
$ENTRY_SHAPE = @{
  'battle' = @( 1.6, -1.2, 0.030)
  'cards'  = @( 0.0, -0.8, 0.026)
  # 下排两块在**同一个大框**里 —— 就是同一格栅的两格，形体必须完全一样：
  # 倾角 / 斜切 / 收缩三个数都一样，两条上沿才平行（2026-09-26 用户指认过）
  'room'   = @(-1.5,  1.0, 0.024)
  'more'   = @(-1.5,  1.0, 0.024)
}
$ENTRY_PAD = 24              # 贴图 px：板身之外多留一圈（外墨边 9 + 圆角外扩余量）
$ENTRY_AA  = 3               # 贴图 px：抗锯齿余量

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

# ── 四个入口板（斜长方形：草图实测 ≈6.1°，现用 5°；每块定尺一张贴图，不能 9-slice）──
# 结构：平底渐变 + 左上角一级亮楔（相邻色阶差 6–8 级）+ 右半低透明徽记 + 外墨边
#       + 等比内缩金细线 + 左端标题槽（竖线 + 上下菱形铆钉）+ 标题下短线。
# 徽记只用线稿 + 单色平涂，不做发光 / 倒角 / 内阴影（本文「界面 / 场景美术方向」那条）。
function New-EntryEmblem($g, [string]$kind, [single]$cx, [single]$cy, [single]$r, [int]$a) {
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $a)), ([single]($r * 0.15))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  if ($kind -eq 'battle') {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    for ($k = 0; $k -lt 2; $k++) {
      $pts = @()
      for ($i = 0; $i -lt 3; $i++) {
        $ang = [Math]::PI * 2 * $i / 3 - [Math]::PI / 2 + $k * [Math]::PI / 3
        $pts += [System.Drawing.PointF]::new([single]($cx + $r * [Math]::Cos($ang)), [single]($cy + $r * [Math]::Sin($ang)))
      }
      $p.AddPolygon([System.Drawing.PointF[]]$pts)
    }
    $g.DrawPath($pen, $p); $p.Dispose()
  } elseif ($kind -eq 'cards') {
    $cw = $r * 0.84; $ch = $r * 1.26
    foreach ($o in @(@(-0.36, -0.20), @(0.04, 0.02), @(0.42, 0.24))) {
      $p = New-RoundPath ($cx + $o[0] * $r - $cw / 2) ($cy + $o[1] * $r - $ch / 2) $cw $ch ($r * 0.13)
      $g.DrawPath($pen, $p); $p.Dispose()
    }
  } elseif ($kind -eq 'room') {
    $dw = $r * 1.06; $dh = $r * 1.62
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(($cx - $dw / 2), ($cy - $dh / 2), $dw, $dw, 180, 180)
    $p.AddLine(($cx + $dw / 2), ($cy - $dh / 2 + $dw / 2), ($cx + $dw / 2), ($cy + $dh / 2))
    $p.AddLine(($cx + $dw / 2), ($cy + $dh / 2), ($cx - $dw / 2), ($cy + $dh / 2))
    $p.CloseFigure()
    $g.DrawPath($pen, $p); $p.Dispose()
  } else {
    $g.DrawArc($pen, ($cx - $r), ($cy - $r), ($r * 2), ($r * 2), 128, 284)
    for ($i = 0; $i -lt 5; $i++) {
      $ang = (128 + 284 * $i / 4) * [Math]::PI / 180
      $g.DrawLine($pen, [single]($cx + $r * 0.84 * [Math]::Cos($ang)), [single]($cy + $r * 0.84 * [Math]::Sin($ang)),
                       [single]($cx + $r * 1.10 * [Math]::Cos($ang)), [single]($cy + $r * 1.10 * [Math]::Sin($ang)))
    }
  }
  $pen.Dispose()
}

# ── 入口板形体：四角各自偏移的「圆角四边形」（2026-09-26 九次修正） ──
# 板身局部坐标：原点 = 板身左上角，x 向右、y 向下。**贴图像素尺寸一律叫 $wt/$ht**
# —— PowerShell 变量大小写不敏感，$W 和 $w 是同一个变量，写成 $W = $w * $S 会把屏幕尺寸也乘掉。
# 先按「右端收缩」做梯形，再绕板身竖直中线做端头斜切；场景里的倾斜由 Rotation Z 另加。
function Get-EntryQuad([int]$w, [int]$h, [single]$shearDeg, [single]$taper) {
  $wt = $w * $S; $ht = $h * $S
  $k = [Math]::Tan($shearDeg * [Math]::PI / 180.0)
  $hr = $ht * $taper / 2.0
  $raw = @(@(0.0, 0.0), @($wt, $hr), @($wt, ($ht - $hr)), @(0.0, $ht))
  $q = @()
  foreach ($c in $raw) { $q += ,@(($c[0] + $k * ($c[1] - $ht / 2.0)), $c[1]) }
  return $q
}
# 四边形双线性取点：(0,0) = 左上角 → (1,1) = 右下角；允许超出 [0,1]（板身外那一圈用得到）
function Get-QuadPoint($q, [double]$u, [double]$v) {
  $x = (1 - $u) * (1 - $v) * $q[0][0] + $u * (1 - $v) * $q[1][0] + $u * $v * $q[2][0] + (1 - $u) * $v * $q[3][0]
  $y = (1 - $u) * (1 - $v) * $q[0][1] + $u * (1 - $v) * $q[1][1] + $u * $v * $q[2][1] + (1 - $u) * $v * $q[3][1]
  return @($x, $y)
}
# 贴图几何（哈希表）：TW / TH = 贴图尺寸（贴图 px），DX / DY = 平移，Corners = 外扩一圈后的四角。
# 平移把**板心**放到贴图正中，所以场景里 centerPos 依旧按板心给、换贴图不用改位置。
function Get-EntryPlateMap([int]$w, [int]$h, [single]$shearDeg, [single]$taper) {
  $wt = $w * $S; $ht = $h * $S
  $q = Get-EntryQuad $w $h $shearDeg $taper
  $pu = $ENTRY_PAD / $wt; $pv = $ENTRY_PAD / $ht
  $corners = @(
    (Get-QuadPoint $q (-1 * $pu) (-1 * $pv)),
    (Get-QuadPoint $q (1 + $pu) (-1 * $pv)),
    (Get-QuadPoint $q (1 + $pu) (1 + $pv)),
    (Get-QuadPoint $q (-1 * $pu) (1 + $pv)))
  $xs = @(); $ys = @()
  foreach ($c in $corners) { $xs += $c[0]; $ys += $c[1] }
  $minX = ($xs | Measure-Object -Minimum).Minimum; $maxX = ($xs | Measure-Object -Maximum).Maximum
  $minY = ($ys | Measure-Object -Minimum).Minimum; $maxY = ($ys | Measure-Object -Maximum).Maximum
  $TW = [int][Math]::Ceiling($maxX - $minX) + 2 * $ENTRY_AA
  $TH = [int][Math]::Ceiling($maxY - $minY) + 2 * $ENTRY_AA
  $ctr = Get-QuadPoint $q 0.5 0.5
  $dx = $TW / 2.0 - $ctr[0]
  $dy = $TH / 2.0 - $ctr[1]
  if ((($minX + $dx) -lt 0) -or (($maxX + $dx) -gt $TW) -or (($minY + $dy) -lt 0) -or (($maxY + $dy) -gt $TH)) {
    throw "Get-EntryPlateMap: 外框超出贴图（$TW x $TH）"
  }
  return @{ TW = $TW; TH = $TH; DX = $dx; DY = $dy; Corners = $corners }
}
# 三点定仿射：把源三角形映到目标三角形（GDI+ 只有仿射，四边形靠两片三角形拼出来）
function Get-AffineFrom3($s, $d) {
  $x1 = $s[0][0]; $y1 = $s[0][1]; $x2 = $s[1][0]; $y2 = $s[1][1]; $x3 = $s[2][0]; $y3 = $s[2][1]
  $X1 = $d[0][0]; $Y1 = $d[0][1]; $X2 = $d[1][0]; $Y2 = $d[1][1]; $X3 = $d[2][0]; $Y3 = $d[2][1]
  $det = $x1 * ($y2 - $y3) - $y1 * ($x2 - $x3) + ($x2 * $y3 - $x3 * $y2)
  if ([Math]::Abs($det) -lt 1e-9) { throw 'Get-AffineFrom3: 三角形退化' }
  $a = ($X1 * ($y2 - $y3) - $y1 * ($X2 - $X3) + ($X2 * $y3 - $X3 * $y2)) / $det
  $b = ($x1 * ($X2 - $X3) - $X1 * ($x2 - $x3) + ($x2 * $X3 - $x3 * $X2)) / $det
  $e = ($x1 * ($y2 * $X3 - $y3 * $X2) - $y1 * ($x2 * $X3 - $x3 * $X2) + $X1 * ($x2 * $y3 - $x3 * $y2)) / $det
  $c = ($Y1 * ($y2 - $y3) - $y1 * ($Y2 - $Y3) + ($Y2 * $y3 - $Y3 * $y2)) / $det
  $fd = ($x1 * ($Y2 - $Y3) - $Y1 * ($x2 - $x3) + ($x2 * $Y3 - $x3 * $Y2)) / $det
  $f = ($x1 * ($y2 * $Y3 - $y3 * $Y2) - $y1 * ($x2 * $Y3 - $x3 * $Y2) + $Y1 * ($x2 * $y3 - $x3 * $y2)) / $det
  return (New-Object System.Drawing.Drawing2D.Matrix($a, $c, $b, $fd, $e, $f))
}
# 把一张图按四角映射画到 $g 上：两片三角形各做一次仿射，第二片沿外扩 0.7px 盖住接缝
function New-QuadWarpDraw($g, $src, [int]$srcW, [int]$srcH, $dst) {
  $src4 = @(@(0.0, 0.0), @([double]$srcW, 0.0), @([double]$srcW, [double]$srcH), @(0.0, [double]$srcH))
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $tris = @(@(0, 1, 3), @(1, 2, 3))
  $ti = 0
  foreach ($tr in $tris) {
    $s3 = @($src4[$tr[0]], $src4[$tr[1]], $src4[$tr[2]])
    $d3 = @($dst[$tr[0]], $dst[$tr[1]], $dst[$tr[2]])
    if ($ti -eq 1) {
      $cx0 = ($d3[0][0] + $d3[1][0] + $d3[2][0]) / 3.0
      $cy0 = ($d3[0][1] + $d3[1][1] + $d3[2][1]) / 3.0
      $d3 = @()
      foreach ($pt in @($dst[$tr[0]], $dst[$tr[1]], $dst[$tr[2]])) {
        $vx = $pt[0] - $cx0; $vy = $pt[1] - $cy0
        $len = [Math]::Sqrt($vx * $vx + $vy * $vy)
        if ($len -lt 1e-6) { $len = 1 }
        $d3 += ,@(($pt[0] + $vx / $len * 0.7), ($pt[1] + $vy / $len * 0.7))
      }
    }
    $tri = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tri.AddPolygon([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF([single]$d3[0][0], [single]$d3[0][1])),
      (New-Object System.Drawing.PointF([single]$d3[1][0], [single]$d3[1][1])),
      (New-Object System.Drawing.PointF([single]$d3[2][0], [single]$d3[2][1]))))
    $st = $g.Save()
    $g.SetClip($tri)
    $g.Transform = (Get-AffineFrom3 $s3 $d3)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $srcW, $srcH))
    $g.Restore($st); $tri.Dispose()
    $ti++
  }
}
# 入口板：先在板身局部坐标里画好（平底渐变 / 亮楔 / 徽记 / 墨边 / 金细线 / 左端标题槽），
# 再按四角映射成梯形 —— 装饰跟着形体一起动，不用逐件改坐标。
function New-LobbyEntryPlate([string]$out, [int]$w, [int]$h, [string]$kind, [single]$shearDeg = [single]::NaN, [single]$taper = [single]::NaN) {
  $shape = $ENTRY_SHAPE[$kind]
  if ($null -eq $shape) { throw "New-LobbyEntryPlate: 形体表里没有 '$kind'" }
  if ([single]::IsNaN($shearDeg)) { $shearDeg = $shape[1] }
  if ([single]::IsNaN($taper)) { $taper = $shape[2] }
  $map = Get-EntryPlateMap $w $h $shearDeg $taper
  $wt = $w * $S; $ht = $h * $S; $P = $ENTRY_PAD
  $srcW = [int]($wt + 2 * $P); $srcH = [int]($ht + 2 * $P)
  $res = New-Bmp $srcW $srcH; $sb = $res[0]; $g = $res[1]
  $g.TranslateTransform($P, $P)
  $R = 30; $INS = 24
  $outer = New-RoundPath 5 5 ($wt - 10) ($ht - 10) $R
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $BAR_T $BAR_B 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(($wt * 0.66), 0)),
    (New-Object System.Drawing.PointF(0, ($ht * 0.86))))) $BAR_T 74
  $st = $g.Save(); $g.SetClip($outer)
  New-EntryEmblem $g $kind ($wt * 0.755) ($ht * 0.50) ($ht * 0.30) 46
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $inner = New-RoundPath (5 + $INS) (5 + $INS) ($wt - 10 - 2 * $INS) ($ht - 10 - 2 * $INS) ($R - $INS)
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  $tx = 108.0
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 110)), $LW_GOLD
  $g.DrawLine($pen, $tx, 62, $tx, ($ht - 62)); $pen.Dispose()
  $dp = New-Object System.Drawing.SolidBrush (New-Col $GOLD 205)
  foreach ($dyy in @(62, ($ht - 62))) {
    $dm = New-Diamond $tx $dyy 11.0
    $g.FillPath($dp, $dm); $dm.Dispose()
  }
  $dp.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 96)), $LW_GOLD
  $g.DrawLine($pen, ($tx + 30), ($ht - 92), ($tx + 30 + $wt * 0.28), ($ht - 92)); $pen.Dispose()
  $outer.Dispose()
  $g.Dispose()
  $res2 = New-Bmp ([int]$map.TW) ([int]$map.TH); $b = $res2[0]; $gg = $res2[1]
  $dst = @()
  foreach ($c in $map.Corners) { $dst += ,@(($c[0] + $map.DX), ($c[1] + $map.DY)) }
  New-QuadWarpDraw $gg $sb $srcW $srcH $dst
  $sb.Dispose()
  return (Save-Bmp $b $gg $out)
}
# 板上文字落点（屏幕 px，相对**板心**的左上角）。板身局部就是 (58, 0.87×字号)：距板身左沿 58px、
# 字顶落在板心上方 0.87×字号。**不加贴图半尺寸** —— 贴图把板心放在正中，加到外框左上角的账是场景那边算的。
# 形体那点弯只有 1~3px，文字不跟着弯（TMP 是子物体，只跟板一起转），肉眼看不出。
function Get-EntryLabelOffset([int]$w, [int]$h, [single]$fontSize) {
  return @((58.0 - $w / 2.0), (0.87 * $fontSize - $h / 2.0))
}
# ── 下排两块共用一个**透明大框**（2026-09-26 十次修正） ───────────────────
# 用户：「下面两个小框实际上就是在一个整体的大框下」「这个大框是透明的只是用于限制位置」
#       「大框和上面两个的大小完全一样」。
# 所以下排**不出合并贴图**：房间 / 其它 仍是各自一张定尺贴图（各自的形体），位置则交给**一个不画的容器**：
#   容器 = 上面那两块同尺寸（666x145，屏幕 px），竖切成两格 —— 每格 ($BOX_W - $BOX_GAP) / 2 = 324 宽、145 高，
#   两块板各占一格、左右贴齐大框外沿（于是下排的左右沿与 战斗 完全对齐）。
#   落差：大框只 145 高、板已 130 高，塞不下原来那 57px 的错落 —— 两格同一行（$ENTRY_CELL 里 y 都是 0）。
#   $BOX_X / $BOX_Y  容器左上角在 1920x1080 版式里的位置（只在预览里用；场景里在 Inspector 拖）
#   $ENTRY_CELL      每块板在自己那格里的左上角（屏幕 px，相对容器左上角）—— 场景脚本照抄同一组数
$BOX_X = 1133.5; $BOX_Y = 668.5
$BOX_W = 666; $BOX_H = 145
$BOX_GAP = 18
$CELL_W = ($BOX_W - $BOX_GAP) / 2
$ENTRY_CELL = @{
  'room' = @(0.0, 0.0)
  'more' = @(($CELL_W + $BOX_GAP), 0.0)
}
function New-LobbyEntrySheet([string]$dir, [string]$out) {
  $W = 1800; $H = 800
  $res = New-Bmp $W $H; $b = $res[0]; $g = $res[1]
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 14, 21))
  $g.FillRectangle($bs, 0, 0, $W, $H); $bs.Dispose()
  Put-Text $g '入口板 ×4 —— 每块定尺贴图（屏幕 ×3）· 屏幕 1px = 贴图 3px · 场景里按屏幕尺寸缩下去画，圆角 10px / 墨边 3px / 金线 1px' 40 26 26 224
  $rows = @(
    @('LobbyEntryPlate_Battle.png', '战斗',      666, 145,  40,  90, 1.30),
    @('LobbyEntryPlate_Cards.png',  '卡牌总览',  661, 149, 960,  90, 1.30),
    @('LobbyEntryPlate_Room.png',   '房间',      324, 130,  40, 400, 1.30),
    @('LobbyEntryPlate_More.png',   '其它',      324, 130, 560, 400, 1.30))
  foreach ($r in $rows) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $r[0]))
    $dw = $r[2] * $r[6]; $dh = $r[3] * $r[6]
    $g.DrawImage($im, $r[4], $r[5], $dw, $dh); $im.Dispose()
    Put-Text $g ($r[1] + ' · ' + $r[0] + ' · ' + $r[2] + 'x' + $r[3]) $r[4] ($r[5] + $dh + 14) 20 200
  }
  Put-Text $g '图层：平底渐变 → 左上角一级亮楔（BAR_T α74）→ 右半徽记（金单色 α46）→ 外墨边 9 → 等比内缩金细线 24 / α150' 40 640 24 200
  Put-Text $g '          → 左端标题槽（竖线 α110 + 上下两枚菱形铆钉 r11 / α205）→ 标题下短线 α96。无发光、无倒角、无内阴影、无材质贴图。' 40 678 24 200
  Put-Text $g '徽记：战斗 = 六芒星（与棋盘中央 / 大厅中央徽记同源）· 卡牌总览 = 三张叠牌 · 房间 = 拱门 · 其它 = 环带刻度（棋盘外圈同源）· 场景里各按自己的倾角（上面朝左下 / 中间不倾 / 下面朝左上）' 40 740 24 200
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
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
# 以 ($cx,$cy) 为轴把文字转 $rot 度，(dx,dy) 是相对该轴的文字左上角 —— 入口板上的字要跟板同角度
function Put-TextRot($g, [string]$s, [single]$cx, [single]$cy, [single]$dx, [single]$dy, [single]$px, [single]$rot, [int]$a = 236) {
  $f = Get-F $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $st = $g.Save()
  $g.TranslateTransform($cx, $cy)
  $g.RotateTransform($rot)
  $g.DrawString($s, $f, $br, $dx, $dy)
  $g.Restore($st); $br.Dispose(); $f.Dispose()
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
function New-LobbyUiMockup([string]$dir, [string]$out, [hashtable]$shape = $null, [switch]$guide) {
  if ($shape) { $script:ENTRY_SHAPE = $shape }
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
  # 上排两块入口板：每块按自己的形体（倾斜 / 端头斜切 / 远端收缩）——板心位置不变（贴图把板心放正中）
  # 上面两块已加长（558→666 / 553→661，右端不动、往左长），左端越过下面两块的最左点，下面的侧边不再露出来
  # 下排两块：位置 = **透明大框**（$BOX_X/$BOX_Y，容器本身不画）+ 各自格子左上 $ENTRY_CELL —— 容器只限位
  # 2026-09-26：整簇平移 → **卡牌总览中心 = 右半屏正中 (1440, 540)**（原稿中心 1336.5, 487.5，即整体 +103.5 / +52.5）
  # 板上只写入口名，文字跟板同角度（不跟形体弯）、字号按板大小分档
  $main = @(
    @(1133.5, 277.5, 666, 145, '战斗',   44, 'LobbyEntryPlate_Battle.png', 'battle'),
    @(1109.5, 465.5, 661, 149, '卡牌总览', 46, 'LobbyEntryPlate_Cards.png', 'cards'))
  foreach ($c in @(
      @('room', '房间', $CELL_W, 130, 30, 'LobbyEntryPlate_Room.png'),
      @('more', '其它', $CELL_W, 130, 30, 'LobbyEntryPlate_More.png'))) {
    $cell = $ENTRY_CELL[$c[0]]
    $main += ,@(($BOX_X + $cell[0]), ($BOX_Y + $cell[1]), $c[2], $c[3], $c[1], $c[4], $c[5], $c[0])
  }
  foreach ($m in $main) {
    $cx = $m[0] + $m[2] / 2; $cy = $m[1] + $m[3] / 2
    $sh = $ENTRY_SHAPE[$m[7]]
    # 形体表第一个数是 **Unity 口径**（+ = 右端高，只给场景的 Rotation Z）—— GDI 正角是**顺时针 = 右端低**，
    # 所以预览里要取相反号，否则预览和场景会左右反过来（2026-09-26 十次修正顺手修掉）。
    $rot = -1 * $sh[0]
    $map = Get-EntryPlateMap $m[2] $m[3] $sh[1] $sh[2]
    Draw-Sprite $g (Join-Path $dir $m[6]) $cx $cy ($map.TW / $S) ($map.TH / $S) $rot
    $lp = Get-EntryLabelOffset $m[2] $m[3] $m[5]
    Put-TextRot $g $m[4] $cx $cy $lp[0] $lp[1] $m[5] $rot 238
  }
  # 可选的「限位网格」层：只画给设计看 —— 透明大框 + 两个格子（正式预览里不画）
  if ($guide) {
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 120, 210, 236)), 2
    $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $g.DrawRectangle($pen, $BOX_X, $BOX_Y, $BOX_W, $BOX_H)
    foreach ($c in @(@('room', $CELL_W, 130), @('more', $CELL_W, 130))) {
      $cell = $ENTRY_CELL[$c[0]]
      $g.DrawRectangle($pen, ($BOX_X + $cell[0]), ($BOX_Y + $cell[1]), $c[1], $c[2])
    }
    $pen.Dispose()
    Put-Text $g ('透明大框（不画 · 只限位）· ' + $BOX_W + 'x' + $BOX_H + ' @ ' + $BOX_X + ',' + $BOX_Y + ' · 左格 0,0 · 右格 ' + $ENTRY_CELL['more'][0] + ',' + $ENTRY_CELL['more'][1]) ($BOX_X - 6) ($BOX_Y - 28) 20 224
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
  Put-Text $g '大厅 UI 占位 v1 · 入口板按各自形体（倾斜 1~2° 方向不一 + 端头斜切 + 远端收缩 2~3%）· 板间法线净距 40 · 左半屏留空 · 下排两块 = 一个透明大框（只限位、不画）的两个格子' 24 ($H - 44) 24 220
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# ── 预览：形体对照表（四块板各自形体，逐行换方案） ──────────────
function New-EntryShapeSheet([string]$genDir, [string]$out) {
  $tmp = Join-Path $env:TEMP 'lobby-shape-variants'
  if (-not (Test-Path $tmp)) { New-Item -ItemType Directory -Path $tmp | Out-Null }
  Get-ChildItem (Join-Path $genDir '*.png') | Copy-Item -Destination $tmp -Force
  $flat = @{
    'battle' = @( 5.0, -5.0, 0.0); 'cards' = @( 5.0, -5.0, 0.0)
    'room'   = @( 5.0, -5.0, 0.0); 'more'  = @( 5.0, -5.0, 0.0) }
  $mix = @{
    'battle' = @( 1.6, -1.2, 0.030); 'cards' = @( 0.0, -0.8, 0.026)
    'room'   = @(-1.5,  1.0, 0.024); 'more'  = @(-1.5,  1.0, 0.024) }
  $deep = @{
    'battle' = @( 1.6, -1.2, 0.060); 'cards' = @( 0.0, -0.8, 0.052)
    'room'   = @(-1.5,  1.0, 0.048); 'more'  = @(-1.5,  1.0, 0.048) }
  $level = @{
    'battle' = @( 0.2, -0.6, 0.030); 'cards' = @( 0.0, -0.5, 0.026)
    'room'   = @(-0.3,  0.5, 0.024); 'more'  = @(-0.3,  0.5, 0.024) }
  $variants = @(
    @('v0  现状：四块统一 Rotation 5° + 统一斜切 -5°、左右端等高（看着就是「斜放的矩形」）', $flat),
    @('v1  低幅 + 方向不统一 + 远端收缩 2~3%（推荐：倾斜 1~2°、收缩 3%）', $mix),
    @('v2  同 v1，但远端收缩加倍（收缩 4~6%）—— 看收缩这一项有没有用', $deep),
    @('v3  近乎水平（倾斜 ≤0.6°）+ 同样收缩 —— 最接近明日方舟那种「几乎摆正、只有一点点」', $level))
  $cropX = 985; $cropY = 190; $cropW = 745; $cropH = 650
  $sc = 0.78
  $cw = [int]($cropW * $sc); $ch = [int]($cropH * $sc)
  $sheetW = 1240; $sheetH = 70 + $variants.Count * ($ch + 56)
  $res = New-Bmp $sheetW $sheetH; $b = $res[0]; $g = $res[1]
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 13, 19))
  $g.FillRectangle($bs, 0, 0, $sheetW, $sheetH); $bs.Dispose()
  Put-Text $g '入口板形体对照（每行只换形体表；板心与位置完全一致）' 24 18 26 230
  $row = 0
  foreach ($v in $variants) {
    $script:ENTRY_SHAPE = $v[1]
    foreach ($e in @(@('Battle', 666, 145, 'battle'), @('Cards', 661, 149, 'cards'), @('Room', $CELL_W, 130, 'room'), @('More', $CELL_W, 130, 'more'))) {
      New-LobbyEntryPlate (Join-Path $tmp ('LobbyEntryPlate_' + $e[0] + '.png')) $e[1] $e[2] $e[3] | Out-Null
    }
    $mock = Join-Path $tmp 'mock.png'
    New-LobbyUiMockup $tmp $mock $v[1]
    $im = [System.Drawing.Image]::FromFile($mock)
    $y = 64 + $row * ($ch + 56)
    $g.DrawImage($im, (New-Object System.Drawing.Rectangle 20, $y, $cw, $ch), (New-Object System.Drawing.Rectangle $cropX, $cropY, $cropW, $cropH), [System.Drawing.GraphicsUnit]::Pixel)
    $im.Dispose()
    Put-Text $g $v[0] 20 ($y + $ch + 12) 22 220
    $row++
  }
  $g.Dispose(); $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out

}# ── 主流程 ─────────────────────────────────────────────────
$made = @()
foreach ($t in @(@('LobbyPanelPlate.png', 0), @('LobbyBtnPlate.png', 0), @('LobbyBtnPlateHover.png', 1), @('LobbyBtnPlatePressed.png', 2))) {
  $r = New-PlateBmp 192 192 $PLATE_R $t[1]
  $made += (Save-Bmp $r[0] $r[1] (Join-Path $GEN $t[0]))
}
$made += (New-LobbyBand (Join-Path $GEN 'LobbyBandRight.png'))
$made += (New-LobbyProfilePlate (Join-Path $GEN 'LobbyProfilePlate.png'))
$made += (New-LobbyAvatarRing (Join-Path $GEN 'LobbyAvatarRing.png'))
foreach ($e in @(@('Battle', 666, 145, 'battle'), @('Cards', 661, 149, 'cards'), @('Room', $CELL_W, 130, 'room'), @('More', $CELL_W, 130, 'more'))) {
  $made += (New-LobbyEntryPlate (Join-Path $GEN ('LobbyEntryPlate_' + $e[0] + '.png')) $e[1] $e[2] $e[3])
}
foreach ($k in @('gear', 'friend', 'shop', 'event', 'tutorial', 'coin', 'ticket')) {
  $nm = 'Icon_Lobby' + $k.Substring(0,1).ToUpper() + $k.Substring(1) + '.png'
  $made += (New-LobbyIcon $k (Join-Path $GEN $nm))
}
$sheet = Join-Path $PREV 'lobby-ui-v1-sheet.png'
$mock  = Join-Path $PREV 'lobby-ui-v1-mockup.png'
$ent   = Join-Path $PREV 'lobby-ui-v1-entryplates.png'
$grid  = Join-Path $PREV 'lobby-ui-v1-grid.png'
New-LobbyUiSheet $GEN $sheet
New-LobbyEntrySheet $GEN $ent
New-LobbyUiMockup $GEN $mock
New-LobbyUiMockup $GEN $grid $null -guide
New-EntryShapeSheet $GEN (Join-Path $PREV 'lobby-ui-v1-shape.png') | Out-Null
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
"entry  : $ent"
"mockup : $mock"
"grid   : $grid"
