# 找到对手后的确认弹窗 v1 —— 「己方头像 / 15 秒倒计时 / 对方头像 + 确认 / 拒绝」（2026-09-27）
#
# 用户 2026-09-27：找到敌人后的确认弹窗，左右两个图案分别是己方和对方头像，中间是一个 15 秒倒计时
#   （倒计时结束若双方有一个不确认就默认取消）；左下角是确认，右下角是拒绝；非确认的头像（一开始双方默认不确认）
#   会有类似于战斗场景里的卡牌压黑机制（**边框本身白色，压黑后是灰色**），确认后恢复原色；
#   若双方点击确认，确认和拒绝会隐藏，中间的倒计时会变成金色 3 秒倒计时，同时开始预加载战斗场景素材。
#
# 手法同全套 UI：深蓝黑石面 + 一条金细线 + 平板；禁倒角 / 内阴影 / 外发光 / 双层面板 / 材质贴图。
# 色值一律取共享调色板（TopBarV2 -> CardFrameV6）。
#
# 产物（Assets/_Game/Art/Sprites/Generated/match-confirm-v1/）
#   Confirm_Plate.png        2700x1560 = 屏幕 900x520 —— 弹窗底板
#   Confirm_TimerPlate.png    750x330  = 屏幕 250x110 —— 中央倒计时衬底
#   Confirm_Frame.png         528x528  = 屏幕 176x176 —— 头像边框（**纯白**，运行时靠 RawImage.color 压成灰）
#   Confirm_BtnPlate.png      960x252  = 屏幕 320x84  —— 确认 / 拒绝底 · 常态
#   Confirm_BtnPlateHover.png 960x252                 —— 同一底 · 悬停（同 LobbyBtnPlateHover 配方）
#   ---- 尺寸不是 2 的幂：TextureImportSettingsGuard 的 NoNpotScaleFolders 已含本目录，禁止 Unity 缩放 ----
#
# 版式（屏幕 px，参考 1920x1080，整块居中）：
#   底板 900x520 居中；左右头像框 176x176 于 (∓280, +55)；倒计时衬底 250x110 于 (0, +55)
#   两键 320x84 于 (∓190, -120)
#
# 预览（Tools/cardframe/preview/match-confirm-v1.png）：未确认 / 双方确认 两种状态 1:1 + 版式标注
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/match-confirm-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }

$S = 3

# 屏幕 px 版式（唯一口径，生成与预览共用）
$PLATE_W = 900; $PLATE_H = 520
$FRM_W   = 176; $FRM_H   = 176
$TMR_W   = 250; $TMR_H   = 110
$BTN_W   = 320; $BTN_H   = 84
$AV_X    = 280; $AV_Y    = 55
$BTN_X   = 190; $BTN_Y   = -120

$LW_INK  = 9
$LW_GOLD = 3
$INS_P   = 24
$INS_B   = 18
$A_P      = 168
$A_B      = 150

function Get-McFont([single]$px) {
  if ($script:McFC -eq $null) {
    $script:McFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:McFC.AddFontFile($FONT)
  }
  return New-Object System.Drawing.Font($script:McFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
function Put-McTxt($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 236, [int]$rr = 240, [int]$gg = 232, [int]$bb = 210) {
  $f = Get-McFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $rr, $gg, $bb))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function Put-McTxtC($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int]$a = 236, [int]$rr = 240, [int]$gg = 232, [int]$bb = 210) {
  $f = Get-McFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $rr, $gg, $bb))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}
function Get-McTinted([System.Drawing.Image]$im, [System.Drawing.Color]$tint) {
  $b = New-Object System.Drawing.Bitmap($im.Width, $im.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = $tint.R / 255.0
  $cm.Matrix11 = $tint.G / 255.0
  $cm.Matrix22 = $tint.B / 255.0
  $cm.Matrix33 = 1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $g.DrawImage($im, (New-Object System.Drawing.Rectangle 0, 0, $im.Width, $im.Height), 0, 0, $im.Width, $im.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $g.Dispose(); $ia.Dispose()
  return $b
}

# 平板底：整块平底 + 外墨边 + 一条金细线（与 MatchWait / battle-mode 同一配方）
function New-McPlate([string]$out, [int]$wt, [int]$ht, [single]$rad, [single]$ins, [int]$aGold, [switch]$hover) {
  $cTop = $BAR_T; $cBot = $BAR_B; $cGold = $GOLD; $aWedge = 64; $aG = $aGold
  if ($hover) {
    $cTop = (Mix-Col $BAR_T $HILITE 0.12); $cBot = (Mix-Col $BAR_B $BAR_T 0.40)
    $cGold = $GOLD_L; $aWedge = 96; $aG = [Math]::Min(255, $aGold + 86)
  }
  $res = New-Bmp $wt $ht; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($wt - $LW_INK) ($ht - $LW_INK) $rad
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $wt $ht $cTop $cBot 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($wt * 0.55), 0)),
    (New-Object System.Drawing.PointF(0, ($ht * 0.52))))) $cTop $aWedge
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $ix = $oi + $ins
  $inner = New-RoundPath $ix $ix ($wt - 2 * $ix) ($ht - 2 * $ix) ([Math]::Max(2.0, $rad - $ins))
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aG)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

