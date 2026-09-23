# 整屏 mockup：背景 + 棋盘 + 卡槽 + 卡牌 + 新 UI（HUD）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\CardFrameV6.ps1"
. "$PSScriptRoot\BoardPreviewV3.ps1"

function New-HudMockup([string]$bg, [string]$board, [string]$plate, [string]$edge, [string]$card, [string]$uiDir, [string]$outPath) {
  $W = 1920; $H = 1080
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(255, 12, 16, 24))
  $imgs = @{}
  foreach ($k in @("TopBorder","EndTurnPlate","DrawCircle","HealthCircle","EnergyCircle","PhaseBand","Icon_health","Icon_energy","Icon_cardcount","Icon_turncount","Icon_settings")) {
    $p = Join-Path $uiDir "$k.png"
    if (Test-Path $p) { $imgs[$k] = [System.Drawing.Image]::FromFile((Resolve-Path $p).Path) }
  }
  $bgI = [System.Drawing.Image]::FromFile((Resolve-Path $bg).Path); $g.DrawImage($bgI, 0, 0, $W, $H); $bgI.Dispose()
  $bdI = [System.Drawing.Image]::FromFile((Resolve-Path $board).Path); $g.DrawImage($bdI, 0, 0, $W, $H); $bdI.Dispose()

  $plateI = [System.Drawing.Image]::FromFile((Resolve-Path $plate).Path)
  $edgeI = [System.Drawing.Image]::FromFile((Resolve-Path $edge).Path)
  $cols = @(760, 960, 1160); $rowEnemy = 292; $rowMine = 604; $sw = 135; $sh = 240
  foreach ($y in @($rowEnemy, $rowMine)) { foreach ($x in $cols) { Draw-ImgTint $g $plateI $x $y $sw $sh 1 1 1 0.45 } }
  Draw-ImgTint $g $plateI 760 $rowMine $sw $sh 1 0.92 0.016 1
  Draw-ImgTint $g $edgeI 760 $rowMine $sw $sh 1 0.92 0.016 1

  $cardI = [System.Drawing.Image]::FromFile((Resolve-Path $card).Path)
  $cw = 150; $chh = 268
  $g.DrawImage($cardI, (870 - $cw / 2), ($rowEnemy - $chh / 2 - 100), $cw, $chh)
  $g.DrawImage($cardI, (1070 - $cw / 2), ($rowEnemy - $chh / 2 - 100), $cw, $chh)
  $hw = 196; $hh = 350
  foreach ($hx in @(820, 1010, 1200)) { $g.DrawImage($cardI, ($hx - $hw / 2), (1080 - $hh + 46), $hw, $hh) }
  $cardI.Dispose(); $plateI.Dispose(); $edgeI.Dispose()

  # HUD
  $g.DrawImage($imgs["TopBorder"], 0, -4, $W, 120)
  $font = New-Object System.Drawing.Font("Microsoft YaHei", 26, [System.Drawing.FontStyle]::Bold)
  $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 209, 138))
  function Ico($n, $x, $s) { $g.DrawImage($imgs[$n], $x, (56 - $s / 2), $s, $s) }
  Ico "Icon_health" 60 58; $g.DrawString("20", $font, $gold, 104, 36)
  Ico "Icon_energy" 200 58; $g.DrawString("0", $font, $gold, 244, 36)
  Ico "Icon_cardcount" 330 58; $g.DrawString("3", $font, $gold, 374, 36)
  Ico "Icon_turncount" 460 58; $g.DrawString("1", $font, $gold, 504, 36)
  Ico "Icon_settings" ($W - 120) 58
  $g.DrawImage($imgs["PhaseBand"], (($W - 400) / 2), 78, 400, 26)
  $g.DrawImage($imgs["EndTurnPlate"], ($W - 330), 630, 290, 96)
  $fm = New-Object System.Drawing.StringFormat
  $fm.Alignment = [System.Drawing.StringAlignment]::Center; $fm.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString("结束回合", (New-Object System.Drawing.Font("Microsoft YaHei", 26, [System.Drawing.FontStyle]::Bold)), $gold, (New-Object System.Drawing.RectangleF(($W - 330), 630, 290, 96)), $fm)
  $g.DrawImage($imgs["DrawCircle"], ($W - 300), 760, 180, 120)
  $g.DrawImage($imgs["HealthCircle"], 40, 900, 190, 127)
  $g.DrawImage($imgs["EnergyCircle"], 40, 780, 190, 127)
  $g.DrawString("20", $font, $gold, 88, 930)
  $g.DrawString("6", $font, $gold, 100, 810)

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
