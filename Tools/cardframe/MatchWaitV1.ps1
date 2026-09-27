# 匹配等待小视窗 v1 —— 顶部中央那块「匹配中：x：xx + 取消」的小窗（2026-09-27）
#
# 用户 2026-09-27：屏幕中央顶侧出现小视窗，上面一栏是「匹配中（金字）：x：xx」（x=匹配时间，白字），
#   下面是取消 button（白字，悬停变金），其有子背景（只覆盖「取消」两个字，用于提示）；
#   匹配到人时取消文字与背景隐藏，「匹配中」变成「已找到对手！」。
#
# 语言与手法同全套 UI：深蓝黑石面 + 一条金细线 + 平板；禁倒角 / 内阴影 / 外发光 / 双层面板 / 材质贴图。
# 色值一律取共享调色板（TopBarV2 -> CardFrameV6）：$INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE。
#
# 产物（Assets/_Game/Art/Sprites/Generated/match-wait-v1/）
#   MatchWait_Plate.png          1080x456 = 屏幕 360x152  —— 小窗底板（固定尺寸，不切片）
#   MatchWait_CancelBg.png        288x144 = 屏幕  96x48   —— 「取消」子背景 · 常态
#   MatchWait_CancelBgHover.png   288x144                 —— 「取消」子背景 · 悬停（同 LobbyBtnPlateHover 配方）
#   ---- 尺寸不是 2 的幂，且要 1:1 采样：TextureImportSettingsGuard 的 NoNpotScaleFolders 里已含本目录 ----
#
# 版式（屏幕 px，锚屏幕顶中）：
#   底板 360x152：上 26 留白 / 第一行字高 40 / 间隔 18 / 取消键 96x48 / 下 20 留白
#   悬停只动色调，形体不动 -> 切换贴图不跳位。
#
# 预览（Tools/cardframe/preview/match-wait-v1.png）：两种状态 1:1 + 子背景放大 + 版式标注
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot/TopBarV2.ps1"          # $ROOT / $INK / $BAR_T / $BAR_B / $GOLD / $GOLD_L / $HILITE 与 New-Col / Mix-Col / New-Bmp / Save-Bmp / New-RoundPath / New-Diamond / Fill-VGrad / Add-Wedge

$GEN  = Join-Path $ROOT 'Assets/_Game/Art/Sprites/Generated/match-wait-v1'
$PREV = Join-Path $PSScriptRoot 'preview'
$FONT = Join-Path $ROOT 'Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf'
if (-not (Test-Path $GEN)) { New-Item -ItemType Directory -Path $GEN | Out-Null }

$S       = 3                       # 出图倍率：贴图 3px = 屏幕 1px（与 battle-mode-v1 / lobby-ui-v1 同口径）
$PLATE_W = 360; $PLATE_H = 152     # 屏幕 px
$BTN_W   = 96;  $BTN_H   = 48

$LW_INK  = 9                       # 贴图 px：外墨边（屏幕 3）
$LW_GOLD = 3                       # 贴图 px：金细线（屏幕 1）
$INS_P   = 24                      # 贴图 px：底板金线内缩（屏幕 8）
$INS_B   = 18                      # 贴图 px：按钮底金线内缩（屏幕 6）
$R_P     = 30                      # 贴图 px：底板圆角
$R_B     = 22                      # 贴图 px：按钮底圆角
$A_P     = 168                     # 底板金线 α（与 entry / mode card 同档）
$A_B     = 150                     # 按钮底金线 α

