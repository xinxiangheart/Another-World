# 费用宝石 v3 —— 平涂三档硬边（亮面 / 本体 / 暗面）+ 统一粗墨线
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

$GEM_R   = 110
$GEM_INK = 15
$GEM_TOP = @(150, 205, 240)
$GEM_MID = @(74, 155, 214)
$GEM_BOT = @(38, 92, 152)

function New-CostGemV3([string]$outPath) {
  $size = 250
  $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $cx = $size / 2.0; $cy = $size / 2.0

  $list = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($i = 0; $i -lt 6; $i++) {
    $ang = [Math]::PI / 180.0 * (60 * $i - 90)
    $list.Add((New-Object System.Drawing.PointF(($cx + $GEM_R * [Math]::Cos($ang)), ($cy + $GEM_R * [Math]::Sin($ang)))))
  }
  $hex = New-Object System.Drawing.Drawing2D.GraphicsPath
  $hex.AddPolygon($list.ToArray())

  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $GEM_MID)), $hex)
  $sv = $g.Clip
  $g.SetClip($hex)
  # 硬边暗面：下半
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $GEM_BOT)), ($cx - $GEM_R), ($cy + 4), (2 * $GEM_R), $GEM_R)
  # 硬边亮面：左上那一块（左顶点 → 上顶点 → 中心 → 左下）
  $lp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $lp.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(($cx - $GEM_R * 0.866), ($cy - $GEM_R * 0.5))),
    (New-Object System.Drawing.PointF($cx, ($cy - $GEM_R))),
    (New-Object System.Drawing.PointF($cx, ($cy + 4))),
    (New-Object System.Drawing.PointF(($cx - $GEM_R * 0.866), ($cy + $GEM_R * 0.5)))))
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $GEM_TOP)), $lp)
  $lp.Dispose()
  $g.Clip = $sv

  $pen = New-Object System.Drawing.Pen (New-Col $INK), $GEM_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $hex)
  $pen.Dispose(); $hex.Dispose()

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
