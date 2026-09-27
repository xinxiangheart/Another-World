# 战斗加载界面 v1 —— 「双方确认后的全遮挡加载界面」（2026-09-27）
#
# 用户 2026-09-27：真正的双方确认后的加载界面，**全遮挡**（包括遮住左上角头像 / 右上角图标）；
#   上半区域背景从右边滑动到最左边，下半区域背景从左边滑动到最右边；
#   上半区域从右向左展示对方头像（下面是名称和未来做的称号），再左边是预留的段位，再左边是总场次，
#   其下面是胜率，其再下面是连胜场次；己方的从左到右展示在左下角；双方信息只占半区；
#   黄色区域是一个**动态旋转**的装饰性不对称图案；下面是双方加载进程，只用 xx% 显示；
#   加载完成后上半很快从右边滑出、下半从左边滑出；加载过程中上下两半有**惯性的缓慢移动**。
#
# 手法同全套 UI：深蓝黑石面 + 一条金细线 + 平板；禁倒角 / 内阴影 / 外发光 / 双层面板 / 材质贴图。
# 色值一律取共享调色板（TopBarV2 -> CardFrameV6）。
#
# 产物（Assets/_Game/Art/Sprites/Generated/battle-loading-v1/）
#   Loading_BandBg.png    4800x1120 = 屏幕 2400x560（2x）—— 半区条带底：比屏幕宽 480（每侧 240），
#      惯性只朝一个方向走（上带向左 / 下带向右），这 240 就是"走到底也不露边"的余量；
#   Loading_Ornament.png  1280x1280 = 屏幕 640x640 （2x）—— 旋转的不对称饰纹（场景按 240 显示）
#   Loading_RankSlot.png   340x340  = 屏幕 170x170 （2x）—— 预留段位位（盾形 + 菱形占位）
#   头像框复用 match-confirm-v1/Confirm_Frame.png（运行时染金 #C8A44A），不再出一张
#
# 版式（屏幕 px，参考 1920x1080，中心为原点，+y 向上）：
#   带 1920x560，上带中心 (0,+280)、下带中心 (0,-280) → 两带内缘在 y=0 相接 = 分隔线
#   带内（局部坐标）：上带 头像(+740,+45)/名字(+740,-75)/称号(+740,-115)/段位(+430,+35)/战绩列(x=+185, y=+85/+35/-15)/饰纹(-560,0)
#                     下带 X 镜像（头像(-740,+45) … 饰纹(+560,0)）
#   进度 xx% 在根上 (0,-470)，不随带滑动
#
# 预览（Tools/cardframe/preview/battle-loading-v1.png）：进场中 / 停位 / 滑出 三格
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/battle-loading-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }

$S = 2
$BAND_W = 2400; $BAND_H = 560   # 条带比屏幕宽 480：每侧 $BAND_PAD 是惯性"走到底也不露边"的余量
$BAND_PAD = 240                    # 每侧多画的屏幕 px（= 运行时的 driftMax）
$ORN_X   = 560                    # 饰纹在带内的 |x|（场景里也是这个值）
$ORN_W  = 640;  $ORN_H  = 640
$RANK_W = 170;  $RANK_H = 170
$ORN_SCENE = 240                   # 饰纹在场景里的屏幕尺寸（旧版 480）

$GOLD_A  = 235
$LW_INK  = 10     # 2x -> 屏幕 5
$LW_GOLD = 3

function Get-BlFont([single]$px) {
  if ($script:BlFC -eq $null) {
    $script:BlFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:BlFC.AddFontFile($FONT)
  }
  return New-Object System.Drawing.Font($script:BlFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
function Put-BlTxt($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 236, [int]$rr = 240, [int]$gg = 232, [int]$bb = 210) {
  $f = Get-BlFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $rr, $gg, $bb))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function Put-BlTxtC($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int]$a = 236, [int]$rr = 240, [int]$gg = 232, [int]$bb = 210) {
  $f = Get-BlFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $rr, $gg, $bb))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}

