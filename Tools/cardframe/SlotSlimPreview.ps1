$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$t0=Get-Date

$shot="$env:TEMP\codex-clipboard-6d262f3c-df66-481b-89c4-f8b6eedeae14.png"
$gen ="$PWD\Assets\_Game\Art\Sprites\Generated\slot-slim-v1"
$outD="$gen"
$board="$PWD\Assets/_Game/Art/Sprites/Board"
$cache="$env:TEMP\slim-boards"; New-Item -ItemType Directory -Path $cache -Force | Out-Null

$SPW=540; $SPH=960
$COLS=@(434.5,685.5,936.5); $ROWS=@(136.0,327.0,542.0,734.0); $GW=104; $GH=185

function S2L([double]$c){ if($c -le 0.04045){ $c/12.92 } else { [Math]::Pow(($c+0.055)/1.055,2.4) } }
$N=4096
$LUT=New-Object 'byte[]' $N
for($i=0;$i -lt $N;$i++){ $c=$i/[double]($N-1); $s= if($c -le 0.0031308){$c*12.92}else{1.055*[Math]::Pow($c,1.0/2.4)-0.055}; $v=[int][Math]::Round($s*255.0); if($v -gt 255){$v=255}; if($v -lt 0){$v=0}; $LUT[$i]=[byte]$v }
function L2S([double]$v){ if($v -lt 0){$v=0.0}; if($v -gt 1.0){$v=1.0}; $i=[int]($v*($N-1)+0.5); if($i -gt $N-1){$i=$N-1}; return $LUT[$i] }

$sb=[System.Drawing.Bitmap]::FromFile($shot)
$IW=$sb.Width; $IH=$sb.Height
$bd=$sb.LockBits([System.Drawing.Rectangle]::new(0,0,$IW,$IH),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$base=New-Object 'byte[]' ($bd.Stride*$IH)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0,$base,0,$base.Length)
$STR=$bd.Stride
$sb.UnlockBits($bd); $sb.Dispose()

function New-BaseField { param([int]$cx,[int]$cy)
  $Bl=New-Object 'double[]' 3; $Br=New-Object 'double[]' 3; $nl=0; $nr=0
  for($k=3;$k -le 6;$k++){
    $xl=$cx-$k; $xr=$cx+$GW-1+$k
    if($xl -lt 0){ $xl=0 }; if($xr -gt $IW-1){ $xr=$IW-1 }
    for($y=$cy+42;$y -le $cy+142;$y++){
      $o=$y*$STR+$xl*4
      $Bl[0]+=(S2L ($base[$o+2]/255.0)); $Bl[1]+=(S2L ($base[$o+1]/255.0)); $Bl[2]+=(S2L ($base[$o]/255.0)); $nl++
      $o=$y*$STR+$xr*4
      $Br[0]+=(S2L ($base[$o+2]/255.0)); $Br[1]+=(S2L ($base[$o+1]/255.0)); $Br[2]+=(S2L ($base[$o]/255.0)); $nr++
    }
  }
  for($c=0;$c -lt 3;$c++){ $Bl[$c]/=$nl; $Br[$c]/=$nr }
  $B=New-Object 'double[]' ($GW*$GH*3)
  for($py=0;$py -lt $GH;$py++){
    $u=0.0
    for($px=0;$px -lt $GW;$px++){
      $u=($px+0.5)/[double]$GW
      $i=($py*$GW+$px)*3
      for($c=0;$c -lt 3;$c++){ $B[$i+$c]=$Bl[$c]*(1-$u)+$Br[$c]*$u }
    }
  }
  $B
}

