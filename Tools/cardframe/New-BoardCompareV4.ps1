Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\BoardLayersV2.ps1"
$ref = "$env:TEMP\codex-clipboard-db81c935-1add-4390-b01d-7a64c52765c0.png"
$dir = "$env:TEMP\board-v4"
$out = "$env:TEMP\board-v4-compare.png"
$S = Load-BoardSetV2 $dir
$cw = 900; $ch = 506
$bmp = New-Object System.Drawing.Bitmap(1900, 700, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::FromArgb(10, 13, 19))
$f = New-Object System.Drawing.Font('Microsoft YaHei', 14)
$fs = New-Object System.Drawing.Font('Microsoft YaHei', 11)
$gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
$white = [System.Drawing.Brushes]::White
$g.DrawString('左：你给的那张（v1 基准）　　右：在它上面加完细节的新版', $f, $gold, 24, 14)
$ri = [System.Drawing.Image]::FromFile($ref)
$g.DrawImage($ri, 24, 52, $cw, $ch); $ri.Dispose()
$flat = New-BoardCompositeV2 $cw $ch $S 0 0 1.0 1.0 0 0
$g.DrawImage($flat, 976, 52, $cw, $ch); $flat.Dispose()
foreach ($k in $S.Keys) { $S[$k].Dispose() }
$pen = New-Object System.Drawing.Pen ((New-Col @(200,164,74) 120)), 2
$g.DrawRectangle($pen, 24, 52, $cw, $ch); $g.DrawRectangle($pen, 976, 52, $cw, $ch); $pen.Dispose()
$g.DrawString('v1 基准：圆角面板 / 双法阵环 / 金框与铆钉 / 深蓝暗底', $fs, $white, 24, 572)
$g.DrawString('新版：同一套构图与几何，未加任何建筑或柱子；板缝更密、裂痕与磨蚀更多、', $fs, $white, 976, 572)
$g.DrawString('加了法阵座圈 / 外圈短齿 / 口袋刻花；中心柔光与浮尘都压弱（不抢卡牌主体）', $fs, $gold, 976, 596)
$g.DrawString('动态：L3 慢转 / L4 反转 / L6 脉冲 / L7 浮尘可调 / L8 视差', $fs, $white, 24, 596)
$g.DrawString('亮度：底色基本不动，只把「亮块」换成「暗刻」', $fs, $gold, 24, 620)
$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
$out
