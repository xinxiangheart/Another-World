# Board_Surface 只保留金色边框（默认只出预览图，不动资产；加 -Apply 写入）
#   2026-09-23 定案：只改这一层 —— 网格 / 磨蚀斑 / 裂痕 / 钢线镶嵌 / 法阵座圈 / 外圈短齿 /
#   口袋内嵌 / 底沿下沉 全部不要，只留金边（主金框 + 内金线 + 铆点 + 四角斜线 + 口袋小菱形）。
#   同日追加：金边整体外扩一圈，几乎抵住面板边框 —— 主金框内缩 $FR（原 84），内金线 $FRIN（原 62），
#   两者间距仍是 22，铆点随主金框走，四角斜线的起点跟着挪到新版圆角弧内。

param([switch]$Apply, [single]$FR = 20.0, [single]$FRIN = 42.0)

foreach ($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')) {
  $p = Join-Path $PSHOME $n
  if (Test-Path $p) { try { [void][System.Reflection.Assembly]::LoadFrom($p) } catch {} }
}
. "$PSScriptRoot/BoardLayersV2.ps1"

function New-LayerSurfaceGoldOnly([string]$out, [single]$FR, [single]$FRIN) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]

  # ① 主金框 —— 原 Surface 的 $GOLD 76 / w5
  $ip = New-ArenaPath $FR
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 76)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ip); $pen.Dispose()

  # ② 内金线 —— 原 Surface 的 $GOLD_D 50 / w2，与主金框保持 22 的间距
  $ip3 = New-ArenaPath $FRIN
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 50)), 2
  $g.DrawPath($pen, $ip3); $pen.Dispose()

  # ③ 主金框上的菱形铆点 40 枚 —— 原 Surface 的 $GOLD 70 / r6
  for ($k = 0; $k -lt 40; $k++) {
    $t4 = $k / 40.0 * 4.0
    $x = $AR_L + $FR; $y = $AR_T + $FR
    if ($t4 -lt 1.0) { $x = $AR_L + $FR + ($AR_W - 2*$FR) * $t4; $y = $AR_T + $FR }
    elseif ($t4 -lt 2.0) { $x = $AR_L + $AR_W - $FR; $y = $AR_T + $FR + ($AR_H - 2*$FR) * ($t4 - 1.0) }
    elseif ($t4 -lt 3.0) { $x = $AR_L + $AR_W - $FR - ($AR_W - 2*$FR) * ($t4 - 2.0); $y = $AR_T + $AR_H - $FR }
    else { $x = $AR_L + $FR; $y = $AR_T + $AR_H - $FR - ($AR_H - 2*$FR) * ($t4 - 3.0) }
    $dp = New-Diamond $x $y 6
    $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD 70)
    $g.FillPath($br, $dp); $br.Dispose(); $dp.Dispose()
  }

  # ④ 四角斜切金线 —— 原 Surface 的 $GOLD 86 w5 + $GOLD_D 60 w4
  #    起点要落在新版圆角弧的内侧：圆角弧心到斜线起点的距离必须小于 (圆角半径)
  $dc = $FR + 96
  $cx1 = $AR_L + $dc; $cx2 = $AR_L + $AR_W - $dc
  $cy1 = $AR_T + $dc; $cy2 = $AR_T + $AR_H - $dc
  foreach ($c in @(@($cx1,$cy1,1,1), @($cx2,$cy1,-1,1), @($cx1,$cy2,1,-1), @($cx2,$cy2,-1,-1))) {
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 86)), 5
    $g.DrawLine($pen, $c[0], $c[1], ($c[0] + 104*$c[2]), ($c[1] + 104*$c[3])); $pen.Dispose()
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 60)), 4
    $g.DrawLine($pen, ($c[0] + 22*$c[2]), ($c[1] + 22*$c[3]), ($c[0] + 132*$c[2]), ($c[1] + 132*$c[3])); $pen.Dispose()
  }

  # ⑤ 口袋区小菱形 4 枚 —— 原 Surface 的 $GOLD 40 / r11
  foreach ($q in @(@(276, 452), @(1772, 452), @(276, 934), @(1772, 934))) {
    $dd = New-Diamond ($q[0] + 30) ($q[1] + 48) 11
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 40)), 2.4
    $g.DrawPath($pen, $dd); $pen.Dispose(); $dd.Dispose()
  }

  $ip.Dispose(); $ip3.Dispose()
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

$repo    = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ldir    = Join-Path $repo 'Assets/_Game/Art/Sprites/Generated/board-layers-v2'
$prevDir = Join-Path $PSScriptRoot 'preview'
$work    = "$env:TEMP\surface-gold-$((Get-Date -Format 'yyyyMMdd-HHmmss'))"
New-Item -ItemType Directory -Force -Path $work | Out-Null

$tmp = Join-Path $work 'Board_Surface_goldonly.png'
[void](New-LayerSurfaceGoldOnly $tmp $FR $FRIN)
Copy-Item $tmp (Join-Path $prevDir 'board-surface-goldonly.png') -Force
Write-Output "预览 -> $(Join-Path $prevDir 'board-surface-goldonly.png')  (主金框内缩 $FR / 内金线 $FRIN)"

if ($Apply) {
  Copy-Item (Join-Path $ldir 'Board_Surface.png') (Join-Path $work 'Board_Surface.before.png') -Force
  Copy-Item $tmp (Join-Path $ldir 'Board_Surface.png') -Force
  Write-Output "已写入 $ldir\Board_Surface.png（改前另存 $work）"
} else {
  Write-Output "未动资产（加 -Apply 才写入）"
}
