# Experimento: piezas foráneas (piezas raras de otro mapa)

Fecha: 2026-10-09. Build del juego analizado: el de `FINDINGS.md` (28/06). Solo para jugar solo.
Código: `src/LabyHelper/Features/ForeignPieces.cs`, opciones `ExperimentForeignPieces` (false por defecto) y
`ExperimentDonorMaze` (`Maze_F` por defecto), sección "Experimental" en Opciones LabyHelper (F1).
Script offline: `re/scripts/inspect_pieces.py [--deep] [--scene-scripts] [Maze_X ...]` (escribe JSON en `re/scripts/out/`).

## 1. Cómo elige piezas el juego (verificado en el binario)

- `GetFloorPiece(x,y)` (RVA 0x807C30) lee `mazeTypeMap[x,y]` (@0x50) e indexa `possibleSubmazes` (@0x28).
  **El índice 0 es el laberinto principal**: no hay arrays de piezas "principales" fuera de `Submaze`. Las demás
  entradas son las zonas mezcladas. Los getters de straight/turn/threeway/endcap/safezone hacen lo mismo.
- Tirada: `Random.Range(0,100)` contra `rareGenerationChance` (@0x178). Si el array `*Rare` está vacío usa el normal.
  Después `Random.Range(0, len)`. Agrandar un array no cambia la cantidad de llamadas al RNG, solo qué pieza sale.
- `wallPieces`/`wallPiecesRare` (@0x40/@0x48), `startPieces`/`endPieces` (@0x180/@0x188) sí están en `MazeGenerator`.
- **`GenerateMap` se llama en cola desde `MazeGenerator.Start`** (`jmp` en 0x810F5C, sin otros llamadores). Corre en
  el primer frame de la escena del mapa y no puede esperar una carga asíncrona sin demorar todo lo demás
  (`WaitForNetwork`, spawn de jugadores). Por eso el prototipo **no** carga el donante dentro del mapa.

## 2. Datos offline (UnityPy + TypeTreeGenerator, los 19 mapas)

Cuentas del laberinto principal (sub0) en formato normales/raras.

| Mapa | Escena | tileSize | rotY | straight | endcap | 3way | turn | floor | walls (n+r) | rareChance | OcclusionSystem |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A | level17 | 10 | 0 | 8/8 | 5/11 | 10/5 | 8/8 | 2/0 | 0+0 | 1 | no |
| B | level21 | 10 | 0 | 17/4 | 8/8 | 12/3 | 13/4 | 1/0 | 0+0 | 1 | no |
| C | level23 | 10 | 0 | 9/3 | 10/3 | 11/1 | 10/2 | 3/0 | 0+0 | 1 | sí |
| D | level6 | **7** | 0 | 1/0 | 1/0 | 1/0 | 1/0 | 1/0 | 0+0 | 5 | sí |
| E | level22 | 10 | 0 | 3/7 | 1/4 | 3/2 | 3/2 | 3/2 | 1+2 | 1 | sí |
| F | level25 | 10 | 0 | 8/4 | 9/4 | 6/7 | 7/6 | 1/0 | 1+0 | 5 | sí |
| G | level29 | 10 | 0 | 13/2 | 17/1 | 14/0 | 15/0 | 1/0 | 0+0 | 1 | sí |
| H | level19 | 10 | 0 | 7/12 | 6/7 | 6/12 | 7/6 | 1/0 | 0+0 | 1 | sí |
| I | level20 | 10 | 0 | 5/8 | 2/6 | 6/5 | 3/3 | 6/4 | 0+0 | 1 | sí |
| J | level10 | 10 | 0 | 11/12 | 9/2 | 7/2 | 8/0 | 7/0 | 0+0 | 1 | sí |
| K | level14 | **30** | 0 | 7/0 | 5/0 | 5/1 | 5/0 | 7/0 | 0+0 | 5 | sí |
| L | level24 | 10 | 0 | 17/4 | 8/7 | 11/3 | 13/4 | 1/0 | 0+0 | 1 | no |
| M | level36 | 10 | 0 | 5/0 | 5/0 | 5/0 | 5/0 | 4/0 | 0+0 | 1 | sí |
| N | level37 | 10 | 0 | 3/26 | 10/26 | 10/26 | 10/26 | 3/26 | 3+0 | 5 | sí |
| O | level33 | **30** | **90** | 7/0 | 5/0 | 7/0 | 5/0 | 7/0 | 8+0 | 1 | sí |
| P | level34 | 10 | 0 | 8/2 | 5/3 | 6/2 | 6/2 | 1/0 | 1+0 | 1 | sí |
| Backrooms | level28 | 10 | 0 | 4/4 | 2/0 | 3/4 | 1/0 | 1/1 | 0+0 | 5 | sí |
| Q | level32 | **30** | 0 | 6/6 | 6/2 | 4/4 | 5/3 | 5/3 | 9+1 | 5 | sí |
| R | level5 | **21** | 0 | 7/0 | 5/0 | 7/0 | 5/0 | 6/0 | 0+0 | 1 | sí |

