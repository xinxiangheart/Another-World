# 卡槽底板 v8 预览 —— 实机截图反解底板 + 前向叠新素材（1:1 屏幕尺度）
# 自动识别「被卡牌占用的格子」并原样保留，不参与重绘。
$ErrorActionPreference = 'Stop'
foreach ($asm in 'System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll') {
    $p = Join-Path $PSHOME $asm
    if (Test-Path $p) { Add-Type -Path $p }
}
$ROOT  = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$SHOT  = "$env:TEMP\codex-clipboard-d5e39102-5e5c-411a-bdd3-074eb056f423.png"
$GEN   = Join-Path $ROOT 'Assets\_Game\Art\Sprites\Generated\slot-design-v1'
$BOARD = Join-Path $ROOT 'Assets\_Game\Art\Sprites\Board'
$CACHE = "$env:TEMP\v8-cache"; [void][System.IO.Directory]::CreateDirectory($CACHE)
if (-not (Test-Path $SHOT)) { throw "截图缺失：$SHOT" }

$SPW = 540; $SPH = 960
$COLS = @(434.5, 685.5, 936.5); $ROWS = @(136.0, 327.0, 542.0, 734.0)
$GW = 104; $GH = 185

function S2L([double]$c) { if ($c -le 0.04045) { $c / 12.92 } else { [Math]::Pow(($c + 0.055) / 1.055, 2.4) } }
$N = 4096
$LUT = New-Object 'byte[]' $N
for ($i = 0; $i -lt $N; $i++) {
    $c = $i / [double]($N - 1)
    $s = if ($c -le 0.0031308) { $c * 12.92 } else { 1.055 * [Math]::Pow($c, 1.0 / 2.4) - 0.055 }
    $v = [int][Math]::Round($s * 255.0); if ($v -gt 255) { $v = 255 }; if ($v -lt 0) { $v = 0 }
    $LUT[$i] = [byte]$v
}
function L2S([double]$v) { if ($v -lt 0) { $v = 0.0 }; if ($v -gt 1.0) { $v = 1.0 }; $i = [int]($v * ($N - 1) + 0.5); if ($i -gt $N - 1) { $i = $N - 1 }; return $LUT[$i] }

$sb = [System.Drawing.Bitmap]::FromFile($SHOT)
$IW = $sb.Width; $IH = $sb.Height
$bd = $sb.LockBits([System.Drawing.Rectangle]::new(0, 0, $IW, $IH), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$baseBuf = New-Object 'byte[]' ($bd.Stride * $IH)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $baseBuf, 0, $baseBuf.Length)
$STRIDE = $bd.Stride
$sb.UnlockBits($bd); $sb.Dispose()
"截图 $IW x $IH"

# 背景底板场：用卡位左右两侧的实测值横向插值（与 v6/v7 预览同式）
function New-BaseField { param([int]$cx, [int]$cy)
    $Bleft = New-Object 'double[]' 3; $Bright = New-Object 'double[]' 3; $nl = 0; $nr = 0
    for ($k = 3; $k -le 6; $k++) {
        $xl = $cx - $k; $xr = $cx + $GW - 1 + $k
        if ($xl -lt 0) { $xl = 0 }; if ($xr -gt $IW - 1) { $xr = $IW - 1 }
        for ($y = $cy + 42; $y -le $cy + 142; $y++) {
            $o = $y * $STRIDE + $xl * 4
            $Bleft[0] += (S2L ($baseBuf[$o + 2] / 255.0)); $Bleft[1] += (S2L ($baseBuf[$o + 1] / 255.0)); $Bleft[2] += (S2L ($baseBuf[$o] / 255.0)); $nl++
            $o = $y * $STRIDE + $xr * 4
            $Bright[0] += (S2L ($baseBuf[$o + 2] / 255.0)); $Bright[1] += (S2L ($baseBuf[$o + 1] / 255.0)); $Bright[2] += (S2L ($baseBuf[$o] / 255.0)); $nr++
        }
    }
    for ($c = 0; $c -lt 3; $c++) { $Bleft[$c] /= $nl; $Bright[$c] /= $nr }
    $B = New-Object 'double[]' ($GW * $GH * 3)
    for ($py = 0; $py -lt $GH; $py++) {
        for ($px = 0; $px -lt $GW; $px++) {
            $u = ($px + 0.5) / [double]$GW
            $i = ($py * $GW + $px) * 3
            for ($c = 0; $c -lt 3; $c++) { $B[$i + $c] = $Bleft[$c] * (1 - $u) + $Bright[$c] * $u }
        }
    }
    , $B
}

