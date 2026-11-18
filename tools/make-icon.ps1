Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::Transparent)

# фон — круг с вертикальным градиентом
$rect = New-Object System.Drawing.Rectangle 8, 8, ($size - 16), ($size - 16)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
    ([System.Drawing.Color]::FromArgb(255, 76, 148, 255)),
    ([System.Drawing.Color]::FromArgb(255, 44, 92, 214)), 90
$g.FillEllipse($brush, $rect)

# земной шар — светлая дуга
$penGlobe = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 255, 255, 255)), 10
$g.DrawEllipse($penGlobe, 40, 40, 176, 176)
$g.DrawArc($penGlobe, 96, 40, 64, 176, 90, 180)

# надпись IP
$font = New-Object System.Drawing.Font 'Segoe UI', 92, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'
$fmt.LineAlignment = 'Center'
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$textRect = New-Object System.Drawing.RectangleF 0, 6, $size, $size
$g.DrawString('IP', $font, $white, $textRect, $fmt)

$g.Dispose()

$pngStream = New-Object System.IO.MemoryStream
$bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $pngStream.ToArray()

# собираем .ico с PNG-кадрами нужных размеров
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $resized = New-Object System.Drawing.Bitmap $s, $s
    $rg = [System.Drawing.Graphics]::FromImage($resized)
    $rg.InterpolationMode = 'HighQualityBicubic'
    $rg.SmoothingMode = 'AntiAlias'
    $rg.DrawImage($bmp, 0, 0, $s, $s)
    $rg.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @{ Size = $s; Data = $ms.ToArray() }
    $resized.Dispose()
    $ms.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type = icon
$bw.Write([UInt16]$frames.Count)

$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([Byte]$dim)     # width
    $bw.Write([Byte]$dim)     # height
    $bw.Write([Byte]0)        # colors
    $bw.Write([Byte]0)        # reserved
    $bw.Write([UInt16]1)      # planes
    $bw.Write([UInt16]32)     # bpp
    $bw.Write([UInt32]$f.Data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $bw.Write($f.Data) }
$bw.Flush()

New-Item -ItemType Directory -Force -Path 'D:\GitHub\IPWatch\Assets' | Out-Null
[System.IO.File]::WriteAllBytes('D:\GitHub\IPWatch\Assets\app.ico', $out.ToArray())

$bw.Dispose(); $out.Dispose(); $pngStream.Dispose(); $bmp.Dispose()
Write-Output ("icon bytes: " + (Get-Item 'D:\GitHub\IPWatch\Assets\app.ico').Length)
