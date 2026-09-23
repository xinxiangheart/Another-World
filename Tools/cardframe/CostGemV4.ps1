# 费用宝石 v4：深青钢底 + 金边 + 费用色硬边档（对齐新卡面）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

function New-CostGemV4([int]$cost, [string]$outPath) {
  $size = 250; $R = 110
  $acc = $COST_ACCENT[$cost]
  $top = Mix-Col $acc @(255,255,255) 0.30
  $mid = Mix-Col $acc @(5,8,13) 0.42
  $bot = Mix-Col $acc @(5,8,13) 0.70
  $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $cx = $size / 2.0; $cy = $size / 2.0
  $list = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($i = 0; $i -lt 6; $i++) {
    $ang = [Math]::PI / 180.0 * (60 * $i - 90)
    $list.Add((New-Object System.Drawing.PointF(($cx + $R * [Math]::Cos($ang)), ($cy + $R * [Math]::Sin($ang)))))
  }
  $hex = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hex.AddPolygon($list.ToArray())
  $sv = $g.Clip; $g.SetClip($hex)
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$size,$size)), (New-Col $mid), (New-Col $bot), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, 0, 0, $size, $size); $lg.Dispose()
  $lp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $lp.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(($cx - $R * 0.866), ($cy - $R * 0.5))),
    (New-Object System.Drawing.PointF($cx, ($cy - $R))),
    (New-Object System.Drawing.PointF($cx, ($cy + 4))),
    (New-Object System.Drawing.PointF(($cx - $R * 0.866), ($cy + $R * 0.5)))))
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $top)), $lp); $lp.Dispose()
  $g.Clip = $sv
  $pen = New-Object System.Drawing.Pen (New-Col (Mix-Col $acc @(0,0,0) 0.30)), 14
  $g.DrawPath($pen, $hex); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 11
  $g.DrawPath($pen, $hex); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD_L 190)), 6
  $g.DrawPath($pen, $hex); $pen.Dispose()
  $hex.Dispose()
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
