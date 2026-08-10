# Game Speed (x1 / x2 / x4 / x8)

> Estado: implementado (2026-08-10). Opción "Velocidad: xN" en el panel de
> opciones — disponible desde el menú principal y desde la pausa in-game.

## Cómo funciona

- **Fuente de verdad**: `Rollgeon.Timing.GameSpeedPrefs` (static). Persiste en
  PlayerPrefs (`Rollgeon.GameSpeed`), sanitiza valores inválidos a x1, y aplica
  `Time.timeScale = Multiplier` al boot y en cada cambio.
- **Palanca**: `Time.timeScale` global. Acelera todo lo scaled: feedbacks
  (`FeedbackManager`), delay de turno enemigo, tweens de gameplay, animators,
  partículas y la secuencia completa del breakdown. Los menús y overlays tweenean
  **unscaled**, así que no se aceleran — es lo deseado.
- **Hitstop**: `DiceHitstop` ya no captura/restaura una copia local — al
  descongelar setea `Time.timeScale = GameSpeedPrefs.Multiplier`, así un cambio
  de velocidad hecho durante el freeze no se pierde. Además el freeze (realtime)
  se divide por el multiplier: a x8 sigue siendo un acento, no un stall.
- **Guard de aplicación**: `ApplyToTimeScale()` no escribe si `timeScale == 0`
  (único caso: hitstop en curso). Nadie más pausa con timeScale — la pausa del
  juego es soft (overlay + scrim).

## Compensaciones unscaled

Tres timings del breakdown corren unscaled a propósito (deben verse durante el
freeze del hitstop) y se dividen por `GameSpeedPrefs.Multiplier` para no quedar
"lentos" respecto de la secuencia acelerada:

| Qué | Dónde |
|---|---|
| Flash del clash | `ScreenFlashView.Flash` |
| Cadencia del ghost trail | `FlyingValueView.OnFlightTick` |
| Rate-limit del tick de roll-up | `BreakdownJuice.OnClashRollupTick` |

**Importante**: esas compensaciones leen la pref, nunca `Time.timeScale` (puede
estar en 0 por hitstop).

## Decisiones (no re-litigar sin razón)

- **Camera shake NO compensado**: es fire-and-forget con decay de amplitud y su
  duración realtime es una constante de feel. Si a x8 el playtest muestra
  "smear", compensarlo es una división en `CameraService` — perilla futura.
- **Física de dados 3D a x8**: el step de FixedUpdate en tiempo de juego no
  cambia (misma trayectoria); solo corren 8× más FixedUpdates por segundo real.
  Aceptado — el proyecto no toca `fixedDeltaTime`.
- **El speed aplica global** (también en el menú principal): como los menús son
  unscaled, no tiene efecto visible ahí; evita un applier por escena.
- **`SteppedAnimation`** drena todos los steps adeudados por frame (a x8 el
  deltaTime escalado supera 1/FPS y el `if` viejo desfasaba el stepping).

## UI / instalación

- El botón `GameSpeedButton` cicla x1→x2→x4→x8 (patrón Tutorial/Analytics:
  Button + TMP_Text + `LocalizedContent.Ui("menu.game_speed", "Velocidad: x{0}")`).
- Instancias construidas por el installer `Rollgeon → Juicy Menu → 4` (main menu)
  y `→ 5` (prefab de pausa). El panel de opciones mide 560×720 desde esta feature;
  si se agrega otra fila, reflotar el layout en `BuildOptionsPanel` y re-correr
  ambos MenuItems.
- Key localizada `menu.game_speed` seedeada en las tablas UI (es/en) por
  `UpsertOptionsLocalization` — es un format string, mantener el `{0}`.

## Checklist de smoke

- [ ] Boot → `Time.timeScale` == valor persistido; opciones muestran el mismo xN.
- [ ] 4 clicks ciclan x1→x2→x4→x8→x1 y el label acompaña (ES y EN).
- [ ] Cambiar velocidad desde la pausa aplica al toque (sin resumir).
- [ ] Cerrar y reabrir el juego conserva la velocidad.
- [ ] A x8: combate acelerado, crit con hitstop → timeScale vuelve a 8.
- [ ] A x8: menús y transiciones de pantalla a velocidad normal.
- [ ] A x8: tiro de dados 3D aterriza y reporta caras normalmente.
