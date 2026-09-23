# UI 一套：深青钢 + 金饰 + 辉光（与 CardFrameV6 同语言）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV6.ps1"

function New-PathRect([single]$x,[single]$y,[single]$w,[single]$h,[single]$r) { return (New-RoundPath $x $y $w $h $r) }

function Fill-GradPath($g, $path, [int[]]$cTop, [int[]]$cBot, [single]$x, [single]$y, [single]$w, [single]$h) {
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle([int]$x,[int]$y,[int]$w,[int]$h)), (New-Col $cTop), (New-Col $cBot), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $sv = $g.Clip; $g.SetClip($path); $g.FillRectangle($lg, $x, $y, $w, $h); $g.Clip = $sv
  $lg.Dispose()
}

# 通用钢板：深青钢渐变 + 金边 + 墨线（r 为圆角）
function New-SteelPlate($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r, [string]$mode = "full") {
  $p = New-RoundPath $x $y $w $h $r
  Fill-GradPath $g $p @(38,52,70) @(14,20,29) $x $y $w $h
  # 顶部高光 + 底部暗档
  $sv = $g.Clip; $g.SetClip($p)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(140,170,205) 70))), $x, ($y + 3), $w, 5)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(0,0,0) 90))), $x, ($y + $h - 12), $w, 12)
  $g.Clip = $sv
  Stroke-RoundCol $g ($x + 12) ($y + 12) ($w - 24) ($h - 24) ([Math]::Max(2, $r - 10)) $GOLD 4 220
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 7
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $p2 = New-RoundPath ($x - 4) ($y - 4) ($w + 8) ($h + 8) ($r + 4)
  $g.DrawPath($pen, $p2); $pen.Dispose(); $p2.Dispose(); $p.Dispose()
}

function New-UiTopBorder([string]$outPath) {
  $W = 2048; $H = 128
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  $p = New-RoundPath 10 0 ($W - 20) ($H - 14) 26
  Fill-GradPath $g $p @(40,54,73) @(12,18,26) 10 0 ($W - 20) ($H - 14)
  $sv = $g.Clip; $g.SetClip($p)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(150,180,215) 80))), 10, 2, ($W - 20), 6)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(0,0,0) 110))), 10, ($H - 34), ($W - 20), 20)
  $g.Clip = $sv
  Stroke-RoundCol $g 22 12 ($W - 44) ($H - 38) 18 $GOLD 5 230
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 8
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK)), 6
  $g.DrawPath($pen, (New-RoundPath 6 -4 ($W - 12) ($H - 6) 30)); $pen.Dispose()
  # 两端金饰 + 中央小牌
  foreach ($x0 in @(40, ($W - 40))) {
    $dx = if ($x0 -lt 100) { 1 } else { -1 }
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddLines([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF(($x0 + $dx * 120), 30)),
      (New-Object System.Drawing.PointF($x0, 30)),
      (New-Object System.Drawing.PointF($x0, ($H - 60)))))
    $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 220)), 12
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $g.DrawPath($pen, $pp); $pen.Dispose(); $pp.Dispose()
  }
  $cx = $W / 2.0
  New-SteelPlate $g ($cx - 150) 18 300 74 16
  $p.Dispose()
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

