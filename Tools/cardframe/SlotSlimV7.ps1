# 卡槽「收细」v7 —— 底板亮边收窄减淡 + 线层由双线改为单细线
#
# RGB 一律纯白，全部结构写在 alpha 通道：slotImage.color 的状态色（黄高亮 / 绿弃牌 / 紫囚牢 / 黑封锁）
# 照旧原样相乘，运行时逻辑一行不用动。线层沿【底板真实轮廓】取等距，保证与底板同心同形。
#
# 底板公式与已装的 v6（Tools/cardframe/SlotPlateV6.ps1 的 New-SlotAlphaMap）逐字一致，
# 唯一差别是亮边参数（RimBand / RimA）—— 这样「收细」以外的像素完全不变。
Add-Type -AssemblyName System.Drawing

$script:REF = Join-Path $env:TEMP 'slot-backup-20260923-185945\SlotPlate.OLD.png'

function Get-Mask {
  # 轮廓取自原始占位图（纯圆角矩形，alpha 184 / 0）—— 底板与线层共用同一套边界
  param([string]$RefPath)
  $bmp=[System.Drawing.Bitmap]::FromFile($RefPath)
  $w=$bmp.Width; $h=$bmp.Height
  $bd=$bmp.LockBits([System.Drawing.Rectangle]::new(0,0,$w,$h),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $raw=New-Object 'byte[]' ($bd.Stride*$h)
  [System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0,$raw,0,$raw.Length)
  $st=$bd.Stride
  $bmp.UnlockBits($bd); $bmp.Dispose()
  $msk=New-Object 'bool[]' ($w*$h)
  $top=New-Object 'int[]' $w; $bot=New-Object 'int[]' $w
  $lef=New-Object 'int[]' $h; $rig=New-Object 'int[]' $h
  for($x=0;$x -lt $w;$x++){ $top[$x]=-1; $bot[$x]=-1 }
  for($y=0;$y -lt $h;$y++){ $lef[$y]=-1; $rig[$y]=-1 }
  for($y=0;$y -lt $h;$y++){
    for($x=0;$x -lt $w;$x++){
      if($raw[$y*$st+$x*4+3] -gt 100){
        $msk[$y*$w+$x]=$true
        if($top[$x] -lt 0){ $top[$x]=$y }
        $bot[$x]=$y
        if($lef[$y] -lt 0){ $lef[$y]=$x }
        $rig[$y]=$x
      }
    }
  }
  @{ W=$w;H=$h;Msk=$msk;Top=$top;Bot=$bot;Lef=$lef;Rig=$rig }
}

function Get-MinDist {
  # 每个内点到四条边的最短距离（线层取等距用）
  param($M)
  $w=$M.W;$h=$M.H
  $out=New-Object 'int[]' ($w*$h)
  for($y=0;$y -lt $h;$y++){
    $lx=$M.Lef[$y]; $rx=$M.Rig[$y]
    for($x=0;$x -lt $w;$x++){
      $i=$y*$w+$x
      if(-not $M.Msk[$i]){ $out[$i]=-1; continue }
      $v=$y-$M.Top[$x]
      $t=$M.Bot[$x]-$y; if($t -lt $v){$v=$t}
      $t=$x-$lx;        if($t -lt $v){$v=$t}
      $t=$rx-$x;        if($t -lt $v){$v=$t}
      $out[$i]=$v
    }
  }
  $out
}

function New-PlateAlpha {
  # 与 v6 的 New-SlotAlphaMap 同式：亮边带 + 上/左内影 + 下/右受光
  param($M,[double]$Base,[double]$RimBand,[double]$RimA,[double]$BT,[double]$BL,[double]$BB,[double]$BR)
  $w=$M.W;$h=$M.H
  $out=New-Object 'double[]' ($w*$h)
  for($y=0;$y -lt $h;$y++){
    $lx=$M.Lef[$y]; $rx=$M.Rig[$y]
    for($x=0;$x -lt $w;$x++){
      $i=$y*$w+$x
      if(-not $M.Msk[$i]){ $out[$i]=0.0; continue }
      $dT=$y-$M.Top[$x]; $dB=$M.Bot[$x]-$y
      $dL=$x-$lx;        $dR=$rx-$x
      $dm=$dT
      if($dB -lt $dm){$dm=$dB}
      if($dL -lt $dm){$dm=$dL}
      if($dR -lt $dm){$dm=$dR}
      if($dm -lt $RimBand){ $a=$RimA }
      else {
        $a=$Base
        $t=1.0-($dT-$RimBand)/$BT; if($t -gt 1){$t=1.0}; if($t -gt 0){ $a-=0.34*$t }
        $t=1.0-($dL-$RimBand)/$BL; if($t -gt 1){$t=1.0}; if($t -gt 0){ $a-=0.14*$t }
        $t=1.0-($dB-$RimBand)/$BB; if($t -gt 1){$t=1.0}; if($t -gt 0){ $a+=0.20*$t }
        $t=1.0-($dR-$RimBand)/$BR; if($t -gt 1){$t=1.0}; if($t -gt 0){ $a+=0.09*$t }
      }
      if($a -lt 0){$a=0.0}; if($a -gt 1){$a=1.0}
      $out[$i]=$a
    }
  }
  $out
}

function New-EdgeAlpha {
  # Bands: 展平的 3 元组 —— 中心 inset, 半厚, alpha（嵌套数组会被 PowerShell 拍平，故用平铺）
  param($M,[double[]]$Bands)
  $dm=Get-MinDist $M
  $out=New-Object 'double[]' ($M.W*$M.H)
  for($i=0;$i -lt $out.Length;$i++){
    $d=$dm[$i]
    if($d -lt 0){ $out[$i]=0.0; continue }
    $a=0.0
    for($k=0;$k+2 -lt $Bands.Count;$k+=3){
      $c=$Bands[$k]; $hw=$Bands[$k+1]; $al=$Bands[$k+2]
      if($d -ge $c-$hw -and $d -le $c+$hw){ if($al -gt $a){ $a=$al } }
    }
    $out[$i]=$a
  }
  $out
}

function Save-Alpha {
  param($M,$Alpha,[string]$Out)
  $w=$M.W;$h=$M.H
  $bmp=New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bd=$bmp.LockBits([System.Drawing.Rectangle]::new(0,0,$w,$h),[System.Drawing.Imaging.ImageLockMode]::WriteOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $dst=New-Object 'byte[]' ($bd.Stride*$h)
  for($y=0;$y -lt $h;$y++){
    for($x=0;$x -lt $w;$x++){
      $a=$Alpha[$y*$w+$x]
      $v=[int][Math]::Round(255.0*$a)
      if($v -lt 0){$v=0}; if($v -gt 255){$v=255}
      $o=$y*$bd.Stride+$x*4
      $dst[$o]=255; $dst[$o+1]=255; $dst[$o+2]=255; $dst[$o+3]=[byte]$v
    }
  }
  [System.Runtime.InteropServices.Marshal]::Copy($dst,0,$bd.Scan0,$dst.Length)
  $bmp.UnlockBits($bd)
  $bmp.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

"SlotSlimV7 loaded. REF=$script:REF"