# ══════════════════════════════════════════════════════════
# 1. 半区条带底 Loading_BandBg.png
#    整块平底（BAR_T -> BAR_B 竖渐变）+ 一圈墨边 + 上缘一条金细线 + 两道极淡的方向性斜光
#    2026-09-27 用户：「上下背景可以稍微加些暗金线纹路状装饰」-> Add-BlEngraving：
#      上缘量尺刻度带 + 四条通长暗金细线（中间那条断开避开内容）+ 两侧对称的同心刻环。
#      全部 GOLD_D 细线、α ≤ 58，只做「刻在石面上」的一层，不做出材质贴图的错觉。
#    暗饰一律画在贴图 row 60..1060 之内：上带顶边有 20 屏幕 px 落在屏外、下带底边同理，
#      只有这一段对上下两半都可见（否则上带看不到刻度、下带看不到底纹，就不对称了）。
#    上下两半共用：金线只在上缘 —— 上带那条落到屏幕外，下带那条正好落在 y=0 当分隔金线
# ══════════════════════════════════════════════════════════
function Add-BlEngraving($g, [single]$wt, [single]$ht) {
  # 1) 上缘量尺刻度带（长短交替，越靠两端越淡）
  $ty = 76.0
  for ($x = 46.0; $x -lt ($wt - 46.0); $x += 64.0) {
    $i = [int][Math]::Floor($x / 64.0)
    if     (($i % 4) -eq 0) { $len = 26.0; $a = 72 }
    elseif (($i % 2) -eq 0) { $len = 16.0; $a = 54 }
    else                    { $len = 9.0;  $a = 38 }
    $fade = [Math]::Min(1.0, [Math]::Min($x / 240.0, ($wt - $x) / 240.0))
    $aa = [int]($a * $fade)
    if ($aa -le 2) { continue }
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D $aa)), 3.4
    $g.DrawLine($pen, [single]$x, [single]$ty, [single]$x, [single]($ty + $len))
    $pen.Dispose()
  }

  # 2) 通长暗金细线（2 条贯通 + 1 条在中段断开，避开正面内容）
  foreach ($seg in @(@(0.185, 0.035, 0.965), @(0.5, 0.035, 0.315), @(0.5, 0.685, 0.965), @(0.815, 0.035, 0.965))) {
    $yy = $ht * $seg[0]
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 34)), 2.2
    $g.DrawLine($pen, [single]($wt * $seg[1]), [single]$yy, [single]($wt * $seg[2]), [single]$yy)
    $pen.Dispose()
  }

  # 3) 两侧对称的同心刻环（饰纹位置 / 头像位置各一组，半径 <= 508 保证整只落在可见区）
  foreach ($side in @((0.5 - $ORN_X / $BAND_W), (0.5 + $ORN_X / $BAND_W))) {
    $ox = $wt * $side; $oy = $ht * 0.5
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 30)), 3.4
    $g.DrawArc($pen, [single]($ox - 380), [single]($oy - 380), 760.0, 760.0, -70, 300)
    $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 19)), 2.6
    $g.DrawArc($pen, [single]($ox - 452), [single]($oy - 452), 904.0, 904.0, 40, 232)
    $pen.Dispose()
  }
}

function New-BlBand([string]$out) {
  $wt = $BAND_W * $S; $ht = $BAND_H * $S
  $res = New-Bmp $wt $ht; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($wt - $LW_INK) ($ht - $LW_INK) 2
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $BAR_T $BAR_B 255 255
  # 两道方向相反的极淡斜光（不对称，让整块不那么"死"）
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($wt * 0.34), 0)),
    (New-Object System.Drawing.PointF(0, ($ht * 0.62))))) $BAR_T 58
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($wt, $ht)),
    (New-Object System.Drawing.PointF(($wt * 0.63), $ht)),
    (New-Object System.Drawing.PointF($wt, ($ht * 0.44))))) $BAR_T 46
  # 暗金线纹路（2026-09-27 用户）
  Add-BlEngraving $g $wt $ht
  $g.Restore($st)
  # 外墨边
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  # 上缘一条金细线（内缩 7 屏幕 px）
  $iy = 7.0 * $S
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $GOLD_A)), $LW_GOLD
  $g.DrawLine($pen, [single]($oi + 6), [single]$iy, [single]($wt - $oi - 6), [single]$iy)
  $pen.Dispose()
  $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

