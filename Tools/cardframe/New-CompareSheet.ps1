# 对比图：新卡面 vs 现行卡面（大图 + 游戏内尺寸）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardPreview.ps1"

$CURSRC = "Assets/_Game/Resources/Cards/Back And Front/Summon/SummonCard_3.png"

# 现行卡框放大到同一像素空间
$cur = New-Object System.Drawing.Bitmap($FW, $FH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cg = [System.Drawing.Graphics]::FromImage($cur)
$cg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$srcImg = Load-Img $CURSRC
$cg.DrawImage($srcImg, (New-Object System.Drawing.RectangleF(0, 0, $FW, $FH)))
$srcImg.Dispose(); $cg.Dispose()
$curTmp = Join-Path $GENDIR "CardFrameCUR_scaled.png"
$cur.Save($curTmp, [System.Drawing.Imaging.ImageFormat]::Png)
$cur.Dispose()

$curCard = New-CardCanvas $curTmp $false (Join-Path $GENDIR "preview-cur-card.png")
$newCard = New-CardCanvas (Join-Path $GENDIR "CardFrameV2_cost3.png") $true (Join-Path $GENDIR "preview-v2-card.png")

$big = 0.42
$bw = [int]($FW * $big); $bh = [int]($FH * $big)
$gw = 118; $gh = [int]($gw * $FH / $FW)
$w2 = 236; $h2 = [int]($w2 * $FH / $FW)

$sheetW = 40 + $bw * 2 + 60
$sheetH = 60 + $bh + 40 + $h2 + 60
$out = New-Object System.Drawing.Bitmap($sheetW, $sheetH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($out)
$g.Clear([System.Drawing.Color]::FromArgb(255, 30, 30, 34))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

$a = Load-Img $newCard;   $g.DrawImage($a, (New-Object System.Drawing.RectangleF(20, 60, $bw, $bh))); $a.Dispose()
$b = Load-Img $curCard;   $g.DrawImage($b, (New-Object System.Drawing.RectangleF((20 + $bw + 20), 60, $bw, $bh))); $b.Dispose()

$y2 = 60 + $bh + 40
$c = Load-Img $newCard;   $g.DrawImage($c, (New-Object System.Drawing.RectangleF(20, $y2, $w2, $h2))); $c.Dispose()
$d = Load-Img $curCard;   $g.DrawImage($d, (New-Object System.Drawing.RectangleF((20 + $w2 + 30), $y2, $w2, $h2))); $d.Dispose()
$e = Load-Img $newCard;   $g.DrawImage($e, (New-Object System.Drawing.RectangleF((20 + $w2 * 2 + 60), $y2, $gw, $gh))); $e.Dispose()
$f = Load-Img $curCard;   $g.DrawImage($f, (New-Object System.Drawing.RectangleF((20 + $w2 * 2 + 60 + $gw + 20), $y2, $gw, $gh))); $f.Dispose()

$font = New-Object System.Drawing.Font("Microsoft YaHei", 20, [System.Drawing.FontStyle]::Bold)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 235, 232, 224))
$g.DrawString("新卡框 v2（名字靠左 / 种类在右）", $font, $brush, 20, 16)
$g.DrawString("现行卡框", $font, $brush, (20 + $bw + 20), 16)
$g.DrawString("2x 游戏内尺寸", $font, $brush, 20, ($y2 - 34))
$g.DrawString("1x 游戏内尺寸", $font, $brush, (20 + $w2 * 2 + 60), ($y2 - 34))
$font.Dispose(); $brush.Dispose()

$g.Dispose()
$sheetPath = Join-Path $GENDIR "preview-compare.png"
$out.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
return $sheetPath
