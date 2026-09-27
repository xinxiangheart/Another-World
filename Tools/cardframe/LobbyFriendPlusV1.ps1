# 好友侧边栏表头那个「+」v1（2026-09-27）—— 打开「好友详情」全屏
#
# 用户 2026-09-27：「在好友右边靠近右边框的地方加上一个加号ui用于打开好友详情全屏（类似于战斗，但不要做卡牌的，
#   我后续给你说怎么做）」。
#
# 配方**逐行照抄 Icon_Close**（Tools/cardframe/LobbyUIv1.ps1 的 New-CloseGlyph）——
# 圆角方印 + 等比内缩金细线 + 圆头金笔画，只把「叉」那两笔改成「加号」那两笔：
#   印 44,44,168,168 圆角 20 / 内框 58,58,140,140 圆角 14 / 笔画 92..164（长 72）、线宽 12（= ICON_GOLD x 2）、圆头。
# 悬停态同全族配方：石面提亮（顶 Mix(BAR_T,HILITE,0.12) / 底 Mix(BAR_B,BAR_T,0.40)）、金 GOLD -> GOLD_L 且 α +40。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   Icon_FriendPlus.png       256x256 —— 常态（场景里 44x44 显示）
#   Icon_FriendPlusHover.png  256x256 —— 悬停
#   256 是 2 的幂 -> 不吃 NoNpotScaleFolders 那条；但「带硬 alpha 边」那条白名单要加 Icon_FriendPlus
#   （见 Assets/_Game/Editor/TextureImportSettingsGuard.cs），否则缩到 44px 边缘会发黑。
#
# 预览：Tools/cardframe/preview/lobby-friend-plus.png（1:1 实尺 + 2x / 3x，衬一条深色底）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN))  { New-Item -ItemType Directory -Path $GEN  | Out-Null }
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$CAN       = 256     # 画布（同 Icon_Close）
$ICON_INK  = 9       # 贴图 px：形状外墨边
$ICON_GOLD = 6       # 贴图 px：金细线
$TONE_N = @{ 'top' = $BAR_T; 'bot' = $BAR_B; 'gold' = $GOLD; 'bright' = $GOLD_L; 'boost' = 0 }
$TONE_H = @{ 'top' = (Mix-Col $BAR_T $HILITE 0.12); 'bot' = (Mix-Col $BAR_B $BAR_T 0.40); 'gold' = $GOLD_L; 'bright' = (Mix-Col $GOLD_L $HILITE 0.35); 'boost' = 40 }

function New-FriendPlusIcon([string]$out, [bool]$hover) {
  $t = $(if ($hover) { $TONE_H } else { $TONE_N })
  $res = New-Bmp $CAN $CAN; $b = $res[0]; $g = $res[1]

  # 方印：深蓝黑平底竖渐变 + 外墨边
  $body = New-RoundPath 44 44 168 168 20
  $st = $g.Save(); $g.SetClip($body)
  $bd = $body.GetBounds()
  Fill-VGrad $g $bd.X $bd.Y $bd.Width $bd.Height $t['top'] $t['bot'] 255 255
  $g.Restore($st)
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 205)), $ICON_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $body); $pen.Dispose()

  # 等比内缩金细线
  $inner = New-RoundPath 58 58 140 140 14
  $pen = New-Object System.Drawing.Pen ((New-Col $t['gold'] ([Math]::Min(255, 235 + $t['boost'])))), $ICON_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $body.Dispose()

  # 加号：圆头，长 72、线宽 12（= ICON_GOLD x 2，同叉那条笔）
  $pen = New-Object System.Drawing.Pen ((New-Col $t['gold'] ([Math]::Min(255, 235 + $t['boost'])))), ($ICON_GOLD * 2)
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($pen, 92, 128, 164, 128)
  $g.DrawLine($pen, 128, 92, 128, 164)
  $pen.Dispose()

  return (Save-Bmp $b $g $out)
}

function Get-PrevFont([single]$px) {
  if ($script:PVFC -eq $null) {
    $script:PVFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:PVFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:PVFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel))
}

function New-FriendPlusSheet([string]$dir, [string]$out) {
  $CW = 1080; $CH = 460
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  Fill-VGrad $g 0 0 $CW $CH $BAR_T $BAR_B 255 255

  $f = Get-PrevFont 24; $br = New-Object System.Drawing.SolidBrush (New-Col $GOLD_L 236)
  $g.DrawString('好友侧边栏表头「+」（打开好友详情全屏）：256 贴图 = 屏幕 44x44', $f, $br, 40, 20)
  $f2 = Get-PrevFont 18; $br2 = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('配方照抄通用关闭叉 Icon_Close（圆角方印 + 内缩金线 + 圆头金笔画），只把叉改成加号；悬停 = 石面提亮 + 金 GOLD->GOLD_L', $f2, $br2, 40, 52)
  $br2.Dispose(); $f2.Dispose(); $br.Dispose(); $f.Dispose()

  $y = 110.0
  $x = 60.0
  foreach ($p in @(@('Icon_FriendPlus.png', '常态', 44), @('Icon_FriendPlusHover.png', '悬停', 44), @('Icon_FriendPlus.png', '常态 2x', 88), @('Icon_FriendPlus.png', '常态 3x', 132))) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $p[0]))
    $s = [single]$p[2]
    $g.DrawImage($im, [single]$x, [single]$y, $s, $s); $im.Dispose()
    $z = Get-PrevFont 18; $brz = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 190)
    $g.DrawString($p[1], $z, $brz, [single]$x, [single]($y + $s + 8)); $brz.Dispose(); $z.Dispose()
    $x += $s + 60
  }

  $n = Get-PrevFont 18; $brn = New-Object System.Drawing.SolidBrush (New-Col @(240,232,210) 176)
  $g.DrawString('场景：好友侧边栏 Body 表头行 —— 标题「好友」左边接 HUD 好友图标右沿 + 16，右边框内缩 28 处放本件（44x44，与标题盒同高）', $n, $brn, 40, 400)
  $brn.Dispose(); $n.Dispose()
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
$made += (New-FriendPlusIcon (Join-Path $GEN 'Icon_FriendPlus.png') $false)
$made += (New-FriendPlusIcon (Join-Path $GEN 'Icon_FriendPlusHover.png') $true)
$sheet = New-FriendPlusSheet $GEN (Join-Path $PREV 'lobby-friend-plus.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"