# 环境美术 v2：对齐开始界面（#10223E 深蓝夜色 + 金饰）——棋盘 / 卡槽
# 环境层允许柔和渐变与氛围（AGENTS.md 的「无渐变」管的是卡牌立绘，不管环境与 UI）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

$BW = 2048; $BH = 1152
$FRAME   = @(12, 17, 26)
$FRAME_H = @(58, 72, 96)
$FLD_C   = @(38, 51, 79)
$FLD_E   = @(18, 25, 40)
$GOLD    = @(154, 130, 72)
$INK3    = @(8, 11, 17)

function New-BoardV4([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($BW, $BH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear((New-Col $FRAME))

  # 台面：中心微亮的径向（对齐开始界面 #10223E 夜色）
  $L = 78; $T = 70; $W = $BW - 156; $H = $BH - 140
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddRectangle((New-Object System.Drawing.RectangleF($L, $T, $W, $H)))
  $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
  $pg.CenterPoint = New-Object System.Drawing.PointF(($L + $W / 2), ($T + $H / 2))
  $pg.CenterColor = (New-Col $FLD_C)
  $pg.SurroundColors = [System.Drawing.Color[]]@((New-Col $FLD_E))
  $pg.SetSigmaBellShape(0.9)
  $g.FillRectangle($pg, $L, $T, $W, $H)
  $pg.Dispose(); $path.Dispose()

  # 台面内凹影（上柔和）
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($L, $T, $W, 120)), (New-Col @(6,9,15) 200), (New-Col @(6,9,15) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $L, $T, $W, 120); $lg.Dispose()

  # 台面外圈：金饰细线 + 墨线
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 7
  $g.DrawRectangle($pen, ($L + 16), ($T + 16), ($W - 32), ($H - 32)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK3 220)), 12
  $g.DrawRectangle($pen, $L, $T, $W, $H); $pen.Dispose()

  # 桌面徽记：双金环 + 刻度
  $cx = $BW / 2.0; $cy = $BH / 2.0
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 130)), 8
  $g.DrawEllipse($pen, ($cx - 430), ($cy - 430), 860, 860); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 80)), 5
  $g.DrawEllipse($pen, ($cx - 332), ($cy - 332), 664, 664); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 90)), 9
  foreach ($i in 0..23) {
    $a = [Math]::PI / 12.0 * $i
    $g.DrawLine($pen, [single]($cx + 430 * [Math]::Cos($a)), [single]($cy + 430 * [Math]::Sin($a)), [single]($cx + 468 * [Math]::Cos($a)), [single]($cy + 468 * [Math]::Sin($a)))
  }
  $pen.Dispose()

  # 四角金饰角铁
  $arm = 140; $t = 16; $o = 52
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
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 190)), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }

  # 边缘压暗（氛围，柔和）
  $vg = New-Object System.Drawing.Drawing2D.GraphicsPath
  $vg.AddRectangle((New-Object System.Drawing.RectangleF(0, 0, $BW, $BH)))
  $vp = New-Object System.Drawing.Drawing2D.PathGradientBrush($vg)
  $vp.CenterPoint = New-Object System.Drawing.PointF(($BW / 2), ($BH / 2))
  $vp.CenterColor = (New-Col @(0,0,0) 0)
  $vp.SurroundColors = [System.Drawing.Color[]]@((New-Col @(0,0,0) 170))
  $g.FillRectangle($vp, 0, 0, $BW, $BH)
  $vp.Dispose(); $vg.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

$SW = 540; $SH = 960
$SL_DEEP = @(16, 21, 32)
$SL_MID  = @(34, 44, 63)
$SL_RIM  = @(52, 66, 92)
$SL_INK  = @(8, 11, 17)

function New-SlotPlateV4([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($SW, $SH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $L = 18; $T = 18; $W = $SW - 36; $H = $SH - 36; $R = 46
  $p = New-RoundPath $L $T $W $H $R
  $sv = $g.Clip; $g.SetClip($p)
  # 竖向柔和渐变底（凹槽内壁）
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($L, $T, $W, $H)), (New-Col $SL_MID), (New-Col $SL_DEEP), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg, $L, $T, $W, $H); $lg.Dispose()
  # 上沿凹影（柔和）
  $lg2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle($L, $T, $W, 240)), (New-Col @(4,6,10) 200), (New-Col @(4,6,10) 0), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($lg2, $L, $T, $W, 240); $lg2.Dispose()
  # 下沿受光
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col $SL_RIM 190))), $L, ($T + $H - 40), $W, 40)
  $g.Clip = $sv
  Stroke-Round $g ($L + 44) ($T + 44) ($W - 88) ($H - 88) ($R - 24) $SL_INK 20
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
  $g.FillPath((New-Object System.Drawing.SolidBrush ((New-Col $SL_RIM 210))), $dm)
  $pen = New-Object System.Drawing.Pen ($SL_INK), 12
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dm); $pen.Dispose(); $dm.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
