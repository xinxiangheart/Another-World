# 卡槽底板 v6：凹槽（内阴影 + 下沿受光 + 细边）
# RGB 一律纯白，全部结构写在 alpha 通道 —— slotImage.color 的状态色（黄高亮 / 绿弃牌 / 紫囚牢 / 黑封锁）
# 照旧原样相乘，运行时逻辑一行不用动。
Add-Type -AssemblyName System.Drawing

function S2L([double]$c){ if($c -le 0.04045){ $c/12.92 } else { [Math]::Pow(($c+0.055)/1.055,2.4) } }
function L2S([double]$c){ if($c -le 0.0031308){ $c*12.92 } else { 1.055*[Math]::Pow($c,1.0/2.4)-0.055 } }

function New-RoundCoverage {
  param([int]$w,[int]$h,[double]$r)
  $bmp=New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  $d=2.0*$r
  $p=New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc(0.0,0.0,$d,$d,180.0,90.0)
  $p.AddArc(($w-$d),0.0,$d,$d,270.0,90.0)
  $p.AddArc(($w-$d),($h-$d),$d,$d,0.0,90.0)
  $p.AddArc(0.0,($h-$d),$d,$d,90.0,90.0)
  $p.CloseFigure()
  $g.FillPath([System.Drawing.Brushes]::White,$p)
  $p.Dispose(); $g.Dispose()
  $alf=New-Object 'byte[]' ($w*$h)
  for($y=0;$y -lt $h;$y++){ for($x=0;$x -lt $w;$x++){ $alf[$y*$w+$x]=$bmp.GetPixel($x,$y).A } }
  $bmp.Dispose()
  $alf
}

function Get-SlotMaskData {
  param([string]$SprPath,[double]$RoundRadius=0.0)
  $bmp=[System.Drawing.Bitmap]::FromFile($SprPath)
  $w=$bmp.Width; $h=$bmp.Height
  $alf=New-Object 'byte[]' ($w*$h)
  for($y=0;$y -lt $h;$y++){
    for($x=0;$x -lt $w;$x++){ $alf[$y*$w+$x]=$bmp.GetPixel($x,$y).A }
  }
  $bmp.Dispose()
  if($RoundRadius -gt 0){ $alf=New-RoundCoverage $w $h $RoundRadius }
  # 主体 alpha = 众数（当前素材 = 184，占 99.2%）
  $hist=New-Object 'int[]' 256
  for($i=0;$i -lt $alf.Length;$i++){ $hist[$alf[$i]]++ }
  $mainA=1; $best=0
  for($a=1;$a -lt 256;$a++){ if($hist[$a] -gt $best){ $best=$hist[$a]; $mainA=$a } }
  $msk=New-Object 'bool[]' ($w*$h)
  $cov=New-Object 'double[]' ($w*$h)
  $top=New-Object 'int[]' $w; $bot=New-Object 'int[]' $w
  $lef=New-Object 'int[]' $h; $rig=New-Object 'int[]' $h
  for($x=0;$x -lt $w;$x++){ $top[$x]=-1; $bot[$x]=-1 }
  for($y=0;$y -lt $h;$y++){ $lef[$y]=-1; $rig[$y]=-1 }
  for($y=0;$y -lt $h;$y++){
    for($x=0;$x -lt $w;$x++){
      $i=$y*$w+$x; $a=[int]$alf[$i]
      $c=$a/[double]$mainA
      if($c -gt 1.0){ $c=1.0 }
      $cov[$i]=$c
      if($a -gt 100){
        $msk[$i]=$true
        if($top[$x] -lt 0){ $top[$x]=$y }
        $bot[$x]=$y
        if($lef[$y] -lt 0){ $lef[$y]=$x }
        $rig[$y]=$x
      }
    }
  }
  @{ W=$w;H=$h;Cov=$cov;Msk=$msk;Top=$top;Bot=$bot;Lef=$lef;Rig=$rig;MaxA=$mainA;Alf=$alf }
}

