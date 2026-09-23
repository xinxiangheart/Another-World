# 棋盘底图 2048x1152：平涂皮革桌面 + 硬边凹槽 + 桌面徽记（无渐变）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

$BW = 2048; $BH = 1152
$INK2      = @(30, 20, 14)
$EDGE      = @(58, 40, 30)   # 桌沿
$EDGE_D    = @(40, 27, 20)   # 桌沿暗档
$FELT      = @(66, 54, 43)   # 台面
$FELT_D    = @(48, 38, 30)   # 台面凹影
$FELT_L    = @(84, 69, 55)   # 台面下沿受光

function New-Board([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($BW, $BH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear((New-Col $EDGE))

  # 桌沿暗档（上下两条硬边）
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $EDGE_D)), 0, ($BH - 40), $BW, 40)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $EDGE_D)), ($BW - 40), 0, 40, $BH)
  # 外墨线
  $pen = New-Object System.Drawing.Pen (New-Col $INK2), 14
  $g.DrawRectangle($pen, 7, 7, ($BW - 14), ($BH - 14))
  $pen.Dispose()

  # 台面
  $L = 72; $T = 64; $W = $BW - 144; $H = $BH - 128
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $FELT)), $L, $T, $W, $H)
  # 台面凹影（上）/ 受光（下）—— 硬边
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $FELT_D)), $L, $T, $W, 74)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $FELT_L)), $L, ($T + $H - 26), $W, 26)
  # 台面墨线
  $pen = New-Object System.Drawing.Pen (New-Col $INK2), 12
  $g.DrawRectangle($pen, $L, $T, $W, $H)
  $pen.Dispose()

  # 桌面徽记：一枚大圆环 + 一圈刻度（中轴）
  $cx = $BW / 2.0; $cy = $BH / 2.0
  $pen = New-Object System.Drawing.Pen (New-Col @(96, 79, 63)), 10
  $g.DrawEllipse($pen, ($cx - 430), ($cy - 430), 860, 860)
  $pen.Dispose()
  $pen = New-Object System.Drawing.Pen (New-Col @(80, 65, 51)), 8
  $g.DrawEllipse($pen, ($cx - 330), ($cy - 330), 660, 660)
  $pen.Dispose()
  $br = New-Object System.Drawing.SolidBrush (New-Col @(92, 75, 60))
  foreach ($i in 0..23) {
    $a = [Math]::PI / 12.0 * $i
    $x1 = $cx + 430 * [Math]::Cos($a); $y1 = $cy + 430 * [Math]::Sin($a)
    $x2 = $cx + 470 * [Math]::Cos($a); $y2 = $cy + 470 * [Math]::Sin($a)
    $pen = New-Object System.Drawing.Pen (New-Col @(86, 70, 55)), 10
    $g.DrawLine($pen, [single]$x1, [single]$y1, [single]$x2, [single]$y2)
    $pen.Dispose()
  }
  $br.Dispose()

  # 四角配件：平涂 L 形角铁
  $arm = 150; $t = 26; $o = 44
  foreach ($pair in @(@(0, 0), @(1, 0), @(0, 1), @(1, 1))) {
    $sx = $pair[0]; $sy = $pair[1]
    $x0 = if ($sx -eq 0) { $o } else { ($BW - $o) }
    $y0 = if ($sy -eq 0) { $o } else { ($BH - $o) }
    $dx = if ($sx -eq 0) { 1 } else { -1 }
    $dy = if ($sy -eq 0) { 1 } else { -1 }
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddLines([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(($x0 + $dx * $arm), $y0)),
      (New-Object System.Drawing.PointF($x0, $y0)),
      (New-Object System.Drawing.PointF($x0, ($y0 + $dy * $arm)))))
    $pen = New-Object System.Drawing.Pen (New-Col @(120, 96, 72)), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
