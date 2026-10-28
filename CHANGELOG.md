# Changelog

Formato: cada cambio entra en "Sin publicar" mientras se desarrolla. `tools\release.ps1 -Version X.Y.Z` mueve esa
sección a la versión nueva.

## [Sin publicar]

## [0.3.18] - 2026-10-28

### Interfaz
- El panel de opciones (F1), la colección (F2) y la barra de teclas usan el estilo del juego: su fuente (Larke Sans),
  sus paneles de borde irregular y los trazos de pincel blancos que se llenan al pasar el mouse, como en el menú
  principal. Todo el texto del mod va en mayúsculas y sin tildes. Los paneles son semi-transparentes y se agrandan
  con la resolución (en 1440p, un tercio más grandes).
- F1 tiene pestañas (General, Dificultad, Mapa, Cosméticos, Avisos, Tablero, Imagen, Partida) y es más ancho.
  Interruptores tipo crossplay del juego, sliders con perilla de pincel y flechas grandes para elegir entre opciones.
- F1 y F2 se abren en cualquier momento, también en medio de un caso fuera de las zonas seguras.
- F1 > General: botón "Forzar actualización" (baja la última versión de GitHub aunque sea la misma, cierra el juego,
  la instala y lo vuelve a abrir por Steam) y botón "Reiniciar el juego". Los dos piden confirmación.
- Vista previa de las opciones gráficas: el minimapa con su tamaño, leyenda y recorrido real en la esquina mientras
  pasás el mouse por esas opciones, el aviso del cosmético a la altura elegida, el cartel de persecución, un degradé
  con y sin el brillo extra, y una carpeta del tablero con el post-it al tamaño elegido.

### Información (no cambia la jugabilidad)
- Minimapa: recorrido de cada jugador, una línea de su color (vos en amarillo) que se desvanece hacia atrás, con los
  últimos ~200 m. Se apaga en F1 > Partida.
- Minimapa: un cono de visión desde cada jugador (vos incluido) muestra hacia dónde mira, en su color.
- Minimapa: la leyenda es más grande y va sobre un fondo casi opaco. Se apaga en F1 > Partida.
- El minimapa ya no se prende ni apaga con F4: solo desde F1 > Partida.

- Póster en la pared más cercana al cosmético del caso (hasta 6 m), con marco, a la altura de los ojos. Se ilumina como
  las paredes (linterna). Solo lo ve quien tiene el mod. `UserData\LabyHelper.poster.png` cambia la imagen. Se apaga en
  F1 > Avisos.

### Jugabilidad
- Vidas infinitas (host): el botón de vidas infinitas del lobby se prende solo, así cada caso que elegís arranca con
  vidas infinitas. Si lo apagás con el botón del juego, vuelve a prenderse al elegir otro caso. Se apaga en
  F1 > Partida.

## [0.3.17] - 2026-10-28

### Jugabilidad
- Tablero de casos (host): si todos en el lobby tienen el DLC egipcio, los mapas egipcios salen 4 veces más seguido.
  Cada jugador con el mod avisa en el lobby si tiene el DLC (chequeo del propio juego); quien no tiene el mod cuenta
  como que no lo tiene. No desbloquea nada. Se apaga en F1 > Dificultad.
- El pool ampliado suma la música que no sale de casos (la trae un pickup de música del juego, con la canción
  cambiada) y, si todos en el lobby tienen el DLC egipcio, sus cosméticos y canciones.

## [0.3.16] - 2026-10-28

### Jugabilidad
- Los casos random también pueden dar los cosméticos que no salen de ningún caso (tienda, recompensas por nivel,
  limitados), con el mismo truco que ya traía ítems de otros mapas. No incluye la música (no tiene modelo en el piso)
  ni el DLC egipcio (a quien no lo tiene le saldría roto; esos salen en los mapas egipcios). Se apaga en
  F1 > Cosméticos.
- Tablero de casos (host): cuando a alguien le faltan cosméticos que solo salen en casos HARDCORE, alrededor de 1 de
  cada 4 carpetas pasa a HARDCORE, y todas cuando ya no queda nada más por sacar de casos normales. Antes solo quedaban
  las HARDCORE que tiraba el juego (pocas), y con esos cosméticos faltantes el reparto quedaba vacío.
- Tablero de casos (host): las carpetas random salen de tamaño mediano, chico o muy chico (Grande pasa a Muy chico,
  Muy grande a Chico y Gigante a Mediano). Las que ya estaban cambian al recargar el tablero o rerollear. Se apaga en
  F1 > Dificultad. Los casos custom mantienen el tamaño elegido.

