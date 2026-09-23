# 卡面预览 v3：卡框 + 真实卡图 + 名字(靠左) / 费用 / 种类(名右) / 攻血
# 画布比卡面大一圈，好让费用宝石悬在卡外时不被裁掉
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV3.ps1"

$CW = 1440; $CH = 2280
$OX = 144; $OY = 114

$FONT_OTF = "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf"
$ART_SRC  = "Assets/_Game/Resources/Cards/Summon/Hero/1/SummonCard_{01103}.png"
$UI       = "Assets/_Game/Resources/UI"

$ART_W = 884.7; $ART_H = 1161.2; $ART_CX = 576.0; $ART_CY = 1016.8
$NAME_L = 186; $ROW_CY = 210; $NAME_PT = 104
$TAG_CX = 950; $TAG_CY = 210
$HP_CX = 275; $ATK_CX = 877; $SOCK_CY = 1764
$COST_CX = 64; $COST_CY = 76; $COST_D = 246
$PRE_CX = 576; $PRE_CY = 362

$script:PFC = $null
function Get-BigFamily() {
  if ($script:PFC -eq $null) {
    $script:PFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PFC.AddFontFile((Resolve-Path $FONT_OTF).Path)
  }
  return $script:PFC.Families[0]
}

function Load-Img([string]$p) { return [System.Drawing.Image]::FromFile((Resolve-Path $p).Path) }

function Draw-Img($g, $img, [single]$cx, [single]$cy, [single]$w, [single]$h) {
  $r = New-Object System.Drawing.RectangleF(($cx - $w / 2), ($cy - $h / 2), $w, $h)
  $g.DrawImage($img, $r)
}

function Draw-TxtLeft($g, [string]$s, $font, [int[]]$rgb, [single]$x, [single]$cy) {
  $brush = New-Object System.Drawing.SolidBrush (New-Col $rgb)
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Near
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $r = New-Object System.Drawing.RectangleF($x, ($cy - 120), 900, 240)
  $g.DrawString($s, $font, $brush, $r, $fmt)
  $brush.Dispose(); $fmt.Dispose()
}

function Draw-TxtCenter($g, [string]$s, $font, [int[]]$rgb, [single]$cx, [single]$cy) {
  $brush = New-Object System.Drawing.SolidBrush (New-Col $rgb)
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $sz = $g.MeasureString($s, $font)
  $r = New-Object System.Drawing.RectangleF(($cx - $sz.Width), ($cy - $sz.Height), ($sz.Width * 2), ($sz.Height * 2))
  $g.DrawString($s, $font, $brush, $r, $fmt)
  $brush.Dispose(); $fmt.Dispose()
}

# layout: "left" 新布局（名字靠左 / 种类在名右）  "center" 现行布局
function New-CardCanvasV3([string]$framePath, [string]$layout, [string]$gemPath, [string]$outPath, [string]$name, [int]$cost, [int]$hp, [int]$atk) {
  $bmp = New-Object System.Drawing.Bitmap($CW, $CH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(255, 40, 40, 44))
  $fam = Get-BigFamily

  # 0) 卡图底衬
  $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 58, 34, 46))
  $g.FillRectangle($bg, ($OX + 140), ($OY + 440), 872, 1120)
  $bg.Dispose()

  # 1) 卡图
  $art = Load-Img $ART_SRC
  Draw-Img $g $art ($OX + $ART_CX) ($OY + $ART_CY) $ART_W $ART_H
  $art.Dispose()

  # 2) 卡框（统一按 1152x2016 画）
  $fr = Load-Img $framePath
  Draw-Img $g $fr ($OX + 576) ($OY + 1008) 1152 2016
  $fr.Dispose()

  # 3) 名字
  $nameFont = New-Object System.Drawing.Font($fam, $NAME_PT, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $inkc = @(42, 26, 19)
  if ($layout -eq "left") {
    Draw-TxtLeft $g $name $nameFont @(5,8,13) ($OX + $NAME_L + 5) ($OY + $ROW_CY + 5)
    Draw-TxtLeft $g $name $nameFont @(232,209,138) ($OX + $NAME_L) ($OY + $ROW_CY)
  } else {
    Draw-TxtCenter $g $name $nameFont $inkc ($OX + 576) ($OY + 206)
  }
  $nameFont.Dispose()

  # 4) 费用宝石
  $gem = Load-Img $gemPath
  Draw-Img $g $gem ($OX + $COST_CX) ($OY + $COST_CY) $COST_D $COST_D
  $gem.Dispose()
  $cf = New-Object System.Drawing.Font($fam, ($COST_D * 0.44), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  Draw-TxtCenter $g "$cost" $cf @(255, 246, 224) ($OX + $COST_CX) ($OY + $COST_CY - 4)
  $cf.Dispose()

  # 5) 种类图标
  $type = Load-Img "$UI/Hero.png"
  if ($layout -eq "left") {
    Draw-Img $g $type ($OX + $TAG_CX) ($OY + $TAG_CY) 128 128
  } else {
    Draw-Img $g $type ($OX + 576) $OY 128 128
  }
  $type.Dispose()

  # 6) 前缀图标
  $pre = Load-Img "$UI/../Icons/Prefixes/Psychic.png"
  Draw-Img $g $pre ($OX + $PRE_CX) ($OY + $PRE_CY) 112 112
  $pre.Dispose()

  # 7) 攻 / 血
  $nf = New-Object System.Drawing.Font($fam, 168, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $hi = Load-Img "$UI/Health.png";  Draw-Img $g $hi ($OX + $HP_CX)  ($OY + $SOCK_CY - 2) 152 152; $hi.Dispose()
  $ai = Load-Img "$UI/Attack.png";  Draw-Img $g $ai ($OX + $ATK_CX) ($OY + $SOCK_CY - 2) 152 152; $ai.Dispose()
  Draw-TxtCenter $g "$hp"  $nf @(232,209,138) ($OX + $HP_CX + 46)  ($OY + $SOCK_CY + 8)
  Draw-TxtCenter $g "$atk" $nf @(232,209,138) ($OX + $ATK_CX - 46) ($OY + $SOCK_CY + 8)
  $nf.Dispose()

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
