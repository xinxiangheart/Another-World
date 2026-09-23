# 费用宝石 v2：平涂 + 统一粗墨线外描边（旧 Cost.png 无描边、且吊在卡框外面）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV2.ps1"

$GEM_R = 108          # 六角半径
$GEM_INK = 16         # 描边粗细
$GEM_FILL = @(88, 186, 245)
$GEM_DARK = @(46, 116, 186)

function New-CostGem([string]$outPath) {
  $size = 250
  $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $cx = $size / 2.0; $cy = $size / 2.0

  # 正六角形（尖顶），平涂 + 硬边暗档
  $pts = [System.Drawing.PointF[]]@()
  $list = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($i = 0; $i -lt 6; $i++) {
    $ang = [Math]::PI / 180.0 * (60 * $i - 90)
    $list.Add((New-Object System.Drawing.PointF(($cx + $GEM_R * [Math]::Cos($ang)), ($cy + $GEM_R * [Math]::Sin($ang)))))
  }
  $hex = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hex.AddPolygon($list.ToArray())

  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $GEM_FILL)), $hex)
  # 硬边暗档：六角的下半（不渐变）
  $clip = $g.Clip
  $g.SetClip($hex)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $GEM_DARK)), 0, ($cy + 6), $size, ($size - $cy))
  $g.Clip = $clip
  $pen = New-Object System.Drawing.Pen (New-Col $INK), $GEM_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $hex)
  $pen.Dispose(); $hex.Dispose()

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