(En N los 5 arrays raros comparten los mismos 26 prefabs. `rareChance` es el valor de la escena, 1 o 5. MapVariety
lo sube a `MapRareChance`, 25 por defecto.)

- **tileSize:** 14 mapas usan 10. D=7, R=21, K/O/Q=30. O además rota 90°. Solo se mezclan mapas con el mismo
  tileSize y rotY. El prototipo lo comprueba en tiempo de ejecución con los valores reales.
- **Mapas sin raras propias** (D, K, M, O, R): en M (tileSize 10) *todas* las tiradas raras salen del donante.
  D, K, O y R quedan afuera por tileSize.
- **Addressables:** el catálogo (`StreamingAssets/aa/catalog.json`, 694 claves) no tiene piezas de laberinto. Solo
  cosméticos, audio, shaders. La única forma de tener los prefabs es cargar la escena del otro mapa.
- **Tamaño del donante por defecto** (F): `level25` 171 KB, `sharedassets25.assets` 24 MB + `.resS` 218 MB.

### Iluminación (la preocupación principal resultó baja)

- **Ninguna escena `Rand_Maze_*` tiene lightmaps** (`LightmapSettings.m_Lightmaps` vacío en las 19, sin LightProbes
  horneados). Lógico: el laberinto se arma en runtime.
- En las piezas, todos los renderers tienen `m_LightmapIndex = 65535` (sin lightmap) y casi todos
  `m_LightProbeUsage = BlendProbes`. Toman la luz ambiente/probes del mapa *actual*: deberían verse con la
  atmósfera del mapa que las recibe, no oscuras.
- Algunas piezas (A, B, C, G, H, I, L) traen **lightmaps de Bakery propios del prefab** (`BakeryLightmappedPrefab` +
  `ftLightmapsStorage` dentro del prefab). Bakery los aplica al instanciar: son portables por diseño. F no tiene.
- Las luces de las piezas son luces HDRP en tiempo real (`HDAdditionalLightData`). `BakeryPointLight` es el
  componente que Bakery usa al hornear.

### Scripts dentro de las piezas raras

| Script | Mapas | Riesgo en un mapa ajeno |
|---|---|---|
| `GPUInstancerPrefab` + `RuntimeHandler` | A, B, C, F, L, P | El `GPUInstancerPrefabManager` del mapa actual no conoce esos prototipos. GPUI sin manager deja el `MeshRenderer` normal: se ven, pero sin instancing (más draw calls). Probar FPS. |
| `PrefabOcclusionData` | casi todos | Datos para `OcclusionSystem` propio del juego. A, B y L no tienen `OcclusionSystem`. Riesgo: piezas que no se ocultan (rendimiento) o que se ocultan mal (pop-in). |
| `IntersectionActivator` | F, P, Backrooms | Desconocido. Probar que no tire errores y que la intersección se vea completa. |
| `RndCollectible(Spawner)` | A, B, C, E, G, H, I, J, K, L, N, P, Q | Coleccionables (tickets). Probablemente genéricos, pero podrían registrar ids de otro mapa. |
| `RndMonsterSpawner` | G (1), N (5) | **Spawnea monstruos.** No usar G ni N como donante hasta probarlo. |
| `PillarObject` | H (98) | Necesita `PillarManager` (solo en H). Probable NullReference. No usar H como donante. |
| `NavMeshModifier(Volume)` | casi todos | Bien: la navmesh se arma en runtime después de colocar las piezas. |

