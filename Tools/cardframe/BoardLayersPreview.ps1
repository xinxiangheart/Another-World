<#
  BoardLayersPreview.ps1  ——  棋盘分层预览（默认读材质的真实 _Color）

  按 Unity 里的真实混合算式合成 board-layers-v2 的 8 层：线性空间 + Glow 走加色(SrcAlpha/One) +
  按材质渲染队列顺序叠加，再与当前单张 Board.png 并排。

  层级（渲染顺序）:
    Board_Plate(不透明底) Board_Surface(板面) Board_Sigil(中心大圆环) Board_Rune(六芒星+内圆)
    Board_Ornament(外框角饰) Board_Glow(中央蓝光,加色) Board_Motes(微尘) Board_Foreground(暗角)

  默认逐层乘数 = 直接读 Assets/_Game/Art/Materials/Board/<Layer>.mat 的 _Color（取 RGB 平均）。
  覆盖参数（不写就用材质里的值）:
    -Gold   4 个含金层一起改 (Surface / Sigil / Rune / Ornament)
    -Glow   只改 Glow 层
    -Set   按层单独指定, 如 -Set Board_Sigil=0.15,Board_Rune=0.5
    -Global 全部 8 层再统一乘一次

  ★ _Color 是**线性值**：线性 0.7 ≈ 视觉 87%；线性 0.5 ≈ 视觉 73%；想视觉减半要填 ≈0.22。
  ★ _Color=0 的层不会消失，而是用它的 alpha 把下面的板面压暗 —— 即「压黑」。

  用法:
    & .\BoardLayersPreview.ps1
    & .\BoardLayersPreview.ps1 -Set 'Board_Sigil=0.15'
    & .\BoardLayersPreview.ps1 -Set 'Board_Sigil=0' -Tag '-s000'
