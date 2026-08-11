# Game Speed (x1 / x2 / x4) — modelo "solo resolución"

> Estado: implementado (2026-08-10, revisado el mismo día). Opción "Velocidad: xN"
> en el panel de opciones — menú principal y pausa in-game.
>
> **Historia**: la v1 escalaba `Time.timeScale` global. Eso aceleraba animators,
> partículas y shaders y volvía ilegible el combate ("no se entiende nada y marea").
> El modelo actual NO toca el timeScale: solo comprime la **resolución**.

## El modelo

- **Fuente de verdad**: `Rollgeon.Timing.GameSpeedPrefs` (static, PlayerPrefs
  `Rollgeon.GameSpeed`, sanitiza a x1). Es un **multiplicador de resolución**:
  los consumidores dividen sus duraciones de *pacing* por `Multiplier`.
- **`Time.timeScale` nunca se altera.** El único que lo toca sigue siendo
  `DiceHitstop` (freeze + restore a la escala capturada, como siempre).
- **Animaciones, partículas, shaders y efectos corren SIEMPRE a velocidad real.**
  Decisión explícita del usuario: legibilidad > velocidad bruta.

## Puntos de inyección (qué divide por `Multiplier`)

| Qué | Dónde |
|---|---|
| TODA la secuencia del breakdown (windups, vuelos, gaps, clash, mitigación) | `BreakdownSequenceDirector.D()` — compone con skip y step-ramp |
| Flash del clash / cadencia de ghosts / rate-limit del tick (corren en real time) | `ScreenFlashView` / `FlyingValueView` / `BreakdownJuice` |
| Hitstop (realtime) | `DiceHitstop.Play` |
| Grace period del turno enemigo (0.8s por acción) | `CombatController` → `deltaTimeProvider` × Multiplier (responde en vivo a cambios desde la pausa) |
| Gaps autorados de secuencias de feedback (`StartDelay`, steps `InlineWait`) | `FeedbackManager` (`PacingSeconds`) — hoy no hay ninguno autorado, queda listo |
| Hold post-aterrizaje de los dados / stagger de spin / stagger del pulso de combo / auto-hide del action roll | `DiceZoneAnimator` / `DiceSlotAnimator` / `DiceZoneJuice` / `ActionRollPanelView` |

## Qué NO escala (a propósito)

- **Duraciones de efectos** (`FeedbackEntry.Duration` del FeedbackDB): son la
  longitud del VFX/SFX/anim — dividirlas cortaría efectos a la mitad.
- **Animators** (`Animator.speed` intacto): los golpes están gateados por el
  Animation Event `"hit"` del clip — corren a tiempo real.
- **Watchdogs y rendezvous** (timeouts del FeedbackManager/TurnManager/director):
  redes de seguridad; a speeds altos quedan holgados, que es el lado correcto.
- Vuelo de los dados (`landSeconds`), tweens de muerte/knockback, números
  flotantes (stagger acoplado a la vida del número).

## Expectativa de resultado (documentada, no es bug)

El tiempo de un golpe lo domina la animación de ataque (~0.95s) + impacto (~0.55s),
que NO escalan. A x4: la **resolución del tablero** (breakdown, dados, turnos)
comprime completo, pero el combate cuerpo a cuerpo se siente ~x2 efectivo. Si algún
día se quiere más, la palanca es `Animator.speed` + `FeedbackEntry.Duration` juntos
(decisión de diseño aparte — hoy explícitamente fuera de alcance).

## UI / instalación

Sin cambios respecto de la v1: botón `GameSpeedButton` cicla x1→x2→x4 (patrón
Tutorial/Analytics), instancias construidas por `Rollgeon → Juicy Menu → 4` y `→ 5`,
key `menu.game_speed` ("Velocidad: x{0}" / "Speed: x{0}") en tablas UI. Panel 560×720.

## Checklist de smoke

- [ ] A cualquier velocidad: `Time.timeScale == 1` siempre (salvo durante un hitstop).
- [ ] 3 clicks ciclan x1→x2→x4→x1; persiste tras reiniciar.
- [ ] A x4: breakdown comprimido (steps rapidísimos, clash a ritmo pleno ÷4).
- [ ] A x4: animación de ataque del enemigo a velocidad NORMAL; su pausa previa casi nula.
- [ ] A x4: dados spinean a velocidad normal pero el stagger/hold casi desaparecen.
- [ ] Cambiar velocidad desde la pausa a mitad de combate aplica al turno enemigo en curso.
- [ ] Crit con hitstop a x4: freeze cortito, timeScale vuelve a 1.
- [ ] Shaders/partículas del menú y combate: sin cambio de velocidad perceptible.