function Get-Sample { param([string]$p)
  $b=[System.Drawing.Bitmap]::FromFile($p)
  $bdd=$b.LockBits([System.Drawing.Rectangle]::new(0,0,$SPW,$SPH),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $raw=New-Object 'byte[]' ($bdd.Stride*$SPH)
  [System.Runtime.InteropServices.Marshal]::Copy($bdd.Scan0,$raw,0,$raw.Length)
  $st=$bdd.Stride
  $b.UnlockBits($bdd); $b.Dispose()
  $alf=New-Object 'double[]' ($GW*$GH)
  for($py=0;$py -lt $GH;$py++){
    $sy=(($py+0.5)/[double]$GH*$SPH)-0.5
    $iy=[int][Math]::Floor($sy); $fy=$sy-$iy
    if($iy -lt 0){$iy=0}; if($iy -gt $SPH-2){$iy=$SPH-2}
    for($px=0;$px -lt $GW;$px++){
      $sx=(($px+0.5)/[double]$GW*$SPW)-0.5
      $ix=[int][Math]::Floor($sx); $fx=$sx-$ix
      if($ix -lt 0){$ix=0}; if($ix -gt $SPW-2){$ix=$SPW-2}
      $a00=$raw[$iy*$st+$ix*4+3]/255.0
      $a10=$raw[$iy*$st+($ix+1)*4+3]/255.0
      $a01=$raw[($iy+1)*$st+$ix*4+3]/255.0
      $a11=$raw[($iy+1)*$st+($ix+1)*4+3]/255.0
      $alf[$py*$GW+$px]=((($a00+($a10-$a00)*$fx)*(1-$fy))+(($a01+($a11-$a01)*$fx)*$fy))
    }
  }
  $alf
}

$variants=@(
 @('现状   亮边 6px@0.80 + 双线 5px/4px', "$board/SlotPlate.png", "$board/SlotEdge.png"),
 @('收细①  亮边 4px@0.58 + 单线 6px',    "$gen/Plate_v7_a.png",  "$gen/Edge_v7_a.png"),
 @('收细②  亮边 3px@0.46 + 单线 4px  ← 推荐', "$gen/Plate_v7_b.png", "$gen/Edge_v7_b.png"),
 @('收细③  亮边 2px@0.34 + 单线 3px',    "$gen/Plate_v7_c.png",  "$gen/Edge_v7_c.png")
)
# 注意：$a += (数组) 会拍平，必须写成 $a += ,(数组) 才能保住嵌套
$cacheP=@(); $cacheE=@()
foreach($v in $variants){ $cacheP+=,(Get-Sample $v[1]); $cacheE+=,(Get-Sample $v[2]) }
"缓存：cacheP.Count=$($cacheP.Count)  单项类型=$($cacheP[0].GetType().Name)  长度=$($cacheP[0].Length)"
"采样完毕 $([int]((Get-Date)-$t0).TotalSeconds)s"

$skip=@(2,3)
function Render-Board { param($plateA,$edgeA,[bool]$hl)
  $out=New-Object 'byte[]' ($STR*$IH)
  [Array]::Copy($base,$out,$out.Length)
  if($hl){ $CRg=1.0; $CGg=0.92; $CBg=0.015686 } else { $CRg=1.0; $CGg=1.0; $CBg=1.0 }
  for($rr=0;$rr -lt 4;$rr++){
    for($cc=0;$cc -lt 3;$cc++){
      if($cc -eq $skip[0] -and $rr -eq $skip[1]){ continue }
      $cx=[int]$COLS[$cc]; $cy=[int]$ROWS[$rr]
      $B=New-BaseField $cx $cy
      if($hl){ $tp=1.0; $te=1.0 } else { $tp=0.10; $te=0.0 }
      for($py=0;$py -lt $GH;$py++){
        $Y=$cy+$py
        if($Y -lt 0 -or $Y -ge $IH){ continue }
        $row=$py*$GW
        for($px=0;$px -lt $GW;$px++){
          $X=$cx+$px
          if($X -lt 0 -or $X -ge $IW){ continue }
          $i=$row+$px
          $ce=1.0-(1.0-$plateA[$i]*$tp)*(1.0-$edgeA[$i]*$te)
          $j=$i*3
          $o=$Y*$STR+$X*4
          $out[$o]  =L2S ($B[$j+2]*(1-$ce)+$CBg*$ce)
          $out[$o+1]=L2S ($B[$j+1]*(1-$ce)+$CGg*$ce)
          $out[$o+2]=L2S ($B[$j+0]*(1-$ce)+$CRg*$ce)
        }
      }
    }
  }
  $out
}

function Save-Bytes { param([byte[]]$buf,[string]$path)
  $bmp=New-Object System.Drawing.Bitmap($IW,$IH,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $b2=$bmp.LockBits([System.Drawing.Rectangle]::new(0,0,$IW,$IH),[System.Drawing.Imaging.ImageLockMode]::WriteOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  [System.Runtime.InteropServices.Marshal]::Copy($buf,0,$b2.Scan0,$buf.Length)
  $bmp.UnlockBits($b2); $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}
function Load-Bytes { param([string]$path)
  $b=[System.Drawing.Bitmap]::FromFile($path)
  $b2=$b.LockBits([System.Drawing.Rectangle]::new(0,0,$IW,$IH),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $buf=New-Object 'byte[]' ($b2.Stride*$IH)
  [System.Runtime.InteropServices.Marshal]::Copy($b2.Scan0,$buf,0,$buf.Length)
  $st=$b2.Stride
  $b.UnlockBits($b2); $b.Dispose()
  @{ buf=$buf; st=$st }
}

foreach($st in @('hl','rest')){
  for($i=0;$i -lt 4;$i++){
    $f="$cache\$st-$i.png"
    if(Test-Path $f){ continue }
    $buf=Render-Board $cacheP[$i] $cacheE[$i] ($st -eq 'hl')
    Save-Bytes $buf $f
    "  渲染 $st-$i  $([int]((Get-Date)-$t0).TotalSeconds)s"
  }
}
"全部渲染完毕 $([int]((Get-Date)-$t0).TotalSeconds)s"
"--- 自检：高亮态槽心 (490,400) 应为暗橄榄黄 ---"
foreach($k in 0..3){
  $b=[System.Drawing.Bitmap]::FromFile("$cache\hl-$k.png")
  $c=$b.GetPixel(490,400); $d=$b.GetPixel(440,400); $b.Dispose()
  "  [$($k+1)] 槽心=$($c.R),$($c.G),$($c.B)   靠边=$($d.R),$($d.G),$($d.B)"
}