**Donante por defecto: Maze_F (maizal).** tileSize 10, 21 raras (straight 4, endcap 4, 3way 7, turn 6, floor 0,
wall 0), sin coleccionables, sin monstruos, sin lightmaps propios. Scripts: GPUI, `PrefabOcclusionData`,
`IntersectionActivator`, `NavMeshModifierVolume`. Siguiente candidato: Backrooms (9 raras, sin GPUI).

### Qué despertaría una carga aditiva de la escena del donante

La escena de cada mapa trae ~310 MonoBehaviours de 55 clases: `MazeGenerator` (su `Awake` toca `GameValues.Lives`,
su `Start` asigna `MazeGenerator.instance` y **genera otro laberinto**), `GameManager`, `RndAIManager`,
`RndCustomizationItemsManager`, `DissonanceComms` (voz), 4 `NetworkIdentity`, `GPUInstancerPrefabManager`,
`ftLightmapsStorage` (pisaría los lightmaps de la escena activa), `Volume`, `StaticLightingSky`, `NavMeshSurface`,
`OcclusionSystem`, cámara, `AudioListener`, UI. Mitigación del prototipo:

1. **52 métodos silenciados** (`Awake/OnEnable/Start/OnDisable/OnDestroy` de 32 clases, unión de las 19 escenas)
   con un prefix que solo actúa sobre objetos de la escena donante (comparando `Scene.handle`) y solo mientras está
   cargada. Verificado en `script.json` que cada uno tiene cuerpo nativo único. Se excluyeron 5 plegados
   (`0x43BA00` es el stub vacío compartido por 5981 métodos; `LocalisedText.OnEnable` y `OcclusionSystem.OnDestroy`
   comparten cuerpo con otro método).
2. En `sceneLoaded` (antes del primer `Start` y del primer render) se desactivan todas las raíces de la escena.
3. Se copian los arrays raros, se marcan los prefabs, meshes y materiales con `HideFlags.DontUnloadUnusedAsset` y se
   descarga la escena. Todo esto ocurre **en el lobby**, no en el mapa.

## 3. Diseño del prototipo

1. Lobby (escena activa `Lobby*`, 4 s después de entrar, cada 2 s mientras falte): si el experimento está prendido y
   estás solo (no cliente Mirror, ≤1 conexión, ≤1 miembro en el lobby de Steam) carga `Rand_<ExperimentDonorMaze>`
   en modo aditivo, copia y descarga. Si falla, reintenta a los 30 s. Cambiar el donante en F1 recarga solo.
2. Mapa: prefix de `MazeGenerator.GenerateMap`. Si es caso random (`!IsCustom`), solo, distinto del donante y con el
   mismo tileSize y rotY, **agrega** (no reemplaza) las raras del donante a `possibleSubmazes[0].*Rare` y a
   `wallPiecesRare`. Corre después del prefix de MapVariety, que sube la chance de raras.
3. Postfix en los 6 getters de piezas para contar cuántas veces salió una pieza del donante, y resumen al terminar.
4. No hay nada que restaurar: el `MazeGenerator` es de la escena y se reconstruye con cada mapa.

Por qué no se carga el donante dentro del mapa: `GenerateMap` sale de `Start`, la carga de una escena tarda segundos
y demorar la generación deja a los jugadores, `WaitForNetwork` y `OcclusionSystem` (`FindObjectOfType`) corriendo
sobre un mapa vacío o encontrando objetos del donante.

## 4. Riesgos

