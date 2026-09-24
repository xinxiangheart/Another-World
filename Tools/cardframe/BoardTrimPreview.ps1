# 金色贴边明灭预览（2026-09-24）
# 复用 BoardLayersPreview.ps1 里那套 C# 分层合成器（线性空间 + Glow 加色 + 按渲染队列叠加），
# 在【一轮的 8 个时刻】各出一帧。
#
# 明灭层的 _Period / _Bright / _Rise / _Fall / _Floor / _Peak 一律**从材质 .mat 里读**，
# 不写死 —— 改了材质再跑一次，图就跟着变。
#
# 当前两层参数不同：
#   内圈跑道框 Board_Surface   _Floor = 0     → 暗的时候整圈消失
#   外框角饰   Board_Ornament  _Floor = 0.25  → 暗的时候还剩四分之一
# 所以每层的 k 要各算各的。
#
# 注意：这是把 shader 的「alpha × k」近似成「rgb × k」来合的（贴边坐落在近黑的板面上，
#       两者在视觉上等价）。微尘层在预览里是静的 —— 它自己那套闪烁见 board-motes-twinkle.png。
$ErrorActionPreference='Stop'
foreach($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')){
  try{ Add-Type -Path (Join-Path $PSHOME $n) }catch{}
}
$root = 'C:\Users\22589\Documents\GitHub\Another-World'
$laydir = Join-Path $root 'Assets\_Game\Art\Sprites\Generated\board-layers-v2'
$matdir = Join-Path $root 'Assets\_Game\Art\Materials\Board'
$board  = Join-Path $root 'Assets\_Game\Art\Sprites\Board\Board.png'
$outdir = Join-Path $root 'Tools\cardframe\preview'
$scratch= Join-Path $env:TEMP 'board-trim-preview'
if(-not (Test-Path $scratch)){ New-Item -ItemType Directory -Path $scratch -Force | Out-Null }

# ---- 分层合成器 ----
if(-not ('BoardLayerMix' -as [type])){
  $src = [System.IO.File]::ReadAllText((Join-Path $root 'Tools\cardframe\BoardLayersPreview.ps1'))
  $b64 = [regex]::Match($src,"FromBase64String\('([A-Za-z0-9+/=]+)'\)")
  if(-not $b64.Success){ throw '取不到分层合成器' }
  $cs = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64.Groups[1].Value))
  $refs = @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll') | ForEach-Object { Join-Path $PSHOME $_ }
  Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs
}

# ---- 与 shader 完全一致的曲线 ----
function Clamp01([double]$v){ if($v -lt 0){0.0} elseif($v -gt 1){1.0} else {$v} }
function SStep([double]$a,[double]$b,[double]$x){
  if($b -le $a){ if($x -lt $a){0.0} else {1.0} }
  else { $u = Clamp01 (($x-$a)/($b-$a)); $u*$u*(3.0-2.0*$u) }
}
function Wof([double]$t,[double]$br,[double]$ri,[double]$fa){
  $ri=[Math]::Min([Math]::Max($ri,0.002),$br)
  $fa=[Math]::Min([Math]::Max($fa,0.002),$br)
  $sum=$ri+$fa
  if($sum -gt $br){ $sc=$br/$sum; $ri=$ri*$sc; $fa=$fa*$sc }
  (SStep 0.0 $ri $t) * (1.0 - (SStep ([Math]::Max($ri,$br-$fa)) $br $t))
}
function Get-Prop([string]$txt,[string]$key){
  $mm=[regex]::Match($txt,('- ' + [regex]::Escape($key) + ': ([\d.\-]+)'))
  if(-not $mm.Success){ throw "材质里读不到 $key" }
  [double]$mm.Groups[1].Value
}

