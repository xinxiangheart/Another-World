# Board_Ornament 重出：去掉里面那圈「放大之前的」圆角金框，只留外圈直角角铁 + 标尺刻记 + 铆钉。
#   背景：当前 Board_Ornament.png 里混进了一圈圆角金框（位置 = Board_Surface 原始的 84/62 那圈），
#   用户 2026-09-23：「之前没放大之前的隐藏掉，只有一层金边够了」。
#   New-LayerOrnament（BoardLayersV2.ps1 L496）本来就只画直角框（f1/f2/f3 = 64/92/118）+ 刻记 + 角铁 + 铆钉，
#   不含圆角框 —— 所以直接用它重出即可。默认只出预览，加 -Apply 写入。

param([switch]$Apply)

foreach ($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')) {
  $p = Join-Path $PSHOME $n
  if (Test-Path $p) { try { [void][System.Reflection.Assembly]::LoadFrom($p) } catch {} }
}
. "$PSScriptRoot/BoardLayersV2.ps1"

$repo    = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ldir    = Join-Path $repo 'Assets/_Game/Art/Sprites/Generated/board-layers-v2'
$prevDir = Join-Path $PSScriptRoot 'preview'
$work    = "$env:TEMP\ornament-$((Get-Date -Format 'yyyyMMdd-HHmmss'))"
New-Item -ItemType Directory -Force -Path $work | Out-Null

$tmp = Join-Path $work 'Board_Ornament_noframe.png'
[void](New-LayerOrnament $tmp)
Copy-Item $tmp (Join-Path $prevDir 'board-ornament-noframe.png') -Force
Write-Output "预览 -> $(Join-Path $prevDir 'board-ornament-noframe.png')"

# 顺便报一下：新层中间行/列还有没有金线（应该只剩 f1/f3 直角框）
$b = [System.Drawing.Bitmap]::new($tmp)
$runs = @(); $start = -1
for ($i = 0; $i -lt $b.Width; $i++) {
  $c = $b.GetPixel($i, [int]($b.Height/2))
  $isGold = ($c.R -gt 55 -and ($c.R - $c.B) -gt 22 -and $c.G -gt 45)
  if ($isGold -and $start -lt 0) { $start = $i } elseif (-not $isGold -and $start -ge 0) { $runs += ("{0}-{1}" -f $start, ($i-1)); $start = -1 }
}
if ($start -ge 0) { $runs += ("{0}-{1}" -f $start, ($b.Width-1)) }
"新 Ornament 中间行金线（x）: " + ($runs -join "  ")
$b.Dispose()

if ($Apply) {
  Copy-Item (Join-Path $ldir 'Board_Ornament.png') (Join-Path $work 'Board_Ornament.before.png') -Force
  Copy-Item $tmp (Join-Path $ldir 'Board_Ornament.png') -Force
  Write-Output "已写入 $ldir\Board_Ornament.png（改前另存 $work）"
} else {
  Write-Output "未动资产（加 -Apply 才写入）"
}