function New-SlotAlphaMap {
  param($MS,[double]$Base,[double]$RimBand,[double]$RimA,[double]$BT=30.0,[double]$BL=20.0,[double]$BB=24.0,[double]$BR=18.0)
  $w=$MS.W; $h=$MS.H
  $out=New-Object 'double[]' ($w*$h)
  $sbShadowT=$BT; $sbShadowL=$BL; $sbLightB=$BB; $sbLightR=$BR
  for($y=0;$y -lt $h;$y++){
    $lx=$MS.Lef[$y]; $rx=$MS.Rig[$y]
    for($x=0;$x -lt $w;$x++){
      $i=$y*$w+$x
      if(-not $MS.Msk[$i]){ $out[$i]=0.0; continue }
      $dT=$y-$MS.Top[$x]; $dB=$MS.Bot[$x]-$y
      $dL=$x-$lx;        $dR=$rx-$x
      $d=$dT
      if($dB -lt $d){$d=$dB}
      if($dL -lt $d){$d=$dL}
      if($dR -lt $d){$d=$dR}
      if($d -lt $RimBand){
        $a=$RimA
      } else {
        $a=$Base
        $t=1.0-($dT-$RimBand)/$sbShadowT
        if($t -gt 0){ if($t -gt 1){$t=1.0}; $a-=0.34*$t }
        $t=1.0-($dL-$RimBand)/$sbShadowL
        if($t -gt 0){ if($t -gt 1){$t=1.0}; $a-=0.14*$t }
        $t=1.0-($dB-$RimBand)/$sbLightB
        if($t -gt 0){ if($t -gt 1){$t=1.0}; $a+=0.20*$t }
        $t=1.0-($dR-$RimBand)/$sbLightR
        if($t -gt 0){ if($t -gt 1){$t=1.0}; $a+=0.09*$t }
      }
      if($a -lt 0){$a=0.0} elseif($a -gt 1){$a=1.0}
      $out[$i]=$a*$MS.Cov[$i]
    }
  }
  $out
}