### Información (no cambia la jugabilidad)
- Palancas y efigies en el minimapa y en F9: el juego las marca como "colocadas" desde el inicio y el mod las daba por
  hechas. Ahora quedan pendientes hasta tirar la palanca o destruir la efigie.
- Leyenda debajo del minimapa con qué significa cada color y símbolo (vos, jugadores, cosmético, objetivos,
  monstruos, salida, zonas seguras, landmarks y, en mapas con pisos, bajadas/subidas). Se apaga en F1 > Partida.
- Jugadores en el minimapa: punto grande con su nombre al lado, y cada uno con su propio color (el mismo en su
  marcador del mundo con F9).
- El cosmético en el minimapa se ve grande: su imagen dentro de un anillo azul que late.
- Con F9, la salida del laberinto se marca en el mundo como "SEXY SALIDA" con la distancia, a través de paredes.
- El MINER se llama LEDES y la MARIONETTE se llama CAS en el cartel de persecución.
- Minimapa: ya no dibuja las zonas seguras grandes de la entrada y la salida (fuera de la grilla).

## [0.3.15] - 2026-10-27

### Arreglado
- Mismo cosmético para todos: ahora lo elige el host y lo publica en el lobby; los demás usan esa elección. Antes cada
  uno lo calculaba con sus propios datos (pools aprendidos en sus partidas, datos del grupo llegando en otro momento) y
  a veces les salía distinto, aun con la misma versión.
- "Te persigue A VOS" les salía a todos: el host tomaba como objetivo al primer jugador de la escena cuando el objetivo
  del monstruo no colgaba del jugador. Ahora toma al jugador que está sobre ese objetivo, y el host publica los
  objetivos aunque tenga su propio cartel apagado o esté caído.

## [0.3.14] - 2026-10-27

### Jugabilidad
- Las reglas del mod (cosmético nuevo, reparto, variedad) se apagan para todos si alguien del lobby tiene otra versión
  de LabyHelper, con el aviso de quién. Antes cada versión elegía su propio cosmético y en un mismo caso a cada uno le
  salía uno distinto.

### Información (no cambia la jugabilidad)
- Minimapa con niveles: en mapas con sótanos o pisos (las criptas debajo de Crypts) muestra el piso en el que estás,
  con su nombre abajo a la izquierda, marca las bajadas (▼) y subidas (▲), y deja tenue lo que está en otro piso.
  Los pisos salen de la malla de navegación del juego.

## [0.3.13] - 2026-10-27

### Arreglado
- La actualización automática fallaba siempre que había una versión nueva (el actualizador cambiaba el tiempo de
  espera de su conexión después de usarla). Por eso nadie pasó de la versión que instaló. Hay que instalar esta versión
  una vez a mano; desde ahí el mod también reemplaza un actualizador viejo por el que trae adentro.
- "Falta agarrar el cosmético" omitía a jugadores con el mod que todavía no lo habían agarrado (los tomaba como sin
  datos). Ahora un jugador con el mod figura hasta que avisa que lo agarró en este caso.

### Información (no cambia la jugabilidad)
- El cartel de monstruo solo aparece si te persigue a vos, y lo dice ("te persigue A VOS"). El host publica a quién
  persigue cada monstruo (solo él lo sabe); si el host no tiene el mod, queda la estimación por distancia de antes.

## [0.3.12] - 2026-10-27

### Arreglado
- En un juego sin la expansión egipcia (o con MelonLoader sin regenerar), el mod tiraba error en cada frame y el host
  veía a ese jugador como "no tiene LabyHelper". Ahora cada función corre aislada (si una falla, las demás siguen y el
  grupo se sincroniza igual) y lo de la expansión se saltea en ese juego.

## [0.3.11] - 2026-10-27

### Jugabilidad
- Los casos custom (cuando elegís el mapa) también siguen la regla de casos random: siempre sale un cosmético y es uno
  que le falta a alguien del grupo. Antes quedaban como el juego (15% en casos normales).
- La linterna arranca en la mano derecha y prendida en cada caso que la tiene. Se apaga en F1 > Partida.

### Información (no cambia la jugabilidad)
- El cartel de un monstruo que te persigue es rojo, parpadea y tiene una franja roja de fondo.
- Objetivos del caso en el minimapa: ahora se encuentran aunque estén lejos (el juego apaga las zonas lejanas y antes
  no se veían). Suma los objetivos en landmarks y los puzzles egipcios (pilares, balanza y sus pesas).
