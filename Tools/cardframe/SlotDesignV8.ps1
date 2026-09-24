# 卡槽底板 v8「凹槽」—— 2026-09-24
#
# 目标：把 12 个卡位从「一张淡白面板」做成棋盘语言里的【凹槽】：
#   ① 最外一圈细金线（跟战场金框同色，只有 2.8 sprite px ≈ 0.6 屏像素，不违「收细」）
#   ② 金线内侧一道暗槽（把金线垫起来，凹槽才有厚度）
#   ③ 槽内上暗下亮（光从上下来），做出「陷进去」的体感
#
# 约束（与 v6/v7 同）：轮廓照抄原占位图（圆角 r=61 sprite px），尺寸 540x960 不变，
#   .meta 不动 → guid 不变 → prefab / 场景零改动。
#   RGB 只在金线上是暖色，槽内一律接近纯白：运行时 slotImage.color 是状态色（黄/绿/紫/黑/蓝），
#   金线乘上状态色会跟着变色（黑封锁时线自然消失），槽内乘上状态色仍是该状态色。
#
# 输出：Assets/_Game/Art/Sprites/Generated/slot-design-v1/Plate_v8_{a,b,c,b_white}.png
$ErrorActionPreference = 'Stop'
foreach ($asm in 'System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll') {
    $p = Join-Path $PSHOME $asm
    if (Test-Path $p) { Add-Type -Path $p }
}

