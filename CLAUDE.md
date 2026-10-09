# Labyrinthine / LabyHelper

Mod de MelonLoader 0.7.3 para Labyrinthine (Unity 2022.3 IL2CPP, HDRP, Mirror). Muestra información oculta y
agrega calidad de vida. Hallazgos de ingeniería inversa en `re/FINDINGS.md`; leelo antes de tocar un hook.

## Flujo de trabajo (Mariano)

1. **Desarrollo:** Claude implementa el primer acercamiento. `pwsh -File tools\dev.ps1 -Restart` compila,
   copia a `Mods\`, cierra y relanza el juego por Steam y muestra el log. Cada cambio visible va a
   `CHANGELOG.md` bajo `## [Sin publicar]`.
2. **Prueba:** Mariano juega y prueba con `TESTING.md`. `pwsh -File tools\log.ps1` sigue el log en vivo.
3. **Release:** solo cuando Mariano lo pide. `pwsh -File tools\release.ps1 -Version X.Y.Z [-Commit]` sube la
   versión (`Directory.Build.props`), cierra la sección del CHANGELOG y genera `dist\LabyHelper-Setup-X.Y.Z.exe`
   y `dist\LabyHelper-X.Y.Z-manual.zip`.

No hacer release ni commits sin que lo pida.

## Estructura
- `src/LabyHelper/`: el mod. `Features/` un archivo por función; `Settings.cs` todas las opciones. Por pedido de
  Mariano todo viene prendido por defecto. La regla de casos random (`RandomCasesOnlyNew`: siempre cosmético y
  solo nuevos, repartidos entre las 4 carpetas por `Distribution.cs`) solo se apaga desde Opciones LabyHelper
  (`OptionsMenu.cs`, F1); no le agregues tecla rápida. Reemplazó a la regla de monstruos mínimos.
- Grupo: `Party.cs` comparte los desbloqueados por Steam lobby member data (`lh_owned`). El juego solo manda al
  host los cosméticos equipados, así que los demás jugadores necesitan el mod.
- `src/Installer/`: instalador .NET Framework 4.8 con el paquete embebido.
- `re/`: `FINDINGS.md`, `scripts/disasm.py <RVA>`, `scripts/xrefs.py <RVA...>`, `scripts/findwrite.py <nombre> <offset>`.
  `re/il2cpp-dump/` no se versiona; se regenera con `tools\setup.ps1 -Dump` (también después de cada update del juego).
- `tools/`: `setup.ps1` (bootstrap), `dev.ps1`, `log.ps1`, `package.ps1`, `release.ps1`.

## Experimentos
- `Features/ForeignPieces.cs` (piezas de otro mapa) es no-go (paredes invisibles, ver re/EXPERIMENT-foreign-pieces.md).
  No se compila salvo con `-p:IncludeExperiments=true` (define `EXPERIMENTS`); los releases no lo incluyen.

## Datos del juego incluidos
- `src/LabyHelper/pools.default.json` (embebido en el DLL): pools de cosméticos de los 19 mapas extraídos de los
  assets con `re/scripts/extract_pools.py`. Regenerar después de cada update del juego:
  `pwsh -File tools\setup.ps1 -Dump -Pools`. Verificado idéntico a lo que el juego carga en Maze_N.

## Gotchas
- Compilar requiere el juego instalado con MelonLoader ya ejecutado una vez (referencia `MelonLoader\Il2CppAssemblies`).
- El juego bloquea `Mods\LabyHelper.dll` mientras corre: usar `dev.ps1 -Restart`.
- Tipos IL2CPP sin namespace quedan en `Il2Cpp.*` (GameValues, MazeGenerator, WeightedPrefab, AnimationState).
- Heredocs de bash con `\n` dentro de strings de C# se convierten en saltos reales: editar con Edit, no con sed/python inline.
- `MelonPreferences.cfg` del juego guarda la configuración personal de Mariano; no pisarla.