- El aviso del cosmético queda justo debajo de los objetivos del juego (arriba a la izquierda) y solo dice el nombre y
  si es nuevo: sin rareza, pool ni distancia (el marcador y el minimapa ya la dan). Altura ajustable en F1 > Avisos.
- La colección (F2) también se abre durante el caso, en el mismo lugar y tamaño que F1, con una pestaña arriba por
  cada parte del cuerpo como el selector del juego (Cabeza, Ropa, Muñeca, Linterna, Farol, Glowstick, Cara, Dedo,
  Música) y su cuenta tenés/total. Los tuyos van en el orden en que los desbloqueaste.
- La colección oculta los ítems sin modelo que el juego tampoco muestra (como "Rounded Circle 64px"), así que las
  cuentas tenés/total coinciden con el selector del juego.
- F9 fuera de un caso dice "No estás en un caso".
- F1 y F2 abiertos durante el juego muestran el cursor y frenan el movimiento y la cámara (PlayerLook), como los
  popups del juego.
  Escape o la X los cierran.
- Minimapa: el fondo oscuro cubre solo el laberinto; la entrada y la salida se dibujan por fuera.
- Debajo del minimapa, quién falta agarrar el cosmético del caso; el marcador de cada jugador que falta lo dice también.
- Minimapa: zonas seguras en verde semitransparente, y el mapa se agranda para incluir la zona fuera del laberinto
  (inicio, salida) cuando estás ahí.
- F1 se puede abrir dentro de una zona segura del caso. Barra grande arriba con cada tecla F y qué hace; el menú usa
  toda la altura debajo de ella, con letra 50% más grande. El scroll de F1 ya no deja espacio vacío al final.

### Quitado
- F5 para levantarse: levantarse solo ya viene prendido y se maneja desde F1.
- "Exclusivos posibles" del aviso de F9.
- Marcadores y resumen de coleccionables (tickets, fichas de reroll, XP).

### Compatibilidad (expansión egipcia del juego, 27/10)
- Arreglado: los nombres de los otros jugadores (marcadores y F9) tiraban error en cada frame porque el juego movió el
  cartel de nombre a otro componente.
- Mapas Egipto A y B: nombre en el tablero y pools aprendidos al jugarlos (no vienen en el extracto embebido porque el
  juego los carga desde el DLC).
- Aviso de monstruos: nombres para Anubis, Mummy, Sand Zombie, Ouphris y Necreph.
- Rareza nueva "DLC Egipto" con color propio. La colección (F2) oculta los cosméticos del DLC si no lo tenés, igual
  que el menú del juego.

## [0.3.10] - 2026-10-15

### Jugabilidad
- Tablero de casos (host): todas las carpetas random salen con sello. Las que iban a salir sin sello pasan a sello azul
  (raro, cosmético garantizado), y las HARDCORE siguen saliendo solo mientras a alguien del grupo le falte un cosmético
  HARDCORE; si ya están todos, pasan a azul. Las carpetas que ya estaban en el tablero cambian al recargarlo o rerollear.
  Se apaga en F1 > Dificultad.

### Información (no cambia la jugabilidad)
- F9 muestra los objetivos pendientes del caso: en el aviso los más cercanos con distancia y dirección, y en el mundo
  un marcador magenta sobre cada uno, a través de paredes.

### Quitado
- F3 (pool de cosméticos del caso).

## [0.3.9] - 2026-10-15

### Nuevo
- F1 > Actualizaciones: interruptor para la actualización automática y una línea con la versión instalada y la última
  de GitHub ("se instala al reiniciar el juego" si hay una nueva, o aviso si falta el actualizador).

### Información (no cambia la jugabilidad)
- F9 dice quién del grupo agarró el cosmético del caso y a quién le FALTA AGARRARLO (cada jugador tiene su propia copia
  en el mapa). Los jugadores sin el mod aparecen como "sin datos".

## [0.3.8] - 2026-10-15

### Información (no cambia la jugabilidad)
- Minimapa: muestra los objetivos del caso que faltan, en magenta: objetos para agarrar o usar (sellos, efigies,
  palancas, nafta y los propios de cada mapa) con un rombo, dónde hay que colocarlos con una O, y lugares a los que hay
  que llegar con un ◎. Desaparecen a medida que se cumplen.

