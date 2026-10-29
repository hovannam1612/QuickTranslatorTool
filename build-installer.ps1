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

# 1) app icon (embedded in the exe and used by the installer); generated once by tools\make-icon.ps1 and kept in the repo
$iconPath = Join-Path $root "app.ico"
if (-not (Test-Path $iconPath)) { & (Join-Path $root "tools\make-icon.ps1") }

# 2) self-contained single-file build (no .NET runtime needed on the target PC)
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
# restore for win-x64 explicitly: an IDE restore without a runtime id leaves obj\project.assets.json without this target
dotnet restore (Join-Path $root "QuickTranslator.csproj") -r win-x64
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }
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
