# Checklist de prueba

Correr antes de cada release. `pwsh -File tools\dev.ps1 -Restart` deja el build nuevo cargado;
`pwsh -File tools\log.ps1` muestra el log en vivo. Marcá cada ítem y anotá lo que falle en CHANGELOG o como issue.

## Carga
- [ ] El log muestra `Loaded.` y un `hooked ...` por cada feature, sin `[ERROR]`.

## Brillo
- [ ] F6 alterna el brillo extra y aparece el aviso arriba a la izquierda.
- [ ] F7/F8 y Shift+F7/F8 cambian exposición y gamma; el valor queda guardado al reiniciar.

## Cosmético del caso
- [ ] Caso Rare (sello azul) como host: aviso a los ~1.5 s con imagen, nombre, rareza, pool y NUEVO/ya lo tenés.
- [ ] Log: `Cosmetic spawned: id N <nombre>` con nombre no vacío.
- [ ] F9: aviso con distancia y dirección + marcador con ícono sobre el objeto, visible a través de paredes.
- [ ] F9 en grupo: línea "Jugadores: <nombre> 23 m adelante ..." y marcador verde sobre cada jugador (rojo y "(caído)" si está caído).
- [ ] Correr: notablemente más rápido que caminar comparado con antes (x1.25); caminar y agachado igual que siempre. F1 > Partida > Velocidad al correr = 1 lo deja como el juego.
- [ ] F9 es toggle: el aviso y el marcador quedan fijos, la distancia se actualiza al moverte; otro F9 los oculta.
- [ ] Con F9 activo, agarrar el cosmético muestra "Agarraste" y después vuelve el aviso fijo.
- [ ] Al agarrarlo: aviso "Agarraste: ...".
- [ ] Como cliente (otro host): el aviso también aparece.

## Pigman
- [ ] Mapa con Pigman (Crypts, Cornfield, Mines, Sewer, Snowy Hedges): log `Pigman: boosted range x2.5 ...`.
- [ ] Pasos y gruñidos audibles desde más lejos que sin el mod.
- [ ] Cualquier monstruo que te persiga: cartel rojo arriba al centro con su nombre ("PIGMAN te persigue"); varios a la vez se listan juntos.
- [ ] En grupo: si el monstruo persigue a otro jugador (más cerca de él que de vos), a vos no te aparece el cartel.

## Temporada y pool (host)
- [ ] F10 cicla el modo; log `Seasonal: spawning with event X` al empezar el caso.
- [ ] F11 activa priorizar nuevos; log `PoolBoost: excluded N owned items`.
- [ ] Con pool ampliado: log `PoolBoost: map pool A -> B`.
- [ ] El cosmético de temporada que sale queda desbloqueado al terminar el caso.

## Cosméticos sin modelo
- [ ] Log: "BrokenItems: #434 ... has no model in the game files".
- [ ] Equipar la gorra de Navidad negra: aviso "no tiene modelo ... no se equipa" y el personaje no se traba.

## Grupo
- [ ] Entra alguien con el mod: no aparece "no tiene LabyHelper" ni se apagan las reglas (log: "join in progress, state kept" a lo sumo).
- [ ] Entra alguien sin el mod: a los 30 s aparece el aviso y las reglas se apagan para todos (los clientes muestran el motivo del host).
- [ ] Varios casos seguidos en grupo: todos ven el mismo laberinto y el mismo cosmético.

## Minimapa y glowsticks
- [ ] En un caso: minimapa arriba a la derecha, el laberinto coincide con lo que recorrés; tu flecha gira con la cámara.
- [ ] Otros jugadores (verde), cosmético (azul) y salida (S roja) en el lugar correcto. F4 lo oculta y lo vuelve a mostrar.
- [ ] Al empezar el caso el glowstick queda solo en la mano izquierda.
- [ ] Cada 2 s cae uno mientras caminás; quieto no tira. Los demás lo ven. Con brújula/radio en la mano no tira.
- [ ] Flecha del minimapa: grande, amarilla, apunta hacia donde mirás.
- [ ] F9 ya está prendido al empezar el caso (aviso fijo y marcadores); F9 lo apaga.

## Levantarse solo
- [ ] Viene prendido; solo se apaga en F1 > Partida. F5 fuera de partida no hace nada.
- [ ] Caído por un monstruo: aparece el aviso nativo de auto-reanimación; mantener la tecla llena la barra y te levanta.
- [ ] Estando caído, F5 te levanta al instante.
- [ ] Si tenías el ítem de auto-reanimación, sigue en el inventario después de levantarte.
- [ ] Como cliente en partida de otro host: también funciona.
- [ ] Apagado en F1: sin el ítem, el aviso nativo desaparece y F5 no te levanta.

