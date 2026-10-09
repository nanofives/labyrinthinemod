<#
.SYNOPSIS
  Builds LabyHelper and produces the distributables in dist\:
    LabyHelper-Setup.exe   one-file installer (finds the game via Steam, backs up saves, unpacks everything)
    LabyHelper-manual.zip  same payload for manual install (extract into the game folder)
.NOTES
  Building the mod needs this machine's game install (it references MelonLoader's generated Il2CppAssemblies).
#>
param(
    [string]$MelonZip = "$PSScriptRoot\melonloader\MelonLoader.x64.zip"
)
$ErrorActionPreference = 'Stop'
$root    = Split-Path $PSScriptRoot -Parent
$dist    = Join-Path $root 'dist'
$staging = Join-Path $dist 'staging'

Write-Host '1/4 Compilando el mod...'
dotnet build "$root\src\LabyHelper\LabyHelper.csproj" -c Release --nologo -v q | Out-Host
if ($LASTEXITCODE) { throw 'Falló el build del mod.' }
$modDll = "$root\src\LabyHelper\bin\Release\net6.0\LabyHelper.dll"

Write-Host '2/4 Armando el paquete (MelonLoader + mod)...'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force $staging, "$staging\Mods" | Out-Null
Expand-Archive $MelonZip -DestinationPath $staging
Copy-Item $modDll "$staging\Mods\"
Copy-Item "$root\src\Installer\LEEME.txt" "$staging\LabyHelper-LEEME.txt"

$payload = Join-Path $dist 'payload.zip'
$manual  = Join-Path $dist 'LabyHelper-manual.zip'
Remove-Item $payload, $manual -ErrorAction SilentlyContinue
Compress-Archive "$staging\*" $payload
Copy-Item $payload $manual

Write-Host '3/4 Compilando el instalador...'
dotnet build "$root\src\Installer\LabyHelperSetup.csproj" -c Release --nologo -v q "-p:PayloadZip=$payload" | Out-Host
if ($LASTEXITCODE) { throw 'Falló el build del instalador.' }
Copy-Item "$root\src\Installer\bin\Release\net48\LabyHelper-Setup.exe" $dist -Force

Write-Host '4/4 Limpiando...'
Remove-Item $staging -Recurse -Force
Remove-Item $payload

Get-ChildItem $dist | Select-Object Name, @{n='MB'; e={[math]::Round($_.Length / 1MB, 1)}} | Format-Table -AutoSize
