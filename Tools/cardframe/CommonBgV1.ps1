# 通用背景底板 v1 —— 供多个场景复用的底图（可重跑）
#
# 设计约束（用户 2026-09-26）：装饰要少、可以有暗纹；**必须支持单项（非等比）缩放而不奇怪**。
# 因此整张图只用三类元素：
#   ① 纵向渐变 / 大范围柔光       —— 拉伸只会变椭，读起来仍是「一片光」
#   ② 轴线直线（竖 / 斜，极淡）   —— 拉伸只是间距变了，形状不变
#   ③ 微尘（无定形亮点）          —— 拉伸仍是微尘
# 明确排除：圆、六芒星、菱形、任何有辨识轮廓的徽记 —— 那些一拉就废。
#
# 2026-09-26 返工（用户：不要暗斑 / 不要砖块纹路）：
#   - 去掉 Add-Pits 的暗色斑点（那批 1400 个暗块就是「暗斑」）
#   - 去掉石缝分块与 512 粗网格（那两套就是「砖块纹路」）
#   - 中央光池与四角压深改用本文件的 New-SoftPool（GDI+ SetSigmaBellShape 会在
#     笔刷中心留一个透明洞 —— 那正是中央那块「暗斑」的根因，见 UrBoardR 备注）
#
# 产物（Generated/common-bg-v1/）
#   CommonBack_A_clean.png   纯净：渐变 + 柔光池 + 微尘（无暗纹）
#   CommonBack_B_vline.png   光帘：A + 极淡竖向发丝线（256px 一道）
#   CommonBack_C_diag.png    斜纹：A + 极淡 45° 发丝线（64px 一道）
# 预览（Tools/cardframe/preview/）
#   common-bg-v1-sheet.png    三方案并排
#   common-bg-v1-aspect.png   拉成 21:9 / 4:3，验证非等比拉伸
#   common-bg-v1-center.png   中央 1:1 放大（自查有没有暗斑 / 环带）
#
# 色值全部来自 BoardLayersV2.ps1 的调色板（同一个变量），不另起配色。
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/BoardLayersV2.ps1"

$ROOT = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/common-bg-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }

# ── 可调参数 ────────────────────────────────────────────────
$W = 2048; $H = 1152
$POOL_CY    = 0.54     # 光心高度（0=顶 1=底）
$POOL_CORE  = 78       # 内层光池峰值 alpha
$POOL_HALO  = 46       # 外层光池峰值 alpha
$DUST_COUNT = 26       # 极淡微尘（亮点，不是暗斑）
$VIGNETTE_A = 110      # 四角压深峰值 alpha
$FRAME      = $true    # 内缩金细框（最边缘那一圈，几乎算「屏幕边框」不算装饰）
$FRAME_IN   = 46
$FRAME_A    = 34
$PAT_A      = 11       # 暗纹发丝线的 alpha（压得很低）
$PAT_LIGHT  = 6        # 暗纹旁边那条「受光」细线

# ── 平滑径向衰减（本地覆盖，不动 BoardLayersV2.ps1）──────────
# GDI+ PathGradientBrush + SetSigmaBellShape(focus) 实测不是「中心亮、边缘淡」：
# 它在中心留一个透明洞（sigma .92 实测 r=0 为 0、r=0.15·rx 才到峰值），
# 半径一放大这块洞就是画面中央的一块暗斑。这里改成显式的线性 alpha 衰减。
$asmCore = Join-Path (Split-Path ([System.Drawing.Color].Assembly.Location)) 'System.Drawing.Common.dll'
$asmPrim = [System.Drawing.Color].Assembly.Location
if (-not ('AwSoftGrad' -as [type])) {
  Add-Type -ReferencedAssemblies @($asmCore, $asmPrim) -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
public static class AwSoftGrad {
  // 从中心 (aMax) 线性衰减到椭圆边界 (0)，中心与边缘同色，只变 alpha
  public static void Pool(Graphics g, float cx, float cy, float rx, float ry, Color col, int aMax) {
    using (GraphicsPath p = new GraphicsPath()) {
      p.AddEllipse(cx - rx, cy - ry, rx * 2f, ry * 2f);
      using (PathGradientBrush pg = new PathGradientBrush(p)) {
        pg.CenterPoint = new PointF(cx, cy);
        pg.CenterColor = Color.FromArgb(aMax, col.R, col.G, col.B);
        pg.SurroundColors = new Color[] { Color.FromArgb(0, col.R, col.G, col.B) };
        g.FillPath(pg, p);
      }
    }
  }
}
"@
}
function New-SoftPool($g, [single]$cx, [single]$cy, [single]$rx, [single]$ry, [int[]]$col, [int]$aMax) {
  [AwSoftGrad]::Pool($g, $cx, $cy, $rx, $ry, (New-Col $col 255), $aMax)
}