function Get-MwFont([single]$px) {
  if ($script:MWFC -eq $null) {
    $script:MWFC = New-Object System.Drawing.Text.PrivateFontCollection
    $script:MWFC.AddFontFile($FONT)
  }
  return New-Object System.Drawing.Font($script:MWFC.Families[0], $px, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}
# 一行里跑两段不同颜色并整体居中（场景里是 TMP 富文本，这里只为预览）。
# 注意：**不能**把「(字符串, 笔刷) 数组的数组」当参数传 —— PowerShell 会把外层数组摊开成多个实参
# （本仓库踩过同一个坑：PointF[] 传进只收 PointF[] 的方法）。所以直接收两段。
function Put-Duo($g, [string]$s1, $br1, [string]$s2, $br2, [single]$cx, [single]$cy, [single]$px) {
  $f = Get-MwFont $px
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Near
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $w1 = 0.0; $w2 = 0.0
  if ($s1) { $w1 = $g.MeasureString($s1, $f, [int]::MaxValue, $sf).Width }
  if ($s2) { $w2 = $g.MeasureString($s2, $f, [int]::MaxValue, $sf).Width }
  $x = $cx - ($w1 + $w2) / 2.0
  if ($s1) { $g.DrawString($s1, $f, $br1, [System.Drawing.PointF]::new($x, $cy), $sf); $x += $w1 }
  if ($s2) { $g.DrawString($s2, $f, $br2, [System.Drawing.PointF]::new($x, $cy), $sf) }
  $sf.Dispose(); $f.Dispose()
}
function Put-Txt($g, [string]$s, [single]$x, [single]$y, [single]$px, [int]$a = 236) {
  $f = Get-MwFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $g.DrawString($s, $f, $br, $x, $y); $br.Dispose(); $f.Dispose()
}
function Put-TxtC($g, [string]$s, [single]$cx, [single]$cy, [single]$px, [int]$a = 236) {
  $f = Get-MwFont $px
  $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 240, 232, 210))
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
  $g.DrawString($s, $f, $br, [System.Drawing.PointF]::new($cx, $cy), $sf)
  $sf.Dispose(); $br.Dispose(); $f.Dispose()
}

# ── 板件：平底竖渐变 + 外墨边 + 等比内缩金细线（同 New-PlateBmp / mode card 的配方）──
function New-MwPlate([string]$out, [int]$w, [int]$h, [single]$rad, [single]$ins, [int]$aGold, [switch]$hover) {
  $cTop = $BAR_T; $cBot = $BAR_B; $cGold = $GOLD; $aWedge = 64; $aG = $aGold
  if ($hover) {
    $cTop = (Mix-Col $BAR_T $HILITE 0.12); $cBot = (Mix-Col $BAR_B $BAR_T 0.40)
    $cGold = $GOLD_L; $aWedge = 96; $aG = [Math]::Min(255, $aGold + 86)
  }
  $res = New-Bmp $w $h; $b = $res[0]; $g = $res[1]
  $oi = $LW_INK / 2.0
  $outer = New-RoundPath $oi $oi ($w - $LW_INK) ($h - $LW_INK) $rad
  $st = $g.Save(); $g.SetClip($outer)
  Fill-VGrad $g 0 0 $w $h $cTop $cBot 255 255
  $g.Restore($st)
  Add-Wedge $g $outer ([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(0, 0)),
    (New-Object System.Drawing.PointF(($w * 0.55), 0)),
    (New-Object System.Drawing.PointF(0, ($h * 0.52))))) $cTop $aWedge
  $pen = New-Object System.Drawing.Pen ((New-Col $INK 240)), $LW_INK
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $outer); $pen.Dispose()
  $ix = $oi + $ins
  $inner = New-RoundPath $ix $ix ($w - 2 * $ix) ($h - 2 * $ix) ([Math]::Max(2.0, $rad - $ins))
  $pen = New-Object System.Drawing.Pen ((New-Col $cGold $aG)), $LW_GOLD
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $inner); $pen.Dispose(); $inner.Dispose()
  $outer.Dispose()
  return (Save-Bmp $b $g $out)
}

