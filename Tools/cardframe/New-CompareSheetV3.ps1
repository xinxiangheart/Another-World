# 对比图 v3：参考图 / 现行卡框 / 新卡框 v3 + 游戏内尺寸
Add-Type -AssemblyName System.Drawing

$GENDIR = "Assets/_Game/Art/Sprites/Generated"
$REF = "C:\Users\22589\AppData\Local\Temp\codex-clipboard-19d0423d-acc2-418a-aa1d-02302657638b.png"

function New-CompareSheetV3([string]$outPath) {
  $sheetW = 1430; $sheetH = 1330
  $bmp = New-Object System.Drawing.Bitmap($sheetW, $sheetH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(255, 26, 26, 30))
  $font = New-Object System.Drawing.Font("Microsoft YaHei", 17)
  $fontS = New-Object System.Drawing.Font("Microsoft YaHei", 14)
  $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

  $panelH = 700
  $scale = 0.30
  $cw = 1440 * $scale; $ch = 2280 * $scale

  # 大图三格
  $items = @(
    @{ label = "参考：杀戮尖塔 2"; path = $REF;                     mode = "fit" },
    @{ label = "现行卡框";         path = "$GENDIR/preview-cur-card.png"; mode = "card" },
    @{ label = "新卡框 v3（名字靠左 / 种类在名右）"; path = "$GENDIR/preview-v3-card.png"; mode = "card" })
  $x = 40
  foreach ($it in $items) {
    $img = [System.Drawing.Image]::FromFile((Resolve-Path $it.path).Path)
    if ($it.mode -eq "card") {
      $g.DrawImage($img, $x, 46, $cw, $ch)
    } else {
      $h2 = $panelH - 60
      $w2 = $img.Width * $h2 / $img.Height
      $g.DrawImage($img, ($x + 20), 40, $w2, $h2)
    }
    $g.DrawString($it.label, $font, $white, $x, 12)
    $img.Dispose()
    $x += ($cw + 20)
  }

  # 游戏内尺寸一行
  $y0 = 46 + $ch + 56
  $g.DrawString("游戏内尺寸（2x / 1x）", $fontS, $white, 40, $y0 - 34)
  $x = 40
  foreach ($p in @("preview-cur-card.png", "preview-v3-card.png")) {
    $img = [System.Drawing.Image]::FromFile((Resolve-Path "$GENDIR/$p").Path)
    foreach ($k in @(0.215, 0.1075)) {
      $g.DrawImage($img, $x, $y0, (1440 * $k), (2280 * $k))
      $x += (1440 * $k + 16)
    }
    $img.Dispose()
  }

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