## [0.3.7] - 2026-10-14

### Nuevo
- Auto-actualización: el instalador agrega un plugin (`Plugins\LabyHelper.Updater.dll`) que al abrir el juego baja
  la última versión de GitHub antes de cargar el mod y avisa en pantalla. Hay que instalar esta versión una vez con el
  .exe; las siguientes llegan solas. Sin internet arranca con la instalada. Se desactiva con `UserData\LabyHelper.noupdate`.

## [0.3.6] - 2026-10-14

### Información (no cambia la jugabilidad)
- Minimapa: muestra los monstruos activos con una X naranja (roja y más grande mientras persiguen a alguien).

## [0.3.5] - 2026-10-14

### Cambiado
- Minimapa: tu flecha ahora es grande y amarilla con borde negro, se ve clara hacia dónde mirás.
- F9 arranca prendido en cada caso (se puede cambiar en F1 > Avisos).
- Glowsticks automáticos: uno cada 2 s por defecto (antes 15 s, se aplica también a configuraciones existentes), y
  al empezar el caso el glowstick queda en la mano izquierda.

## [0.3.4] - 2026-10-14

### Arreglado
- Ponerse la gorra de Navidad negra (#434) trababa el personaje: el juego la tiene en el pool de Navidad pero su
  modelo no está en los archivos, así que no puede cargarla. El mod ya no la reparte en los casos y no deja equiparla
  (ni a vos ni, si sos host, a nadie del grupo); vale para cualquier cosmético sin modelo.
- Al entrar alguien al lobby aparecía "X no tiene LabyHelper" aunque lo tuviera y se apagaban las reglas: el que
  entra publicaba sus datos recién a los 15 s. Ahora los publica al instante y, mientras tanto, durante 30 s cuenta
  como "cargando" (se mantiene el estado y no hay aviso).
- Posible causa de jugadores desincronizados (otro laberinto o paredes invisibles): cada jugador decidía por su
  cuenta si las reglas estaban activas, y si el host y otro jugador veían distinto en el momento de arrancar el caso,
  generaban mapas distintos. Ahora decide solo el host y todos siguen esa decisión. Todos necesitan esta versión.

### Información (no cambia la jugabilidad)
- Minimapa fijo (norte arriba) arriba a la derecha durante los casos: laberinto completo, vos (flecha), los otros
  jugadores, el cosmético que queda en el piso y la salida. F4 lo muestra/oculta; tamaño en F1.

### Jugabilidad
- Glowsticks automáticos: tira uno cada 15 s mientras avanzás (no si estás quieto o caído), igual que con la tecla,
  así que todos lo ven. Solo si tenés el glowstick en la mano izquierda. Se configura en F1 > Partida.

## [0.3.3] - 2026-10-14

### Información (no cambia la jugabilidad)
- Colección (F2): al pasar el mouse por un cosmético se muestra en grande al costado, con nombre, rareza y parte.
- Al agarrar un cosmético el aviso dice si ya quedó en tu colección o si se guarda al terminar el caso (el juego
  guarda la mayoría recién cuando completás el caso). Al completarlo, un aviso confirma los desbloqueados. F9 muestra
  los agarrados que siguen pendientes.
- F9 muestra dónde están los otros jugadores: en el aviso (nombre, distancia, dirección, si está caído) y con un
  marcador verde sobre cada uno, visible a través de paredes (rojo si está caído).

### Jugabilidad
- F5 ya no apaga "Levantarse solo" (se cambia solo en F1). Estando caído dentro de un caso, F5 te levanta: antes
  F5 se ignoraba durante el caso y eso no funcionaba nunca.
- Tu jugador corre un 25% más rápido (`SprintSpeedMultiplier`, ajustable en F1 > Partida, 1 = como el juego). Solo
  afecta a los jugadores con el mod, nunca a los monstruos.

## [0.3.2] - 2026-10-14

### Cambiado
- F9 ahora es un toggle: el aviso del cosmético (con distancia y dirección actualizadas) y el marcador quedan en
  pantalla hasta volver a apretar F9, en lugar de desaparecer a los pocos segundos. Se apaga solo al cambiar de escena.

### Arreglado
- Tablero de casos: el post-it seguía mostrando un cosmético "garantizado" después de que las reglas se apagaban (por
  ejemplo cuando entra alguien sin LabyHelper), y en la partida salía otro. Ahora el tablero se actualiza cuando cambian
  las reglas o el grupo, el post-it solo aparece cuando el cosmético es exacto, y un aviso explica por qué se apagaron.
- El cosmético de cada caso se fija en el lobby: en la partida se usa el mismo que mostró el tablero aunque los datos
  del grupo lleguen tarde o cambien.
- El cartel de persecución ahora es individual: solo le aparece al jugador perseguido (el vivo más cercano al
  monstruo), no a todo el grupo.

## [0.3.1] - 2026-10-09

### Arreglado
- Caídas de rendimiento al pasar el mouse por el post-it del tablero y con la colección (F2) abierta: la colección
  de cosméticos se buscaba en toda la memoria en cada ícono y cada repintado. Ahora se cachea (colección, ítems por ID,
  nombres e íconos).

## [0.3.0] - 2026-10-09

### Información (no cambia la jugabilidad)
- Aviso al empezar el caso con el cosmético que apareció: imagen, nombre, rareza (HARDCORE en rojo), de dónde viene
  (exclusivo del mapa, común, temporada, traído de otro mapa) y si es nuevo. F9 lo repite con distancia, dirección
  y un marcador visible a través de paredes, y suma tickets, fichas de reroll y XP del caso con sus marcadores.
- Cartel rojo con el nombre de cada monstruo que te está persiguiendo (todos los monstruos).
- Tablero de casos: cada carpeta lleva un post-it con la imagen de su cosmético; al pasar el mouse muestra mapa,
  cosmético exacto (casos random) o probabilidades, y qué jugadores del grupo lo tienen (verde) y cuáles no (rojo).
  Funciona en los 19 mapas sin haberlos jugado (pools extraídos de los archivos del juego).
- Colección (F2 o botón en el menú de cosméticos): tengo / faltan / todos, tradicionales y HARDCORE separados, por
  parte del cuerpo y con contadores. Pool en vivo (F3) del caso actual con probabilidades y faltantes por jugador.
- Íconos: si el sprite no se puede dibujar se renderiza el modelo 3D; si tampoco, un marcador por tipo (♪ música).
- Brillo extra dentro de los casos (exposición y gamma sobre el resultado final de HDRP). F6/F7/F8. En el lobby y
  los menús no se aplica.
- Menú "Opciones LabyHelper" (F1 o botón en la pantalla de opciones del juego) con todas las opciones; se guardan
  al cambiarlas. Idioma: Auto, Español, English.
- F1, F2, F3 y F5 no se abren durante un caso.

### Jugabilidad
- Casos random del tablero (las 4 carpetas): siempre sale un cosmético y siempre uno que le falta a alguien del
  grupo, repartidos entre las 4 carpetas (los exclusivos van a su mapa si está en el tablero). Cualquier cosmético
  faltante puede salir en cualquier carpeta: si no está cargado en ese mapa, lo trae un pickup que arma su modelo
  según el ID. Solo se apaga desde Opciones LabyHelper.
- Casos custom: priorizar cosméticos que nadie del grupo tiene, sumar los de todos los eventos al pool y evento de
  temporada forzado (Off, Random, Halloween, Christmas, Easter, Valentines, StPatrick, Summer). Se configuran en F1.
- Variedad de mapa en casos random: más piezas en su variante rara (25%; el juego usa 1-5%) y el doble de zonas
  mezcladas (submazes). No cambia la forma del laberinto.
- Pigman 2,5 veces más silencioso (alcance del sonido 0,4; ajustable en F1).
- Levantarse solo después de que un monstruo te tire, sin el ítem de auto-reanimación: mantener la tecla del juego,
  o F5 estando caído. No gasta el ítem si lo tenés.

### Grupo
- Todos los jugadores necesitan el mod: el juego no le manda al host los cosméticos desbloqueados de los demás.
  Cada jugador publica los suyos en el lobby de Steam y las reglas que cambian lo que se genera (cosméticos,
  temporada, variedad de mapa) las fija el host para todos. Si alguien no tiene el mod, no se aplican para nadie.

### Distribución
- Instalador de un archivo (`LabyHelper-Setup-X.Y.Z.exe`): encuentra el juego vía Steam, hace backup de partidas e
  instala MelonLoader 0.7.3 + el mod. También zip para instalación manual.

### Notas
- Todo viene prendido por defecto, incluido ignorar el detector de trampas del juego.
- El experimento de piezas de otro mapa quedó fuera del build (paredes invisibles, ver re/EXPERIMENT-foreign-pieces.md).
- No probado todavía con otros jugadores reales.