# 素材采样：540x960 → 104x185 网格（a/r/g/b 四通道，双线性）
function Get-Sample { param([string]$path)
    $b = [System.Drawing.Bitmap]::FromFile($path)
    $bdd = $b.LockBits([System.Drawing.Rectangle]::new(0, 0, $SPW, $SPH), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rawPix = New-Object 'byte[]' ($bdd.Stride * $SPH)
    [System.Runtime.InteropServices.Marshal]::Copy($bdd.Scan0, $rawPix, 0, $rawPix.Length)
    $st = $bdd.Stride
    $b.UnlockBits($bdd); $b.Dispose()
    $out = New-Object 'double[]' ($GW * $GH * 4)
    for ($py = 0; $py -lt $GH; $py++) {
        $sy = (($py + 0.5) / [double]$GH * $SPH) - 0.5
        $iy = [int][Math]::Floor($sy); $fy = $sy - $iy
        if ($iy -lt 0) { $iy = 0 }; if ($iy -gt $SPH - 2) { $iy = $SPH - 2 }
        for ($px = 0; $px -lt $GW; $px++) {
            $sx = (($px + 0.5) / [double]$GW * $SPW) - 0.5
            $ix = [int][Math]::Floor($sx); $fx = $sx - $ix
            if ($ix -lt 0) { $ix = 0 }; if ($ix -gt $SPW - 2) { $ix = $SPW - 2 }
            $o00 = $iy * $st + $ix * 4; $o10 = $o00 + 4; $o01 = $o00 + $st; $o11 = $o01 + 4
            $res = New-Object 'double[]' 4
            # BGRA
            $res[3] = $rawPix[$o00 + 3] / 255.0; $res[2] = $rawPix[$o00 + 2] / 255.0; $res[1] = $rawPix[$o00 + 1] / 255.0; $res[0] = $rawPix[$o00] / 255.0
            $q3 = $rawPix[$o10 + 3] / 255.0; $q2 = $rawPix[$o10 + 2] / 255.0; $q1 = $rawPix[$o10 + 1] / 255.0; $q0 = $rawPix[$o10] / 255.0
            $w3 = $rawPix[$o01 + 3] / 255.0; $w2 = $rawPix[$o01 + 2] / 255.0; $w1 = $rawPix[$o01 + 1] / 255.0; $w0 = $rawPix[$o01] / 255.0
            $e3 = $rawPix[$o11 + 3] / 255.0; $e2 = $rawPix[$o11 + 2] / 255.0; $e1 = $rawPix[$o11 + 1] / 255.0; $e0 = $rawPix[$o11] / 255.0
            $top3 = $res[3] + ($q3 - $res[3]) * $fx; $bot3 = $w3 + ($e3 - $w3) * $fx
            $top2 = $res[2] + ($q2 - $res[2]) * $fx; $bot2 = $w2 + ($e2 - $w2) * $fx
            $top1 = $res[1] + ($q1 - $res[1]) * $fx; $bot1 = $w1 + ($e1 - $w1) * $fx
            $top0 = $res[0] + ($q0 - $res[0]) * $fx; $bot0 = $w0 + ($e0 - $w0) * $fx
            $i = ($py * $GW + $px) * 4
            $out[$i]     = $top3 + ($bot3 - $top3) * $fy
            $out[$i + 1] = $top2 + ($bot2 - $top2) * $fy   # R
            $out[$i + 2] = $top1 + ($bot1 - $top1) * $fy   # G
            $out[$i + 3] = $top0 + ($bot0 - $top0) * $fy   # B
        }
    }
    , $out
}

$variants = @(
    @{ tag = '现状 v7（已装）';      plate = "$BOARD\SlotPlate.png";                     edge = "$BOARD\SlotEdge.png";                  gold = $false },
    @{ tag = 'v8 a 细线';            plate = "$GEN\Plate_v8_a.png";                       edge = "$BOARD\SlotEdge.png";                  gold = $true },
    @{ tag = 'v8 b 标准 ← 推荐';     plate = "$GEN\Plate_v8_b.png";                       edge = "$BOARD\SlotEdge.png";                  gold = $true },
    @{ tag = 'v8 c 深槽';            plate = "$GEN\Plate_v8_c.png";                       edge = "$BOARD\SlotEdge.png";                  gold = $true },
    @{ tag = 'v8 b 白线版';          plate = "$GEN\Plate_v8_b_white.png";                 edge = "$BOARD\SlotEdge.png";                  gold = $true }
)
$pl = @(); $ed = @()
foreach ($v in $variants) { $pl += , (Get-Sample $v.plate); $ed += , (Get-Sample $v.edge) }
"素材采样完毕（$($variants.Count) 版）"

# 每格底板场缓存一次
$cells = @()
$bfield = @()
for ($rr = 0; $rr -lt 4; $rr++) {
    for ($cc = 0; $cc -lt 3; $cc++) {
        $cx = [int]$COLS[$cc]; $cy = [int]$ROWS[$rr]
        $cells += , @($cc, $rr, $cx, $cy)
        $bfield += , (New-BaseField $cx $cy)
    }
}
"底板场缓存完毕（12 格）"

# 一格的合成：a1 = 素材A*tp；a2 = 线层A*te；C1 = 素材RGB*状态色；C2 = 状态色
function Render-Cell { param($B, $plate, $edge, [int]$cx, [int]$cy, [double]$tp, [double]$te, [double[]]$stateRGB, [byte[]]$outBuf, [bool]$dry)
    for ($py = 0; $py -lt $GH; $py++) {
        $Y = $cy + $py
        if ($Y -lt 0 -or $Y -ge $IH) { continue }
        for ($px = 0; $px -lt $GW; $px++) {
            $X = $cx + $px
            if ($X -lt 0 -or $X -ge $IW) { continue }
            $i4 = ($py * $GW + $px) * 4
            $a1 = $plate[$i4] * $tp
            $a2 = $edge[$i4] * $te
            $j = ($py * $GW + $px) * 3
            $c1r = $plate[$i4 + 1] * $stateRGB[0]; $c1g = $plate[$i4 + 2] * $stateRGB[1]; $c1b = $plate[$i4 + 3] * $stateRGB[2]
            $c2r = $edge[$i4 + 1] * $stateRGB[0];  $c2g = $edge[$i4 + 2] * $stateRGB[1];  $c2b = $edge[$i4 + 3] * $stateRGB[2]
            $k1 = 1.0 - $a1; $k2 = 1.0 - $a2
            $nr = ($B[$j] * $k1 + $c1r * $a1) * $k2 + $c2r * $a2
            $ng = ($B[$j + 1] * $k1 + $c1g * $a1) * $k2 + $c2g * $a2
            $nb = ($B[$j + 2] * $k1 + $c1b * $a1) * $k2 + $c2b * $a2
            if (-not $dry) {
                $o = $Y * $STRIDE + $X * 4
                $outBuf[$o] = L2S $nb; $outBuf[$o + 1] = L2S $ng; $outBuf[$o + 2] = L2S $nr; $outBuf[$o + 3] = 255
            } else {
                # 自检：只回传预测值（用 outBuf 前 3 字节塞不上，改走全局数组）
                $script:dR[$j] = $nr; $script:dG[$j] = $ng; $script:dB[$j] = $nb
            }
        }
    }
}

function Save-Bytes { param([byte[]]$buf, [string]$path)
    $bmp = New-Object System.Drawing.Bitmap($IW, $IH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $b2 = $bmp.LockBits([System.Drawing.Rectangle]::new(0, 0, $IW, $IH), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $b2.Scan0, $buf.Length)
    $bmp.UnlockBits($b2)
    $tmp = Join-Path $CACHE ([System.IO.Path]::GetFileName($path))
    $bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    [System.IO.File]::Copy($tmp, $path, $true)
}

# 自检：现状素材理论上应与截图在空格上一致
$script:dR = New-Object 'double[]' ($GW * $GH * 3)
$script:dG = New-Object 'double[]' ($GW * $GH * 3)
$script:dB = New-Object 'double[]' ($GW * $GH * 3)
$occupied = @{}
"--- 自检：空格应吻合，有卡片的格子会被标出 ---"
for ($ci = 0; $ci -lt 12; $ci++) {
    $cx = $cells[$ci][2]; $cy = $cells[$ci][3]
    Render-Cell $bfield[$ci] $pl[0] $ed[0] $cx $cy 0.10 0.0 @(1.0, 1.0, 1.0) $null $true
    $sum = 0.0; $cnt = 0
    for ($py = 10; $py -lt $GH - 10; $py += 3) {
        for ($px = 8; $px -lt $GW - 8; $px += 3) {
            $j = ($py * $GW + $px) * 3
            $o = ($cy + $py) * $STRIDE + ($cx + $px) * 4
            $sum += [Math]::Abs($script:dR[$j] - (S2L ($baseBuf[$o + 2] / 255.0)))
            $sum += [Math]::Abs($script:dG[$j] - (S2L ($baseBuf[$o + 1] / 255.0)))
            $sum += [Math]::Abs($script:dB[$j] - (S2L ($baseBuf[$o] / 255.0)))
            $cnt++
        }
    }
    $mean = $sum / $cnt
    $flag = ''
    if ($mean -gt 0.02) { $flag = '  ← 被占用，保留原图'; $occupied[$ci] = $true }
    "  格 {0,2} (col{1} row{2}) 平均偏差 {3:N4}{4}" -f $ci, $cells[$ci][0], $cells[$ci][1], $mean, $flag
}

foreach ($vi in 0..($variants.Count - 1)) {
    foreach ($st in @('rest', 'hl')) {
        $tp = 0.10; $te = 0.0; $c = @(1.0, 1.0, 1.0)
        if ($st -eq 'hl') { $tp = 1.0; $te = 1.0; $c = @(1.0, 0.92, 0.015686) }
        $out = New-Object 'byte[]' ($STRIDE * $IH)
        [Array]::Copy($baseBuf, $out, $out.Length)
        for ($ci = 0; $ci -lt 12; $ci++) {
            if ($occupied.ContainsKey($ci)) { continue }
            Render-Cell $bfield[$ci] $pl[$vi] $ed[$vi] $cells[$ci][2] $cells[$ci][3] $tp $te $c $out $false
        }
        $f = Join-Path $CACHE ("v{0}-{1}.png" -f $vi, $st)
        Save-Bytes $out $f
        "  渲染 [$vi] $($variants[$vi].tag)  $st  -> $f"
    }
}
"OK"
