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

## Auto-actualización
- [ ] Log de MelonLoader al abrir: "LabyHelper Updater ... is up to date" (o "Updated LabyHelper X -> Y" y aviso en pantalla).
- [ ] Con una versión vieja instalada y una nueva publicada: al abrir el juego se reemplaza sola; `UserData\LabyHelper.dll.bak` guarda la anterior.
- [ ] Sin internet: el juego arranca normal con la versión instalada (espera máx. ~6 s).
- [ ] F1 > Actualizaciones: muestra "Instalada: X · es la última" (o "nueva: Y, se instala al reiniciar"); sin el plugin dice que falta el actualizador.
- [ ] Apagar el interruptor crea `UserData\LabyHelper.noupdate` y al reiniciar no busca actualizaciones; prenderlo lo borra.

## Tablero con sellos
- [ ] Como host, al rerollear: las 4 carpetas random tienen sello azul o HARDCORE (log "BoardSeals: case ... Normal -> Rare").
- [ ] Con todos los HARDCORE del grupo obtenidos, no aparecen carpetas HARDCORE.

## Objetivos en F9
- [ ] F9 en un caso: línea "Objetivos pendientes (N): ◆ efigie 23 m adelante · O dónde colocarlo ..." y marcadores magenta en el mundo; se van al cumplirlos.
- [ ] F3 ya no hace nada.

## Grupo
- [ ] En grupo, F9: línea "<cosmético>: lo agarraron: ... · FALTA AGARRARLO: ..." que se actualiza a los pocos segundos de que otro lo agarra; sin el mod = "sin datos".
- [ ] Entra alguien con el mod: no aparece "no tiene LabyHelper" ni se apagan las reglas (log: "join in progress, state kept" a lo sumo).
- [ ] Entra alguien sin el mod: a los 30 s aparece el aviso y las reglas se apagan para todos (los clientes muestran el motivo del host).
- [ ] Varios casos seguidos en grupo: todos ven el mismo laberinto y el mismo cosmético.

## Minimapa y glowsticks
- [ ] En un caso: minimapa arriba a la derecha, el laberinto coincide con lo que recorrés; tu flecha gira con la cámara.
- [ ] Otros jugadores (verde), cosmético (azul) y salida (S roja) en el lugar correcto. F4 ya no hace nada; F1 > Partida > Minimapa lo oculta y lo vuelve a mostrar.
- [ ] Al empezar el caso el glowstick queda solo en la mano izquierda.
- [ ] Cada 2 s cae uno mientras caminás; quieto no tira. Los demás lo ven. Con brújula/radio en la mano no tira.
- [ ] Monstruos en el minimapa: X naranja donde está cada uno; roja cuando persigue.
- [ ] Objetivos en el minimapa (magenta): rombo = objeto a agarrar/usar, O = dónde colocarlo, ◎ = lugar a alcanzar; se borran al cumplirlos. Log: "Minimap: objectives pending: ...".
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

## Pedidos del 27/10
- [ ] F1: el scroll termina justo después de la última opción, sin espacio vacío.
- [ ] Caso custom (elegís el mapa) como host, todos con el mod: siempre sale un cosmético y es uno que le falta a alguien.
- [ ] Al empezar un caso con linterna: queda en la mano derecha y prendida (log `AutoFlashlight: ...`). F1 > Partida la apaga.
- [ ] Monstruo persiguiendo: cartel rojo que parpadea sobre una franja roja oscura.
- [ ] Objetivos: el log muestra `Objectives: N objective object(s) in the scene` con N > 0, y aparecen en el minimapa
      (magenta) aunque estén lejos. Anotá el mapa si sigue en 0.
- [ ] El texto de la izquierda (aviso del cosmético, F9) arranca más abajo y no tapa las condiciones del caso.
      F1 > Avisos > Altura lo ajusta.
- [ ] F9 ya no lista "Exclusivos posibles" ni coleccionables, y no hay marcadores de tickets/XP.
- [ ] F1 y F2 abren en cualquier momento del caso, dentro o fuera de zona segura; mientras están abiertos el jugador no
      se mueve ni mira y el cursor queda libre. Esc o la X los cierran.
- [ ] Barra arriba con F1, F2, F6, F7/F8, F9 y qué hace cada una (tecla blanca si está prendida) en el lobby, en zona
      segura y con F1 abierto. En el caso no tapa el minimapa. El menú F1 queda debajo de la barra, sin pisarse.
- [ ] En grupo: debajo del minimapa "Falta agarrar el cosmético: ..." y en el marcador del jugador "◆ falta el cosmético".
- [ ] Minimapa: zonas seguras en verde claro; al estar en la zona de inicio (fuera de la grilla) el mapa se agranda con
      un borde marrón y la flecha queda dentro del recuadro.

## Grupo (todos con el mod)
- [ ] Log `Party: published N owned items to the lobby.` y `Party: Vos N owned, Amigo M owned -> K needed.`
- [ ] Sale un cosmético que le falta a otro jugador aunque vos ya lo tengas.
- [ ] Log `Rules: new=1;dist=1;...` igual en todos; con alguien sin mod: `Rules: off for everyone (X no tiene LabyHelper)`.

## Pool en vivo y colección
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

