#requires -Version 5.1
<#
.SYNOPSIS
  Builds a self-contained QuickTranslator.exe and wraps it in an installer (dist\QuickTranslator-Setup-<version>.exe).
.PARAMETER Version
  Version number shown in the installer.
.PARAMETER SkipInstaller
  Only produce the self-contained exe in publish-installer\ (no Inno Setup needed).
#>
param(
    [string]$Version = "1.0.0",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishDir = Join-Path $root "publish-installer"
$installerDir = Join-Path $root "installer"

# 1) icon for the installer, generated from the same drawing the app uses
$iconPath = Join-Path $installerDir "app.ico"
if (-not (Test-Path $iconPath)) {
    Add-Type -AssemblyName System.Drawing
    $size = 256
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 56; $d = $r * 2; $m = 8; $w = $size - 2 * $m
    $path.AddArc($m, $m, $d, $d, 180, 90); $path.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
    $path.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90); $path.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.Rectangle 0, 0, $size, $size), [System.Drawing.Color]::FromArgb(66, 133, 244), [System.Drawing.Color]::FromArgb(40, 90, 200), 90.0)
    $g.FillPath($brush, $path)
    $font = New-Object System.Drawing.Font "Microsoft YaHei UI", 96, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString([string]::Concat([char]0x6587, "A"), $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 8, $size, $size), $fmt)
    $g.Dispose()

    # PNG-compressed single-image .ico
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $fs = [System.IO.File]::Create($iconPath)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$png.Length); $bw.Write([uint32]22)
    $bw.Write($png); $bw.Close(); $fs.Close()
    Write-Host "Created $iconPath"
}

# 2) self-contained single-file build (no .NET runtime needed on the target PC)
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root "QuickTranslator.csproj") -c Release -r win-x64 -o $publishDir `
    -p:PublishSingleFile=true --self-contained true `
    -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $publishDir -Filter *.pdb | Remove-Item -Force

$exe = Join-Path $publishDir "QuickTranslator.exe"
Write-Host ("Self-contained exe: {0} ({1:N1} MB)" -f $exe, ((Get-Item $exe).Length / 1MB))

if ($SkipInstaller) { return }

# 3) installer
$candidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

if (-not $candidates) {
    throw "Inno Setup 6 not found. Install it (winget install JRSoftware.InnoSetup) and run this script again, or use -SkipInstaller."
}

& ($candidates | Select-Object -First 1) "/DMyAppVersion=$Version" "/DSourceDir=$publishDir" (Join-Path $installerDir "QuickTranslator.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

$setup = Get-ChildItem (Join-Path $root "dist") -Filter "QuickTranslator-Setup-$Version.exe" | Select-Object -First 1
Write-Host ("Installer: {0} ({1:N1} MB)" -f $setup.FullName, ($setup.Length / 1MB))