# ══════════════════════════════════════════════════════════
# 2. 旋转饰纹 Loading_Ornament.png
#    2026-09-27 用户：「重出装饰型旋转装上物，现在这个过大且过简陋」→「更小点，没有必要非要是个完整的圆环吧？」
#    迭代记录：v1（缩小 + 锯齿圈）读成"破多边形"；v2（完整圆环 + 36 格刻度盘）判「还是大、还是简陋」；
#              v3（232° 大环 + 断口）判「还是一只圈」—— 都不对。
#    v4（当前）＝ **旋臂盘**：不画圆，把「环带刻度」这条母题拆成 4 段同心弧带，
#      半径 578 / 470 / 352 / 252 逐级收紧、起角逐段错开，串成一条从外绕到内的旋臂（合计约 498°）：
#        · 每段 = 双线夹的环带（带宽 56 / 50 / 44 / 36），带内每 10° 一根刻度、每 3 根一根通带长刻度
#        · 每段起角压一颗菱形铆钉（当作"关节"）—— 四颗的角与半径都不同，所以没有 4 次对称
#        · 中心：一条断口宝石座弧（r=186）+ 4 根微刻 + 带芯菱形
#      一眼看得出**不是圆环**：每条半径上都有断口，四段只是同心、半径不同 → 螺旋臂。
#    尺寸：外沿只画到 R=578（画布半宽 640），场景按 $ORN_SCENE=240 显示 → 外沿 Ø≈217 屏幕 px（旧版 Ø≈330）；
#      画布四角空着，等于"不占满格子"，观感上还会再小一档。
#    线宽一律 >=5 tex px：显示缩到 240 后 1 tex px 只有 0.1875 屏幕 px，4 tex px 只有 0.75 屏幕 px，会被 mipmap 吃掉。
# ══════════════════════════════════════════════════════════
function Bl-Pt([single]$cx, [single]$cy, [single]$deg, [single]$r) {
  $a = $deg * [Math]::PI / 180.0
  return (New-Object System.Drawing.PointF([single]($cx + [Math]::Cos($a) * $r), [single]($cy + [Math]::Sin($a) * $r)))
}
function New-BlOrnament([string]$out) {
  $wt = $ORN_W * $S; $ht = $ORN_H * $S
  $res = New-Bmp $wt $ht; $b = $res[0]; $g = $res[1]
  $cx = $wt / 2.0; $cy = $ht / 2.0
  $cap  = [System.Drawing.Drawing2D.LineCap]::Round
  $join = [System.Drawing.Drawing2D.LineJoin]::Round

  # 1) 四段同心弧带 —— 半径逐级收紧、起角逐段错开，串成一条旋臂
  $arms = @(
    @{ Ro = 578.0; Ri = 522.0; A = -78.0; Sw = 126.0 },
    @{ Ro = 470.0; Ri = 420.0; A =  34.0; Sw = 142.0 },
    @{ Ro = 352.0; Ri = 308.0; A = 168.0; Sw = 126.0 },
    @{ Ro = 252.0; Ri = 214.0; A = 300.0; Sw = 104.0 }
  )
  foreach ($ar in $arms) {
    foreach ($q in @(@($ar.Ro, 196, 6.0), @($ar.Ri, 132, 5.2))) {
      $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $q[1])), $q[2]
      $pen.StartCap = $cap; $pen.EndCap = $cap
      $g.DrawArc($pen, [single]($cx - $q[0]), [single]($cy - $q[0]), [single]($q[0] * 2), [single]($q[0] * 2), $ar.A, $ar.Sw)
      $pen.Dispose()
    }
    # 带内刻度
    $mid = ($ar.Ro + $ar.Ri) / 2.0
    $n = [int][Math]::Floor($ar.Sw / 10.0)
    for ($k = 1; $k -lt $n; $k++) {
      $a = $ar.A + $k * ($ar.Sw / $n)
      if (($k % 3) -eq 0) {
        $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 142)), 5.6
        $g.DrawLine($pen, (Bl-Pt $cx $cy $a $ar.Ri), (Bl-Pt $cx $cy $a $ar.Ro))
      } else {
        $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 82)), 5.0
        $g.DrawLine($pen, (Bl-Pt $cx $cy $a ($ar.Ri + 4)), (Bl-Pt $cx $cy $a $mid))
      }
      $pen.StartCap = $cap; $pen.EndCap = $cap
      $pen.Dispose()
    }
    # 段首"关节"菱形铆钉
    $dc = Bl-Pt $cx $cy $ar.A $mid
    $dp = New-Diamond $dc.X $dc.Y 26
    $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 44)
    $g.FillPath($bs, $dp); $bs.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 176)), 5.2
    $pen.LineJoin = $join; $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  }

  # 2) 中心：断口宝石座弧 + 4 根微刻 + 带芯菱形
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 84)), 5.0
  $pen.StartCap = $cap; $pen.EndCap = $cap
  $g.DrawArc($pen, [single]($cx + 12 - 186), [single]($cy - 14 - 186), 372.0, 372.0, 210, 140)
  $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 78)), 5.0
  $pen.StartCap = $cap; $pen.EndCap = $cap
  foreach ($a in @(45, 135, 225, 315)) { $g.DrawLine($pen, (Bl-Pt $cx $cy $a 118), (Bl-Pt $cx $cy $a 146)) }
  $pen.Dispose()
  $dp = New-Diamond $cx $cy 40
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 58)
  $g.FillPath($bs, $dp); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 200)), 5.6
  $pen.LineJoin = $join; $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()
  $dp = New-Diamond $cx $cy 16
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 138)), 5.0
  $pen.LineJoin = $join; $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose()

  return (Save-Bmp $b $g $out)
}