#>
param(
  [double]$Gold   = -1,
  [double]$Glow   = -1,
  [string[]]$Set  = @(),
  [double]$Global = 1.0,
  [int]$W = 1024,
  [int]$H = 576,
  [string]$Tag = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if(-not (Test-Path (Join-Path $root 'Assets'))){ $root = 'C:\Users\22589\Documents\GitHub\Another-World' }
$laydir  = Join-Path $root 'Assets\_Game\Art\Sprites\Generated\board-layers-v2'
$matdir  = Join-Path $root 'Assets\_Game\Art\Materials\Board'
$board   = Join-Path $root 'Assets\_Game\Art\Sprites\Board\Board.png'
$outdir  = Join-Path $PSScriptRoot 'preview'
$scratch = Join-Path $env:TEMP 'board-preview'
New-Item -ItemType Directory -Path $outdir  -Force | Out-Null
New-Item -ItemType Directory -Path $scratch -Force | Out-Null

$cs = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uRHJhd2luZzsKdXNpbmcgU3lzdGVtLkRyYXdpbmcuSW1hZ2luZzsKdXNpbmcgU3lzdGVtLlJ1bnRpbWUuSW50ZXJvcFNlcnZpY2VzOwoKcHVibGljIHN0YXRpYyBjbGFzcyBCb2FyZExheWVyTWl4CnsKICAgIHB1YmxpYyBzdGF0aWMgcmVhZG9ubHkgc3RyaW5nW10gT3JkZXIgPSB7IkJvYXJkX1BsYXRlIiwiQm9hcmRfU3VyZmFjZSIsIkJvYXJkX1NpZ2lsIiwiQm9hcmRfUnVuZSIsIkJvYXJkX09ybmFtZW50IiwiQm9hcmRfR2xvdyIsIkJvYXJkX01vdGVzIiwiQm9hcmRfRm9yZWdyb3VuZCJ9OwogICAgY29uc3QgaW50IEdMT1cgPSA1OwogICAgc3RhdGljIGRvdWJsZSBTMkwoZG91YmxlIGMpeyByZXR1cm4gYzw9MC4wNDA0NSA/IGMvMTIuOTIgOiBNYXRoLlBvdygoYyswLjA1NSkvMS4wNTUsMi40KTsgfQogICAgc3RhdGljIGRvdWJsZSBMMlMoZG91YmxlIGMpeyBpZihjPDApYz0wOyBpZihjPjEpYz0xOyByZXR1cm4gYzw9MC4wMDMxMzA4ID8gYyoxMi45MiA6IDEuMDU1Kk1hdGguUG93KGMsMS4wLzIuNCktMC4wNTU7IH0KCiAgICBzdGF0aWMgZmxvYXRbXSBMb2FkKEJpdG1hcCBibXAsaW50IFcsaW50IEgpewogICAgICAgIHVzaW5nKHZhciByZXM9bmV3IEJpdG1hcChXLEgsUGl4ZWxGb3JtYXQuRm9ybWF0MzJicHBBcmdiKSl7CiAgICAgICAgICAgIHVzaW5nKHZhciBnPUdyYXBoaWNzLkZyb21JbWFnZShyZXMpKXsgZy5JbnRlcnBvbGF0aW9uTW9kZT1TeXN0ZW0uRHJhd2luZy5EcmF3aW5nMkQuSW50ZXJwb2xhdGlvbk1vZGUuSGlnaFF1YWxpdHlCaWN1YmljOyBnLkRyYXdJbWFnZShibXAsMCwwLFcsSCk7IH0KICAgICAgICAgICAgdmFyIGJkPXJlcy5Mb2NrQml0cyhuZXcgUmVjdGFuZ2xlKDAsMCxXLEgpLEltYWdlTG9ja01vZGUuUmVhZE9ubHksUGl4ZWxGb3JtYXQuRm9ybWF0MzJicHBBcmdiKTsKICAgICAgICAgICAgaW50IHN0cmlkZT1iZC5TdHJpZGU7IHZhciByYXc9bmV3IGJ5dGVbc3RyaWRlKkhdOwogICAgICAgICAgICBNYXJzaGFsLkNvcHkoYmQuU2NhbjAscmF3LDAscmF3Lkxlbmd0aCk7IHJlcy5VbmxvY2tCaXRzKGJkKTsKICAgICAgICAgICAgdmFyIGY9bmV3IGZsb2F0W1cqSCo0XTsKICAgICAgICAgICAgZm9yKGludCB5PTA7eTxIO3krKyl7IGludCByb3c9eSpzdHJpZGU7IGZvcihpbnQgeD0wO3g8Vzt4KyspeyBpbnQgaT1yb3creCo0LCBvPSh5KlcreCkqNDsKICAgICAgICAgICAgICAgIGZbb109KGZsb2F0KVMyTChyYXdbaSsyXS8yNTUuMCk7IGZbbysxXT0oZmxvYXQpUzJMKHJhd1tpKzFdLzI1NS4wKTsgZltvKzJdPShmbG9hdClTMkwocmF3W2ldLzI1NS4wKTsgZltvKzNdPXJhd1tpKzNdLzI1NWY7IH0gfQogICAgICAgICAgICByZXR1cm4gZjsKICAgICAgICB9CiAgICB9CgogICAgcHVibGljIHN0YXRpYyBzdHJpbmcgUnVuKHN0cmluZyBsYXllckRpcixzdHJpbmcgYm9hcmRQYXRoLHN0cmluZyBzY3JhdGNoRGlyLGludCBXLGludCBILGZsb2F0W10gbXVsLHN0cmluZyB0YWcpewogICAgICAgIHZhciBMPW5ldyBmbG9hdFtPcmRlci5MZW5ndGhdW107CiAgICAgICAgZm9yKGludCBpPTA7aTxPcmRlci5MZW5ndGg7aSsrKXsgdXNpbmcodmFyIGI9bmV3IEJpdG1hcChTeXN0ZW0uSU8uUGF0aC5Db21iaW5lKGxheWVyRGlyLE9yZGVyW2ldKyIucG5nIikpKSBMW2ldPUxvYWQoYixXLEgpOyB9CiAgICAgICAgZG91YmxlW10gcz1uZXcgZG91YmxlWzZdOwogICAgICAgIHVzaW5nKHZhciBvPW5ldyBCaXRtYXAoVyxILFBpeGVsRm9ybWF0LkZvcm1hdDMyYnBwQXJnYikpewogICAgICAgICAgICB2YXIgYmQ9by5Mb2NrQml0cyhuZXcgUmVjdGFuZ2xlKDAsMCxXLEgpLEltYWdlTG9ja01vZGUuV3JpdGVPbmx5LFBpeGVsRm9ybWF0LkZvcm1hdDMyYnBwQXJnYik7CiAgICAgICAgICAgIGludCBzdHJpZGU9YmQuU3RyaWRlOyB2YXIgcmF3PW5ldyBieXRlW3N0cmlkZSpIXTsKICAgICAgICAgICAgZm9yKGludCBwPTA7cDxXKkg7cCsrKXsKICAgICAgICAgICAgICAgIGRvdWJsZSByPTAsZz0wLGI9MCxhPTA7CiAgICAgICAgICAgICAgICBmb3IoaW50IGk9MDtpPEwuTGVuZ3RoO2krKyl7CiAgICAgICAgICAgICAgICAgICAgdmFyIHE9TFtpXTsgaW50IGs9cCo0OwogICAgICAgICAgICAgICAgICAgIGRvdWJsZSBtPW11bFtpXTsKICAgICAgICAgICAgICAgICAgICBkb3VibGUgc3I9cVtrXSptLCBzZz1xW2srMV0qbSwgc2I9cVtrKzJdKm0sIHNhPXFbayszXTsKICAgICAgICAgICAgICAgICAgICBpZihpPT1HTE9XKXsgcis9c3Iqc2E7IGcrPXNnKnNhOyBiKz1zYipzYTsgfQogICAgICAgICAgICAgICAgICAgIGVsc2UgeyByPXNyKnNhK3IqKDEtc2EpOyBnPXNnKnNhK2cqKDEtc2EpOyBiPXNiKnNhK2IqKDEtc2EpOyBhPXNhK2EqKDEtc2EpOyB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBzWzBdKz1MMlMocikqMjU1OyBzWzFdKz1MMlMoZykqMjU1OyBzWzJdKz1MMlMoYikqMjU1OwogICAgICAgICAgICAgICAgaW50IHg9cCVXLCB5PXAvVywgaj15KnN0cmlkZSt4KjQ7CiAgICAgICAgICAgICAgICByYXdbal09KGJ5dGUpTWF0aC5Sb3VuZChMMlMoYikqMjU1KTsgcmF3W2orMV09KGJ5dGUpTWF0aC5Sb3VuZChMMlMoZykqMjU1KTsgcmF3W2orMl09KGJ5dGUpTWF0aC5Sb3VuZChMMlMocikqMjU1KTsgcmF3W2orM109KGJ5dGUpTWF0aC5Sb3VuZChNYXRoLk1pbigxLjAsYSkqMjU1KTsKICAgICAgICAgICAgfQogICAgICAgICAgICBNYXJzaGFsLkNvcHkocmF3LDAsYmQuU2NhbjAscmF3Lkxlbmd0aCk7IG8uVW5sb2NrQml0cyhiZCk7CiAgICAgICAgICAgIG8uU2F2ZShTeXN0ZW0uSU8uUGF0aC5Db21iaW5lKHNjcmF0Y2hEaXIsImNvbXBvc2l0ZSIrdGFnKyIucG5nIiksSW1hZ2VGb3JtYXQuUG5nKTsKICAgICAgICB9CiAgICAgICAgdXNpbmcodmFyIG89bmV3IEJpdG1hcChXLEgsUGl4ZWxGb3JtYXQuRm9ybWF0MzJicHBBcmdiKSl7CiAgICAgICAgICAgIHZhciBiZD1vLkxvY2tCaXRzKG5ldyBSZWN0YW5nbGUoMCwwLFcsSCksSW1hZ2VMb2NrTW9kZS5Xcml0ZU9ubHksUGl4ZWxGb3JtYXQuRm9ybWF0MzJicHBBcmdiKTsKICAgICAgICAgICAgaW50IHN0cmlkZT1iZC5TdHJpZGU7IHZhciByYXc9bmV3IGJ5dGVbc3RyaWRlKkhdOwogICAgICAgICAgICB1c2luZyh2YXIgc3JjPW5ldyBCaXRtYXAoYm9hcmRQYXRoKSl7CiAgICAgICAgICAgICAgICB2YXIgZj1Mb2FkKHNyYyxXLEgpOwogICAgICAgICAgICAgICAgZm9yKGludCBwPTA7cDxXKkg7cCsrKXsgaW50IGs9cCo0LCB4PXAlVywgeT1wL1csIGo9eSpzdHJpZGUreCo0OwogICAgICAgICAgICAgICAgICAgIHNbM10rPUwyUyhmW2tdKSoyNTU7IHNbNF0rPUwyUyhmW2srMV0pKjI1NTsgc1s1XSs9TDJTKGZbaysyXSkqMjU1OwogICAgICAgICAgICAgICAgICAgIHJhd1tqXT0oYnl0ZSlNYXRoLlJvdW5kKEwyUyhmW2srMl0pKjI1NSk7IHJhd1tqKzFdPShieXRlKU1hdGguUm91bmQoTDJTKGZbaysxXSkqMjU1KTsgcmF3W2orMl09KGJ5dGUpTWF0aC5Sb3VuZChMMlMoZltrXSkqMjU1KTsgcmF3W2orM109KGJ5dGUpTWF0aC5Sb3VuZChmW2srM10qMjU1KTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAgICBNYXJzaGFsLkNvcHkocmF3LDAsYmQuU2NhbjAscmF3Lkxlbmd0aCk7IG8uVW5sb2NrQml0cyhiZCk7CiAgICAgICAgICAgIG8uU2F2ZShTeXN0ZW0uSU8uUGF0aC5Db21iaW5lKHNjcmF0Y2hEaXIsImJvYXJkLXNpbmdsZSIrdGFnKyIucG5nIiksSW1hZ2VGb3JtYXQuUG5nKTsKICAgICAgICB9CiAgICAgICAgZG91YmxlIG49KGRvdWJsZSlXKkg7CiAgICAgICAgcmV0dXJuIHN0cmluZy5Gb3JtYXQoIuWQiOaIkCBtZWFuUkdCPSh7MDpGMX0sezE6RjF9LHsyOkYxfSkiLHNbMF0vbixzWzFdL24sc1syXS9uKTsKICAgIH0KfQ=='))
$refs = @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll') |
        ForEach-Object { Join-Path $PSHOME $_ }
if(-not ('BoardLayerMix' -as [type])){ Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs }

$names  = [BoardLayerMix]::Order
$isGold = @($false,$true,$true,$true,$true,$false,$false,$false)
$mul = New-Object 'float[]' 8
for($i=0;$i -lt 8;$i++){
  $t = [System.IO.File]::ReadAllText((Join-Path $matdir ($names[$i] + '.mat')))
  $m = [regex]::Match($t,'- _Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
  if(-not $m.Success){ throw "读不到 $($names[$i]).mat 的 _Color" }
  $mul[$i] = [float]((([double]$m.Groups[1].Value + [double]$m.Groups[2].Value + [double]$m.Groups[3].Value)/3.0) * $Global)
}
if($Gold -ge 0){ for($i=0;$i -lt 8;$i++){ if($isGold[$i]){ $mul[$i] = [float]($Gold * $Global) } } }
if($Glow -ge 0){ $mul[5] = [float]($Glow * $Global) }
foreach($spec in $Set){
  $kv = $spec -split '='
  if($kv.Count -ne 2){ throw "格式应为 Layer=value: $spec" }
  $idx = [Array]::IndexOf($names, $kv[0].Trim())
  if($idx -lt 0){ throw "未知层名: $($kv[0])" }
  $mul[$idx] = [float]([double]$kv[1].Trim() * $Global)
}

"逐层乘数:"
for($i=0;$i -lt 8;$i++){ "   {0,-18} {1}" -f $names[$i], $mul[$i].ToString('0.###') }
[BoardLayerMix]::Run($laydir, $board, $scratch, $W, $H, $mul, $Tag)

$lab = 30; $pad = 14
$sheet = [System.Drawing.Bitmap]::new(($W*2 + $pad*3), ($H + $lab + $pad*2), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(18,18,20))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = [System.Drawing.Font]::new('Microsoft YaHei UI', 12)
$labels = @('改动前  单张 Board.png（原状）', '改动后  8 层分层合成（按材质 _Color）')
$srcs   = @("board-single$Tag.png","composite$Tag.png")
for($i=0;$i -lt 2;$i++){
  $x = $pad + $i*($W + $pad); $y = $pad
  $img = [System.Drawing.Image]::FromFile((Join-Path $scratch $srcs[$i]))
  $g.DrawImage($img, $x, $y+$lab, $W, $H); $img.Dispose()
  $g.DrawRectangle([System.Drawing.Pens]::DimGray, $x, $y+$lab, $W, $H)
  $g.DrawString($labels[$i], $font, [System.Drawing.Brushes]::White, $x+2, $y+5)
}
$out = Join-Path $outdir "board-before-after$Tag.png"
if(Test-Path $out){ [System.IO.File]::Delete($out) }
$sheet.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $sheet.Dispose()
"-> $out ($((Get-Item $out).Length) B)"