# ---- 逐层乘数（照材质 _Color），并读每个明灭层自己的曲线参数 ----
$names = [BoardLayerMix]::Order
$mulBase = New-Object 'float[]' 8
for($i=0;$i -lt 8;$i++){
  $txt = [System.IO.File]::ReadAllText((Join-Path $matdir ($names[$i]+'.mat')))
  $mm = [regex]::Match($txt,'- _Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
  if(-not $mm.Success){ throw "读不到 $($names[$i]).mat 的 _Color" }
  $mulBase[$i] = [float]((([double]$mm.Groups[1].Value + [double]$mm.Groups[2].Value + [double]$mm.Groups[3].Value)/3.0))
}
$PULSE = New-Object 'System.Collections.Generic.List[object]'
foreach($ln in @('Board_Surface','Board_Ornament')){
  $ix = [Array]::IndexOf($names,$ln)
  if($ix -lt 0){ throw "层名找不到: $ln" }
  $txt = [System.IO.File]::ReadAllText((Join-Path $matdir ($ln+'.mat')))
  $pr = [PSCustomObject]@{
    ix     = $ix
    name   = $ln
    short  = $(if($ln -eq 'Board_Surface'){ '内圈跑道' } else { '外框角饰' })
    period = (Get-Prop $txt '_Period')
    bright = (Get-Prop $txt '_Bright')
    rise   = (Get-Prop $txt '_Rise')
    fall   = (Get-Prop $txt '_Fall')
    floor  = (Get-Prop $txt '_Floor')
    peak   = (Get-Prop $txt '_Peak')
  }
  $PULSE.Add($pr)
  "{0,-16} 索引 {1}  乘数 {2}  周期 {3}s  亮 {4}  上升 {5}  下降 {6}  暗 {7}  峰 {8}" -f $ln,$ix,$mulBase[$ix],$pr.period,$pr.bright,$pr.rise,$pr.fall,$pr.floor,$pr.peak
}
$Period = $PULSE[0].period
foreach($p in $PULSE){ if([Math]::Abs($p.period - $Period) -gt 1e-6){ throw '两层的 _Period 不一致，预览假定周期相同' } }

# ---- 8 个取样时刻（覆盖：暗 → 慢上升 → 峰 → 下降 → 暗）----
$T = @(0.00,0.06,0.13,0.20,0.26,0.33,0.45,0.70)

$W=1024; $H=576
$frames = New-Object 'System.Collections.Generic.List[object]'
for($i=0;$i -lt $T.Count;$i++){
  $mul = $mulBase.Clone()
  $ks = New-Object 'System.Collections.Generic.List[object]'
  foreach($p in $PULSE){
    $hump = Wof $T[$i] $p.bright $p.rise $p.fall
    $k = $p.floor + ($p.peak - $p.floor) * $hump
    $mul[$p.ix] = [float]($mulBase[$p.ix] * $k)
    $ks.Add([PSCustomObject]@{ short=$p.short; k=$k })
  }
  $tag = "-f$i"
  [void][BoardLayerMix]::Run($laydir,$board,$scratch,$W,$H,$mul,$tag)
  $frames.Add([PSCustomObject]@{ t=$T[$i]; ks=$ks; png=(Join-Path $scratch "composite$tag.png") })
  $desc = ($ks | ForEach-Object { "$($_.short) $([Math]::Round($_.k,3))" }) -join '   '
  "frame $i  t=$($T[$i])  第 $([Math]::Round($T[$i]*$Period,2))s   $desc"
}

# ---- 拼图 ----
$cols=4; $cellW=470; $cellH=264; $gap=12; $lab=22
$gridW = $gap + $cols*($cellW+$gap)
$gridH = $gap + 2*($lab+$cellH+$gap)
$curveH= 210
$Wtot = $gridW
$Htot = $gridH + $curveH
$sheet=[System.Drawing.Bitmap]::new($Wtot,$Htot,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(16,18,24))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$fLab=[System.Drawing.Font]::new('Microsoft YaHei',11)
$fBig=[System.Drawing.Font]::new('Microsoft YaHei',13,[System.Drawing.FontStyle]::Bold)
$penCell=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(64,68,80),1)
$penTick=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(120,120,140),1)
for($i=0;$i -lt $frames.Count;$i++){
  $r=[Math]::Floor($i/$cols); $c=$i%$cols
  $x=$gap+$c*($cellW+$gap); $y=$gap+$r*($lab+$cellH+$gap)
  $img=[System.Drawing.Image]::FromFile($frames[$i].png)
  $g.DrawImage($img,$x,$y+$lab,$cellW,$cellH); $img.Dispose()
  $g.DrawRectangle($penCell,$x,$y+$lab,$cellW,$cellH)
  $tt=[Math]::Round($frames[$i].t*$Period,2)
  $desc = ($frames[$i].ks | ForEach-Object { "$($_.short) {0:N3}" -f $_.k }) -join '    '
  $g.DrawString(("第 {0:N2}s    明度 {1}" -f $tt,$desc),$fLab,[System.Drawing.Brushes]::Gainsboro,$x+1,$y+2)
}
# ---- 曲线条 ----
$cy=$gridH
$br=$PULSE[0].bright
$g.DrawString(("明灭曲线（周期 {0:N0}s：亮 {1:P0}，暗 {2:P0}；上升 {3:N2} 周期、下降 {4:N2} 周期）" -f $Period,$br,(1-$br),$PULSE[0].rise,$PULSE[0].fall),$fBig,[System.Drawing.Brushes]::White,$gap,($cy+6))
$px0=$gap+10; $px1=$Wtot-$gap-10
$py0=$cy+44; $py1=$py0+130
$g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(24,28,38)),$px0,$py0,($px1-$px0),($py1-$py0))
$g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(34,44,30)),$px0,$py0,([int](($px1-$px0)*$br)),($py1-$py0))
$cols2=@([System.Drawing.Color]::FromArgb(214,180,104),[System.Drawing.Color]::FromArgb(128,132,146))
$N=800
for($pi=0;$pi -lt $PULSE.Count;$pi++){
  $pp=$PULSE[$pi]
  $pen=[System.Drawing.Pen]::new($cols2[$pi % 2],2)
  $pts=New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
  for($i=0;$i -le $N;$i++){
    $tt=2.0*$i/$N
    $hh=Wof ($tt-[Math]::Floor($tt)) $pp.bright $pp.rise $pp.fall
    $kk=$pp.floor + ($pp.peak-$pp.floor)*$hh
    $pts.Add([System.Drawing.PointF]::new([single]($px0+($px1-$px0)*$tt/2.0), [single]($py1-($py1-$py0)*$kk)))
  }
  $g.DrawLines($pen,$pts.ToArray()); $pen.Dispose()
  $g.DrawString($pp.short,$fLab,[System.Drawing.SolidBrush]::new($cols2[$pi % 2]),[single]($px0+8+$pi*90),[single]($py0+4))
}
for($i=0;$i -lt $frames.Count;$i++){
  $tt=$frames[$i].t*$Period
  $xx=$px0+($px1-$px0)*($tt/(2.0*$Period))
  $g.DrawLine($penTick,$xx,$py0,$xx,$py1)
}
$g.DrawString('1.00',[System.Drawing.Font]::new('Consolas',9),[System.Drawing.Brushes]::Gray,$px0-38,[single]($py0-2))
$g.DrawString('0.00',[System.Drawing.Font]::new('Consolas',9),[System.Drawing.Brushes]::Gray,$px0-38,[single]($py1-10))
$g.DrawString('0s',$fLab,[System.Drawing.Brushes]::Gray,[single]($px0-4),[single]($py1+4))
$g.DrawString('6s',$fLab,[System.Drawing.Brushes]::Gray,[single]($px0+($px1-$px0)/2-8),[single]($py1+4))
$g.DrawString('12s',$fLab,[System.Drawing.Brushes]::Gray,[single]($px1-16),[single]($py1+4))

$out=Join-Path $outdir 'board-trim-pulse-frames.png'
$tmp=Join-Path $scratch 'sheet.png'
$sheet.Save($tmp,[System.Drawing.Imaging.ImageFormat]::Png)
[System.IO.File]::Copy($tmp,$out,$true)
$g.Dispose();$sheet.Dispose()
"-> $out  ($Wtot x $Htot)  $((Get-Item $out).Length) B"