| Riesgo | Nivel | Estado |
|---|---|---|
| Iluminación de las piezas ajenas | Bajo | Sin lightmaps de escena. Probes del mapa actual o lightmaps Bakery del prefab. Verificar a ojo. |
| tileSize / bordes | Medio | Se filtra por tileSize y rotY. Bordes, alturas de piso y transiciones (no hay `TransitionPieces` para piezas ajenas) se ven solo en partida. |
| Efectos de cargar la escena donante en el lobby | Medio-alto | 52 silenciados + raíces desactivadas. Clases sin métodos silenciables (p. ej. `LocalisedText.OnEnable`) corren igual. Probar lobby: luz, niebla, audio, UI, errores. |
| Supervivencia de los prefabs entre escenas | Medio | `DontUnloadUnusedAsset` en GO, meshes y materiales. El log avisa si alguno murió. |
| GPU Instancer | Bajo-medio | Fallback a render normal esperado. Ver FPS. |
| `OcclusionSystem` y `IntersectionActivator` | Medio | Desconocido. Pop-in o errores. |
| Monstruos/coleccionables de otro mapa | Alto para G/N/H | Por eso F es el default y G/N/H no se recomiendan. |
| Memoria | Bajo | +240 MB mientras carga F en el lobby. Después quedan solo las 21 piezas y sus dependencias. |
| Hitch en el lobby | Bajo | Una carga de escena de 1 a 3 s, una vez por sesión. |
| Multijugador | Bloqueante | La generación es local en cada peer. Desincroniza mapas. El prototipo se apaga con cualquier otro jugador. |

## 5. Plan de prueba en partida

Log: `pwsh -File tools\log.ps1`. También mirar `%USERPROFILE%\AppData\LocalLow\Valko Game Studios\Labyrinthine\Player.log`.

1. **Arranque.** Al iniciar el juego tiene que aparecer
   `ForeignPieces: hooked GenerateMap (experiment off, donor Maze_F).`
   Con el experimento apagado no debe haber ninguna otra línea `ForeignPieces`.
2. **Activar, solo.** Crear lobby sin nadie. F1 > Experimental > "Piezas raras de otro mapa", donante `Maze_F`.
   Cerrar F1. Esperar unos 5 s en el lobby. Esperado, en este orden:
   - `ForeignPieces: muted 52/52 donor lifecycle methods.` (si dice menos, anotar la lista `Missing:`)
   - `ForeignPieces: counting hooks 6/6.`
   - `ForeignPieces: loading donor scene Rand_Maze_F additively...`
   - `ForeignPieces: donor scene handle <n>.` (si dice `unknown, matching by name`, anotarlo)
   - `ForeignPieces: deactivated <n> root object(s) of Rand_Maze_F.`
   - `ForeignPieces: donor Maze_F ready: straight 4, endcap 4, threeway 7, turn 6, floor 0, wall 0 (e.g. Straight_010, ...), tileSize 10, rotY 0. Load <ms> ms, total <ms> ms.`
   - Toast "Experimento: piezas de Maze_F listas (21)".
   Revisar en el lobby: que la luz, la niebla, la música y la UI sigan iguales, que no aparezca otra cámara ni HUD,
   que no se oiga ambiente del maizal, y que no haya excepciones nuevas en el log. Anotar el tiempo de carga.
3. **Caso random en otro mapa de tileSize 10** (N, A, B, C, E, H, I, J, L, M, P o Backrooms). Para ver muchas piezas
   ajenas, subir antes "Piezas raras (%)" a 100. Esperado:
   - `MapVariety: Maze_X rare tiles ... -> 100%, ...`
   - `ForeignPieces: injected Maze_F rare pieces into Maze_X: straight a+4, endcap b+4, threeway c+7, turn d+6, floor e+0, wall f+0 (own+foreign), rare chance 100%.`
   - `ForeignPieces: generation done, foreign pieces picked <N> times (straight .., endcap .., threeway .., turn .., floor 0, wall 0).`
   - **No** debe aparecer `only X/21 donor prefabs survived the scene change`. Si aparece, falló `DontUnloadUnusedAsset`.
