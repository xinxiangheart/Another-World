# 卡槽美术：SlotPlate（凹槽底板）/ SlotEdge（线层，纯白以便被状态色相乘）
# 尺寸沿用 540x960（显示 135x240，1/4 缩放），pivot 居中
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

$SW = 540; $SH = 960
$IN = @(40, 26, 17)          # 墨线
$SLOT_DEEP  = @(58, 44, 34)  # 凹面（深）
$SLOT_MID   = @(78, 60, 47)  # 凹面（本体）
$SLOT_RIM   = @(104, 82, 64) # 下沿受光
$SLOT_SHADE = @(44, 33, 25)  # 上沿凹影

function New-SlotPlate([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($SW, $SH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $L = 18; $T = 18; $W = $SW - 36; $H = $SH - 36; $R = 46

  # 底面
  $p = New-RoundPath $L $T $W $H $R
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $SLOT_MID)), $p)
  # 硬边凹影：上沿一条
  $sv = $g.Clip; $g.SetClip($p)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $SLOT_SHADE)), $L, $T, $W, 210)
  # 硬边受光：下沿一条
  $g.FillRectangle((New-Object System.Drawing.SolidBrush (New-Col $SLOT_RIM)), $L, ($T + $H - 46), $W, 46)
  $g.Clip = $sv
  # 内沿一圈亮线（凹槽的内壁）
  Stroke-Round $g ($L + 46) ($T + 46) ($W - 92) ($H - 92) ($R - 26) $SLOT_DEEP 22
  # 外墨线
  $pen = New-Object System.Drawing.Pen (New-Col $IN), 18
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()

  # 槽心标记：一枚平涂菱形（空槽时才看得到）
  $cx = $SW / 2.0; $cy = $SH / 2.0; $d = 52
  $dm = New-Object System.Drawing.Drawing2D.GraphicsPath
  $dm.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF($cx, ($cy - $d))),
    (New-Object System.Drawing.PointF(($cx + $d), $cy)),
    (New-Object System.Drawing.PointF($cx, ($cy + $d))),
    (New-Object System.Drawing.PointF(($cx - $d), $cy))))
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $SLOT_RIM)), $dm)
  $pen = New-Object System.Drawing.Pen (New-Col $SLOT_DEEP), 12
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $dm); $pen.Dispose(); $dm.Dispose()

  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

function New-SlotEdge([string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($SW, $SH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $white = @(255, 255, 255)
  $L = 18; $T = 18; $W = $SW - 36; $H = $SH - 36; $R = 46
  # 主框线
  Stroke-Round $g ($L + 22) ($T + 22) ($W - 44) ($H - 44) ($R - 8) $white 26
  # 四角直角括号
  $arm = 150; $t = 34; $o = $L + 6
  foreach ($pair in @(@(0, 0), @(1, 0), @(0, 1), @(1, 1))) {
    $sx = $pair[0]; $sy = $pair[1]
    $x0 = if ($sx -eq 0) { $o } else { ($SW - $o) }
    $y0 = if ($sy -eq 0) { $o } else { ($SH - $o) }
    $dx = if ($sx -eq 0) { 1 } else { -1 }
    $dy = if ($sy -eq 0) { 1 } else { -1 }
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddLines([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(($x0 + $dx * $arm), $y0)),
      (New-Object System.Drawing.PointF($x0, $y0)),
      (New-Object System.Drawing.PointF($x0, ($y0 + $dy * $arm)))))
    $pen = New-Object System.Drawing.Pen (New-Col $white), $t
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