# ══════════════════════════════════════════════════════════
# 3. 预留段位 Loading_RankSlot.png（小平板 + 盾形 + 菱形占位）
# ══════════════════════════════════════════════════════════
function New-BlRank([string]$out) {
  $wt = $RANK_W * $S; $ht = $RANK_H * $S
  $res = New-Bmp $wt $ht; $b = $res[0]; $g = $res[1]
  $oi = 8.0 / 2.0
  $outer = New-RoundPath $oi $oi ($wt - 8) ($ht - 8) 10
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $BAR_T $BAR_B 255 255
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($wt * 0.62), 0)),
    (New-Object System.Drawing.PointF(0, ($ht * 0.56))))) $BAR_T 60
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), 8.0
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $ix = $oi + 9
  $inner = New-RoundPath $ix $ix ($wt - 2 * $ix) ($ht - 2 * $ix) 5
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 3.0
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $outer.Dispose()

  # 盾形（段位位）
  # 注意：PowerShell 变量不区分大小写 —— $B 会盖掉位图 $b，所以这里一律用 $shL/$shR/$shT/$shB
  $shL = $wt * 0.30; $shR = $wt * 0.70; $shT = $ht * 0.21; $shB = $ht * 0.81
  $cw = $shR - $shL
  $y1 = $shT + ($shB - $shT) * 0.42
  $y2 = $shT + ($shB - $shT) * 0.78
  $sh = New-Object System.Drawing.Drawing2D.GraphicsPath
  $sh.StartFigure()
  $sh.AddLine([single]$shL, [single]$shT, [single]$shR, [single]$shT)
  $sh.AddLine([single]$shR, [single]$shT, [single]$shR, [single]$y1)
  $sh.AddBezier([single]$shR, [single]$y1, [single]$shR, [single]$y2, [single]($wt * 0.5 + $cw * 0.30), [single]$shB, [single]($wt / 2.0), [single]$shB)
  $sh.AddBezier([single]($wt / 2.0), [single]$shB, [single]($shL - $cw * 0.30), [single]$y2, [single]$shL, [single]$y2, [single]$shL, [single]$y1)
  $sh.CloseFigure()
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 26)
  $g.FillPath($bs, $sh); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 210)), 6.0
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $sh); $pen.Dispose()
  $dp = New-Diamond ($wt / 2.0) ($ht * 0.45) ($wt * 0.12)
  $bs = New-Object System.Drawing.SolidBrush (New-Col $GOLD 60)
  $g.FillPath($bs, $dp); $bs.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 190)), 5.0
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dp); $pen.Dispose(); $dp.Dispose(); $sh.Dispose()

  return (Save-Bmp $b $g $out)
}