# 头像框：**纯白**（运行时靠 RawImage.color 乘灰）—— 外圈 + 内发丝圈 + 四向刻度，形体同大厅金圆框那一族
function New-McFrame([string]$out, [int]$wt) {
  $res = New-Bmp $wt $wt; $b = $res[0]; $g = $res[1]
  $cx = $wt / 2.0
  $white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
  $soft  = [System.Drawing.Color]::FromArgb(200, 255, 255, 255)
  $w1 = 14.0
  $ro = $cx - $w1 / 2.0 - 3
  $pen = New-Object System.Drawing.Pen $white, $w1
  $g.DrawEllipse($pen, ($cx - $ro), ($cx - $ro), ($ro * 2), ($ro * 2)); $pen.Dispose()
  $w2 = 4.0
  $ri = $ro - 34
  $pen = New-Object System.Drawing.Pen $soft, $w2
  $g.DrawEllipse($pen, ($cx - $ri), ($cx - $ri), ($ri * 2), ($ri * 2)); $pen.Dispose()
  $pen = New-Object System.Drawing.Pen $white, 10.0
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  foreach ($k in 0..3) {
    $a = [Math]::PI / 2.0 * $k
    $x1 = $cx + [Math]::Cos($a) * ($ri + 6);  $y1 = $cx + [Math]::Sin($a) * ($ri + 6)
    $x2 = $cx + [Math]::Cos($a) * ($ro - 10); $y2 = $cx + [Math]::Sin($a) * ($ro - 10)
    $g.DrawLine($pen, [single]$x1, [single]$y1, [single]$x2, [single]$y2)
  }
  $pen.Dispose()
  return (Save-Bmp $b $g $out)
}

# ── 预览 ────────────────────────────────────────────────────
function Draw-McAvatar($g, [single]$cx, [single]$cy, [single]$rad, [bool]$locked) {
  $col = [System.Drawing.Color]::FromArgb(255, 190, 200, 214)
  if ($locked) { $col = [System.Drawing.Color]::FromArgb(255, 92, 99, 110) }
  $br = New-Object System.Drawing.SolidBrush $col
  $g.FillEllipse($br, ($cx - $rad), ($cy - $rad), ($rad * 2), ($rad * 2)); $br.Dispose()
}
function Draw-McFrame($g, [single]$cx, [single]$cy, [single]$size, [string]$path, [bool]$locked) {
  $im = [System.Drawing.Image]::FromFile($path)
  if ($locked) { $ti = Get-McTinted $im ([System.Drawing.Color]::FromArgb(255, 110, 119, 131)); $im.Dispose(); $im = $ti }
  $g.DrawImage($im, ($cx - $size / 2.0), ($cy - $size / 2.0), $size, $size); $im.Dispose()
}

