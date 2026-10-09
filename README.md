# Labyrinthine: LabyHelper (MelonLoader)

Mod personal de calidad de vida: muestra información oculta sin cambiar la jugabilidad.

## Estructura
- `src/LabyHelper/`: código del mod (net6.0, MelonLoader 0.7.3). `dotnet build -c Release` compila y copia el DLL a `<juego>\Mods`.
- `re/`: ingeniería inversa. `FINDINGS.md` (clases y hooks), `il2cpp-dump/` (Il2CppDumper), `scripts/disasm.py <RVA>` (desensamblado anotado con nombres IL2CPP).
- `backups/`: copia de los saves antes de instalar el mod.
- `tools/`: MelonLoader e Il2CppDumper descargados.

## Distribuir
`pwsh -File tools\package.ps1` genera en `dist\`:
- `LabyHelper-Setup.exe`: instalador de un archivo (.NET Framework 4.8). Encuentra el juego vía Steam (registro + `libraryfolders.vdf` + `appmanifest_1302240.acf`), pide cerrar el juego, hace backup de saves en `Documentos\LabyHelper backups`, descomprime MelonLoader + mod. Flags: `--uninstall`, `--path <carpeta>`, `--yes`, `--detect`.
- `LabyHelper-manual.zip`: el mismo contenido para descomprimir a mano en la carpeta del juego.

Todo viene prendido por defecto (decisión de Mariano). Tu `MelonPreferences.cfg` conserva los valores que cambies.

## Instalación en el juego
MelonLoader 0.7.3 descomprimido en la carpeta del juego (`version.dll` + `MelonLoader/`). Para desinstalar: borrar `version.dll`, `MelonLoader/`, `Mods/`, `Plugins/`, `UserLibs/`, `UserData/`.

## Funciones
| Tecla | Acción |
|---|---|
| F6 | Brillo extra on/off |
| F7 / F8 | Exposición -/+ (0.25 EV) |
| Shift+F7 / Shift+F8 | Gamma -/+ (0.05) |
| F9 | Cosmético del caso con distancia y dirección, más los exclusivos posibles del mapa. También muestra 8 s el marcador sobre el cosmético (visible a través de paredes) |
| F1 | Opciones LabyHelper: todas las opciones; la dificultad solo se apaga acá (también botón en la pantalla de opciones del juego) |
| F3 | Pool del caso en vivo |
| F2 | Colección: tengo / faltan, tradicionales y HARDCORE (también botón en el menú de cosméticos) |
| F5 | Levantarse solo: alterna la opción; estando caído, te levanta al instante |
| F10 | Cicla el evento de temporada forzado: Off, Random, Halloween, Christmas, Easter, Valentines, StPatrick, Summer |

- **Cosmético del caso:** al empezar aparece nombre, rareza, pool (EXCLUSIVO DEL MAPA / pool común / temporada) y si ya lo tenés. Funciona como cliente.
- **Pigman:** el rango de su audio se multiplica (`PigmanAudioMultiplier`, 2.5 por defecto) y aparece un aviso rojo mientras persigue o carga, con distancia y dirección (`PigmanChaseAlert`). Funciona como cliente.
- **Temporada:** durante el spawn del cosmético el juego ve el evento elegido como activo. Solo tiene efecto si sos host. `SeasonalChance` (0 a 1) fuerza la probabilidad de que salga de temporada; -1 deja la del juego.

- **Tablero de casos:** cada carpeta muestra la probabilidad de que el cosmético sea nuevo para vos, la parte de temporada y los exclusivos del mapa con su probabilidad. El ítem exacto no se puede saber antes: depende de cuántos intentos tarda en generarse el laberinto. El pool de cada mapa se aprende al jugarlo (`UserData\LabyHelper.pools.json`).
- **Pool dinámico (host):** `PoolPreferNew` (F11) excluye del sorteo lo que ya tenés; `PoolAddAllSeasonal` suma los cosméticos de todos los eventos al pool del mapa (no en Hardcore). Solo usa prefabs ya cargados, así los clientes ven el mismo objeto.
- **Imágenes:** el aviso y el marcador muestran el ícono del cosmético (el render del modelo que usa el menú de personalización) con su marco de rareza.
- **Marcador en el mundo:** `MarkerMode` = Off / Hotkey (F9) / Always.

Valores guardados en `<juego>\UserData\MelonPreferences.cfg`, sección `[LabyHelper]`.