$REF   = Join-Path $env:TEMP 'slot-backup-20260923-185945\SlotPlate.OLD.png'
$OUTD  = Join-Path $PSScriptRoot '..\..\Assets\_Game\Art\Sprites\Generated\slot-design-v1'
if (-not (Test-Path $REF)) { throw "轮廓参考图缺失：$REF" }
[void][System.IO.Directory]::CreateDirectory((Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path + '\Assets\_Game\Art\Sprites\Generated\slot-design-v1')

# ---------- 读轮廓（原占位图：圆角矩形 alpha 184）----------
$bmp = [System.Drawing.Bitmap]::FromFile($REF)
$W = $bmp.Width; $H = $bmp.Height
$bd = $bmp.LockBits([System.Drawing.Rectangle]::new(0, 0, $W, $H), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$raw = New-Object 'byte[]' ($bd.Stride * $H)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $raw, 0, $raw.Length)
$STRIDE = $bd.Stride
$bmp.UnlockBits($bd); $bmp.Dispose()

$msk = New-Object 'bool[]' ($W * $H)
$top = New-Object 'int[]' $W; $bot = New-Object 'int[]' $W
$lef = New-Object 'int[]' $H; $rig = New-Object 'int[]' $H
for ($x = 0; $x -lt $W; $x++) { $top[$x] = -1; $bot[$x] = -1 }
for ($y = 0; $y -lt $H; $y++) { $lef[$y] = -1; $rig[$y] = -1 }
for ($y = 0; $y -lt $H; $y++) {
    $row = $y * $STRIDE
    for ($x = 0; $x -lt $W; $x++) {
        if ($raw[$row + $x * 4 + 3] -gt 100) {
            $msk[$y * $W + $x] = $true
            if ($top[$x] -lt 0) { $top[$x] = $y }
            $bot[$x] = $y
            if ($lef[$y] -lt 0) { $lef[$y] = $x }
            $rig[$y] = $x
        }
    }
}
"mask: ${W}x${H}  corner r ≈ " + (($top[0] - $top[61]) | ForEach-Object { $_ }) + " px"

# ---------- 预计算：到四边距离 / 分带 / 四条渐变权重 ----------
$dmin = New-Object 'int[]' ($W * $H)
$band = New-Object 'byte[]' ($W * $H)      # 0 = 金线带, 1 = 暗槽带, 2 = 槽内
$rt = New-Object 'double[]' ($W * $H); $rl = New-Object 'double[]' ($W * $H)
$rb = New-Object 'double[]' ($W * $H); $rr = New-Object 'double[]' ($W * $H)
for ($y = 0; $y -lt $H; $y++) {
    $lx = $lef[$y]; $rx = $rig[$y]
    for ($x = 0; $x -lt $W; $x++) {
        $i = $y * $W + $x
        if (-not $msk[$i]) { $dmin[$i] = -1; $band[$i] = 255; continue }
        $dT = $y - $top[$x]; $dB = $bot[$x] - $y
        $dL = $x - $lx; $dR = $rx - $x
        $dm = $dT
        if ($dB -lt $dm) { $dm = $dB }
        if ($dL -lt $dm) { $dm = $dL }
        if ($dR -lt $dm) { $dm = $dR }
        $dmin[$i] = $dm
        $rt[$i] = $dT; $rl[$i] = $dL; $rb[$i] = $dB; $rr[$i] = $dR
    }
}
"distances done"

function Clamp01([double]$v) { if ($v -lt 0) { return 0.0 }; if ($v -gt 1) { return 1.0 }; return $v }
function Ramp([double]$d, [double]$d0, [double]$span) { return (Clamp01 (1.0 - ($d - $d0) / $span)) }

# ---------- 生成一张底板（alpha + RGB）----------
function New-Plate {
    param([double]$Rim, [double]$RimA, [double]$Groove, [double]$GrooveA,
          [double]$Floor, [double]$BT, [double]$BTA, [double]$BL, [double]$BLA,
          [double]$BB, [double]$BBA, [double]$BR, [double]$BRA,
          [double[]]$RimRGB, [double[]]$GrooveRGB, [double[]]$FloorRGB)
    $a = New-Object 'double[]' ($W * $H)
    $r = New-Object 'double[]' ($W * $H); $g = New-Object 'double[]' ($W * $H); $chB = New-Object 'double[]' ($W * $H)
    $grooveEnd = $Rim + $Groove
    for ($i = 0; $i -lt $a.Length; $i++) {
        $d = $dmin[$i]
        if ($d -lt 0) { $a[$i] = 0.0; continue }
        if ($d -lt $Rim) {
            $a[$i] = $RimA; $r[$i] = $RimRGB[0]; $g[$i] = $RimRGB[1]; $chB[$i] = $RimRGB[2]
        } elseif ($d -lt $grooveEnd) {
            $a[$i] = $GrooveA; $r[$i] = $GrooveRGB[0]; $g[$i] = $GrooveRGB[1]; $chB[$i] = $GrooveRGB[2]
        } else {
            $v = $Floor
            $v -= $BTA * (Ramp $rt[$i] $grooveEnd $BT)
            $v -= $BLA * (Ramp $rl[$i] $grooveEnd $BL)
            $v += $BBA * (Ramp $rb[$i] $grooveEnd $BB)
            $v += $BRA * (Ramp $rr[$i] $grooveEnd $BR)
            $a[$i] = (Clamp01 $v)
            $r[$i] = $FloorRGB[0]; $g[$i] = $FloorRGB[1]; $chB[$i] = $FloorRGB[2]
        }
    }
    @{ A = $a; R = $r; G = $g; B = $chB }
}

function Save-Plate {
    param($P, [string]$Out)
    $bmp2 = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bd2 = $bmp2.LockBits([System.Drawing.Rectangle]::new(0, 0, $W, $H), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $dst = New-Object 'byte[]' ($bd2.Stride * $H)
    for ($y = 0; $y -lt $H; $y++) {
        $o = $y * $bd2.Stride
        $so = $y * $W
        for ($x = 0; $x -lt $W; $x++) {
            $i = $so + $x
            $av = [int][Math]::Round(255.0 * $P.A[$i]); if ($av -lt 0) { $av = 0 }; if ($av -gt 255) { $av = 255 }
            $rv = [int][Math]::Round($P.R[$i]); if ($rv -lt 0) { $rv = 0 }; if ($rv -gt 255) { $rv = 255 }
            $gv = [int][Math]::Round($P.G[$i]); if ($gv -lt 0) { $gv = 0 }; if ($gv -gt 255) { $gv = 255 }
            $bv = [int][Math]::Round($P.B[$i]); if ($bv -lt 0) { $bv = 0 }; if ($bv -gt 255) { $bv = 255 }
            $dst[$o] = [byte]$bv; $dst[$o + 1] = [byte]$gv; $dst[$o + 2] = [byte]$rv; $dst[$o + 3] = [byte]$av
            $o += 4
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($dst, 0, $bd2.Scan0, $dst.Length)
    $bmp2.UnlockBits($bd2)
    $bmp2.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp2.Dispose()
}

$GOLD  = @(214, 180, 104)
$COOL  = @(150, 172, 200)
$WHITE = @(240, 246, 255)

$variants = @(
    @{ f = 'Plate_v8_a'; rim = 2.2; rimA = 0.80; gro = 1.8; groA = 0.04; floor = 0.15; BT = 28; BTA = 0.15; BL = 16; BLA = 0.05; BB = 20; BBA = 0.20; BR = 12; BRA = 0.08; rgb = $GOLD;  note = '细线：金线最细最淡' },
    @{ f = 'Plate_v8_b'; rim = 2.8; rimA = 0.95; gro = 2.2; groA = 0.05; floor = 0.18; BT = 34; BTA = 0.18; BL = 20; BLA = 0.07; BB = 26; BBA = 0.26; BR = 16; BRA = 0.10; rgb = $GOLD;  note = '标准（推荐）：金线清晰、凹槽有厚度' },
    @{ f = 'Plate_v8_c'; rim = 3.4; rimA = 1.00; gro = 2.6; groA = 0.06; floor = 0.20; BT = 40; BTA = 0.21; BL = 24; BLA = 0.09; BB = 30; BBA = 0.30; BR = 18; BRA = 0.12; rgb = $GOLD;  note = '深槽：金线最粗最亮、阴影最深' },
    @{ f = 'Plate_v8_b_white'; rim = 2.8; rimA = 0.95; gro = 2.2; groA = 0.05; floor = 0.18; BT = 34; BTA = 0.18; BL = 20; BLA = 0.07; BB = 26; BBA = 0.26; BR = 16; BRA = 0.10; rgb = @(255, 255, 255); note = '标准但改为白线（不要金色时用这版）' }
)

foreach ($v in $variants) {
    $P = New-Plate -Rim $v.rim -RimA $v.rimA -Groove $v.gro -GrooveA $v.groA -Floor $v.floor `
        -BT $v.BT -BTA $v.BTA -BL $v.BL -BLA $v.BLA -BB $v.BB -BBA $v.BBA -BR $v.BR -BRA $v.BRA `
        -RimRGB $v.rgb -GrooveRGB $COOL -FloorRGB $WHITE
    $out = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\Assets\_Game\Art\Sprites\Generated\slot-design-v1')).Path ($v.f + '.png')
    Save-Plate $P $out
    # 实测几个位置
    $cx = [int]($W / 2)
    $iRim = (($top[$cx] + 1) * $W + $cx)
    $iGro = (($top[$cx] + [int]($v.rim + 1)) * $W + $cx)
    $iTop = (($top[$cx] + [int]($v.rim + $v.gro + 2)) * $W + $cx)
    $iMid = ([int]($H / 2) * $W + $cx)
    $iBot = (($bot[$cx] - 1) * $W + $cx)
    "{0,-18} 金线a={1:N2}({2},{3},{4})  暗槽a={5:N2}  上内影a={6:N2}  槽心a={7:N2}  下内亮a={8:N2}" -f `
        $v.f, $P.A[$iRim], $P.R[$iRim], $P.G[$iRim], $P.B[$iRim], $P.A[$iGro], $P.A[$iTop], $P.A[$iMid], $P.A[$iBot]
}
"--- 不透明覆盖：金线带 $(($dmin | Where-Object { $_ -ge 0 -and $_ -lt 2.8 }).Count) px"
$inMask = 0; foreach ($d in $dmin) { if ($d -ge 0) { $inMask++ } }
"mask px = $inMask"
