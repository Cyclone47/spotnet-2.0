$f = Get-Item 'D:\sourcecode\newbranding\spotnet.ico'
Write-Host "File: $($f.FullName)"
Write-Host "Size: $($f.Length) bytes"

$bytes    = [System.IO.File]::ReadAllBytes($f.FullName)
$reserved = [BitConverter]::ToUInt16($bytes, 0)
$type     = [BitConverter]::ToUInt16($bytes, 2)
$count    = [BitConverter]::ToUInt16($bytes, 4)
Write-Host "ICO header: reserved=$reserved, type=$type, images=$count"

for ($i = 0; $i -lt $count; $i++) {
    $base = 6 + $i * 16
    $w    = $bytes[$base];   if ($w -eq 0) { $w = 256 }
    $h    = $bytes[$base+1]; if ($h -eq 0) { $h = 256 }
    $bpp  = [BitConverter]::ToUInt16($bytes, $base + 6)
    $sz   = [BitConverter]::ToUInt32($bytes, $base + 8)
    Write-Host "  Image $($i+1): ${w}x${h}, $bpp bpp, $sz bytes"
}
