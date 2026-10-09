<#
.SYNOPSIS
  Follow the game's MelonLoader log, filtered to LabyHelper lines and errors.
.EXAMPLE
  pwsh -File tools\log.ps1         # live
  pwsh -File tools\log.ps1 -All    # every line, not just LabyHelper/errors
#>
param(
    [switch]$All,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine'
)
$log = Join-Path $GameDir 'MelonLoader\Latest.log'
$pattern = $All ? '.' : '\[LabyHelper\]|\[ERROR\]|Exception'
Get-Content $log -Wait -Tail 50 | Where-Object { $_ -match $pattern }
