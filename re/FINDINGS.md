# Labyrinthine: hallazgos de ingeniería inversa

Fecha del análisis: 2026-10-08. Build analizado: GameAssembly.dll del 28/06 (build-guid 260dab8d430140acbe9661b9620d263f).

## Stack

- Unity 2022.3.62f3, **IL2CPP**, metadata v31 sin cifrar (Il2CppDumper 6.7.46 la abre sin problemas).
- HDRP, Mirror (+FizzySteamworks, kcp2k) para multijugador, BehaviorDesigner para la IA, DOTween, Addressables.
- **ACTk (Code Stage Anti-Cheat Toolkit)** presente: las listas de cosméticos usan `ObscuredUShort`/`ObscuredByte`. No es un anti-cheat de kernel, pero explica por qué buscar valores con Cheat Engine falla.
- Saves locales cifrados en `%USERPROFILE%\AppData\LocalLow\Valko Game Studios\Labyrinthine\<steamid>\` (`customization_n.dat`, `player_n.dat`, `equipment.dat`, ...), con Steam Cloud.
- Volcado completo: `re/il2cpp-dump/dump.cs` (RVAs incluidas), `DummyDll/` para referencias, `il2cpp.h` + `script.json` para Ghidra/IDA.

## Clases relevantes por objetivo

### 1. Ruido del Pigman
- `PigmanAIController : AIController` (MonsterType.Pigman = 5, Pigman_Hunter = 29). `RatAIController` hereda de él.
- `MonsterAudio` (por monstruo): `patrolClips`, `idleClips`, `chaseClips`, `screamClips`, `aSrc`, `stateSounds[]`, `PlayAudio(clip, override)`, `HandleAudio()` coroutine, `Update()`.
- `MonsterNetworkSync` sincroniza el estado (AnimationState) a clientes, así que el audio se decide en cada cliente: un hook local funciona aunque no seas host.
- `AIController.Target`, `MonsterType`, `NavMeshAgent` sirven para saber si te está persiguiendo y a qué distancia.

### 2. Cosmético especial del mapa
- `RndCustomizationItemsManager` (singleton): `chanceForItem[]` por ContractType (Normal/Rare/Hardcore), `seasonalItems[]`, `SpawnCustomizationItems()`, `TryToSpawn(spawner)`, `TryGetCurrentSeasonalConfig(out cfg)`.
- `RndCustomizationSpawner.Spawn(List<ushort> excluded)` usa `globalPrefabs` / `possiblePrefabs` (`WeightedPrefab[]`).
- El objeto spawneado lleva `CustomizationPickup` con `ItemID`. Nombre/rareza: `CustomizationManager.Instance.Collection` -> `CustomizationItem.Name`, `ItemRarity` (Common, Uncommon, Rare, Event, Hardcore, Limited).
- Plan: postfix en `SpawnCustomizationItems` (o `FindObjectsOfType<CustomizationPickup>` al cargar escena) -> mostrar nombre, rareza y opcionalmente distancia/dirección.

### 3. Cosméticos fuera de temporada
- `SeasonalEventsTimeHandler.TryGetActiveSeasonalEvent(out ISeasonalEvent, out TimeSpan, out DateTimeOffset)`; eventos: Halloween, Christmas, Easter, Valentines, StPatrick, Summer.
- La fecha sale de `TimeManager.SteamUnixTime` (hora del servidor de Steam): cambiar el reloj de Windows no sirve.
- `SeasonalItemsConfig { seasonalEventType, prefabs, chance }` en `RndCustomizationItemsManager.seasonalItems`.
- Plan: hook en `RndCustomizationItemsManager.TryGetCurrentSeasonalConfig` para devolver el config elegido (y/o `SeasonalConfig.IsActive`). El spawn ocurre en el host: funciona si sos host.
- Desbloqueo real: `CustomizationPickup.Pickup()` -> `CustomizationManager.UnlockItem(id, unlockOnContractFinish)` -> `CustomizationSave` (se confirma al terminar el contrato).

### 4. Brillo extra
- `LabyrinthineSettingsManager.SetBrightness(float)` remapea el slider a `brightnessMinMax` (Vector2) y hay `PerSceneConfig[] sceneConfigs` con un rango por escena.
- Plan: prefix en `SetBrightness` que amplíe `brightnessMinMax` y los rangos por escena, o forzar `Exposure.compensation` del Volume HDRP / sumar un `LiftGammaGain`.

## Otros hallazgos
- `LabyrinthineSettingsManager.DEV_INTERACTIONS_ARG = "-allow_dev"`: argumento de lanzamiento que habilita interacciones de desarrollo. Sin investigar qué expone.
- `DevCustomizationSave`, `DevSaveToolMonsterToggle`: herramientas internas de edición de saves.
- `CustomizationSave.PIGMASK_ITEM_ID = 194`.

## Agregado tras la investigación web
- `ValkoGames.Labyrinthine.Misc.CheatingDetector`: `ObscuredBool isCheating`, se activa con `SpeedHackDetectedListener` y `ObscuredCheatingDetectedListener` (ACTk). El speedhack de Cheat Engine y editar valores Obscured lo disparan. Los mods existentes parchean `get_CheatingDetected` -> false.
- Brillo: el valor vive en `Labyrinthine_Data\settings.cfg` (`brightnessLevel=`). El menú lo limita a 4.2 y lo vuelve a clampear al abrir opciones (ZakDank sugirió ~5 a mano).
- Comunidad: MelonLoader 0.7.x (Thunderstore). Referencias de código: limitbrk/Labyrinthine-TellMeCosmetics (ya muestra el cosmético del caso en Tab), Dekirai/LabyrinthineCheat. Evitar JellyfishCraft86/Labyrinthine-Trainer (señales de malware).
- Pigman sí tiene audio (música por cercanía, pasos, cadenas, gruñidos), pero los pasos solo se oyen muy cerca.
- Cosméticos: 15% de spawn en casos normales, garantizado en rare/hardcore; el exclusivo del mapa ~2% una vez que spawnea algo; Halloween 50% de reemplazo, Navidad 35%.

## Implementación (LabyHelper 0.1)
- Brillo: el slider escribe `ColorAdjustments.postExposure` (EV) en los VolumeProfile de `LabyrinthineSettingsManager.volumes`, remapeado por `PerSceneConfig`. El mod suma EV y `LiftGammaGain.gamma.w` en un postfix de `VolumeManager.Update(VolumeStack, Transform, LayerMask)`.
- Temporada: `TryGetCurrentSeasonalConfig` no tiene llamadores directos. Quien decide es `RndCustomizationSpawner.Spawn` (+0x27f llama a `TryGetActiveSeasonalEvent`, recorre `manager.seasonalItems`, compara `seasonalEventType`, tira RNG con semilla de `MazeGenerator.GetSeed()` contra `chance`). También consultan el evento `Contract.FindContractType` (probabilidad de caso raro) y `SeasonalEventsTimerUI`. El mod reemplaza `SeasonalEventsTimeHandler.seasonalConfigs` solo durante `Spawn`.
- Pigman: `PigmanMonsterAnimationHandler` existe en clientes. `MonsterNetworkSync.IsChasingPlayer` y `CurrentAnimationState` (MonsterRun=102, MonsterCharge=105, MonsterSpot=104). Pasos: `Footsteps.audioSource`.
- Herramientas: `re/scripts/disasm.py <RVA>` y `re/scripts/xrefs.py <RVA...>` (llamadores directos).

## Predicción del cosmético (LabyHelper 0.2)
- `RndCustomizationItemsManager.SpawnCustomizationItems`: tirada `MazeGenerator.GetRandom()` contra `chanceForItem[contractType]` (RNG del laberinto, no predecible en Normal), después `RndUtils.SpawnObjects(ValidateSpawner, TryToSpawn)`.
- `TryToSpawn`: `spawner.Spawn(new List<ushort>(GameValues.instance.RandomMazeContract.m_ExcludedCustomizationItemIDs))`.
- `MazeGenerator.GetSeed(offset, usePrimary)` = `(usePrimary ? seed@0x120 : secondSeed@0x124) + offset`.
- `Spawn`: si contrato != Hardcore y hay evento: `WeightedRandom(secondSeed+0x12B5).Random.NextDouble() < cfg.chance` -> lista = prefabs del evento. Si no: `global.Value + possiblePrefabs` (Hardcore: `globalPrefabsHardcore.Value + possiblePrefabsHardcore`). Elección: `WeightedRandom((secondSeed+0x23E3 + seed+0x692)/2)`, `PickRandom` hasta 50 veces mientras el id (TryGetComponent<CustomizationPickup> en la raíz del prefab) esté excluido; si no, recorre la lista.
- `PickRandom`: `r = NextDouble() * Sum(Weight)`, primer i con `acumulado + w_i >= r`.
- **Verificado en partida: no son iguales.** `MazeGenerator.Awake` copia `Contract.Seed/SecondSeed`, pero `CreateMaze` suma `+0x4A3` / `+0x18C1` por cada `GenerateNewMaze` fallido (hasta 10). Ejemplo Maze_N: 7 reintentos (8309 = 7x1187, 44359 = 7x6337). La cantidad depende de generar el laberinto con las estructuras del mapa, así que el ítem exacto no es predecible desde el lobby. El tablero muestra probabilidades exactas en su lugar.

## Auto-reanimación (Death, ValkoGames.Labyrinthine.Players)
- `isSelfReviveAvailable` (SyncVar @0x118) lo setea el dueño con `CmdSetSelfReviveAvailable(bool)` desde `AvailableInventoryItemsChangedCallback` = `inventory.ContainsItem(selfReviveItem)`. El servidor no valida.
- `HandleSelfReviveInput`: al llenarse la barra (`selfReviveTimer`/`selfReviveTime`) y con disponibilidad, llama `RndPlayerInventory.RemoveItem(id, true)` y solo si devuelve true envía `CmdReviveSelf` (hash 0x417D88BF).
- `UserCode_CmdReviveSelf`: limpia `reviveStartTime`/`bleedoutStartTime` y llama `ServerOnRevivalCompleted(this)` sin chequear el ítem.
- Input: `Death.Inputs.selfRevive` desde `LabyrinthineInputManager.GetButton` (rebindeable).

## Generación de mapas (variedad)
- `MazeGenerator.rareGenerationChance` (float @0x178, 5 por defecto en el .ctor): `GetPiecePrefab`, `GetFloorPiece`,
  `GetStraightPiece`, `GetTurnPiece`, `GetThreewayPiece`, `GetEndCapPiece`, `GetWallPiece`, `GetSafezonePiece` hacen
  `UnityEngine.Random.Range(0, 100)` y usan el array `*Rare` si el valor es <= la chance. La tirada ocurre siempre:
  subir la chance cambia variantes, no la topología.
- `possibleSubmazes[]` (`MazeGenerator.Submaze`): `chanceToSpawn`, `spawnTries`, radios y sus propios arrays de
  piezas (incluidas variantes raras). Son zonas de otro estilo ya configuradas en la escena (iluminación correcta).
- La generación corre en cada peer a partir de `Contract.Seed/SecondSeed` (`MazeGenerator.Awake`): cualquier cambio
  a la generación tiene que ser idéntico en todos los jugadores.
- Portar piezas de otros mapas: los prefabs viven en la escena de cada mapa (levelN/sharedassetsN), con lightmaps
  propios (Bakery) y `tileSize`/bordes por mapa; habría que cargar la otra escena en modo aditivo. Descartado por ahora.

## Generación local de cosméticos y cambio de itemID
- `RndSpawnerBase.SpawnItem` es un `Object.Instantiate` con `MazeGenerator.GetRandomForGameObject`; no hay
  `NetworkServer.Spawn`. Cada peer genera su cosmético (igual que el laberinto): cualquier cambio a la elección
  tiene que ser idéntico en todos (ver `Rules.cs`).
- `CustomizationPickup.<Start>d__6.MoveNext`: espera, `EquipmentSave.OnCaseCustomizationItemSpawned(itemID)` en
  modo random, y si `loadObjectModel` → `ItemsCollectionSO.TryGetItem(itemID)` + Addressables del `AssetReference`
  + `HandleObjectReplacement` del hijo 0. 472 de 504 pickups tienen `loadObjectModel` (re/scripts/inspect_pickups.py).
  Cambiar `itemID` en un prefix de `Start` cambia modelo, registro y desbloqueo.
- `CustomizationManager.CmdSetCustomization` solo serializa los ítems aplicados (equipados), no los desbloqueados.
