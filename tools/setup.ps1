<#
.SYNOPSIS
  Bootstrap a fresh clone: download MelonLoader and Il2CppDumper into tools\, install MelonLoader into the game,
  and optionally regenerate the IL2CPP dump in re\il2cpp-dump (needed after game updates).
.EXAMPLE
  pwsh -File tools\setup.ps1          # tools + MelonLoader in the game
  pwsh -File tools\setup.ps1 -Dump    # also regenerate re\il2cpp-dump
  pwsh -File tools\setup.ps1 -Pools   # regenerate src\LabyHelper\pools.default.json from the game files (needs the dump)
#>
param(
    [switch]$Dump,
    [switch]$Pools,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine',
    [string]$MelonVersion = 'v0.7.3',
    [string]$DumperVersion = 'v6.7.46'
)
$ErrorActionPreference = 'Stop'
$root  = Split-Path $PSScriptRoot -Parent
$ml    = Join-Path $PSScriptRoot 'melonloader'
$dumpr = Join-Path $PSScriptRoot 'il2cppdumper'

New-Item -ItemType Directory -Force $ml, $dumpr | Out-Null
$mlZip = Join-Path $ml 'MelonLoader.x64.zip'
if (-not (Test-Path $mlZip)) {
    Write-Host "Descargando MelonLoader $MelonVersion..."
    Invoke-WebRequest "https://github.com/LavaGang/MelonLoader/releases/download/$MelonVersion/MelonLoader.x64.zip" -OutFile $mlZip
}

if (-not (Test-Path (Join-Path $GameDir 'version.dll'))) {
    if (Get-Process Labyrinthine -ErrorAction SilentlyContinue) { throw 'Cerrá el juego antes de instalar MelonLoader.' }
    Write-Host 'Instalando MelonLoader en el juego...'
    Expand-Archive $mlZip -DestinationPath $GameDir -Force
    Write-Host 'Abrí el juego una vez para que MelonLoader genere Il2CppAssemblies (necesarios para compilar).'
}

if ($Dump) {
    $exe = Join-Path $dumpr 'Il2CppDumper.exe'
    if (-not (Test-Path $exe)) {
        Write-Host "Descargando Il2CppDumper $DumperVersion..."
        $zip = Join-Path $dumpr 'dumper.zip'
        Invoke-WebRequest "https://github.com/Perfare/Il2CppDumper/releases/download/$DumperVersion/Il2CppDumper-win-$DumperVersion.zip" -OutFile $zip
        Expand-Archive $zip -DestinationPath $dumpr -Force
        (Get-Content "$dumpr\config.json") -replace '"RequireAnyKey": true', '"RequireAnyKey": false' | Set-Content "$dumpr\config.json"
    }
    $out = Join-Path $root 're\il2cpp-dump'
    New-Item -ItemType Directory -Force $out | Out-Null
    Remove-Item "$out\names.pickle" -ErrorAction SilentlyContinue  # disasm.py cache, tied to the old binary
    $env:DOTNET_ROLL_FORWARD = 'Major'
    & $exe "$GameDir\GameAssembly.dll" "$GameDir\Labyrinthine_Data\il2cpp_data\Metadata\global-metadata.dat" $out
    Write-Host "Dump regenerado en $out"
}

if ($Pools) {
    # Offline extraction of every map's cosmetic pools (UnityPy + generated type trees from DummyDll).
    python -m pip install --quiet UnityPy TypeTreeGeneratorAPI
    python (Join-Path $root 're\scripts\extract_pools.py')
    if ($LASTEXITCODE) { throw 'Falló la extracción de pools.' }
}
