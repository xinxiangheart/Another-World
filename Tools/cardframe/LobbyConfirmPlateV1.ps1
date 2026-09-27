# 「确认删除 / 确认拉黑」长条弹窗底板（2026-09-27）
#
# 用户 2026-09-27：「删除好友和拉黑好友都有一个长子弹窗，上面是确认删除/拉黑（金色的好友名称），
# 下面是有子背景的确认和取消」。
#
# 语言与手法同全套 UI（与 Invite_Plate / MatchWait_Plate 同一支笔）：深蓝黑石面 + 一条金细线 + 平板；
# 禁倒角 / 内阴影 / 外发光 / 双层面板 / 材质贴图。色值取共享调色板（TopBarV2 -> CardFrameV6）。
#
# 比例：**760x200 = 3.8:1** —— 比「收到邀请」小窗（420x144 = 2.92:1）再扁一档，因为这一块只有
# 「一行标题 + 一排两个键」，没有头像行；用户要的就是「长条」。
#
# 产物（Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/）
#   LobbyConfirmPlate.png   760x200 —— 窗口底板（固定尺寸，不切片）
#   ---- 760x200 不是 2 的幂：TextureImportSettingsGuard 的 lobby-ui-v1 分支已含 LobbyConfirmPlate ----
#   ---- 存成 1:1（不是全族那个 x3）：760x3 = 2280 超过导入器默认 maxTextureSize 2048 会被静默缩掉 ----
#       做法同 LobbyFriendRow：按 x3 画、再高质量降采样到屏幕尺寸。
#
# 版式（屏幕 px，居中；场景侧在 LobbyUIBuilder.BuildConfirmPanelMenu）：
#   标题 40..84（字号 34，左让 40 / 宽 680，居中一行）/
#   两键 96x48 @ y 124（x = 272 / 392，中间 24 间隔，整排居中）/ 下留 28   —— 合计 200
#
# 预览：Tools/cardframe/preview/lobby-confirm.png（1:1 实尺 + 1.5x + 版式标注）
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/lobby-ui-v1'
$CHIPPNG = Join-Path $GEN 'LobbyChip_Kick.png'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $PREV)) { New-Item -ItemType Directory -Path $PREV | Out-Null }

$S       = 3                       # 出图倍率：贴图 3px = 屏幕 1px
$PLATE_W = 760; $PLATE_H = 200     # 屏幕 px（3.8:1，长条）

$LW_INK  = 9                       # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3                       # 贴图 px：金细线（屏幕 1）
$INS_P   = 24                      # 贴图 px：金线内缩（屏幕 8）
$R_P     = 30                      # 贴图 px：圆角
$A_P     = 150                     # 金线 alpha（与 LobbyFriendRow 同档）

# ── 底板：配方同 New-InvPlate（只换尺寸），按 x3 画再降到 1:1 ────────────────
function Save-Down([System.Drawing.Bitmap]$big, [int]$dw, [int]$dh, [string]$out) {
  $res = New-Bmp $dw $dh; $b = $res[0]; $g = $res[1]
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingMode   = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  $g.DrawImage($big, (New-Object System.Drawing.Rectangle 0, 0, $dw, $dh))
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

function New-ConfirmPlate([string]$out) {
  $w = $PLATE_W * $S; $h = $PLATE_H * $S
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($w - $LW_INK) ($h - $LW_INK) $R_P
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $w $h $BAR_T $BAR_B 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($w * 0.52), 0)),
    (New-Object System.Drawing.PointF(0, ($h * 0.55))))) $BAR_T 64
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $ix = $oi + $INS_P
  $inner = New-RoundPath $ix $ix ($w - 2 * $ix) ($h - 2 * $ix) ([Math]::Max(2.0, $R_P - $INS_P))
  $pen = New-Object System.Drawing.Pen ((New-Col $GOLD $A_P)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose(); $outer.Dispose()
  $g.Dispose()
  return (Save-Down $b $PLATE_W $PLATE_H $out)
}

function Get-CfFont([single]$px) {
  if ($script:CFC -eq $null) {
    $script:CFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:CFC.AddFontFile($FONT)
  }
  return (New-Object System.Drawing.Font($script:CFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel))
}
function Put-Cf($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int[]]$rgb, [int]$a = 236) {
  $f = Get-CfFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $rgb[0], $rgb[1], $rgb[2]))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}
function Put-CfL($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 176) {
  $f = Get-CfFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}

# 一块底板 + 一屏内容（预览与场景共用同一套相对坐标）
# 场景口径：标题一行居中于窗心（y 60）/ 两个子背景键 96x48 @ y 124（x = 272 与 392）
function Draw-CfWindow($g, [string]$dir, [single]$px, [single]$py, [single]$k) {
  $w = $PLATE_W * $k; $h = $PLATE_H * $k
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'LobbyConfirmPlate.png'))
  $g.DrawImage($im, $px, $py, $w, $h); $im.Dispose()
  $cx = $px + $w / 2.0
  # 标题「确认删除 在线甲」：动词用奶油、好友名用金 —— 场景里是同一行富文本
  Put-Cf $g '确认删除' ($px + 300 * $k) ($py + 60 * $k) (34 * $k) @(240, 232, 210) 236
  Put-Cf $g '在线甲'   ($px + 470 * $k) ($py + 60 * $k) (34 * $k) @(228, 203, 132) 236   # 亮金 #E4CB84
  foreach ($t in @(@('确认', 272), @('取消', 392))) {
    $chipImg = [System.Drawing.Image]::FromFile($CHIPPNG)
    $g.DrawImage($chipImg, ($px + [single]$t[1] * $k), ($py + 124 * $k), (96 * $k), (48 * $k)); $chipImg.Dispose()
    Put-Cf $g $t[0] ($px + ([single]$t[1] + 48) * $k) ($py + 148 * $k) (30 * $k) @(240, 232, 210) 255
  }
}

function New-CfSheet([string]$dir, [string]$out) {
  $CW = 1760; $CH = 560
  $res = New-Bmp $CW $CH; $b = $res[0]; $g = $res[1]
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 9, 12, 18))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-CfL $g '确认删除 / 确认拉黑 —— 长条弹窗底板（760x200 = 3.8:1）' 36 16 28 236
  Put-CfL $g '深蓝黑石面 + 一条金细线 + 平板（与 Invite_Plate 同一支笔）· 存成 1:1（x3 画完降采样）' 36 54 18
  Put-CfL $g '标题一行（动词奶油 + 好友名亮金 #E4CB84，同一条富文本）/ 两键 = LobbyChip_Kick 子背景 96x48' 36 80 18
  Put-CfL $g '760x200 不是 2 的幂 —— 已进 NoNpotScaleFolders（禁止 Unity 缩放）' 36 106 18

  Draw-CfWindow $g $dir 56 150 1.0
  Put-CfL $g '① 1:1 实尺（760x200）' 56 366 18 210

  Draw-CfWindow $g $dir 900 150 1.0
  Put-CfL $g '② 同尺，换个动词（确认拉黑）' 900 366 18 210

  Put-CfL $g '版式：标题 40..84（字号 34、左让 40、宽 680）/ 两键 y124（x = 272 与 392，间隔 24，整排居中）/ 下留 28 = 200' 56 430 18
  Put-CfL $g '金细线内缩 24 贴图 px（屏幕 8）· 外墨边 9（屏幕 3）· 圆角 30（屏幕 10）' 56 456 18
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
$made += (New-ConfirmPlate (Join-Path $GEN 'LobbyConfirmPlate.png'))
$sheet = New-CfSheet $GEN (Join-Path $PREV 'lobby-confirm.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"
