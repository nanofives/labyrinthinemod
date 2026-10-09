<#
.SYNOPSIS
  Cut a release: bump the version, close the "Sin publicar" CHANGELOG section, build the installer and zip
  into dist\ with the version in their names, and (with -Commit) commit + tag in git.
.EXAMPLE
  pwsh -File tools\release.ps1 -Version 0.3.0
  pwsh -File tools\release.ps1 -Version 0.3.0 -Commit
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [switch]$Commit,
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
$root      = Split-Path $PSScriptRoot -Parent
$props     = Join-Path $root 'Directory.Build.props'
$changelog = Join-Path $root 'CHANGELOG.md'
$isGit     = Test-Path (Join-Path $root '.git')

if ($isGit -and -not $AllowDirty) {
    $dirty = git -C $root status --porcelain
    if ($dirty) { throw "Hay cambios sin commitear. Commiteá primero o usá -AllowDirty.`n$dirty" }
}
if ($isGit -and (git -C $root tag --list "v$Version")) { throw "El tag v$Version ya existe." }

# 1. CHANGELOG: the "Sin publicar" section becomes this version.
$text = Get-Content $changelog -Raw
$m = [regex]::Match($text, '(?s)## \[Sin publicar\]\s*(.*?)(?=\n## \[|\z)')
if (-not $m.Success -or -not $m.Groups[1].Value.Trim()) { throw 'CHANGELOG.md no tiene nada en "## [Sin publicar]".' }
$date = Get-Date -Format 'yyyy-MM-dd'
$text = $text.Remove($m.Index, $m.Length).Insert($m.Index, "## [Sin publicar]`n`n## [$Version] - $date`n`n$($m.Groups[1].Value.Trim())`n")
Set-Content $changelog $text -NoNewline

# 2. Version
(Get-Content $props -Raw) -replace '<LabyHelperVersion>[^<]+</LabyHelperVersion>', "<LabyHelperVersion>$Version</LabyHelperVersion>" |
    Set-Content $props -NoNewline
Write-Host "Versión $Version"

# 3. Build the distributables
& (Join-Path $PSScriptRoot 'package.ps1')
$dist = Join-Path $root 'dist'
Move-Item -Force "$dist\LabyHelper-Setup.exe"  "$dist\LabyHelper-Setup-$Version.exe"
Move-Item -Force "$dist\LabyHelper-manual.zip" "$dist\LabyHelper-$Version-manual.zip"

# 4. Git
if ($Commit -and $isGit) {
    git -C $root add -A
    git -C $root commit -m "Release $Version" | Out-Host
    git -C $root tag -a "v$Version" -m "LabyHelper $Version"
    Write-Host "Commit y tag v$Version creados."
}

Write-Host "`nListo para compartir:"
Get-ChildItem $dist -Filter "*$Version*" | ForEach-Object { "  $($_.FullName)" }