# 图标：裸字形 + 墨线 + 硬边暗档（保留语义色）
function New-UiIcon([string]$kind, [string]$outPath) {
  $S = 256
  $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $cx = $S / 2.0; $cy = $S / 2.0
  $main = switch ($kind) {
    "health" { @(206, 74, 66) }
    "energy" { @(86, 160, 226) }
    "cardcount" { @(206, 172, 88) }
    "turncount" { @(206, 172, 88) }
    "settings" { @(150, 172, 194) }
    default { @(200, 164, 74) }
  }
  $dark = Mix-Col $main @(8, 12, 20) 0.55
  $light = Mix-Col $main @(255, 255, 255) 0.35
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  switch ($kind) {
    "health" {
      $w = 168.0; $h = 152.0
      $path.AddBezier($cx, ($cy + $h / 2), ($cx - $w / 2), ($cy + $h * 0.05), ($cx - $w * 0.62), ($cy - $h / 2), $cx, ($cy - $h * 0.28))
      $path.AddBezier($cx, ($cy - $h * 0.28), ($cx + $w * 0.62), ($cy - $h / 2), ($cx + $w / 2), ($cy + $h * 0.05), $cx, ($cy + $h / 2))
      $path.CloseFigure()
    }
    "energy" {
      $path.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(($cx + 26), ($cy - 96))),
        (New-Object System.Drawing.PointF(($cx - 62), ($cy + 12))),
        (New-Object System.Drawing.PointF(($cx - 4), ($cy + 12))),
        (New-Object System.Drawing.PointF(($cx - 26), ($cy + 96))),
        (New-Object System.Drawing.PointF(($cx + 62), ($cy - 18))),
        (New-Object System.Drawing.PointF(($cx + 4), ($cy - 18)))))
    }
    "cardcount" {
      $path.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(($cx - 46), ($cy - 88))),
        (New-Object System.Drawing.PointF(($cx + 82), ($cy - 88))),
        (New-Object System.Drawing.PointF(($cx + 82), ($cy + 40))),
        (New-Object System.Drawing.PointF(($cx - 46), ($cy + 40)))))
      $p2 = New-Object System.Drawing.Drawing2D.GraphicsPath
      $p2.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(($cx - 82), ($cy - 52))),
        (New-Object System.Drawing.PointF(($cx + 46), ($cy - 52))),
        (New-Object System.Drawing.PointF(($cx + 46), ($cy + 76))),
        (New-Object System.Drawing.PointF(($cx - 82), ($cy + 76)))))
      $path.AddPath($p2, $false)
      $p2.Dispose()
    }
    "turncount" {
      $path.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(($cx - 62), ($cy - 88))),
        (New-Object System.Drawing.PointF(($cx + 62), ($cy - 88))),
        (New-Object System.Drawing.PointF(($cx + 6), ($cy))),
        (New-Object System.Drawing.PointF(($cx + 62), ($cy + 88))),
        (New-Object System.Drawing.PointF(($cx - 62), ($cy + 88))),
        (New-Object System.Drawing.PointF(($cx - 6), ($cy)))))
    }
    "settings" {
      $path.AddEllipse(($cx - 46), ($cy - 46), 92, 92)
      for ($i = 0; $i -lt 8; $i++) {
        $a = [Math]::PI / 4 * $i
        $r1 = 66; $r2 = 92
        $p2 = New-Object System.Drawing.Drawing2D.GraphicsPath
        $p2.AddPolygon([System.Drawing.PointF[]]@(
          (New-Object System.Drawing.PointF(($cx + $r1 * [Math]::Cos($a - 0.30)), ($cy + $r1 * [Math]::Sin($a - 0.30)))),
          (New-Object System.Drawing.PointF(($cx + $r2 * [Math]::Cos($a - 0.19)), ($cy + $r2 * [Math]::Sin($a - 0.19)))),
          (New-Object System.Drawing.PointF(($cx + $r2 * [Math]::Cos($a + 0.19)), ($cy + $r2 * [Math]::Sin($a + 0.19)))),
          (New-Object System.Drawing.PointF(($cx + $r1 * [Math]::Cos($a + 0.30)), ($cy + $r1 * [Math]::Sin($a + 0.30))))))
        $path.AddPath($p2, $false)
        $p2.Dispose()
      }
      $path.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
    }
  }
  $g.FillPath((New-Object System.Drawing.SolidBrush (New-Col $main)), $path)
  $sv = $g.Clip; $g.SetClip($path)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col $dark 200))), 0, ($cy + 10), $S, $S)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col $light 150))), 0, 0, ($cx - 10), 120)
  $g.Clip = $sv
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 235)), 14
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 150)), 5
  $g.DrawPath($pen, $path); $pen.Dispose()
  if ($kind -eq "settings") {
    $g.FillPath((New-Object System.Drawing.SolidBrush ((New-Col @(20,28,38)))), (New-Object System.Drawing.Drawing2D.GraphicsPath))
  }
  $path.Dispose()
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

function New-UiEndTurn([string]$outPath) {
  $W = 768; $H = 512
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  New-SteelPlate $g 8 8 ($W - 16) ($H - 16) 40
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

function New-UiCirclePlate([string]$kind, [string]$outPath) {
  $W = 768; $H = 512
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $cx = $W / 2.0; $cy = $H / 2.0
  $acc = switch ($kind) { "health" { @(196, 66, 58) } "energy" { @(72, 150, 220) } default { @(206, 172, 88) } }
  # 内盘
  $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
  $inner.AddEllipse(($cx - 196), ($cy - 196), 392, 392)
  Fill-GradPath $g $inner @(34,46,63) @(12,18,26) ($cx - 196) ($cy - 196) 392 392
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 200)), 8
  $g.DrawPath($pen, $inner); $pen.Dispose()
  # 外环：青钢 + 金
  $ring = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ring.AddEllipse(($cx - 232), ($cy - 232), 464, 464)
  $ring.AddEllipse(($cx - 196), ($cy - 196), 392, 392)
  $ring.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
  Fill-GradPath $g $ring @(46,62,84) @(16,22,32) ($cx - 232) ($cy - 232) 464 464
  $pen = New-Object System.Drawing.Pen ((New-Col $acc)), 10
  $g.DrawPath($pen, $ring); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD 210)), 5
  $g.DrawPath($pen, $ring); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 220)), 7
  $ring2 = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ring2.AddEllipse(($cx - 240), ($cy - 240), 480, 480)
  $g.DrawPath($pen, $ring2); $pen.Dispose(); $ring2.Dispose()
  $inner.Dispose(); $ring.Dispose()
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}

function New-UiPhaseBand([string]$outPath) {
  $W = 853; $H = 55
  $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $p = New-RoundPath 0 0 $W $H 16
  Fill-GradPath $g $p @(40,54,73) @(14,20,29) 0 0 $W $H
  $sv = $g.Clip; $g.SetClip($p)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ((New-Col @(0,0,0) 90))), 0, ($H - 8), $W, 8)
  $g.Clip = $sv
  Stroke-RoundCol $g 8 8 ($W - 16) ($H - 16) 10 $GOLD 4 220
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD)), 5
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
  $g.Dispose(); $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return $outPath
}