function Save-SlotAlphaPng {
  param($MS,$Alpha,[string]$Out)
  $w=$MS.W;$h=$MS.H
  $bmp=New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  for($y=0;$y -lt $h;$y++){
    for($x=0;$x -lt $w;$x++){
      $a=[int][Math]::Round(255.0*$Alpha[$y*$w+$x])
      if($a -lt 0){$a=0} elseif($a -gt 255){$a=255}
      [System.Drawing.Color]$cc=[System.Drawing.Color]::FromArgb($a,255,255,255)
      $bmp.SetPixel($x,$y,$cc)
    }
  }
  $bmp.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

# ── 槽位网格（画布 @0.76875，取自实机截图亮度台阶实测）──
$script:SLOT_COLS=@(434.5,685.5,936.5)
$script:SLOT_ROWS=@(136.0,327.0,542.0,734.0)

function Sample-Bilin {
  param($Arr,[int]$w,[int]$h,[double]$sx,[double]$sy)
  $fx0=[Math]::Floor($sx-0.5); $fy0=[Math]::Floor($sy-0.5)
  $tx=($sx-0.5)-$fx0; $ty=($sy-0.5)-$fy0
  $x0=[int]$fx0; $y0=[int]$fy0
  $x1=$x0+1; $y1=$y0+1
  if($x0 -lt 0){$x0=0}; if($y0 -lt 0){$y0=0}
  if($x1 -lt 0){$x1=0}; if($y1 -lt 0){$y1=0}
  if($x0 -gt $w-1){$x0=$w-1}; if($x1 -gt $w-1){$x1=$w-1}
  if($y0 -gt $h-1){$y0=$h-1}; if($y1 -gt $h-1){$y1=$h-1}
  $v00=$Arr[$y0*$w+$x0]; $v10=$Arr[$y0*$w+$x1]
  $v01=$Arr[$y1*$w+$x0]; $v11=$Arr[$y1*$w+$x1]
  $a=$v00+($v10-$v00)*$tx
  $b=$v01+($v11-$v01)*$tx
  $a+($b-$a)*$ty
}

# 单像素：先把旧槽位从实机截图反解回底板，再叠新槽位（线性空间混合；工程 m_ActiveColorSpace: 1）
function Invoke-Relight {
  param($SrcBmp,[int]$X,[int]$Y,$MS,$NewAlpha,[double]$PlateAlpha)
  $sx=($X-($script:SLOT_COLS[1]))/104.0*$MS.W
  $sy=($Y-($script:SLOT_ROWS[0]))/185.0*$MS.H
  $ao=(Sample-Bilin $MS.Alf $MS.W $MS.H $sx $sy)/255.0
  $an=Sample-Bilin $NewAlpha $MS.W $MS.H $sx $sy
  $oldEff=$ao*$PlateAlpha; $inv=1.0-$oldEff
  $pc=$SrcBmp.GetPixel($X,$Y)
  $r0=S2L ($pc.R/255.0); $g0=S2L ($pc.G/255.0); $b0=S2L ($pc.B/255.0)
  $br=($r0-$oldEff)/$inv; $bg=($g0-$oldEff)/$inv; $bb=($b0-$oldEff)/$inv
  if($br -lt 0){$br=0.0}; if($bg -lt 0){$bg=0.0}; if($bb -lt 0){$bb=0.0}
  $e=$an*$PlateAlpha; $ie=1.0-$e
  $rr=[int][Math]::Round(255.0*(L2S ($br*$ie+$e)))
  $gg=[int][Math]::Round(255.0*(L2S ($bg*$ie+$e)))
  $bb2=[int][Math]::Round(255.0*(L2S ($bb*$ie+$e)))
  if($rr -gt 255){$rr=255}; if($rr -lt 0){$rr=0}
  if($gg -gt 255){$gg=255}; if($gg -lt 0){$gg=0}
  if($bb2 -gt 255){$bb2=255}; if($bb2 -lt 0){$bb2=0}
  [System.Drawing.Color]::FromArgb(255,$rr,$gg,$bb2)
}

function New-BoardPanel {
  param($SrcBmp,[hashtable]$MS,$NewAlpha,[double]$PlateAlpha,[string]$Label,[System.Drawing.Font]$Font,[int]$SkipCol,[int]$SkipRow,[bool]$Apply)
  $srcD=$SrcBmp
  $w=$SrcBmp.Width; $h=$SrcBmp.Height
  $out=New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($out)
  $g.DrawImage($SrcBmp,0,0,$w,$h)
  $g.Dispose()
  if($Apply){
    for($r=0;$r -lt 4;$r++){
      for($c=0;$c -lt 3;$c++){
        if($SkipCol -ge 0 -and $r -eq $SkipRow -and $c -eq $SkipCol){ continue }
        $x0=[int][Math]::Round($script:SLOT_COLS[$c])
        $y0=[int][Math]::Round($script:SLOT_ROWS[$r])
        for($py=0;$py -lt 185;$py++){
          for($px=0;$px -lt 104;$px++){
            $X=$x0+$px; $Y=$y0+$py
            if($X -lt 0 -or $Y -lt 0 -or $X -ge $w -or $Y -ge $h){ continue }
            $out.SetPixel($X,$Y,(Invoke-Relight $srcD $X $Y $MS $NewAlpha $PlateAlpha))
          }
        }
      }
    }
  }
  $sheet=New-Object System.Drawing.Bitmap($w,($h+30),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $sg=[System.Drawing.Graphics]::FromImage($sheet)
  $sg.Clear([System.Drawing.Color]::FromArgb(255,18,20,24))
  $sg.DrawImage($out,0,30)
  $sg.DrawString($Label,$Font,[System.Drawing.Brushes]::White,10,5)
  $sg.Dispose(); $out.Dispose()
  $sheet
}

function New-ZoomPanel {
  param($SrcBmp,[hashtable]$MS,$NewAlpha,[double]$PlateAlpha,$Crop,$Zoom,[string]$Label,[System.Drawing.Font]$Font,[bool]$Apply)
  $cw=[int]($Crop[2]); $ch=[int]($Crop[3])
  $base=New-Object System.Drawing.Bitmap($cw,$ch,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bg=[System.Drawing.Graphics]::FromImage($base)
  $bg.DrawImage($SrcBmp,(New-Object System.Drawing.Rectangle(0,0,$cw,$ch)),
                (New-Object System.Drawing.Rectangle([int]$Crop[0],[int]$Crop[1],$cw,$ch)),
                [System.Drawing.GraphicsUnit]::Pixel)
  $bg.Dispose()
  if($Apply){
    for($py=0;$py -lt $ch;$py++){
      for($px=0;$px -lt $cw;$px++){
        $X=[int]$Crop[0]+$px; $Y=[int]$Crop[1]+$py
        $base.SetPixel($px,$py,(Invoke-Relight $SrcBmp $X $Y $MS $NewAlpha $PlateAlpha))
      }
    }
  }
  $zw=$cw*$Zoom; $zh=$ch*$Zoom
  $panel=New-Object System.Drawing.Bitmap($zw,($zh+30),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $pg=[System.Drawing.Graphics]::FromImage($panel)
  $pg.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $pg.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
  $pg.Clear([System.Drawing.Color]::FromArgb(255,18,20,24))
  $pg.DrawImage($base,(New-Object System.Drawing.Rectangle(0,30,$zw,$zh)))
  $pg.DrawString($Label,$Font,[System.Drawing.Brushes]::White,10,5)
  $pg.Dispose(); $base.Dispose()
  $panel
}

"Loaded SlotPlateV6 functions."