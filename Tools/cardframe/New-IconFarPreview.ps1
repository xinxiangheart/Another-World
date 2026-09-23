# 远景可读性对照：新图标 vs 旧图标，按游戏内实际尺寸（行标 13% / 角标 18% 卡宽）贴在卡面底色上
param([string]$Out, [string]$NewDir, [string]$RepoRoot)
Add-Type -AssemblyName System.Drawing
$W = 1240; $H = 460
$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$W,$H)), [System.Drawing.Color]::FromArgb(255,30,41,56), [System.Drawing.Color]::FromArgb(255,14,20,29), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillRectangle($lg, 0, 0, $W, $H); $lg.Dispose()
$font = New-Object System.Drawing.Font('Consolas', 11)
$fontS = New-Object System.Drawing.Font('Consolas', 9)
$gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,232,209,138))
$white = [System.Drawing.Brushes]::White
$order = @('UI__Cost','UI__Attack','UI__Health','UI__Hero','UI__Chosen','UI__Special',
  'Icons__Prefixes__Abyss','Icons__Prefixes__Blood','Icons__Prefixes__Mech','Icons__Prefixes__Psychic','Icons__Prefixes__Scroll',
  'UI__First','UI__Enter','UI__Leave','UI__Exit','UI__Reverge','UI__Discard','UI__Attach',
  'Icons__Buffs__Shield','Icons__Buffs__Buff','Icons__Buffs__DeBuff')
$oldMap = @{
  'UI__Cost'='Resources/UI/Cost.png'; 'UI__Attack'='Resources/UI/Attack.png'; 'UI__Health'='Resources/UI/Health.png'
  'UI__Hero'='Resources/UI/Hero.png'; 'UI__Chosen'='Resources/UI/Chosen.png'; 'UI__Special'='Resources/UI/Special.png'
  'Icons__Prefixes__Abyss'='Resources/Icons/Prefixes/Abyss.png'; 'Icons__Prefixes__Blood'='Resources/Icons/Prefixes/Blood.png'
  'Icons__Prefixes__Mech'='Resources/Icons/Prefixes/Mech.png'; 'Icons__Prefixes__Psychic'='Resources/Icons/Prefixes/Psychic.png'
  'Icons__Prefixes__Scroll'='Resources/Icons/Prefixes/Scroll.png'
  'UI__First'='Resources/UI/First.png'; 'UI__Enter'='Resources/UI/Enter.png'; 'UI__Leave'='Resources/UI/Leave.png'
  'UI__Exit'='Resources/UI/Exit.png'; 'UI__Reverge'='Resources/UI/Reverge.png'; 'UI__Discard'='Resources/UI/Discard.png'
  'UI__Attach'='Resources/UI/Attach.png'
  'Icons__Buffs__Shield'='Resources/Icons/Buffs/Shield.png'; 'Icons__Buffs__Buff'='Resources/Icons/Buffs/Buff.png'
  'Icons__Buffs__DeBuff'='Resources/Icons/Buffs/DeBuff.png'
}
function PasteRow([int]$y, [int]$px, [string]$label, [bool]$isNew) {
  $g.DrawString($label, $font, $gold, 16, ($y - 20))
  $x = 16
  foreach ($k in $order) {
    $p = if ($isNew) { Join-Path $NewDir (($k -replace '__','/') + '.png') } else { Join-Path $RepoRoot ('Assets/_Game/' + $oldMap[$k]) }
    try {
      $img = [System.Drawing.Image]::FromFile($p)
      $g.DrawImage($img, $x, $y, $px, $px)
      $img.Dispose()
    } catch { $g.FillRectangle([System.Drawing.Brushes]::DarkRed, $x, $y, $px, $px) }
    $x += ($px + 10)
  }
}
$g.DrawString('新图标套件 v1 — 与开始界面 / 卡框 V6 同一材质语言（钢底 + 金边 + 属性色）', $font, $white, 16, 12)
PasteRow 44 44 '角标 18% 卡宽 + 行标 13% 卡宽（游戏内实际尺寸）' $true
PasteRow 140 44 '同上 — 旧图标（当前线上）' $false
PasteRow 240 130 '新图标 — 放大约 3 倍看细节' $true
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"$Out saved"
