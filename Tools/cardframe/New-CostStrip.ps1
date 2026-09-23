# 六色卡框条：验证费用配色
Add-Type -AssemblyName System.Drawing
function New-CostStrip([string]$outDir, [string]$outPath) {
  $k = 0.24
  $w = 1152 * $k; $h = 2016 * $k
  $sheetW = [int](40 + 6 * ($w + 16)); $sheetH = [int]($h + 70)
  $bmp = New-Object System.Drawing.Bitmap($sheetW, $sheetH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::FromArgb(255, 26, 26, 30))
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $font = New-Object System.Drawing.Font("Microsoft YaHei", 15)
  $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
  $x = 40
  for ($c = 0; $c -le 5; $c++) {
    $p = "$outDir/CardFrameV3_cost$c.png"
    if (Test-Path $p) {
      $img = [System.Drawing.Image]::FromFile((Resolve-Path $p).Path)
      $g.DrawImage($img, $x, 46, $w, $h)
      $img.Dispose()
    }
    $g.DrawString("$c 费", $font, $white, $x, 14)
    $x += ($w + 16)
  }
  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
