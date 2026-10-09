<#
.SYNOPSIS
  Dev loop: build the mod, deploy it to the game's Mods folder, (re)launch through Steam and show the mod's log.
.EXAMPLE
  pwsh -File tools\dev.ps1            # build + deploy; launch if the game is closed
  pwsh -File tools\dev.ps1 -Restart   # close the running game first so the new DLL loads
  pwsh -File tools\dev.ps1 -NoLaunch  # build + deploy only
#>
param(
    [switch]$Restart,
    [switch]$NoLaunch,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$log  = Join-Path $GameDir 'MelonLoader\Latest.log'

$running = Get-Process Labyrinthine -ErrorAction SilentlyContinue
if ($running -and $Restart) {
    Write-Host 'Cerrando el juego...'
    $null = $running.CloseMainWindow()
    if (-not $running.WaitForExit(15000)) { $running | Stop-Process -Force }
    $running = $null
    Start-Sleep 2
}
if ($running) { Write-Warning 'El juego está abierto: el DLL está bloqueado. Usá -Restart para cerrarlo y recargar.' }

Write-Host 'Compilando...'
dotnet build "$root\src\LabyHelper\LabyHelper.csproj" -c Release --nologo -v q "-p:GameDir=$GameDir" | Out-Host
if ($LASTEXITCODE) { throw 'Falló el build.' }

$built    = "$root\src\LabyHelper\bin\Release\net6.0\LabyHelper.dll"
$deployed = Join-Path $GameDir 'Mods\LabyHelper.dll'
$same = (Test-Path $deployed) -and ((Get-FileHash $built).Hash -eq (Get-FileHash $deployed).Hash)
Write-Host ($same ? 'Deploy OK: Mods\LabyHelper.dll actualizado.' : 'Deploy pendiente: cerrá el juego y volvé a correr.')

if ($NoLaunch -or $running) { return }

Write-Host 'Lanzando por Steam...'
$start = Get-Date
Start-Process 'steam://rungameid/1302240'
$deadline = $start.AddMinutes(3)
while ((Get-Date) -lt $deadline) {
    Start-Sleep 3
    if ((Test-Path $log) -and (Get-Item $log).LastWriteTime -gt $start -and (Select-String -Path $log -Pattern 'LabyHelper\] Loaded' -Quiet)) { break }
}
Select-String -Path $log -Pattern '\[LabyHelper\]|\[ERROR\]|Exception' | ForEach-Object { $_.Line }
Write-Host "`nPara seguir el log en vivo: pwsh -File tools\log.ps1"
