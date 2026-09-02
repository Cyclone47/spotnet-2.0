Add-Type -AssemblyName System.Drawing

$sizes   = @(256, 128, 64, 48, 32, 16)
$srcPath = Join-Path $PSScriptRoot 'spotnet_icon.png'
$dstPath = Join-Path $PSScriptRoot 'spotnet.ico'

$src = [System.Drawing.Image]::FromFile($srcPath)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICO header: reserved=0, type=1 (ICO), image count
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

# First image data offset: 6-byte header + 16-byte directory entry per image
$offset = 6 + 16 * $sizes.Count

# Render each size to PNG bytes
$imageData = @()
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $size, $size)
    $g.Dispose()

    $imgMs = New-Object System.IO.MemoryStream
    $bmp.Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $imageData += ,$imgMs.ToArray()
    $imgMs.Dispose()
}

# Write ICONDIRENTRY for each image
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz    = $sizes[$i]
    $bytes = $imageData[$i]

    # width / height: 0 means 256 in the ICO spec
    $bw.Write([byte]$(if ($sz -eq 256) { 0 } else { $sz }))
    $bw.Write([byte]$(if ($sz -eq 256) { 0 } else { $sz }))
    $bw.Write([byte]0)          # color count (0 = no palette)
    $bw.Write([byte]0)          # reserved
    $bw.Write([uint16]1)        # color planes
    $bw.Write([uint16]32)       # bits per pixel
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}

# Write image data blobs
foreach ($bytes in $imageData) {
    $bw.Write($bytes, 0, $bytes.Length)
}

$bw.Flush()
$src.Dispose()

[System.IO.File]::WriteAllBytes($dstPath, $ms.ToArray())

$info = Get-Item $dstPath
Write-Host "SUCCESS: $dstPath ($($info.Length) bytes)"


