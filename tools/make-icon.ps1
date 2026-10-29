#requires -Version 5.1
<#
.SYNOPSIS
  Generates app.ico (the application/installer icon) with the same drawing as the tray icon in IconFactory.cs:
  a blue rounded square with the text "文A". Run it again only if you change the design.
#>
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$out = Join-Path (Split-Path $PSScriptRoot -Parent) "app.ico"
$sizes = 16, 24, 32, 48, 64, 128, 256

function New-IconBitmap([int]$size) {
    $scale = $size / 64.0   # IconFactory designs the icon on a 64px canvas
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $m = 2 * $scale; $w = $size - 2 * $m; $r = 14 * $scale; $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, $d, $d, 180, 90)
    $path.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
    $path.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90)
    $path.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(66, 133, 244)), ([System.Drawing.Color]::FromArgb(40, 90, 200)), 90.0
    $g.FillPath($brush, $path)

    $font = New-Object System.Drawing.Font "Microsoft YaHei UI", (24 * $scale), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $text = [string]::Concat([char]0x6587, "A")
    $g.DrawString($text, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, (2 * $scale), $size, $size), $fmt)
    $g.Dispose()
    return $bmp
}

# image data for one ICO entry: PNG for 256px, classic 32-bit DIB (+AND mask) for the smaller sizes
function Get-IconImageData([System.Drawing.Bitmap]$bmp) {
    $size = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    if ($size -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return $ms.ToArray()
    }

    $bw = New-Object System.IO.BinaryWriter $ms
    $maskRow = [int]([Math]::Floor(($size + 31) / 32) * 4)
    $bw.Write([uint32]40); $bw.Write([int32]$size); $bw.Write([int32]($size * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($size * $size * 4 + $maskRow * $size))
    $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)

    $rectAll = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $bmp.LockBits($rectAll, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($size * $size * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bmp.UnlockBits($data)
    for ($y = $size - 1; $y -ge 0; $y--) { $bw.Write($bytes, $y * $size * 4, $size * 4) }   # DIB rows are bottom-up
    $bw.Write((New-Object byte[] ($maskRow * $size)))                                        # AND mask: all zero, alpha channel does the work
    $bw.Flush()
    return $ms.ToArray()
}

$images = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    [pscustomobject]@{ Size = $s; Data = [byte[]](Get-IconImageData $bmp) }   # cast: PowerShell unrolls the returned array into object[]
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }     # 0 means 256 in the ICO directory
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$img.Data.Length); $bw.Write([uint32]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $bw.Write($img.Data) }
$bw.Close(); $fs.Close()

Write-Host ("Created {0} ({1} sizes, {2:N1} KB)" -f $out, $images.Count, ((Get-Item $out).Length / 1KB))