# ── 预览：两种状态 1:1 + 子背景放大 + 版式标注 ──────────────────────────────
function New-MatchWaitSheet([string]$dir, [string]$out) {
  $CW = 1340; $CH = 780
  $b = New-Object System.Drawing.Bitmap($CW, $CH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $bs = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 9, 12, 18))
  $g.FillRectangle($bs, 0, 0, $CW, $CH); $bs.Dispose()

  Put-Txt $g '匹配等待小视窗 v1' 36 24 30
  Put-Txt $g '底板 360x152 屏幕 px（贴图 3x = 1080x456）· 子背景 96x48（288x144）· 锚屏幕顶中 · 深蓝黑石面 + 一条金细线' 36 66 19 176
  Put-Txt $g '上文金字「匹配中：」+ 白字计时「x：xx」；下为取消键（白字，悬停变金 + 子背景提亮）' 36 94 19 176
  Put-Txt $g '找到对手后：取消整组隐藏，文字变「已找到对手！」（居中）' 36 122 19 176

  $goldBr = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 200, 164, 74))
  $whiteBr = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))

  # 左：匹配中
  $px = 60.0; $py = 160.0
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'MatchWait_Plate.png'))
  $g.DrawImage($im, $px, $py, $PLATE_W, $PLATE_H); $im.Dispose()
  Put-Duo $g '匹配中：' $goldBr '0：12' $whiteBr ($px + $PLATE_W / 2.0) ($py + 46.0) 30
  $bg = [System.Drawing.Image]::FromFile((Join-Path $dir 'MatchWait_CancelBg.png'))
  $bx = $px + ($PLATE_W - $BTN_W) / 2.0; $by = $py + 106.0 - $BTN_H / 2.0
  $g.DrawImage($bg, $bx, $by, $BTN_W, $BTN_H); $bg.Dispose()
  Put-TxtC $g '取消' ($px + $PLATE_W / 2.0) ($py + 106.0) 28 255
  Put-Txt $g '① 匹配中（常态）' $px ($py + $PLATE_H + 14) 20 210

  # 右：已找到对手
  $px2 = 480.0
  $im = [System.Drawing.Image]::FromFile((Join-Path $dir 'MatchWait_Plate.png'))
  $g.DrawImage($im, $px2, $py, $PLATE_W, $PLATE_H); $im.Dispose()
  Put-Duo $g '已找到对手！' $goldBr '' $null ($px2 + $PLATE_W / 2.0) ($py + $PLATE_H / 2.0) 32
  Put-Txt $g '② 已找到对手（取消整组隐藏）' $px2 ($py + $PLATE_H + 14) 20 210

  # 右栏：子背景放大 2x（常态 / 悬停）
  $ex = 1090.0
  Put-TxtC $g '取消子背景 · 放大 2x' $ex 150 20 210
  foreach ($p in @(@('MatchWait_CancelBg.png', 210, '常态'), @('MatchWait_CancelBgHover.png', 330, '悬停'))) {
    $im = [System.Drawing.Image]::FromFile((Join-Path $dir $p[0]))
    $g.DrawImage($im, ($ex - $BTN_W), $p[1], ($BTN_W * 2), ($BTN_H * 2)); $im.Dispose()
    Put-TxtC $g $p[2] $ex ($p[1] - 30) 19 210
    Put-TxtC $g '取消' $ex ($p[1] + $BTN_H) 40 255
  }

  # 版式标注（左侧留白 / 字高 / 间隔 / 按钮）
  $anx = 60.0
  Put-Txt $g '版式：上留白 26 · 文字行 40 · 间隔 18 · 按钮 96x48 · 下留白 20' $anx ($py + $PLATE_H + 54) 19 176
  Put-Txt $g '金细线：底板内缩 24 贴图 px（屏幕 8）· α168 → 悬停不换底板，只换子背景' $anx ($py + $PLATE_H + 82) 19 176
  Put-Txt $g '尺寸非 2 的幂 —— TextureImportSettingsGuard 的 NoNpotScaleFolders 已含 match-wait-v1，禁止 Unity 缩放' $anx ($py + $PLATE_H + 110) 19 176
  $g.Dispose()
  $b.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  return $out
}

# ── 主流程 ───────────────────────────────────────────────────
$made = @()
$made += (New-MwPlate (Join-Path $GEN 'MatchWait_Plate.png')         ($PLATE_W * $S) ($PLATE_H * $S) $R_P $INS_P $A_P)
$made += (New-MwPlate (Join-Path $GEN 'MatchWait_CancelBg.png')      ($BTN_W * $S)   ($BTN_H * $S)   $R_B $INS_B $A_B)
$made += (New-MwPlate (Join-Path $GEN 'MatchWait_CancelBgHover.png') ($BTN_W * $S)   ($BTN_H * $S)   $R_B $INS_B $A_B -hover)
$sheet = New-MatchWaitSheet $GEN (Join-Path $PREV 'match-wait-v1.png')
$made | ForEach-Object { "sprite : $_" }
"sheet  : $sheet"