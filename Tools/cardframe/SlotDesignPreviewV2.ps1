$ErrorActionPreference='Stop'
foreach($n in @('System.Drawing.dll','System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.Core.dll','System.Private.Windows.GdiPlus.dll')){
  try{ Add-Type -Path (Join-Path $PSHOME $n) }catch{}
}
$ROOT='C:\Users\22589\Documents\GitHub\Another-World'
$SHOT='C:\Users\22589\AppData\Local\Temp\codex-clipboard-d5e39102-5e5c-411a-bdd3-074eb056f423.png'
$OUTDIR=Join-Path $ROOT 'Tools\cardframe\preview'
$CACHE=Join-Path $env:TEMP 'zz-slotprev'
if(-not (Test-Path $CACHE)){ New-Item -ItemType Directory -Path $CACHE -Force | Out-Null }

function Save-Bytes([System.Drawing.Bitmap]$bmp,[string]$target){
  $tmp=Join-Path $CACHE ('tmp-'+[guid]::NewGuid().ToString('N')+'.png')
  $bmp.Save($tmp,[System.Drawing.Imaging.ImageFormat]::Png)
  [System.IO.File]::Copy($tmp,$target,$true)
  [System.IO.File]::Delete($tmp)
}
function New-Tint([double]$r,[double]$g,[double]$b,[double]$a){
  $cm=[System.Drawing.Imaging.ColorMatrix]::new()
  $cm.Matrix00=[single]$r; $cm.Matrix11=[single]$g; $cm.Matrix22=[single]$b; $cm.Matrix33=[single]$a
  $ia=[System.Drawing.Imaging.ImageAttributes]::new()
  $ia.SetColorMatrix($cm)
  return $ia
}

$variants=@(
  [PSCustomObject]@{ name='当前 v7';   path=(Join-Path $ROOT 'Assets\_Game\Art\Sprites\Board\SlotPlate.png') },
  [PSCustomObject]@{ name='v8 a 细线'; path=(Join-Path $ROOT 'Assets\_Game\Art\Sprites\Generated\slot-design-v1\Plate_v8_a.png') },
  [PSCustomObject]@{ name='v8 b 推荐'; path=(Join-Path $ROOT 'Assets\_Game\Art\Sprites\Generated\slot-design-v1\Plate_v8_b.png') },
  [PSCustomObject]@{ name='v8 c 深槽'; path=(Join-Path $ROOT 'Assets\_Game\Art\Sprites\Generated\slot-design-v1\Plate_v8_c.png') },
  [PSCustomObject]@{ name='v8 b 白线'; path=(Join-Path $ROOT 'Assets\_Game\Art\Sprites\Generated\slot-design-v1\Plate_v8_b_white.png') }
)

$shot=[System.Drawing.Bitmap]::new($SHOT)

$xs=New-Object 'System.Collections.Generic.List[int]'
$vs=New-Object 'System.Collections.Generic.List[double]'
for($x=380;$x -le 1120;$x++){
  $s=0.0
  for($y=360;$y -lt 500;$y+=2){ $c=$shot.GetPixel($x,$y); $s+=($c.R+$c.G+$c.B)/3.0 }
  $xs.Add($x); $vs.Add($s/70.0)
}
$colRanges=New-Object 'System.Collections.Generic.List[object]'
$inRun=$false
$start=0
for($i=1;$i -lt $xs.Count;$i++){
  $d=$vs[$i]-$vs[$i-1]
  if((-not $inRun) -and ($d -gt 6.0)){ $start=$xs[$i]; $inRun=$true }
  elseif($inRun -and ($d -lt -6.0)){ $colRanges.Add([PSCustomObject]@{ x0=$start; x1=($xs[$i]-1) }); $inRun=$false }
}
"COL RANGES:"
foreach($cr in $colRanges){ "  $($cr.x0) .. $($cr.x1)  w=$($cr.x1-$cr.x0+1)" }

$ROWS=@(
  [PSCustomObject]@{ y0=155; y1=320 },
  [PSCustomObject]@{ y0=328; y1=512 },
  [PSCustomObject]@{ y0=548; y1=728 },
  [PSCustomObject]@{ y0=736; y1=920 }
)

