# Setup — Juice de dados legacy (Classic) con PrimeTween + Feel

> Feature #0021: animaciones del panel de dados legacy — spin del Roll (0.5s
> configurable), raise al holdear, throw al centro de la mesa + fade al confirmar,
> fade + scale-down de los no usados — más capa de juice Feel (springs, flashes,
> shakes) y hooks de audio.
> Estado: código + wiring + feedbacks autorados por Unity MCP; **falta playtest
> manual** y sourcear los SFX (solo hay 1 clip placeholder).

## Qué se agregó

**Código nuevo** (`Assets/Scripts/Rollgeon/UI/HUD/`):

| Archivo | Rol |
|---------|-----|
| `DiceAnim/DiceUiAnimationSettingsSO.cs` | SO de tuning (duraciones, stagger, eases). Duración ≤ 0 = instantáneo (kill switch). |
| `DiceAnim/DiceAnimChoreographer.cs` | Coreografía como data pura (planes de spin/outro, tick desacelerante, caras preview) — 100% testeada. |
| `DiceAnim/DiceSlotAnimator.cs` | Motion PrimeTween por slot: spin+ciclado de caras, raise, throw/discard. Agregado por código en `Bind` — no vive en el prefab. |
| `DiceAnim/DiceZoneAnimator.cs` | Coordinador: `TryBeginSpin`/`TryBeginOutro` (false ⇒ path instantáneo legacy), hooks para juice. El spin es Classic-only; raise/outro corren en TODOS los modos (gate partido en `CanAnimateRoll`/`CanAnimatePostReveal`). |
| `DiceAnim/DiceOutroGate.cs` | Latch estático que difiere el teardown de la zona mientras los dados vuelan. |
| `DiceSlotJuice.cs` | Dispara los MMF_Player del slot por momento (spin-start, reveal ± crit, lock/unlock, throw, discard, kept-pulse). |
| `DiceZoneJuice.cs` | Shakes de mesa, flourish de combo y TODO el audio (via `IAudioService`, con pitch ramps). |
| `DiceSlotHoverJuice.cs` | Hover scale en dados lockeables (gateado por `Button.interactable`). |
| `UiButtonJuice.cs` | Press squash + click SFX genérico para botones (Roll/Confirm). |

**Modificados:** `DiceSlotView` (+`SetSpinPreviewFace`, `SetHoldInteractable`),
`DiceZoneView` (integración animator), `PlayerActionButtonsView` / `RerollCountView`
(lockout de Confirm/Roll durante animación), `CombatHudZoneFlow` /
`ActionRollExplorationVisibility` (teardown diferido por `DiceOutroGate`),
`Rollgeon.asmdef` (+`MoreMountains.Tools` — Feel compila ahí via los .asmref del
vendor; **no crear asmdefs dentro de Assets/Feel**).

**Tests EditMode** (verdes, suite completa 1963): `UI/Tests/DiceAnimChoreographerTests.cs`
(16), `UI/Tests/DiceOutroGateTests.cs` (4).

## Wiring (ya aplicado por Unity MCP)

**Asset de tuning:** `Assets/Resources/Dice/DiceUiAnimationSettings.asset`
(defaults de código: spin 0.5s, raise 18px/0.12s, throw 0.35s, discard 0.25s).
Todo el feeling se tunea acá sin tocar código.

**`Assets/Prefabs/UI/DiceSlotView.prefab`** (aditivo — se propaga a las 5 instancias
de combate en `Canvas.prefab`):

- Root: +`CanvasGroup` (fades), +`DiceSlotJuice`, +`DiceSlotHoverJuice` (refs wireadas).
- Hijo `FlashOverlay`: Image blanca alpha 0, raycast off, stretch full (los flashes).
- Hijo `Juice` con 10 GOs, cada uno con un `MMF_Player` autorado:

| Player | Feedbacks autorados |
|--------|--------------------|
| `Juice_SpinStart` | SquashAndStretchSpring bump vertical −30/−20 (anticipación) |
| `Juice_Reveal` | ScaleSpring bump 18–25 + Graphic flash blanco 0.6→0 (0.15s) |
| `Juice_CritReveal` | ScaleSpring bump 30–40 + flash dorado 0.8→0 (0.25s) — cara ≥ 6 |
| `Juice_Lock` | RotationSpring Z 600–900 + ScaleSpring 10–14 + flash cyan 0.5→0 |
| `Juice_Unlock` | ScaleSpring dip −14/−10 + flash gris 0.25→0 |
| `Juice_Throw` | Graphic flash blanco sutil 0.25→0 (la motion ya escala el root) |
| `Juice_Discard` | Graphic del `DiceLabel` → gris 50% (0.2s) |
| `Juice_KeptPulse` | ScaleSpring bump 8–12 ("este se queda" en rerolls / combo) |
| `Juice_HoverEnter/Exit` | ScaleSpring MoveTo 1.05 / 1.0 |

