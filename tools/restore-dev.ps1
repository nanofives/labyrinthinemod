<#
.SYNOPSIS
  Put back a dev install backed up from the game folder (MelonLoader + generated Il2CppAssemblies, Mods, UserData).
  Replaces whatever is installed there now (e.g. after testing the public installer).
.EXAMPLE
  pwsh -File tools\restore-dev.ps1                       # latest backup in backups\dev-install-*
  pwsh -File tools\restore-dev.ps1 -From backups\dev-install-2026-10-09
#>
param(
    [string]$From,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $From) {
    $From = Get-ChildItem (Join-Path $root 'backups') -Directory -Filter 'dev-install-*' | Sort-Object Name | Select-Object -Last 1 -ExpandProperty FullName
}
if (-not $From -or -not (Test-Path $From)) { throw 'No hay backup de instalación de desarrollo.' }
if (Get-Process Labyrinthine -ErrorAction SilentlyContinue) { throw 'Cerrá el juego primero.' }

foreach ($item in 'MelonLoader', 'Mods', 'UserData', 'UserLibs', 'Plugins', 'version.dll', 'LabyHelper-LEEME.txt') {
    $p = Join-Path $GameDir $item
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
Copy-Item (Join-Path $From '*') $GameDir -Recurse -Force
Write-Host "Instalación de desarrollo restaurada desde $From"