## Casos random y reparto (host)
- [ ] Log al entrar al lobby: `Distribution: Maze_X/Rare=N, ...` con la cantidad asignada a cada carpeta.
- [ ] Cada carpeta random dice "Cosmético nuevo garantizado | N asignados" y nombra algunos; el post-it es uno de ellos.
- [ ] Caso random normal: log `RandomCaseRule: ... cosmetic chance 0.15 -> 1` y `N allowed new items`; sale uno nuevo.
- [ ] Caso custom: el cosmético sale como en el juego (sin esas líneas).
- [ ] Al rerollear una carpeta, el reparto se recalcula.

## Variedad de mapa
- [ ] Caso random: log `MapVariety: Maze_X rare tiles 5% -> 25%, N submaze type(s) x2.0`.
- [ ] Se notan más piezas distintas a las de siempre (paredes, pisos, decoración).
- [ ] Con un amigo sin el mod: log `MapVariety: not applied (X no tiene LabyHelper)` y el mapa queda normal.
- [ ] En grupo con mod: todos ven las mismas piezas en el mismo lugar (sin paredes invisibles ni atravesables).

## Cualquier cosmético en cualquier caso
- [ ] Tablero: cada carpeta random dice "Cosmético nuevo: <nombre>" (y en grupo, a quién le falta).
- [ ] Al entrar: log `RandomCaseRule: Maze_X/... -> <nombre> (#id) carried by #N` o `native to this map`.
- [ ] Si fue "carried": log `RandomCaseRule: pickup #N became <nombre>` y el modelo que se ve es el del ítem nuevo.
- [ ] Al agarrarlo y terminar el caso queda desbloqueado el ítem nuevo (no el del portador).
- [ ] En grupo: todos ven el mismo cosmético; el log de cada uno muestra el mismo `party needed hash`.

## Coleccionables
- [ ] F9 en un caso: marcadores "● Tickets x5 23 m", "● Ficha de reroll", "● XP" sobre los más cercanos.
- [ ] El aviso de F9 suma la línea "Coleccionables: Tickets N · ...".
- [ ] Al agarrar uno, su marcador desaparece en ~2 s.

## Grupo (todos con el mod)
- [ ] Log `Party: published N owned items to the lobby.` y `Party: Vos N owned, Amigo M owned -> K needed.`
- [ ] F3 muestra "Jugador: X faltan" por cada uno; un jugador sin mod aparece "sin datos".
- [ ] Sale un cosmético que le falta a otro jugador aunque vos ya lo tengas.
- [ ] Log `Rules: new=1;dist=1;...` igual en todos; con alguien sin mod: `Rules: off for everyone (X no tiene LabyHelper)`.

## Pool en vivo y colección
- [ ] F3 en un caso: panel con monstruos/umbral, chance de cosmético, % nuevo y lista con faltantes primero.
- [ ] El panel cambia al tocar la dificultad, F10 o F11 (en menos de 1 s).
- [ ] Menú de cosméticos abierto: aparece el botón "Colección (F2)" arriba a la derecha.
- [ ] Colección: pestañas Faltan/Tengo/Todos y Tradicionales/Hardcore/Ambos; contadores correctos; scroll con rueda.
- [ ] Colección: mouse sobre un cosmético → imagen grande al costado del panel con nombre, rareza y parte; filas fuera del scroll no la disparan.
- [ ] Agarrar un cosmético: el aviso dice "Se guarda en tu colección al terminar el caso" (o "Ya está en tu colección"); al completar el caso aparece "Desbloqueado: ...".
- [ ] Los hardcore aparecen como HARDCORE en rojo.

## Tablero de casos
- [ ] Mapa nunca jugado: la carpeta ya muestra probabilidades (log `PoolCache: 19 maps known (... from the bundled extract)`).
- [ ] Idioma: cambiar a English en Opciones LabyHelper traduce avisos, paneles y carpetas.
- [ ] Mapa aprendido: línea negra con % nuevo, % temporada y exclusivos.
- [ ] Post-it amarillo con la imagen debajo del texto, legible al hacer zoom en el reroll (ver `PostIt:` en el log).
- [ ] Entra alguien sin el mod: aviso "Reglas de LabyHelper apagadas" y los post-its desaparecen al instante.
- [ ] Con reglas activas, el cosmético del post-it es el que aparece en la partida (log: `frozen in the lobby`).

## Instalador (antes de compartir)
- [ ] `dist\LabyHelper-Setup-X.Y.Z.exe --detect` encuentra el juego.
- [ ] En una carpeta de prueba: instalar con `--path <carpeta> --yes` y desinstalar con `--uninstall` la deja limpia.
- [ ] Instalación real: el juego abre desde Steam y el log muestra la versión correcta.