**`Assets/Prefabs/UI/Canvas.prefab`:**

- `Canvas/DiceZoneView`: +`DiceZoneJuice` (refs + `SE-Collision_03` como thud
  placeholder) e hijo `ZoneJuice` con `Juice_ThrowPreShake` / `Juice_OutroLand`
  (PositionSpring bump de la `RollArea`) y `Juice_ComboFlourish` (ScaleSpring de
  la zona).
- Botón Roll (`RerollCountView`) y Confirm (`PlayerActionButtonsView`):
  +`UiButtonJuice` con hijo `Juice_Press` (ScaleSpring squash −16/−10).

## GOTCHA MAYOR: el HUD de runtime vive en la ESCENA, no en Canvas.prefab

**`Canvas.prefab` no se instancia en ninguna escena** — el canvas real es una
copia desempaquetada dentro de `02_Gameplay.unity`. Cualquier authoring de UI
tiene que hacerse en la ESCENA (o en prefabs que la escena realmente
instancie). Estado actual:

- Los 5 slots de combate de la escena SON instancias reales de
  `DiceSlotView.prefab` (reemplazo manual, 2026-07-13) → autorar en el prefab
  base propaga. El prefab trae el kit completo + Button + LockIcon.
- La capa de zona (DiceZoneJuice, ZoneJuice players, RollAreaHighlight,
  UiButtonJuice de Roll/Confirm, DiceImpactParticles en el root del canvas)
  está autorada EN LA ESCENA.
- Backlog: re-vincular el canvas de la escena a `Canvas.prefab` o borrar el
  prefab muerto — mientras coexistan van a divergir.
- Gotcha de authoring por código: `MMF_Player.FeedbacksList` es **null** en
  players recién agregados — null-check antes de leer `.Count`.

## Contrato anti-conflicto motion ↔ juice

La motion (PrimeTween) es dueña de: posición y alpha del root SIEMPRE, rotación
durante el spin, escala durante el outro. El juice usa escala/rotación del root
solo en fases sin overlap, y colores solo en `FlashOverlay`/`DiceLabel` — el color
del background es de `DiceSlotView` (tints de hold/blocked). Si agregás feedbacks,
respetá ese reparto.

**Gotcha springs de Feel (bug del "primer dado squashado", 2026-07-13).**
Dos vectores corrompen el punto de reposo de los springs:

1. **Captura inicial a destiempo** (la causa del primer dado): el default
   `InitializationMode = Start` captura los valores iniciales 1 frame después
   de activarse la zona — el slot 0 gira sin stagger ese mismo frame
   (1440°/s ⇒ ~45° al frame siguiente, + squash de anticipación activo), así
   que su lock/reveal guardaban ese estado como "reposo". Los slots 1-4
   zafaban por el stagger de 50ms+. Fix: `MmfJuice.CaptureRestPose` en el
   `OnEnable` de cada componente de juice (activación = reposo garantizado;
   pasa el player a modo Script para que su `Start()` no re-capture).
2. **Stop a mitad de oscilación**: `StopFeedbacks()` re-basa el reposo al valor
   desplazado del momento (`CustomStopFeedback: _targetValue = _currentValue`).
   Fix: disparar/frenar SIEMPRE via `MmfJuice.Replay`/`Rest`
   (`RestoreInitialValues()` antes de re-disparar y después de frenar).

## Capa v2 (segunda pasada — "todos los sugeridos")

**Motion** (tuning en el mismo SO): bob flotante del dado holdeado
(`HoldBobAmplitude/Seconds`), salto parabólico con sombra durante el spin
(`SpinJumpHeight` + hijo `Shadow` del prefab), vuelo en arco con rotación en el
throw (`ThrowArcHeight/SpinDegrees`), caída con rotación en el descarte
(`DiscardFallDistance/SpinDegrees`), raise con overshoot (`RaiseEase=OutBack`).