# ══════════════════════════════════════════════════════════
# 预览：进场中 / 停位 / 滑出 三格（1920x1080 版式换算）
# ══════════════════════════════════════════════════════════
function Bl-ScreenPt([single]$x, [single]$y, [single]$k, [single]$ox, [single]$topBase, [single]$flip) {
  # 屏幕坐标（中心原点，+y 向上）-> 预览像素。$ox = 带偏移，$flip = 1 上带 / -1 下带
  return (New-Object System.Drawing.PointF(
    [single]((960 + ($x * $flip) + $ox) * $k),
    [single](($topBase - $y) * $k)))
}
function Bl-DrawScreenView($g, [System.Drawing.Image]$img, [single]$dx, [single]$dy, [single]$w, [single]$h, [single]$ox) {
  # 把「屏幕真正看到的那一块条带」画进预览格：源矩形随带的偏移 $ox 平移，越界部分自然留空。
  # 条带比屏幕宽 2*$BAND_PAD，所以 |$ox| <= $BAND_PAD 时永远取得到源（2026-09-27 起）
  $sw = 1920.0 * $S; $sh = $BAND_H * $S
  $sx = ($BAND_PAD - $ox) * $S
  $ix = [Math]::Max(0.0, $sx)
  $iw = [Math]::Min($sw, $img.Width - $ix)
  if ($iw -le 0) { return }
  $f = $iw / $sw
  $ddx = $dx + ($ix - $sx) / $sw * $w
  $g.DrawImage($img,
    (New-Object System.Drawing.RectangleF($ddx, $dy, [single]($w * $f), $h)),
    (New-Object System.Drawing.RectangleF([single]$ix, 0.0, [single]$iw, [single]$sh)),
    [System.Drawing.GraphicsUnit]::Pixel)
}
function New-BlPreview($dir, [string]$out) {
  $CW = 1920; $CH = 1500
  $b = New-Object System.Drawing.Bitmap($CW, $CH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 9, 12, 18))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-BlTxt $g '战斗加载界面 v1（双方确认后的全遮挡加载）' 24 12 26
  Put-BlTxt $g '上带从右滑入 / 下带从左滑入；进场后两带慢速惯性位移，只朝一个方向、只沿左右（上带向左 / 下带向右）；转完出结果：上带向右滑出、下带向左滑出' 24 46 16 176

  $bandImg = [System.Drawing.Image]::FromFile((Join-Path $dir 'Loading_BandBg.png'))
  $ornImg  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Loading_Ornament.png'))
  $rankImg = [System.Drawing.Image]::FromFile((Join-Path $dir 'Loading_RankSlot.png'))

  $k = 0.30                       # 每格缩到 30%
  $cells = @(
    @{ X = 6;    Ox = 1250;  Tag = '① 进场中：上带还在右边、下带还在左边（背景也是从两侧滑进来的）' },
    @{ X = 646;  Ox = 0;     Tag = '② 停位：上下各占半区，信息只占各自半区；饰纹自转；底部 xx%' },
    @{ X = 1286; Ox = -1250; Tag = '③ 加载完成：上带向右、下带向左迅速滑出' }
  )
  $tw = 1920.0 * $k; $th = $BAND_H * $k
  foreach ($c in $cells) {
    $gx = $c.X
    $g.DrawRectangle((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 40, 50, 64)), 1), $gx, 74, 628, 356)
    Bl-DrawScreenView $g $bandImg $gx 74 $tw $th $c.Ox              # 上带（屏幕视图）
    Bl-DrawScreenView $g $bandImg $gx (74 + $th) $tw $th (-$c.Ox)   # 下带（左右相反）
    if ($c.Ox -lt 800) {
      $o = Bl-ScreenPt -540 -45 1 $c.Ox 270 1; $g.DrawImage($ornImg, [single]($gx + $o.X - $ORN_SCENE * $k / 2), [single]($o.Y - $ORN_SCENE * $k / 2), [single]($ORN_SCENE * $k), [single]($ORN_SCENE * $k))
      $a = Bl-ScreenPt 740 45 1 $c.Ox 270 1
      $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 190, 200, 214))
      $g.FillEllipse($bs, [single]($gx + $a.X - 90 * $k), [single]($a.Y - 90 * $k), [single](180 * $k), [single](180 * $k)); $bs.Dispose()
      Put-BlTxtC $g '对方名' ($gx + $a.X) ($a.Y + 118 * $k) 26 236
      Put-BlTxtC $g '称号·待定' ($gx + $a.X) ($a.Y + 142 * $k) 17 146 142 162 180
      $r = Bl-ScreenPt 430 35 1 $c.Ox 270 1
      $g.DrawImage($rankImg, [single]($gx + $r.X - 170 * $k / 2), [single]($r.Y - 170 * $k / 2), [single](170 * $k), [single](170 * $k))
      foreach ($row in @(@('总场次 128', 85), @('胜率 55.3%', 35), @('连胜 3', -15))) {
        $sp = Bl-ScreenPt 185 $row[1] 1 $c.Ox 270 1
        Put-BlTxtC $g $row[0] ($gx + $sp.X) $sp.Y 22 236
      }
      # 下带内容（flip=-1）
      $o2 = Bl-ScreenPt 540 -45 1 $c.Ox 810 -1
      $g.DrawImage($ornImg, [single]($gx + $o2.X - $ORN_SCENE * $k / 2), [single]($o2.Y - $ORN_SCENE * $k / 2), [single]($ORN_SCENE * $k), [single]($ORN_SCENE * $k))
      $a2 = Bl-ScreenPt 740 45 1 $c.Ox 810 -1
      $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 190, 200, 214))
      $g.FillEllipse($bs, [single]($gx + $a2.X - 90 * $k), [single]($a2.Y - 90 * $k), [single](180 * $k), [single](180 * $k)); $bs.Dispose()
      Put-BlTxtC $g '己方名' ($gx + $a2.X) ($a2.Y + 118 * $k) 26 236
      Put-BlTxtC $g '称号·待定' ($gx + $a2.X) ($a2.Y + 142 * $k) 17 146 142 162 180
      $r2 = Bl-ScreenPt 430 35 1 $c.Ox 810 -1
      $g.DrawImage($rankImg, [single]($gx + $r2.X - 170 * $k / 2), [single]($r2.Y - 170 * $k / 2), [single](170 * $k), [single](170 * $k))
      foreach ($row in @(@('总场次 128', 85), @('胜率 55.3%', 35), @('连胜 3', -15))) {
        $sp = Bl-ScreenPt 185 $row[1] 1 $c.Ox 810 -1
        Put-BlTxtC $g $row[0] ($gx + $sp.X) $sp.Y 22 236
      }
      Put-BlTxtC $g '47%' ($gx + 320) 400 30 236
    }
    Put-BlTxt $g $c.Tag ($gx + 4) 438 16 176
  }

  Put-BlTxt $g '注：条带底上下共用一张 —— 金细线只画在「上缘」，上带那条落到屏幕外，下带那条正好落在 y=0 当中央分隔金线（所以分隔线只有一条金线）。' 24 470 16 150
  Put-BlTxt $g '　　条带出图 2400x560（屏幕 px），比屏幕宽 480 —— 每侧 240 就是惯性"走到底"的余量；饰纹按场景尺寸 240 显示，运行时整体慢速自转（12°/s）。' 24 494 16 150
  $by = 562
  Put-BlTxt $g '饰纹实尺（1:1 · 就是运行时屏幕看到的那一块条带 · 饰纹按场景真尺寸 240 落在带内 x=-560 / y=+10）' 24 536 16 176
  Bl-DrawScreenView $g $bandImg 0 $by 1920 $BAND_H 0
  $g.DrawRectangle((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 40, 50, 64)), 1), 0, $by, 1920, $BAND_H)
  $ocx = 960 - 560; $ocy = $by + 280 - 10
  $g.DrawImage($ornImg, [single]($ocx - $ORN_SCENE / 2), [single]($ocy - $ORN_SCENE / 2), [single]$ORN_SCENE, [single]$ORN_SCENE)
  Put-BlTxt $g ("1:1  " + $ORN_SCENE + "px") 30 ($by + 12) 15 140
  $zx = 1250; $zy = $by + 300; $z = $ORN_SCENE * 2
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(214, 9, 12, 18))
  $g.FillRectangle($bs, [single]($zx - $z / 2 - 14), [single]($zy - $z / 2 - 14), [single]($z + 28), [single]($z + 28)); $bs.Dispose()
  $g.DrawImage($ornImg, [single]($zx - $z / 2), [single]($zy - $z / 2), [single]$z, [single]$z)
  Put-BlTxt $g '2x 放大' ($zx - $z / 2 + 4) ($zy - $z / 2 + 4) 15 140
  Put-BlTxt $g '　　说明：饰纹不是圆环 —— 四段同心弧带（r=578/470/352/252）起角逐段错开，串成一条约 498° 的旋臂；每条半径上都有断口，四颗关节铆钉的角与半径各部相同。' 24 ($by + 566) 16 150

  # ── 惯性极限检查：带走到头（每侧 240）时，屏幕左右缘还填不填得满（品红 = 露底）
  $ey = $by + 600
  Put-BlTxt $g '惯性极限检查（1:1 横切 · 品红 = 露底）：走到 driftMax = 240 时屏幕边缘仍被底纹填满' 24 $ey 16 176
  $eh = 100
  foreach ($strip in @(@{ Y = ($ey + 26); Ox = -$BAND_PAD; Tag = '上带向左走满 240 —— 注意屏幕右缘' }, @{ Y = ($ey + 150); Ox = $BAND_PAD; Tag = '下带向右走满 240 —— 注意屏幕左缘' })) {
    $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 0, 220))
    $g.FillRectangle($bs, 0, $strip.Y, 1920, $eh); $bs.Dispose()
    $sx = ($BAND_PAD - $strip.Ox) * $S
    $g.DrawImage($bandImg,
      (New-Object System.Drawing.Rectangle(0, [int]$strip.Y, 1920, $eh)),
      (New-Object System.Drawing.Rectangle([int]$sx, 300, [int](1920 * $S), [int]($eh * $S))),
      [System.Drawing.GraphicsUnit]::Pixel)
    Put-BlTxt $g $strip.Tag 10 ($strip.Y + 6) 15 240 240 240 240
  }
  Put-BlTxt $g '　　240 = 出图脚本的 $BAND_PAD（每侧多画的屏幕 px）= 运行时的 driftMax：单向惯性走得再久也只到 240，永远不露边。' 24 ($ey + 262) 16 150
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  $bandImg.Dispose(); $ornImg.Dispose(); $rankImg.Dispose()
  return $out
}

$made = @()
$made += (New-BlBand     (Join-Path $GEN 'Loading_BandBg.png'))
$made += (New-BlOrnament (Join-Path $GEN 'Loading_Ornament.png'))
$made += (New-BlRank     (Join-Path $GEN 'Loading_RankSlot.png'))
$sheet = New-BlPreview $GEN (Join-Path $PREV 'battle-loading-v1.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