## Interfaz nativa (0.3.18)
- [ ] Log `UiSkin: game font and sprites ready.` unos segundos después de abrir el juego.
- [ ] F1, F2 y la barra de teclas con la fuente del juego, paneles de borde irregular semi-transparentes y trazos de
      pincel: la pestaña elegida con pincel blanco y texto negro; al pasar el mouse el pincel se llena de izquierda a
      derecha. Todo en mayúsculas y sin tildes (COSMETICOS, TAMANO, CUANTOS).
- [ ] Texto de la barra de teclas centrado en vertical dentro de cada tecla.
- [ ] F1: 8 pestañas, ninguna cortada. Cada pestaña recuerda su scroll. Interruptores, sliders (arrastrar y click
      directo) y flechas de "Marcador en el mundo" e "Idioma" funcionan y se guardan.
- [ ] Clic en el texto de una opción con interruptor también la cambia.
- [ ] En 1440p el panel se ve un tercio más grande que en 1080p y nada se sale de la pantalla.
- [ ] F1 > General > Reiniciar el juego: pide confirmación, cierra el juego y vuelve a abrirse solo por Steam.
- [ ] F1 > General > Forzar actualización: "Descargando...", el juego se cierra y vuelve a abrir; aparece el aviso
      "LabyHelper se actualizó solo: X -> Y" y Mods\LabyHelper.dll es el del último release. (En la PC de desarrollo
      reemplaza el DLL de desarrollo: volvé a correr dev.ps1 después.)
- [ ] Vistas previas: pasar el mouse por Partida > Minimapa / Tamaño / Leyenda / Recorrido muestra el minimapa de
      ejemplo arriba a la derecha a ese tamaño; Avisos > Altura del texto muestra el aviso a esa altura; "Cartel cuando
      un monstruo te persigue" muestra el cartel rojo; Imagen muestra el degradé y cambia al mover los sliders;
      Tablero muestra la carpeta con el post-it al tamaño elegido.

## Minimapa: recorrido y leyenda
- [ ] En un caso: línea amarilla detrás tuyo por donde caminaste, precisa en las esquinas, que se desvanece hacia atrás.
- [ ] Otros jugadores: línea de su color (el mismo que su punto). Al morir o reaparecer no cruza el mapa con una recta.
- [ ] Cono de visión: el tuyo (amarillo) gira con la cámara; el de cada jugador sigue hacia dónde mira, sin saltos.
- [ ] Leyenda debajo del minimapa grande y legible sobre fondo oscuro, con la fila "Recorrido". F1 > Partida la apaga.

## Vidas infinitas (host)
- [ ] En el lobby como host, al elegir un caso del tablero: el botón de vidas infinitas del juego queda prendido (log
      `InfiniteLives: on (zone N)`), y el caso arranca con vidas infinitas.
- [ ] Apagarlo con el botón del juego lo deja apagado; al elegir otro caso se vuelve a prender.
- [ ] F1 > Partida > "Vidas infinitas al elegir un caso" apagado: no lo toca. Como cliente no hace nada.

## Póster junto al cosmético
- [ ] En un caso, ~4 s después de empezar: log `Poster: on a wall X m from the cosmetic` y el póster con marco oscuro en
      la pared más cercana al cosmético, mirando hacia afuera, a la altura de los ojos, iluminado por la linterna.
- [ ] Sin pared a menos de 6 m: log `Poster: no wall within 6 m of the cosmetic.` y nada colgado.
- [ ] No traba al jugador (no tiene colisión). F1 > Avisos lo apaga para el próximo caso.

## 0.3.19
- [ ] Con F2 abierto, F1 cierra F2 y abre opciones (y al revés). El jugador sigue quieto y el cursor libre en el cambio.
- [ ] Interruptores: al hacer clic la perilla se desliza (~0,2 s); apagado = píldora oscura, perilla gris a la izquierda;
      prendido = blanca, perilla negra a la derecha. Sin líneas en el medio.
- [ ] Póster: 2,4 m de lado, borde de abajo a ~1,7 m; en paredes bajas queda más abajo pero sin sobresalir por arriba.
- [ ] Leyenda: mismo ancho que el minimapa (probá tamaños chicos y grandes en F1: pasa a una columna si no entra),
      fila "SEXY SALIDA". Se ve 10 s al empezar el caso y se desvanece; TAB la muestra de nuevo 10 s.
- [ ] M en un caso: mapa grande en el medio, fondo oscurecido, jugador quieto y cursor libre. M o Esc lo cierran.
      Abrir F1 o F2 lo cierra (y M cierra F1/F2).
- [ ] Clic en un pasillo: línea celeste desde vos hasta el destino por los pasillos (sin cruzar paredes), en el mapa
      grande y en el minimapa; se recalcula al caminar. Clic en una pared marca el pasillo más cercano. Clic derecho borra.
- [ ] En el mundo: "DOBLA ACA · N M" en la próxima esquina y "DESTINO · N M" celeste, a través de paredes.
- [ ] Al llegar: "Llegaste al destino" y se borra la ruta. Con el minimapa apagado en F1, M igual funciona.
- [ ] Revisar que M no choque con ninguna tecla del juego.
- [ ] Flecha 3D celeste en la próxima esquina, de frente al acercarte, apuntando hacia donde hay que doblar; sube y baja
      y empuja hacia el giro. Pasa a la siguiente esquina al doblar; sin giros restantes desaparece. Log
      `Route: 3D turn arrow ready (HDRP/Unlit)`.
- [ ] Barra de teclas siempre visible (menú, lobby, todo el caso), con M "Minimapa grande y ruta" y TAB "Leyenda"; en el caso no tapa el minimapa.
