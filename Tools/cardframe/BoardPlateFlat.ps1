# 战场底板 v4 —— 平黑底板
#   背景（2026-09-23）：用户指出 v3 的「干净底板」把板面画成了发蓝光的一版，跟他发来的参考图完全不像。
#   实测参考图（codex-clipboard-44bda6dd）：板面框内/框外都是 (5,6,8) 近黑，没有光池；
#   而 v3 底板中心 (17,26,39)、中心上方 260px (30,45,69) —— 所以观感差别巨大。
#   本脚本：板面压到近平黑、去掉光池，并修掉 v1/v3 都有的「正中心黑斑」。
#
#   黑斑根因：共享函数 Fill-Radial 用 SetSigmaBellShape(bias)，而这个 API 在本环境下会把渐变翻过来 ——
#   CenterColor 被画成一个半径约 (1-bias)*r 的环，环外与「正中心」都回落成 SurroundColor。
#   对不透明底板＝正中心一颗黑斑；对透明底的光晕＝正中心一个透明针孔。
#   修法：改用显式 Blend，Positions 方向是 0.0=路径边界(边缘色) -> 1.0=中心点(中心色)。已用 900x520 实测确认。
#
# 不重跑 BoardFrameV3.ps1（它会把金框再次叠进 Board_Ornament，跑两次会变亮）。

foreach ($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')) {
  $p = Join-Path $PSHOME $n
  if (Test-Path $p) { try { [void][System.Reflection.Assembly]::LoadFrom($p) } catch {} }
}
. "$PSScriptRoot/BoardLayersV2.ps1"

function Fill-RadialFix($g, [single]$cx, [single]$cy, [single]$rx, [single]$ry, [int[]]$cIn, [int[]]$cOut,
                       [int]$aIn = 255, [int]$aOut = 0,
                       [double[]]$pos = @(0.0, 0.30, 0.70, 1.0), [single[]]$fac = @(0.0, 0.25, 0.75, 1.0)) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddEllipse(($cx-$rx), ($cy-$ry), ($rx*2), ($ry*2))
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($p)
  $pg.CenterPoint = New-Object System.Drawing.PointF($cx, $cy)
  $pg.CenterColor = (New-Col $cIn $aIn)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $cOut $aOut))
  $bl = New-Object System.Drawing.Drawing2D.Blend
  $bl.Positions = [single[]]$pos
  $bl.Factors   = $fac
  $pg.Blend = $bl
  $g.FillPath($pg, $p); $pg.Dispose(); $p.Dispose()
}

function New-PlateFlat([string]$out) {
  $r = New-Layer $BW $BH $true @(5,7,10); $bmp = $r[0]; $g = $r[1]

  # 夜空：只留一丝上下方向感（参考图里框内外都是 (5,6,8)，所以差距要极小）
  Fill-VGrad $g 0 0 $BW $BH @(5,7,10) @(8,11,16)

  # 面板：近平黑；中心只做极弱托起，不构成光池
  $ap = New-ArenaPath 0
  $br = New-Object System.Drawing.SolidBrush (New-Col @(6,9,13) 255)
  $g.FillPath($br, $ap); $br.Dispose()
  Fill-RadialFix $g $AR_CX ($AR_CY - 30) 830 470 @(10,14,20) @(6,9,13) 255 255 @(0.0,0.35,0.72,1.0) @(0.0,0.22,0.70,1.0)

  # 面板沿内侧压一圈薄暗，把金框托出来
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 140)), 24
  $g.DrawPath($pen, $ap); $pen.Dispose()
  $ap.Dispose()

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

function New-TransparentLayer([string]$path) {
  $t = [System.Drawing.Bitmap]::new($BW, $BH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $t.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $t.Dispose()
}

# ══════════════════════════════════════════════════════════
$proj    = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ldir    = Join-Path $proj 'Assets/_Game/Art/Sprites/Generated/board-layers-v2'
$prevDir = Join-Path $PSScriptRoot 'preview'
$ts      = Get-Date -Format 'yyyyMMdd-HHmmss'
$work    = "$env:TEMP\board-flat-$ts"
New-Item -ItemType Directory -Force -Path $work | Out-Null

# 1) 备份现有底板
Copy-Item (Join-Path $ldir 'Board_Plate.png') (Join-Path $work 'Board_Plate.before.png') -Force
Write-Output "backup -> $(Join-Path $work 'Board_Plate.before.png')"

# 2) 新底板
$plate = Join-Path $work 'Board_Plate_flat.png'
[void](New-PlateFlat $plate)
Copy-Item $plate (Join-Path $ldir 'Board_Plate.png') -Force
Copy-Item $plate (Join-Path $prevDir 'board-flat-plate.png') -Force
Write-Output "Board_Plate.png 已更新（平黑）"

# 3) 只可见 Plate + Ornament 的一套（其余层喂全透明图，仅用于预览）
$vis = Join-Path $work 'vis'
New-Item -ItemType Directory -Force -Path $vis | Out-Null
Copy-Item $plate (Join-Path $vis 'Board_Plate.png') -Force
Copy-Item (Join-Path $ldir 'Board_Ornament.png') (Join-Path $vis 'Board_Ornament.png') -Force
foreach ($n in @('Board_Surface','Board_Sigil','Board_Rune','Board_Glow','Board_Motes','Board_Foreground')) {
  New-TransparentLayer (Join-Path $vis "$n.png")
}

# 4) 平铺合成预览
$S = Load-BoardSetV2 $vis
$flat = New-BoardCompositeV2 2048 1152 $S 0 0 0.0 0.0 0 0 0.0 0.0
$flatOut = Join-Path $prevDir 'board-flat-frame.png'
$flat.Save($flatOut, [System.Drawing.Imaging.ImageFormat]::Png)
$flat.Dispose()
foreach ($k in $S.Keys) { $S[$k].Dispose() }
Write-Output "flat -> $flatOut"

# 5) 和你发来的那张并排
$user = "$env:TEMP\codex-clipboard-44bda6dd-70e7-4589-9add-00440c764503.png"
$W = 1280
function Row-H([string]$f, [int]$w) { $i = [System.Drawing.Image]::FromFile($f); $h = [int]($w * $i.Height / $i.Width); $i.Dispose(); return $h }
$rows = @(
  @('USER 你发来的（参考）', $user),
  @('NEW  平黑底板 + 只留金框', $flatOut)
)
$hs = @(); foreach ($r in $rows) { $hs += (Row-H $r[1] $W) }
$lab = 32
$tot = 0; for ($i = 0; $i -lt $rows.Count; $i++) { $tot += $hs[$i] + $lab }
$sheet = [System.Drawing.Bitmap]::new($W, $tot, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(255, 22, 22, 24))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font('Microsoft YaHei', 15)
$y = 0
for ($i = 0; $i -lt $rows.Count; $i++) {
  $g.DrawString($rows[$i][0], $font, [System.Drawing.Brushes]::White, 8, ($y + 5))
  $im = [System.Drawing.Image]::FromFile($rows[$i][1])
  $g.DrawImage($im, 0, ($y + $lab), $W, $hs[$i]); $im.Dispose()
  $y += $hs[$i] + $lab
}
$g.Dispose()
$vsOut = Join-Path $prevDir 'board-flat-vs-user.png'
$sheet.Save($vsOut, [System.Drawing.Imaging.ImageFormat]::Png); $sheet.Dispose()
Write-Output "vs sheet -> $vsOut"

# 6) 带卡牌的摆位预览
$mock = New-BoardMockupV2 $vis (Join-Path $prevDir 'board-flat-mockup.png')
Write-Output "mockup -> $mock"
Write-Output "work: $work"
