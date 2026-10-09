# Changelog

Formato: cada cambio entra en "Sin publicar" mientras se desarrolla. `tools\release.ps1 -Version X.Y.Z` mueve esa
sección a la versión nueva.

## [Sin publicar]

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
