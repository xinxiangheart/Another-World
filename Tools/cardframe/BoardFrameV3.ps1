# 战场底板 v3 —— 只保留金色边框（2026-09-23 定）
# 用户口径：「重出这种图，只保留金色边框，其它不要，太杂乱了」
#
# 产出两块图：
#   ① 纯金框（L5 Board_Ornament = 旧直角金框/铆钉/刻记 + 从 L2 抽出来的金线）
#        · 主金框 New-ArenaPath 84 $GOLD 76 w5      （原 Surface L289-293）
#        · 内金线 New-ArenaPath 62 $GOLD_D 50 w2     （原 Surface L297-299）
#        · 线上菱形铆点 40 枚 $GOLD 70 r6            （原 Surface L300-311）
#        · 四角斜切金线 $GOLD 86 w5 + $GOLD_D 60 w4  （原 Surface L312-319）
#        · 口袋区小菱形 4 枚 $GOLD 40 r11            （原 Surface L351-353）
#   ② 干净底板（L1 Board_Plate 重画：夜空渐变 + 面板渐变 + 中心一团软冷光 + 四角压深）
#      去掉：v1 三圈冷光池 / r430 暖灰法阵环 / 左右两块光池 / 110 颗星点 / 面板内缝
#      以及原 L2 的全部线稿、L3/L4 的环、L6 的辉光、L7 的微尘 —— 一律不参与。
#
# 原有层图全部有 git 备份，本次另存 %TEMP%\board-frame-backup-*。
# L8 Board_Foreground（暗角）不在本脚本内改，只在预览里给「带 / 不带」两版。

foreach ($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')) {
  $p = Join-Path $PSHOME $n
  if (Test-Path $p) { try { [void][System.Reflection.Assembly]::LoadFrom($p) } catch {} }
}
. "$PSScriptRoot/BoardLayersV2.ps1"

function New-LayerGoldFrameOnly([string]$out) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]

  $ip = New-ArenaPath 84
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 76)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ip); $pen.Dispose()

  $ip3 = New-ArenaPath 62
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 50)), 2
  $g.DrawPath($pen, $ip3); $pen.Dispose()

  for ($k = 0; $k -lt 40; $k++) {
    $t4 = $k / 40.0 * 4.0
    $x = $AR_L + 84.0; $y = $AR_T + 84.0
    if ($t4 -lt 1.0) { $x = $AR_L + 84 + ($AR_W - 168) * $t4; $y = $AR_T + 84 }
    elseif ($t4 -lt 2.0) { $x = $AR_L + $AR_W - 84; $y = $AR_T + 84 + ($AR_H - 168) * ($t4 - 1.0) }
    elseif ($t4 -lt 3.0) { $x = $AR_L + $AR_W - 84 - ($AR_W - 168) * ($t4 - 2.0); $y = $AR_T + $AR_H - 84 }
    else { $x = $AR_L + 84; $y = $AR_T + $AR_H - 84 - ($AR_H - 168) * ($t4 - 3.0) }
    $dp = New-Diamond $x $y 6
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD 70)
    $g.FillPath($br, $dp); $br.Dispose(); $dp.Dispose()
  }

  $cx1 = $AR_L + 168; $cx2 = $AR_L + $AR_W - 168; $cy1 = $AR_T + 168; $cy2 = $AR_T + $AR_H - 168
  foreach ($c in @(@($cx1,$cy1,1,1), @($cx2,$cy1,-1,1), @($cx1,$cy2,1,-1), @($cx2,$cy2,-1,-1))) {
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 86)), 5
    $g.DrawLine($pen, $c[0], $c[1], ($c[0] + 104*$c[2]), ($c[1] + 104*$c[3])); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 60)), 4
    $g.DrawLine($pen, ($c[0] + 22*$c[2]), ($c[1] + 22*$c[3]), ($c[0] + 132*$c[2]), ($c[1] + 132*$c[3])); $pen.Dispose()
  }

  foreach ($q in @(@(276, 452), @(1772, 452), @(276, 934), @(1772, 934))) {
    $dd = New-Diamond ($q[0] + 30) ($q[1] + 48) 11
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 40)), 2.4
    $g.DrawPath($pen, $dd); $pen.Dispose(); $dd.Dispose()
  }

  $ip.Dispose(); $ip3.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

function New-LayerPlateClean([string]$out) {
  $r = New-Layer $BW $BH $true $SKY_T; $bmp = $r[0]; $g = $r[1]

  # 夜空底（原样）
  Fill-VGrad $g 0 0 $BW $BH $SKY_T $SKY_B

  # 面板：中心托起、四周压深（原样）
  $ap = New-ArenaPath 0
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($ap)
  $pg.CenterPoint = New-Object System.Drawing.PointF($AR_CX, ($AR_CY - 40))
  $pg.CenterColor = (New-Col $FL_C 242)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $FL_E 232))
  $pg.SetSigmaBellShape(0.78)
  $g.FillPath($pg, $ap); $pg.Dispose()

  # 中心一团软冷光（单团，无环、无第二池）
  Fill-Radial $g ($BW/2.0) ($BH/2.0) 780 450 @(44,74,120) @(0,0,0) 0.9 150 0
  Fill-Radial $g ($BW/2.0) ($BH/2.0) 430 260 @(56,94,148) @(0,0,0) 0.85 110 0

  # 面板外一圈压深 + 四角再压一档（原样）
  $pen = New-Object System.Drawing.Pen ((New-Col $INK0 110)), 26
  $g.DrawPath($pen, $ap); $pen.Dispose()
  foreach ($c in @(@(0,0,700,470), @($BW,0,700,470), @(0,$BH,700,470), @($BW,$BH,700,470))) {
    Fill-Radial $g $c[0] $c[1] $c[2] $c[3] $INK0 $INK0 0.92 96 0
  }
  $ap.Dispose()

  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

