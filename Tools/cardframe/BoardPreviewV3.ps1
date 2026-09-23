# 桌面预览：背景 + 棋盘 + 卡槽（常态/高亮）+ 卡牌，按游戏 16:9 摆位
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

function Draw-ImgTint($g, $img, [single]$cx, [single]$cy, [single]$w, [single]$h, [double]$tr, [double]$tg, [double]$tb, [double]$ta) {
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = [single]$tr; $cm.Matrix11 = [single]$tg; $cm.Matrix22 = [single]$tb; $cm.Matrix33 = [single]$ta
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $r = New-Object System.Drawing.Rectangle(0, 0, $img.Width, $img.Height)
  $dst = New-Object System.Drawing.Rectangle([int]($cx - $w / 2), [int]($cy - $h / 2), [int]$w, [int]$h)
  $g.DrawImage($img, $dst, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
}

function New-BoardMockup([string]$bgPath, [string]$boardPath, [string]$platePath, [string]$edgePath, [string]$cardPath, [string]$outPath) {
  $W = 1920; $H = 1080
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(255, 22, 20, 26))

  if ($bgPath -ne "" -and (Test-Path $bgPath)) {
    $bg = [System.Drawing.Image]::FromFile((Resolve-Path $bgPath).Path)
    $g.DrawImage($bg, 0, 0, $W, $H); $bg.Dispose()
  }
  $bd = [System.Drawing.Image]::FromFile((Resolve-Path $boardPath).Path)
  $g.DrawImage($bd, 0, 0, $W, $H); $bd.Dispose()

  $plate = [System.Drawing.Image]::FromFile((Resolve-Path $platePath).Path)
  $edge  = [System.Drawing.Image]::FromFile((Resolve-Path $edgePath).Path)

  # 槽位：3 列 x 2 行（列间距 200，敌我两排）
  $cols = @(760, 960, 1160)
  $rowEnemy = 292; $rowMine = 604
  $sw = 135; $sh = 240
  foreach ($y in @($rowEnemy, $rowMine)) {
    foreach ($x in $cols) {
      Draw-ImgTint $g $plate $x $y $sw $sh 1 1 1 0.45
    }
  }
  # 一个高亮格：底板走 highlightColor(黄)，线层出现
  Draw-ImgTint $g $plate 760 $rowMine $sw $sh 1 0.92 0.016 1
  Draw-ImgTint $g $edge  760 $rowMine $sw $sh 1 0.92 0.016 1

  # 棋盘上的卡（用新卡面预览）
  $card = [System.Drawing.Image]::FromFile((Resolve-Path $cardPath).Path)
  $cw = 150; $chh = 268; $gapY = 100
  $g.DrawImage($card, (870 - $cw / 2), ($rowEnemy - $chh / 2 - $gapY), $cw, $chh)
  $g.DrawImage($card, (1070 - $cw / 2), ($rowEnemy - $chh / 2 - $gapY), $cw, $chh)
  # 手牌一排
  $hw = 196; $hh = 350
  foreach ($hx in @(820, 1010, 1200)) {
    $g.DrawImage($card, ($hx - $hw / 2), (1080 - $hh + 46), $hw, $hh)
  }
  $card.Dispose()
  $plate.Dispose(); $edge.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
