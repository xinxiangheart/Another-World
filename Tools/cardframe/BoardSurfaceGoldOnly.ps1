# Board_Surface 只保留金色边框（默认只出预览图，不动资产；加 -Apply 写入）
#   2026-09-23 定案：只改这一层 —— 网格 / 磨蚀斑 / 裂痕 / 钢线镶嵌 / 法阵座圈 / 外圈短齿 /
#   口袋内嵌 / 底沿下沉 全部不要，只留金边。
#   金边外扩并贴住面板四边：主金框内缩 $FR（原 84），上下左右一样贴。
#   默认不再画那圈内金线（用户：「只有一层金边够了」），加 -KeepInnerLine 才画。
#   铆点沿圆角路径按弧长等距排（原先是按矩形周长排，框一大角上的铆点会飘到框外）。

param([switch]$Apply, [switch]$KeepInnerLine, [single]$FR = 10.0, [single]$FRIN = 32.0, [int]$Rivet = 40)

foreach ($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')) {
  $p = Join-Path $PSHOME $n
  if (Test-Path $p) { try { [void][System.Reflection.Assembly]::LoadFrom($p) } catch {} }
}
. "$PSScriptRoot/BoardLayersV2.ps1"

function Get-PathPointsEven([System.Drawing.Drawing2D.GraphicsPath]$path, [int]$count) {
  $flat = $path.Clone()
  $flat.Flatten((New-Object System.Drawing.Drawing2D.Matrix), 0.15)
  $pts = $flat.PathPoints
  $lens = [System.Collections.Generic.List[double]]::new()
  $lens.Add(0.0); $tot = 0.0
  for ($i = 1; $i -lt $pts.Length; $i++) {
    $dx = $pts[$i].X - $pts[$i-1].X; $dy = $pts[$i].Y - $pts[$i-1].Y
    $tot += [Math]::Sqrt($dx*$dx + $dy*$dy)
    $lens.Add($tot)
  }
  $out = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
  $k = 0
  for ($n2 = 0; $n2 -lt $count; $n2++) {
    $s = ($n2 + 0.5) / [double]$count * $tot
    while (($k + 1) -lt $lens.Count -and $lens[$k+1] -lt $s) { $k++ }
    if (($k + 1) -ge $lens.Count) { break }
    $seg = $lens[$k+1] - $lens[$k]
    $u = 0.0; if ($seg -gt 0.0) { $u = ($s - $lens[$k]) / $seg }
    $out.Add((New-Object System.Drawing.PointF(($pts[$k].X + ($pts[$k+1].X - $pts[$k].X) * $u), ($pts[$k].Y + ($pts[$k+1].Y - $pts[$k].Y) * $u))))
  }
  $flat.Dispose()
  return $out
}

function New-LayerSurfaceGoldOnly([string]$out, [single]$FR, [single]$FRIN, [int]$Rivet, [bool]$InnerLine) {
  $r = New-Layer $BW $BH $false @(0,0,0); $bmp = $r[0]; $g = $r[1]

  # ① 主金框 —— 原 Surface 的 $GOLD 76 / w5
  $ip = New-ArenaPath $FR
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 76)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $ip); $pen.Dispose()

  # ② 内金线（可选）—— 原 Surface 的 $GOLD_D 50 / w2
  $ip3 = $null
  if ($InnerLine) {
    $ip3 = New-ArenaPath $FRIN
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_D 50)), 2
    $g.DrawPath($pen, $ip3); $pen.Dispose()
  }

  # ③ 主金框上的菱形铆点 —— 原 Surface 的 $GOLD 70 / r6
  $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD 70)
  foreach ($pt in (Get-PathPointsEven $ip $Rivet)) {
    $dp = New-Diamond $pt.X $pt.Y 6
    $g.FillPath($br, $dp); $dp.Dispose()
  }
  $br.Dispose()

  # ④ 四角斜切金线 —— 原 Surface 的 $GOLD 86 w5 + $GOLD_D 60 w4
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

  $ip.Dispose(); if ($ip3) { $ip3.Dispose() }
  $g.Dispose(); $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $out
}

$repo    = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ldir    = Join-Path $repo 'Assets/_Game/Art/Sprites/Generated/board-layers-v2'
$prevDir = Join-Path $PSScriptRoot 'preview'
$work    = "$env:TEMP\surface-gold-$((Get-Date -Format 'yyyyMMdd-HHmmss'))"
New-Item -ItemType Directory -Force -Path $work | Out-Null

$tmp = Join-Path $work 'Board_Surface_goldonly.png'
[void](New-LayerSurfaceGoldOnly $tmp $FR $FRIN $Rivet ([bool]$KeepInnerLine))
Copy-Item $tmp (Join-Path $prevDir 'board-surface-goldonly.png') -Force
Write-Output "预览 -> $(Join-Path $prevDir 'board-surface-goldonly.png')  (主金框内缩 $FR / 内金线 $KeepInnerLine / 铆点 $Rivet)"

if ($Apply) {
  Copy-Item (Join-Path $ldir 'Board_Surface.png') (Join-Path $work 'Board_Surface.before.png') -Force
  Copy-Item $tmp (Join-Path $ldir 'Board_Surface.png') -Force
  Write-Output "已写入 $ldir\Board_Surface.png（改前另存 $work）"
} else {
  Write-Output "未动资产（加 -Apply 才写入）"
}