**Juice**: glow pulsante en holdeados (hijo `GlowOverlay`), pop del número al
revelar (Label Pop en Juice_Reveal/CritReveal), intensidad del reveal escalonada
por cara (`_highFace`/`_highFaceIntensity` en DiceSlotJuice), hitstop en crit y
aterrizaje (`DiceHitstop`, tuning en DiceZoneJuice), borde dorado pulsante de la
RollArea mientras hay holds esperando confirm, micro-shake de zona al rollear,
pulso de disponibilidad del Confirm (`UiButtonJuice._availablePulsePlayer`),
duck de música durante el outro (`IAudioService.DuckMusic`), floating damage
diferido hasta el aterrizaje de los dados, tick de audio por cara preview.

**Accesibilidad**: `dicemotion on|off` en la DevConsole (persistido en
PlayerPrefs) — off = todo instantáneo, path legacy. Exponer en la futura
pantalla de opciones leyendo `DiceUiMotionPrefs.ReducedMotion`.

**Partículas (ACTIVAS via UIParticle)**: se instaló
`com.coffee.ui-particle@4.13.3` (git, pinneado en el manifest) — renderiza
ParticleSystems como geometría UI, compatible con el canvas Overlay. Wireadas:
`HoldSparkles` (chispitas idle en holdeados) y `RevealPuff` (burst al revelar)
en `DiceSlotView.prefab`, `ImpactParticles` (polvo al aterrizar el throw) bajo
la RollArea en `Canvas.prefab`. Material placeholder: Default-ParticleSystem
(círculo soft builtin) — reemplazable por sprites propios en el
ParticleSystemRenderer. El `scale` del componente UIParticle está en 1
(1 unidad de simulación = 1 px de canvas).

**Gotcha auto-scaling (2026-07-13)**: los UIParticle van con
`AutoScalingMode.UIParticle` (compensa la escala del canvas en el mesh
horneado). El default del package (`Transform`) escribe
`transform.localScale = inverse(lossyScale del padre)` — bajo un padre
tweeneado por la motion calculaba 0, quedaba GUARDADO en la escena y las
partículas se volvían invisibles. No volver a `Transform`.

## SFX — placeholders sintetizados (reemplazar por assets sourceados)

`Assets/Sounds/Dice/` tiene 9 WAVs **procedurales** (generados por script,
funcionales pero básicos): rattle, preview_tick, reveal_tick, lock, unlock,
throw_whoosh, discard_poof, combo_chime, ui_click — ya wireados en
`DiceZoneJuice`/`UiButtonJuice`; el thud sigue siendo `SE-Collision_03`.
Para reemplazar: sourcear (ej. Kenney Casino/UI Audio, CC0), importar con
Force Mono + Decompress On Load, y re-wirear los campos en los prefabs.

## Cómo probar (playtest manual — el Play va a mano)

1. `dicemode classic` en la DevConsole (F1) si no es el modo activo.
2. **Roll:** los 5 dados giran ~0.5s ciclando caras (rápido→lento), revelan con
   stagger + punch + flash; cara 6 = flash dorado. Roll/Confirm deshabilitados
   durante el giro.
3. **Lock:** click a un dado → se eleva 18px con wobble + flash cyan; re-click →
   baja con dip. El tint azul de hold sigue funcionando.
4. **Reroll:** solo giran los NO holdeados; los holdeados quedan elevados y hacen
   un pulso de reaseguro.
5. **Confirm:** los holdeados vuelan al centro de la mesa achicándose con fade;
   los demás se desvanecen con scale-down; thump de la mesa al final; recién ahí
   vuelven los chips de acciones.
6. **Combo:** al formar un combo nuevo con los holds, bump escalonado de los
   dados holdeados + bump de zona.
7. **Regresión:** `dicemode 2d` y `3d` siguen igual que antes (presenters propios);
   dados bloqueados por boss quedan grises con candado y no se pueden holdear ni
   elevar; Heal/Forzar Puerta (action-rolls) animan igual que combate y el cancel
   NO reproduce el outro; end-turn con dados en mesa reproduce el outro (quirk
   conocido, barato de distinguir a futuro).
8. Tuning: todo en `Assets/Resources/Dice/DiceUiAnimationSettings.asset`; poner
   duraciones en 0 desactiva esa animación.

## Backlog P2 (documentado, no implementado)

- Slam del `LockIcon` al bloquear un dado (boss).
- Idle micro-breathing en dados sin holdear (`MMF_Wiggle`, detrás de un toggle).
- Partículas de polvo al aterrizar el throw (necesita asset de VFX).
- Distinguir confirm vs end-turn en `OnRollResolved` para no reproducir el throw
  en end-turn.