4. **En el mapa**, buscar las piezas del maizal y anotar:
   - Luz: ¿se ven negras, rosas (shader faltante) o normales? ¿Toman la niebla del mapa?
   - Bordes: ¿coinciden piso y paredes con las piezas vecinas? ¿Hay huecos o escalones?
   - Colisiones: caminar contra las paredes de maíz. ¿Se atraviesan? ¿Caés al vacío?
   - Monstruos: ¿persiguen a través de esas piezas (navmesh)?
   - Oclusión: girar rápido cerca de una pieza ajena. ¿Desaparece o parpadea?
   - FPS comparado con el mismo mapa sin el experimento (GPU Instancer).
   - Que el cosmético del caso spawnee y el caso se pueda terminar. Volver al lobby: no debe recargar el donante
     (no aparece de nuevo `loading donor scene`).
5. **Casos de corte:**
   - Caso en Maze_F: `ForeignPieces: Maze_F is the donor itself, not applied.`
   - Caso en K, O, Q, R o D: `ForeignPieces: Maze_X tileSize 30/rotY 0 vs donor Maze_F 10/0: not applied.`
   - Caso custom: `ForeignPieces: custom case, not applied.`
   - Con un amigo en el lobby: `ForeignPieces: donor not loaded, not solo (...)` o `not applied, not solo (...)`.
6. **Opcional:** repetir con donante `Maze_Backrooms` (sin GPUI) para separar problemas de GPU Instancer.

## 6. Recomendación

**Go para la prueba en partida, no-go para publicarlo todavía.**

- Lo que más preocupaba (lightmaps horneados por escena) no aplica: las escenas random no tienen lightmaps y las
  piezas usan probes o lightmaps propios del prefab.
- tileSize 10 en 14 de 19 mapas da combinaciones de sobra.
- El costo real está en cargar una escena entera de otro mapa solo para sacar unos prefabs. El prototipo lo hace en
  el lobby con 52 métodos silenciados, que es lo más frágil del diseño y lo primero a validar (paso 2).
- Si el paso 2 sale limpio y el paso 4 se ve bien, lo siguiente sería: mezclar piezas normales además de raras (F no
  tiene pisos raros), una lista negra de prefabs con `RndMonsterSpawner`/`PillarObject` y, para multijugador, que
  todos los peers carguen el mismo donante y publiquen su lista de piezas por el lobby (mismo esquema que
  `Party.Agreed` de MapVariety). Como la generación es determinista con los mismos arrays y el mismo RNG, eso
  alcanzaría para que todos vean el mismo mapa.
- Si el paso 2 rompe algo en el lobby (voz, luz, UI, excepciones), la alternativa barata es no cargar otra escena y
  usar lo que ya está en memoria: las zonas mezcladas (`possibleSubmazes[1..]`) que MapVariety ya multiplica.

## 7. Resultado de la prueba en partida (2026-10-09): NO-GO

- Donante Maze_Backrooms, caso random en Maze_H (Criptas), piezas raras al 100%: se inyectaron 9 piezas y el
  generador las eligió 34 veces. Carga del donante en el lobby: 4,1 s.
- Primer intento: Mirror registró el donante en `NetworkManager.OnSceneLoaded` → `NetworkServer.SpawnObjects` y
  tiró `NullReferenceException` en `NetworkIdentity.OnStartServer` (identidades silenciadas). Arreglado ocultando
  la escena donante de 5 listeners de sceneLoaded (Mirror, PrefabLightmapData, SubtitleManager, ObjectiveManager,
  StatsMonitor).
- **Paredes invisibles** desde el inicio del mapa. Mismo caso (misma semilla) con el experimento apagado y piezas
  raras al 100%: se explora sin problemas. La causa es el experimento: piezas ajenas con colisión pero sin dibujarse
  (sospecha: el `OcclusionSystem`/`PrefabOcclusionData` del mapa no las conoce y las oculta).
- Queda apagado. Para retomarlo habría que revisar qué renderers de las piezas ajenas quedan deshabilitados después
  de generar y registrar las piezas en el sistema de oclusión del mapa, o quitarles `PrefabOcclusionData`.
