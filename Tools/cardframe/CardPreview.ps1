# 卡面预览合成：卡框 + 真实卡图 + 名字 / 费用 / 种类 / 攻血，与现行卡面并排对比
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/CardFrameV2.ps1"

$GENDIR = "Assets/_Game/Art/Sprites/Generated"
$FONT_OTF = "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf"
$ART_SRC  = "Assets/_Game/Resources/Cards/Summon/Hero/1/SummonCard_{01103}.png"
$UI       = "Assets/_Game/Resources/UI"

$FW = 1152; $FH = 2016
$ART_W = 884.7; $ART_H = 1161.2; $ART_CX = 576.0; $ART_CY = 1016.8
$NAME_L = 200; $ROW_CY = 205; $NAME_MAXW = 610
$TYPE_CX = 930; $TYPE_CY = 205
$HP_CX = 275; $ATK_CX = 877; $SOCK_Y = 1764
$COST_CX = 64; $COST_CY = 76; $COST_D = 240
$SAMPLE_NAME = "能量收割者"     # 6 字，全库最长档
$SAMPLE_COST = 3
$SAMPLE_HP   = 6
$SAMPLE_ATK  = 2

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

function Draw-Txt($g, [string]$s, $font, [int[]]$rgb, [single]$x, [single]$cy) {
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, $rgb[0], $rgb[1], $rgb[2]))
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Near
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $sz = $g.MeasureString($s, $font)
  $rect = New-Object System.Drawing.RectangleF($x, ($cy - $sz.Height), ($sz.Width + 60), ($sz.Height * 2))
  $g.DrawString($s, $font, $brush, $rect, $fmt)
  $brush.Dispose(); $fmt.Dispose()
}

function Draw-TxtCenter($g, [string]$s, $font, [int[]]$rgb, [single]$cx, [single]$cy) {
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, $rgb[0], $rgb[1], $rgb[2]))
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $sz = $g.MeasureString($s, $font)
  $rect = New-Object System.Drawing.RectangleF(($cx - $sz.Width), ($cy - $sz.Height), ($sz.Width * 2), ($sz.Height * 2))
  $g.DrawString($s, $font, $brush, $rect, $fmt)
  $brush.Dispose(); $fmt.Dispose()
}

function New-CardCanvas([string]$framePath, [bool]$newLayout, [string]$outPath) {
  $bmp = New-Object System.Drawing.Bitmap($FW, $FH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([System.Drawing.Color]::FromArgb(255, 40, 40, 44))

  $fam = Get-BigFamily

  # 0) 卡图底衬（对应 prefab 的 PrefixBg，让缩略图读起来像游戏里）
  $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 58, 34, 46))
  $g.FillRectangle($bg, 146, 442, 860, 1104)
  $bg.Dispose()

  # 1) 卡图
  $art = Load-Img $ART_SRC
  Draw-Img $g $art $ART_CX $ART_CY $ART_W $ART_H
  $art.Dispose()

  # 2) 卡框
  $fr = Load-Img $framePath
  Draw-Img $g $fr ($FW / 2) ($FH / 2) $FW $FH
  $fr.Dispose()

  # 3) 名字（新布局＝左对齐；旧布局＝居中原位）
  $nameFont = New-Object System.Drawing.Font($fam, ($NAME_MAXW / 6.0), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  if ($newLayout) {
    $sz = $g.MeasureString($SAMPLE_NAME, $nameFont)
    Draw-Txt $g $SAMPLE_NAME $nameFont @(255, 255, 255) $NAME_L $ROW_CY
  } else {
    Draw-TxtCenter $g $SAMPLE_NAME $nameFont @(255, 255, 255) 576 201
  }
  $nameFont.Dispose()

  # 4) 费用宝石 + 数字
  $cost = Load-Img (Join-Path $GENDIR "CostV2.png")
  Draw-Img $g $cost $COST_CX $COST_CY $COST_D $COST_D
  $cost.Dispose()
  $costFont = New-Object System.Drawing.Font($fam, ($COST_D * 0.46), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  Draw-TxtCenter $g "$SAMPLE_COST" $costFont @(255, 255, 255) $COST_CX $COST_CY
  $costFont.Dispose()

  # 5) 种类图标（新布局＝名条右侧；旧布局＝卡片顶边正中）
  $type = Load-Img "$UI/Hero.png"
  if ($newLayout) {
    Draw-Img $g $type $TYPE_CX $TYPE_CY 150 150
  } else {
    Draw-Img $g $type 576 0 258 258
  }
  $type.Dispose()

  # 6) 攻 / 血
  $numFont = New-Object System.Drawing.Font($fam, 160, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $hpIcon = Load-Img "$UI/Health.png"; Draw-Img $g $hpIcon $HP_CX $SOCK_Y 210 210; $hpIcon.Dispose()
  $atkIcon = Load-Img "$UI/Attack.png"; Draw-Img $g $atkIcon $ATK_CX $SOCK_Y 210 210; $atkIcon.Dispose()
  Draw-TxtCenter $g "$SAMPLE_HP" $numFont @(255, 255, 255) $HP_CX $SOCK_Y
  Draw-TxtCenter $g "$SAMPLE_ATK" $numFont @(255, 255, 255) $ATK_CX $SOCK_Y
  $numFont.Dispose()

  # 7) 前缀图标（落在名条中央小饰上）
  $pre = Load-Img "$UI/../Icons/Prefixes/Psychic.png"
  Draw-Img $g $pre 576 352 138 138
  $pre.Dispose()

  $g.Dispose()
  $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $outPath
}
