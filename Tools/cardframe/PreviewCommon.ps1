# 卡面预览合成：把卡框 + 真实卡图 + 名字/费用/种类/攻血 拼成完整卡面，与现行卡面并排对比
Add-Type -AssemblyName System.Drawing

$GENDIR = "Assets/_Game/Art/Sprites/Generated"
$FONT   = "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf"
$ART    = "Assets/_Game/Resources/Cards/Summon/Hero/1/SummonCard_{01103}.png"
$ICON   = "Assets/_Game/Resources/UI"

# 卡面尺寸基准：剪影内缩 81px 之后是本体；立绘窗口与 prefab 里 CardArt 的实际占位一致
$FW = 1152; $FH = 2016
$ART_W = 884.7; $ART_H = 1161.2; $ART_CX = 576.0; $ART_CY = 1016.8   # 与 prefab CardArt(scale 0.06, y=-0.007) 一致
$NAME_L = 200; $ROW_CY = 205
$TYPE_CX = 930; $TYPE_CY = 205
$HP_CX = 275; $ATK_CX = 877; $SOCK_Y = 1764
$COST_CX = 64; $COST_CY = 76; $COST_D = 240

function Get-PrivateFamily([string]$path) {
  $pfc = New-Object System.Drawing.Text.PrivateFontCollection
  $pfc.AddFontFile((Resolve-Path $path).Path)
  return $pfc.Families[0]
}

function Load-Img([string]$path) { return [System.Drawing.Image]::FromFile((Resolve-Path $path).Path) }

function Draw-Img($g, $img, [single]$cx, [single]$cy, [single]$w, [single]$h) {
  $rect = New-Object System.Drawing.RectangleF(($cx - $w / 2), ($cy - $h / 2), $w, $h)
  $g.DrawImage($img, $rect)
}

function Draw-Text($g, [string]$s, $font, [int[]]$rgb, [single]$x, [single]$cy, [string]$align = "Near") {
  $col = [System.Drawing.Color]::FromArgb(255, $rgb[0], $rgb[1], $rgb[2])
  $brush = New-Object System.Drawing.SolidBrush $col
  $fmt = New-Object System.Drawing.StringFormat
  if ($align -eq "Near") { $fmt.Alignment = [System.Drawing.StringAlignment]::Near } else { $fmt.Alignment = [System.Drawing.StringAlignment]::Center }
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $sz = $g.MeasureString($s, $font)
  $rect = New-Object System.Drawing.RectangleF($x, ($cy - $sz.Height), ($sz.Width + 40), ($sz.Height * 2))
  $g.DrawString($s, $font, $brush, $rect, $fmt)
  $brush.Dispose(); $fmt.Dispose()
}
