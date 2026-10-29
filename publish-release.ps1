#requires -Version 5.1
<#
.SYNOPSIS
  Builds the installer for <Version> and publishes it as a GitHub Release (tag v<Version>).
  Installed copies of the app find it through "Check for updates…" / the check at startup.
.EXAMPLE
  .\publish-release.ps1 -Version 1.1.0 -Notes "Added custom shortcut"
.NOTES
  Needs the GitHub CLI (winget install GitHub.cli) and `gh auth login`, run from a clone of the repository.
  The version must be higher than the previous release, and AppInfo.GitHubRepo must point at this repository.
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI not found. Install it with: winget install GitHub.cli  (then: gh auth login)"
}

& (Join-Path $root "build-installer.ps1") -Version $Version
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$setup = Join-Path $root "dist\QuickTranslator-Setup-$Version.exe"
if (-not (Test-Path $setup)) { throw "Installer not found: $setup" }

gh release create "v$Version" $setup --title "Quick Translator $Version" --notes $Notes
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
Write-Host "Published release v$Version"
