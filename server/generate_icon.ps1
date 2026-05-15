param([string]$ProjectDir)

# Normalize the path — remove quotes and trailing slashes injected by cmd
$ProjectDir = $ProjectDir.Trim('"').TrimEnd('\').TrimEnd('/')
if (-not $ProjectDir) { $ProjectDir = $PSScriptRoot }

Add-Type -AssemblyName System.Drawing

function New-NexusBitmap {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg   = [System.Drawing.Color]::FromArgb(255, 13, 13, 26)
    $cyan = [System.Drawing.Color]::FromArgb(255, 0, 229, 255)
    $dim  = [System.Drawing.Color]::FromArgb(80,  0, 229, 255)

    # Rounded background
    [int]$radius = [Math]::Max(2, [int]($size * 0.15))
    [int]$d      = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0,          0,           $d, $d, 180, 90)
    $path.AddArc($size - $d, 0,           $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d,  $d, $d,   0, 90)
    $path.AddArc(0,          $size - $d,  $d, $d,  90, 90)
    $path.CloseFigure()

    $bgBrush   = New-Object System.Drawing.SolidBrush($bg)
    $borderPen = New-Object System.Drawing.Pen($dim, 1)
    $cBrush    = New-Object System.Drawing.SolidBrush($cyan)
    $g.FillPath($bgBrush, $path)
    $g.DrawPath($borderPen, $path)

    # Letter N
    [int]$fontSize = [Math]::Max(6, [int]($size * 0.55))
    $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf   = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString("N", $font, $cBrush, $rect, $sf)

    # Corner nodes — uses explicit int variables to avoid array arithmetic issues
    if ($size -ge 32) {
        [int]$nr  = [Math]::Max(2, [int]($size * 0.09))
        [int]$mg  = [int]($size * 0.15)
        [int]$far = $size - $mg
        $nb = New-Object System.Drawing.SolidBrush($cyan)
        $g.FillEllipse($nb, $mg  - $nr, $mg  - $nr, $nr * 2, $nr * 2)
        $g.FillEllipse($nb, $far - $nr, $mg  - $nr, $nr * 2, $nr * 2)
        $g.FillEllipse($nb, $mg  - $nr, $far - $nr, $nr * 2, $nr * 2)
        $g.FillEllipse($nb, $far - $nr, $far - $nr, $nr * 2, $nr * 2)
        $nb.Dispose()
    }

    $bgBrush.Dispose()
    $borderPen.Dispose()
    $cBrush.Dispose()
    $font.Dispose()
    $g.Dispose()
    return $bmp
}

function Write-IcoFile {
    param([string]$outputPath, [int[]]$sizes)

    $images = @()
    foreach ($s in $sizes) {
        $bmp = New-NexusBitmap -size $s
        $ms  = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $entry = @{ Size = $s; Data = $ms.ToArray() }
        $images += $entry
        $bmp.Dispose()
        $ms.Dispose()
    }

    $fs     = New-Object System.IO.FileStream($outputPath, [System.IO.FileMode]::Create)
    $writer = New-Object System.IO.BinaryWriter($fs)

    # ICO header
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    # Directory entries
    [int]$dataOffset = 6 + 16 * $images.Count
    foreach ($img in $images) {
        [int]$s = $img.Size
        if ($s -ge 256) { [byte]$bval = 0 } else { [byte]$bval = [byte]$s }
        $writer.Write([byte]$bval)      # width
        $writer.Write([byte]$bval)      # height
        $writer.Write([byte]0)          # color count
        $writer.Write([byte]0)          # reserved
        $writer.Write([uint16]1)        # planes
        $writer.Write([uint16]32)       # bpp
        $writer.Write([uint32]$img.Data.Length)
        $writer.Write([uint32]$dataOffset)
        $dataOffset += $img.Data.Length
    }

    # Image data
    foreach ($img in $images) {
        $writer.Write([byte[]]$img.Data)
    }

    $writer.Flush()
    $writer.Dispose()
    $fs.Dispose()
}

$out = Join-Path $ProjectDir "nexus.ico"
Write-IcoFile -outputPath $out -sizes @(16, 32, 48)
Write-Host "Icon saved: $out"