$baseX=$colRanges[0].x0
$baseW=$colRanges[0].x1-$colRanges[0].x0+1
$baseY=328
$baseH=184
$baseRect=[System.Drawing.Rectangle]::new($baseX,$baseY,$baseW,$baseH)
$baseShot=$shot.Clone($baseRect,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# ---------- 预览一：真尺寸 3 状态 x 5 变体 ----------
$SCALE=3.0
$SW=[int]($baseW*$SCALE); $SH=[int]($baseH*$SCALE)
$LABW=112; $TOPL=32; $GAP=18
$cols=$variants.Count
$states=@(
  [PSCustomObject]@{ name='静置 a0.10'; r=1.0; g=1.0; b=1.0; a=0.10 },
  [PSCustomObject]@{ name='高亮 a1.00'; r=1.0; g=1.0; b=1.0; a=1.00 },
  [PSCustomObject]@{ name='黄态 可放';  r=1.0; g=0.92; b=0.015; a=1.00 }
)
$rows=$states.Count
$sheetW=$LABW+$cols*$SW+($cols-1)*$GAP+$GAP
$sheetH=$TOPL+$rows*($SH+$GAP)+$GAP
$sheet=[System.Drawing.Bitmap]::new($sheetW,$sheetH,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg=[System.Drawing.Graphics]::FromImage($sheet)
$sg.Clear([System.Drawing.Color]::FromArgb(12,16,24))
$sg.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$sg.CompositingQuality=[System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$fLab=[System.Drawing.Font]::new('Microsoft YaHei',10)
$fHead=[System.Drawing.Font]::new('Microsoft YaHei',11,[System.Drawing.FontStyle]::Bold)
$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(70,70,84),1)

for($r=0;$r -lt $rows;$r++){
  $ry=$TOPL+$r*($SH+$GAP)
  $sg.DrawString($states[$r].name,$fLab,[System.Drawing.Brushes]::Gainsboro,4,([single]($ry+$SH/2-12)))
  for($c=0;$c -lt $cols;$c++){
    $cx=$LABW+$c*($SW+$GAP)
    if($r -eq 0){ $sg.DrawString($variants[$c].name,$fHead,[System.Drawing.Brushes]::White,([single]$cx),4) }
    $sg.DrawImage($baseShot,$cx,$ry,$SW,$SH)
    $plate=[System.Drawing.Image]::FromFile($variants[$c].path)
    $ia=New-Tint $states[$r].r $states[$r].g $states[$r].b $states[$r].a
    $sg.DrawImage($plate,[System.Drawing.Rectangle]::new($cx,$ry,$SW,$SH),0,0,$plate.Width,$plate.Height,[System.Drawing.GraphicsUnit]::Pixel,$ia)
    $plate.Dispose()
    $sg.DrawRectangle($pen,[System.Drawing.Rectangle]::new($cx,$ry,$SW,$SH))
  }
}
$out1=Join-Path $OUTDIR 'slot-v8-realsize-3states.png'
Save-Bytes $sheet $out1
$sg.Dispose(); $sheet.Dispose()
"SAVED $out1  ($sheetW x $sheetH)"

# ---------- 预览二：全盘上下文 ----------
$full=[System.Drawing.Bitmap]::new($shot.Width,$shot.Height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$fg=[System.Drawing.Graphics]::FromImage($full)
$fg.DrawImage($shot,0,0)
$fg.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fg.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$plate2=[System.Drawing.Image]::FromFile($variants[2].path)
$iaStatic=New-Tint 1.0 1.0 1.0 0.10
foreach($cr in $colRanges){
  foreach($rw in $ROWS){
    $dr=[System.Drawing.Rectangle]::new($cr.x0,$rw.y0,($cr.x1-$cr.x0+1),($rw.y1-$rw.y0+1))
    $fg.DrawImage($plate2,$dr,0,0,$plate2.Width,$plate2.Height,[System.Drawing.GraphicsUnit]::Pixel,$iaStatic)
  }
}
$iaHi=New-Tint 1.0 1.0 1.0 1.0
foreach($rw in @($ROWS[1],$ROWS[2])){
  $fg.DrawImage($plate2,[System.Drawing.Rectangle]::new($colRanges[1].x0,$rw.y0,($colRanges[1].x1-$colRanges[1].x0+1),($rw.y1-$rw.y0+1)),0,0,$plate2.Width,$plate2.Height,[System.Drawing.GraphicsUnit]::Pixel,$iaHi)
}
$plate2.Dispose()
$out2=Join-Path $OUTDIR 'slot-v8b-fullboard.png'
Save-Bytes $full $out2
$fg.Dispose(); $full.Dispose()
"SAVED $out2"

$shot.Dispose(); $baseShot.Dispose()