function Stroke-Axis($g, [bool]$vertical, [single]$pos, [single]$from, [single]$to, [int]$dark, [int]$light) {
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 $dark)), 1.2
  if ($vertical) { $g.DrawLine($pen, $pos, $from, $pos, $to) } else { $g.DrawLine($pen, $from, $pos, $to, $pos) }
  $pen.Dispose()
  if ($light -gt 0) {
    $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D $light)), 1.0
    $off = $pos + 1.6
    if ($vertical) { $g.DrawLine($pen, $off, $from, $off, $to) } else { $g.DrawLine($pen, $from, $off, $to, $off) }
    $pen.Dispose()
  }
}

function New-CommonPlate([string]$out, [string]$pattern) {
  $r = New-Layer $W $H $true $SKY_T; $bmp = $r[0]; $g = $r[1]
  $cx = $W / 2.0
  $py = $H * $POOL_CY

  # ① 纵向渐变（与战场 L1 同配方：上暗下亮）
  Fill-VGrad $g 0 0 $W $H $SKY_T $SKY_B

  # ② 中央柔光：两层叠加（内层窄而亮 + 外层宽而淡），只加光不压暗
  New-SoftPool $g $cx $py 1720 940 $GLOW_C $POOL_HALO
  New-SoftPool $g $cx $py 1150 640 $GLOW_C $POOL_CORE

  # ③ 微尘：少、小、淡（是亮点，不是暗斑）
  $rng = New-Object System.Random 20260927
  for ($i = 0; $i -lt $DUST_COUNT; $i++) {
    $x = $rng.Next(0, $W); $y = $rng.Next(0, [int]($H * 0.78))
    $rr = 0.9 + $rng.NextDouble() * 0.9
    $a = 8 + $rng.Next(0, 12)
    $br = New-Object System.Drawing.SolidBrush (New-Col @(210,224,244) $a)
    $g.FillEllipse($br, [single]($x-$rr), [single]($y-$rr), [single]($rr*2), [single]($rr*2)); $br.Dispose()
  }

  # ④ 暗纹（三选一；只有极淡的轴线直线，没有分块、没有网格）
  if ($pattern -eq 'vline') {
    for ($x = 256; $x -lt $W; $x += 256) { Stroke-Axis $g $true $x 0 $H $PAT_A $PAT_LIGHT }
  }
  elseif ($pattern -eq 'diag') {
    for ($x0 = -$H; $x0 -lt $W; $x0 += 64) {
      $pen = New-Object System.Drawing.Pen ((New-Col $INK0 $PAT_A)), 1.2
      $g.DrawLine($pen, [single]$x0, 0, [single]($x0 + $H), $H); $pen.Dispose()
      $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D $PAT_LIGHT)), 1.0
      $g.DrawLine($pen, [single]($x0 + 1.6), 0, [single]($x0 + $H + 1.6), $H); $pen.Dispose()
    }
  }

  # ⑤ 四角压深（同样用平滑衰减：角上最深，向外淡出）
  foreach ($c in @(@(0,0,900,620), @($W,0,900,620), @(0,$H,900,620), @($W,$H,900,620))) {
    New-SoftPool $g $c[0] $c[1] $c[2] $c[3] $INK0 $VIGNETTE_A
  }

  # ⑥ 顶 / 底各压一档（给 UI 留头尾）
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$W,180)), (New-Col $INK0 92), (New-Col $INK0 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, 0, 0, $W, 180); $lg.Dispose()
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,($H-160),$W,160)), (New-Col $INK0 0), (New-Col $INK0 84), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, 0, ($H-160), $W, 160); $lg.Dispose()

  # ⑦ 内缩金细框（可有可无，压得很暗）
  if ($FRAME) {
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $FRAME_A)), 2
    $g.DrawRectangle($pen, $FRAME_IN, $FRAME_IN, ($W - $FRAME_IN*2), ($H - $FRAME_IN*2)); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $STEEL_D 22)), 2
    $g.DrawRectangle($pen, ($FRAME_IN+6), ($FRAME_IN+6), ($W - ($FRAME_IN+6)*2), ($H - ($FRAME_IN+6)*2)); $pen.Dispose()
  }

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}
# ── 预览 ────────────────────────────────────────────────────
$script:PFC = $null
function Get-CF([single]$px) {
  if ($script:PFC -eq $null) {
    $script:PFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PFC.AddFontFile((Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'))
  }
  return New-Object System.Drawing.Font($script:PFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
function Put-Text($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 240) {
  $f = Get-CF $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function New-PrevCanvas([int]$w, [int]$h) {
  $b = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 12, 15, 21))
  $g.FillRectangle($bg, 0, 0, $w, $h); $bg.Dispose()
  return @($b, $g)
}

function New-CommonSheet($files, $outs) {
  $pw = 640; $ph = 360
  $r = New-PrevCanvas 2048 480; $b = $r[0]; $g = $r[1]
  $x = 24
  for ($i = 0; $i -lt $files.Count; $i++) {
    Put-Text $g $outs[$i] $x 18 26
    $im = [System.Drawing.Image]::FromFile($files[$i])
    $g.DrawImage($im, $x, 62, $pw, $ph); $im.Dispose()
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 200, 164, 74)), 2
    $g.DrawRectangle($pen, $x, 62, $pw, $ph); $pen.Dispose()
    $x += $pw + 24
  }
  Put-Text $g '三方案同尺寸（640x360 = 16:9 等比）对照 · 无暗斑 / 无分块，暗纹只在近看时可见' 24 440 24 200
  $g.Dispose(); $b.Save((Join-Path $PREV 'common-bg-v1-sheet.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

function New-AspectProof([string]$file) {
  $r = New-PrevCanvas 2048 560; $b = $r[0]; $g = $r[1]
  $im = [System.Drawing.Image]::FromFile($file)
  $cases = @(
    @(24,   16, 9,  '16:9  1920x1080（原比例）'),
    @(880,  21, 9,  '21:9  2560x1080（横向拉伸）'),
    @(1600, 4,  3,  '4:3   1440x1080（纵向拉伸）'))
  foreach ($c in $cases) {
    $boxW = 400.0
    $boxH = $boxW * $c[2] / $c[1]
    if ($boxH -gt 420) { $boxH = 420.0; $boxW = $boxH * $c[1] / $c[2] }
    Put-Text $g $c[3] $c[0] 16 24
    $g.DrawImage($im, [single]$c[0], 60, [single]$boxW, [single]$boxH)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 200, 164, 74)), 2
    $g.DrawRectangle($pen, [single]$c[0], 60, [single]$boxW, [single]$boxH); $pen.Dispose()
  }
  $im.Dispose()
  Put-Text $g '非等比拉伸验证：同一张图拉成 21:9 与 4:3 —— 没有圆、没有徽记，所以只是「光池变椭、纹路间距变化」' 24 510 24 200
  $g.Dispose(); $b.Save((Join-Path $PREV 'common-bg-v1-aspect.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# 中央 1:1 放大：光池中心那块有没有洞 / 环带，一眼就能看出来
function New-CenterCrop([string]$file) {
  $cw = 1024; $ch = 576
  $r = New-PrevCanvas ($cw + 60) ($ch + 92); $b = $r[0]; $g = $r[1]
  Put-Text $g '中央 1:1 放大（原尺寸像素，未缩放）· 光池中心必须平滑：无洞 / 无环带 / 无暗斑' 24 18 24 200
  $im = [System.Drawing.Image]::FromFile($file)
  $src = New-Object System.Drawing.Rectangle([int](($W-$cw)/2), [int]($H*$POOL_CY - $ch/2), $cw, $ch)
  $g.DrawImage($im, (New-Object System.Drawing.Rectangle(24, 62, $cw, $ch)), $src, [System.Drawing.GraphicsUnit]::Pixel)
  $im.Dispose()
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 200, 164, 74)), 2
  $g.DrawRectangle($pen, 24, 62, $cw, $ch); $pen.Dispose()
  $g.Dispose(); $b.Save((Join-Path $PREV 'common-bg-v1-center.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# ── 主流程 ─────────────────────────────────────────────────
$variants = @(
  @('none',  'CommonBack_A_clean.png', 'A · 纯净（只有渐变 + 柔光池 + 微尘，无暗纹）'),
  @('vline', 'CommonBack_B_vline.png', 'B · 光帘（+ 极淡竖向发丝线，256px 一道）'),
  @('diag',  'CommonBack_C_diag.png',  'C · 斜纹（+ 极淡 45° 发丝线，64px 一道）'))
$files = @(); $labels = @()
foreach ($v in $variants) {
  $f = Join-Path $GEN $v[1]
  New-CommonPlate $f $v[0] | Out-Null
  $files += $f; $labels += $v[2]
  "plate  : $f"
}
New-CommonSheet $files $labels
New-AspectProof $files[0]
New-CenterCrop $files[0]
"sheet  : $(Join-Path $PREV 'common-bg-v1-sheet.png')"
"aspect : $(Join-Path $PREV 'common-bg-v1-aspect.png')"
"center : $(Join-Path $PREV 'common-bg-v1-center.png')"