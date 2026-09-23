# 环境美术 v3：按游戏自身配色（开始界面 #10223E 深蓝夜色 + 青钢 + 金饰）重做棋盘与卡槽
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

$BW = 2048; $BH = 1152
$FRAME   = @(10, 14, 22)
$FLD_C   = @(34, 48, 70)
$FLD_M   = @(22, 31, 47)
$FLD_E   = @(11, 16, 26)
$STEEL   = @(110, 147, 176)
$GOLD    = @(200, 164, 74)
$INK3    = @(6, 9, 14)

function New-BoardV5([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($BW, $BH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear((New-Col $FRAME))
  $L = 84; $T = 76; $W = $BW - 168; $H = $BH - 152

  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddRectangle((New-Object System.Drawing.RectangleF($L, $T, $W, $H)))
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
  $pg.CenterPoint = New-Object System.Drawing.PointF(($L + $W / 2), ($T + $H / 2))
  $pg.CenterColor = (New-Col $FLD_C)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $FLD_E))
  $pg.SetSigmaBellShape(0.75)
  $g.FillRectangle($pg, $L, $T, $W, $H); $pg.Dispose(); $path.Dispose()

  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($L, $T, $W, 150)), (New-Col @(4,6,10) 210), (New-Col @(4,6,10) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $L, $T, $W, 150); $lg.Dispose()

  # 金饰 + 青钢双线
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 120)), 6
  $g.DrawRectangle($pen, ($L + 20), ($T + 20), ($W - 40), ($H - 40)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 70)), 4
  $g.DrawRectangle($pen, ($L + 34), ($T + 34), ($W - 68), ($H - 68)); $pen.Dispose()

  # 桌面徽记：双环 + 刻度（金，低对比）
  $cx = $BW / 2.0; $cy = $BH / 2.0
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 110)), 7
  $g.DrawEllipse($pen, ($cx - 430), ($cy - 430), 860, 860); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $STEEL 55)), 4
  $g.DrawEllipse($pen, ($cx - 332), ($cy - 332), 664, 664); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 80)), 8
  foreach ($i in 0..23) {
    $a = [Math]::PI / 12.0 * $i
    $g.DrawLine($pen, [single]($cx + 430 * [Math]::Cos($a)), [single]($cy + 430 * [Math]::Sin($a)), [single]($cx + 466 * [Math]::Cos($a)), [single]($cy + 466 * [Math]::Sin($a)))
  }
  $pen.Dispose()

  # 四角金饰角铁
  $arm = 132; $t = 14; $o = 56
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
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 170)), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }

  # 氛围：边缘压暗 + 顶部一点冷光（对齐开始界面）
  $vg = New-Object System.Drawing.Drawing2D.GraphicsPath
  $vg.AddRectangle((New-Object System.Drawing.RectangleF(0, 0, $BW, $BH)))
  $vp = New-Object System.Drawing.Drawing2D.PathGradientBrush($vg)
  $vp.CenterPoint = New-Object System.Drawing.PointF(($BW / 2), ($BH / 2))
  $vp.CenterColor = (New-Col @(0,0,0) 0)
  $vp.SurroundColors = [System.Drawing.Color[]]@((New-Col @(0,0,0) 190))
  $g.FillRectangle($vp, 0, 0, $BW, $BH); $vp.Dispose(); $vg.Dispose()
  $tg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0, 0, $BW, 320)), (New-Col @(90,130,175) 40), (New-Col @(90,130,175) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($tg, 0, 0, $BW, 320); $tg.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

$SW = 540; $SH = 960
$SL_DEEP = @(10, 15, 23)
$SL_MID  = @(28, 39, 56)
$SL_RIM  = @(70, 100, 133)
$SL_INK  = @(5, 8, 13)

function New-SlotPlateV5([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($SW, $SH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $L = 18; $T = 18; $W = $SW - 36; $H = $SH - 36; $R = 46
  $p = New-RoundPath $L $T $W $H $R
  $sv = $g.Clip; $g.SetClip($p)
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($L, $T, $W, $H)), (New-Col $SL_MID), (New-Col $SL_DEEP), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $L, $T, $W, $H); $lg.Dispose()
  $lg2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($L, $T, $W, 260)), (New-Col @(2,4,7) 215), (New-Col @(2,4,7) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg2, $L, $T, $W, 260); $lg2.Dispose()
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col $SL_RIM 120))), $L, ($T + $H - 34), $W, 34)
  $g.Clip = $sv
  Stroke-Round $g ($L + 44) ($T + 44) ($W - 88) ($H - 88) ($R - 24) $SL_INK 18
  $pen = New-Object System.Drawing.Pen ((New-Col $SL_INK 235)), 18
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()

  $cx = $SW / 2.0; $cy = $SH / 2.0; $d = 50
  $dm = New-Object System.Drawing.Drawing2D.GraphicsPath
  $dm.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($cx, ($cy - $d))),
    (New-Object System.Drawing.PointF(($cx + $d), $cy)),
    (New-Object System.Drawing.PointF($cx, ($cy + $d))),
    (New-Object System.Drawing.PointF(($cx - $d), $cy))))
  $g.FillPath((New-Object System.Drawing.SolidBrush ((New-Col $SL_RIM 170))), $dm)
  $pen = New-Object System.Drawing.Pen ((New-Col $SL_INK)), 12
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dm); $pen.Dispose(); $dm.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