function New-TransparentLayer([string]$path) {
  $t = [System.Drawing.Bitmap]::new($BW, $BH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $t.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $t.Dispose()
}

# ══════════════════════════════════════════════════════════
$proj    = 'C:\Users\22589\Documents\GitHub\Another-World'
$ldir    = Join-Path $proj 'Assets/_Game/Art/Sprites/Generated/board-layers-v2'
$prevDir = Join-Path $proj 'Tools/cardframe/preview'
$ts      = Get-Date -Format 'yyyyMMdd-HHmmss'
$bk      = "$env:TEMP\board-frame-backup-$ts"
$work    = "$env:TEMP\board-frame-work-$ts"
New-Item -ItemType Directory -Force -Path $bk, $work | Out-Null

foreach ($n in @('Board_Plate','Board_Surface','Board_Sigil','Board_Rune','Board_Ornament','Board_Glow','Board_Motes','Board_Foreground','Board_MoteDot')) {
  Copy-Item (Join-Path $ldir "$n.png") $bk -Force
}
Write-Output "backup -> $bk ($((Get-ChildItem $bk -File).Count) files)"

# 1) 纯金框
$framePng = Join-Path $work 'gold-frame.png'
[void](New-LayerGoldFrameOnly $framePng)
Copy-Item $framePng (Join-Path $prevDir 'board-frame-layer.png') -Force
Write-Output "gold frame ($((Get-Item $framePng).Length) B)"

# 2) 金框并入 Board_Ornament
$ornOld = [System.Drawing.Image]::FromFile((Join-Path $ldir 'Board_Ornament.png'))
$frameImg = [System.Drawing.Image]::FromFile($framePng)
$merged = [System.Drawing.Bitmap]::new($BW, $BH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mg = [System.Drawing.Graphics]::FromImage($merged)
$mg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$mg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$mg.DrawImage($ornOld, 0, 0, $BW, $BH)
$mg.DrawImage($frameImg, 0, 0, $BW, $BH)
$mg.Dispose()
$mergedPath = Join-Path $work 'Board_Ornament_merged.png'
$merged.Save($mergedPath, [System.Drawing.Imaging.ImageFormat]::Png)
$ornOld.Dispose(); $frameImg.Dispose(); $merged.Dispose()
Copy-Item $mergedPath (Join-Path $ldir 'Board_Ornament.png') -Force
Write-Output "Board_Ornament.png updated ($((Get-Item (Join-Path $ldir 'Board_Ornament.png')).Length) B)"

# 3) 干净底板 -> Board_Plate.png
$platePath = Join-Path $work 'Board_Plate_clean.png'
[void](New-LayerPlateClean $platePath)
Copy-Item $platePath (Join-Path $prevDir 'board-plate-clean.png') -Force
Copy-Item $platePath (Join-Path $ldir 'Board_Plate.png') -Force
Write-Output "Board_Plate.png updated ($((Get-Item (Join-Path $ldir 'Board_Plate.png')).Length) B)"

# 4) 组装「只留金框」的可见集
function New-VisibleSet([string]$dir, [bool]$withVignette) {
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  Copy-Item (Join-Path $ldir 'Board_Plate.png') (Join-Path $dir 'Board_Plate.png') -Force
  Copy-Item (Join-Path $ldir 'Board_Ornament.png') (Join-Path $dir 'Board_Ornament.png') -Force
  if ($withVignette) { Copy-Item (Join-Path $ldir 'Board_Foreground.png') (Join-Path $dir 'Board_Foreground.png') -Force }
  else { New-TransparentLayer (Join-Path $dir 'Board_Foreground.png') }
  foreach ($n in @('Board_Surface','Board_Sigil','Board_Rune','Board_Glow','Board_Motes')) {
    New-TransparentLayer (Join-Path $dir "$n.png")
  }
}

function New-Flat([string]$dir, [string]$out) {
  $S = Load-BoardSetV2 $dir
  $flat = New-BoardCompositeV2 2048 1152 $S 0 0 0.0 0.0 0 0 1.0 0.0
  $flat.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  $flat.Dispose()
  foreach ($k in $S.Keys) { $S[$k].Dispose() }
}

$vA = Join-Path $work 'vis-plain'
$vB = Join-Path $work 'vis-vignette'
New-VisibleSet $vA $false
New-VisibleSet $vB $true
New-Flat $vA (Join-Path $prevDir 'board-frame-a-plain.png')
New-Flat $vB (Join-Path $prevDir 'board-frame-b-vignette.png')
Write-Output "flat previews done"

$mock = New-BoardMockupV2 $vB (Join-Path $prevDir 'board-frame-mockup.png')
Write-Output "mockup -> $mock"
Write-Output "work: $work"