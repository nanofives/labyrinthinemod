# Changelog

Formato: cada cambio entra en "Sin publicar" mientras se desarrolla. `tools\release.ps1 -Version X.Y.Z` mueve esa
sección a la versión nueva.

## [Sin publicar]

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