function New-McSheet([string]$dir, [string]$out) {
  $CW = 2000; $CH = 800
  $b = New-Object System.Drawing.Bitmap($CW, $CH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 9, 12, 18))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-McTxt $g '找到对手 · 确认弹窗 v1' 40 22 30
  Put-McTxt $g '底板 900x520 屏幕 px（贴图 3x）· 头像框 176（贴图纯白，运行时乘灰 = 未确认）· 倒计时衬底 250x110 · 两键 320x84' 40 62 19 176
  Put-McTxt $g '左 = 己方 / 右 = 对方；一开始双方都未确认（框是灰的）→ 各自点确认后那一侧恢复纯白' 40 88 19 176

  $plate = [System.Drawing.Image]::FromFile((Join-Path $dir 'Confirm_Plate.png'))
  $tmr   = [System.Drawing.Image]::FromFile((Join-Path $dir 'Confirm_TimerPlate.png'))
  $btnN  = [System.Drawing.Image]::FromFile((Join-Path $dir 'Confirm_BtnPlate.png'))
  $framePath = (Join-Path $dir 'Confirm_Frame.png')
  $frS = $FRM_W

  # ① 未确认
  $px = 40.0; $py = 140.0
  $g.DrawImage($plate, $px, $py, $PLATE_W, $PLATE_H)
  $ccx = $px + $PLATE_W / 2.0; $ccy = $py + $PLATE_H / 2.0
  Draw-McAvatar $g ($ccx - $AV_X) ($ccy - $AV_Y) ($frS * 0.36) $true
  Draw-McFrame  $g ($ccx - $AV_X) ($ccy - $AV_Y) $frS $framePath $true
  Draw-McAvatar $g ($ccx + $AV_X) ($ccy - $AV_Y) ($frS * 0.36) $true
  Draw-McFrame  $g ($ccx + $AV_X) ($ccy - $AV_Y) $frS $framePath $true
  $g.DrawImage($tmr, ($ccx - $TMR_W / 2.0), ($ccy - $AV_Y - $TMR_H / 2.0), $TMR_W, $TMR_H)
  Put-McTxtC $g '15' $ccx ($ccy - $AV_Y) 62 255 255 255 255
  foreach ($sx in @(-1, 1)) {
    $bx = $ccx + $sx * $BTN_X - $BTN_W / 2.0
    $by = $ccy + $BTN_Y - $BTN_H / 2.0
    $g.DrawImage($btnN, $bx, $by, $BTN_W, $BTN_H)
    $t = if ($sx -lt 0) { '确认' } else { '拒绝' }
    Put-McTxtC $g $t ($ccx + $sx * $BTN_X) ($ccy + $BTN_Y) 34 255 255 255 255
  }
  Put-McTxt $g '① 刚找到对手：两边框都是灰的（未确认）· 白字 15 秒倒计时 · 左下确认 / 右下拒绝' $px ($py + $PLATE_H + 14) 19 210

  # ② 双方已确认
  $px2 = 1010.0
  $g.DrawImage($plate, $px2, $py, $PLATE_W, $PLATE_H)
  $ccx2 = $px2 + $PLATE_W / 2.0; $ccy2 = $py + $PLATE_H / 2.0
  Draw-McAvatar $g ($ccx2 - $AV_X) ($ccy2 - $AV_Y) ($frS * 0.36) $false
  Draw-McFrame  $g ($ccx2 - $AV_X) ($ccy2 - $AV_Y) $frS $framePath $false
  Draw-McAvatar $g ($ccx2 + $AV_X) ($ccy2 - $AV_Y) ($frS * 0.36) $false
  Draw-McFrame  $g ($ccx2 + $AV_X) ($ccy2 - $AV_Y) $frS $framePath $false
  $g.DrawImage($tmr, ($ccx2 - $TMR_W / 2.0), ($ccy2 - $AV_Y - $TMR_H / 2.0), $TMR_W, $TMR_H)
  Put-McTxtC $g '3' $ccx2 ($ccy2 - $AV_Y) 62 255 200 164 74
  Put-McTxt $g '② 双方确认：两边框恢复纯白 · 确认 / 拒绝整组隐藏 · 倒计时变金色 3 秒（同时开始预加载战斗素材）' $px2 ($py + $PLATE_H + 14) 19 210

  Put-McTxt $g '注：『压黑』不动贴图 —— 边框贴图本身纯白，运行时把 RawImage.color 乘成钢灰 #6E7783；头像图也一起压暗（同战斗里卡牌压黑的手感）。' 40 726 19 176
  Put-McTxt $g '两键悬停 = 底板换 *Hover（金线提亮）+ 文字变亮金 #E4CB84，形体不动所以不跳位。' 40 754 19 176
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

$made = @()
$made += (New-McPlate (Join-Path $GEN 'Confirm_Plate.png')        ($PLATE_W * $S) ($PLATE_H * $S) 34 $INS_P $A_P)
$made += (New-McPlate (Join-Path $GEN 'Confirm_TimerPlate.png')   ($TMR_W * $S)   ($TMR_H * $S)   22 $INS_B $A_B)
$made += (New-McPlate (Join-Path $GEN 'Confirm_BtnPlate.png')     ($BTN_W * $S)   ($BTN_H * $S)   22 $INS_B $A_B)
$made += (New-McPlate (Join-Path $GEN 'Confirm_BtnPlateHover.png') ($BTN_W * $S)  ($BTN_H * $S)   22 $INS_B $A_B -hover)
$made += (New-McFrame (Join-Path $GEN 'Confirm_Frame.png')        ($FRM_W * $S))
$sheet = New-McSheet $GEN (Join-Path $PREV 'match-confirm-v1.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"