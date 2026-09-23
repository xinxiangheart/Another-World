param([string]$Out, [string[]]$Files, [int]$Cols = 7, [int]$Cell = 200)
Add-Type -AssemblyName System.Drawing
$rows = [Math]::Ceiling($Files.Count / $Cols)
$lab = 26
$bmp = New-Object System.Drawing.Bitmap ($Cols*$Cell), ($rows*($Cell+$lab))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(20,24,32))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font('Consolas',11)
for($i=0; $i -lt $Files.Count; $i++){
  $r = [Math]::Floor($i/$Cols); $c = $i % $Cols
  $x = $c*$Cell; $y = $r*($Cell+$lab)
  try {
    $img = [System.Drawing.Image]::FromFile($Files[$i])
    $s = [Math]::Min($Cell/$img.Width, $Cell/$img.Height)
    $w = [int]($img.Width*$s); $h = [int]($img.Height*$s)
    $g.DrawImage($img, $x + ($Cell-$w)/2, $y + $lab + ($Cell-$h)/2, $w, $h)
    $img.Dispose()
  } catch { $g.FillRectangle([System.Drawing.Brushes]::DarkRed, $x, $y+$lab, $Cell, $Cell) }
  $name = [System.IO.Path]::GetFileNameWithoutExtension($Files[$i])
  $g.DrawString($name, $font, [System.Drawing.Brushes]::White, $x+4, $y+4)
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"$Out  ($($Files.Count) files)"
