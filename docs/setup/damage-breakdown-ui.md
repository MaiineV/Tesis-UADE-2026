# Setup — UI de breakdown de daño (N × M)

> Feature `sprint03/feature/0105-damage-breakdown-v3`. Referencias de feel: Dicero
> (dados) y Balatro (multis). La fórmula v3 está documentada en
> `docs/planning/plan-de-accion-2026-07-09.md` (nota 2026-08-09).

## Qué quedó cableado (vía Unity MCP — verificar, no re-crear)

### `Assets/Prefabs/UI/DiceSlotView.prefab`
- Hijo nuevo **`ContributionLabel`** (TMP, anchor (0.5, 0), pivot (0.5, 1), pos (0, −6),
  80×28, inactivo): el "+N" bajo el dado. Cableado a `DiceSlotView._contribution`.

### `Assets/Prefabs/UI/Canvas/Canvas_ActionRoll.prefab`
| GO | Qué es | Notas |
|---|---|---|
| `DamageBreakdown` (0, 225, 700×74) | `DamageBreakdownView` + CanvasGroup (alpha 0) | Hijos `CounterN` (−90), `MultSign` "×", `CounterM` (+90). Lo muestra/oculta `DamageFormulaView` (campo `_breakdownView`, ya cableado). |
| `PlayerBaseDamage` (−420, 75) | `PlayerBaseDamageView` | `SwordIcon` (Image 64×64 **sin sprite — asignar arte de espada**) + `ValueLabel` (ATQ ModifiedValue). |
| `GlobalModifierCascade` (anchor der., −40, 260, 256×144) | `GlobalModifierSpinnerView` + CanvasGroup (alpha 0) | Spinner de tambor (2026-08-12): `Interior` (FrameAnim_1) + `SlotsRoot` (RectMask2D) con `SlotA`/`SlotB` (ModifierEntryView, Icon 44×44 + Label) + `Frame` (FrameAnim_0) encima. Se reconstruye con `Rollgeon/Breakdown/Setup Spinner (All)`. **`_fallbackIcon` sin asignar** — `ItemSlot.png` está en spriteMode Multiple sin slices (pendiente de arte). |
| `FlyingValueLayer` (full-stretch, último sibling) | `FlyingValuePool` | `FlyingValueTemplate` inactivo (pool), `ClashAnchor` (0, −180 desde arriba) + `ClashLabel` (TMP 64, inactivo), `SkipButton` (full-screen, Image alpha 0, inactivo). |
| `BreakdownDirector` | `BreakdownSequenceDirector` | Referencias completas: settings, vistas, dice zone, pool, clash, skip. **`_mitigationSprite` sin asignar** — sugerido un sprite de escudo. |

### Assets / código
- `Assets/Rollgeon/UI/BreakdownAnimSettings.asset` — perillas de la secuencia (tiempos,
  arcos, skip ×3, timeout 8 s).
- `CombatHUDView` auto-resuelve `PlayerBaseDamageView` y `BreakdownSequenceDirector` por
  `FindFirstObjectByType` (mismo patrón que los chips) — no requiere wiring de escena,
  pero se pueden arrastrar en el Inspector para evitar el find.

## Pendiente de autoría (usuario)
1. **Sprite de espada** en `PlayerBaseDamage/SwordIcon` (hoy Image blanca).
2. **`_fallbackIcon`** del spinner (slicear `ItemSlot.png` o autorar un sprite) y
   **`_mitigationSprite`** del director.
3. **Iconos de `ItemSO`**: los 22 items tienen `Icon` vacío — mientras tanto los popups
   muestran el fallback + el "+X".
4. Pasada de layout en editor: tamaños de fuente, posición del `ClashAnchor`. (El solape
   del viejo cascade con `ConfirmButton`/`RerollCountView` quedó resuelto por el recuadro
   fijo 256×144 del spinner.)

## Cómo funciona (resumen para debugging)
1. Preview: `DiceZoneView.RunComboDetection` → `ComboMatchedPayload` → `DamageFormulaView`
   delega en `DamageBreakdownView` (N = base combo, M = perilla de habilidad) y pinta los
   "+N" por dado (cara + encantos aditivos del journal at-match).
2. Confirm: `BeginPlay` llena el journal at-played → `DamageBreakdownAnnouncer` emite
   `DamageBreakdownComputedPayload` → el director levanta `BreakdownUiGate` y reproduce:
   base PJ → dados (orden de slot) → procs por dado → spinner de globales (una entrada
   por vez: pop → vuelo → tambor rota) → choque N/M → total crudo → mitigación visible (si el target mitiga) →
   libera el gate. Recién ahí `FeedbackManager` despacha la secuencia real del golpe
   (anim → "hit" → daño → floating numbers).
3. Anti soft-lock: timeout del director (8 s) + failsafe del FeedbackManager (10 s) +
   `Abort` en Unbind/OnDisable. Skip: 1er click acelera ×3, 2do salta al choque.
4. **Escudo (paridad 2026-08-10)**: la fase de defensa usa la MISMA UI — preview N×M
   (N = base de la tabla de escudo del combo, M = perilla) y secuencia completa al
   confirmar vía `AnnounceShield` (fórmula de escudo, payload sin target ⇒ nunca hay
   paso de mitigación). Limitación conocida: el escudo se APLICA al ejecutar la fase
   (los chips del HUD suben antes del choque) — solo el daño real está diferido.
5. Fuera de alcance: action rolls y exploración (sin payload → path intacto).

## Feel (pase 2026-08-10 — backlog `docs/planning/breakdown-feel-backlog-2026-08-10.md`)

### Identidad de color
- **N azul `#4FA8FF`**, **M con "heat"**: gris `#8A8F98` en 1.0 → naranja `#FF6B47` → rojo
  fuego `#FF3B2F` en 2+. Todo lo que vuela (y su trail) hereda el color de su destino.
- Backplates pill `#14141E @ .88` detrás del N×M (el spinner usa su frame propio); outline negro en los
  "+N" y en el total del choque; "+N" separa cara (hueso) de bono de encantos (dorado).
- **Convención de dorados**: dorado `#FFD75A` = weakness / bonus especial. El oro-moneda
  (`FloatingNumberPalette.Gold #FFC533`) es otro concepto — no mezclar.

### GOs nuevos en `Canvas_ActionRoll.prefab` (wireados vía MCP)
| GO | Qué es |
|---|---|
| `DamageBreakdown/Backplate` | pill oscuro (el del cascade fue reemplazado por el frame del spinner) |
| `ScreenFlashOverlay` (último sibling) | `ScreenFlashView` — flash del clash, unscaled |
| `FlyingValueLayer/ImpactBurst` | `DiceThrowImpactBurst` (bursts de contadores/choque) |
| `FlyingValueLayer/DepartSparkles` | chispas de despegue (copia de HoldSparkles) |
| `FlyingValueLayer/ProcGlowRing` | anillo del popup de proc (Knob placeholder) |
| `ClashLabel/ClashFire`, `CounterM/CounterFire` | fuego del flaming number (M ≥ 2 / ≥ 3) |
| `BreakdownDirector/FireLoopSource` | AudioSource loop del fuego |
| `FlyingValueTemplate/TrailParticles` | estela por distancia, tintada por vuelo |
| `BreakdownDirector` + `BreakdownJuice` | todo el juice, campos 100% opcionales |

### SFX
- 11 placeholders **generados proceduralmente** en `Assets/Sounds/Breakdown/`
  (generador: script Python de sesión; regenerables/reemplazables por audio final 1:1).
- Reusados: `sfx_dice_preview_tick` (roll-up del preview), `sfx_combo_chime` (ahora con
  pitch por tier de combo en `DiceZoneJuice`).

### Perillas nuevas (`BreakdownAnimSettings.asset`)
Secciones Colores / Preview / Ramp de dados / Ramp de pasos / Punches / Clash /
Mitigación / Toggles. Claves: `TierThreshold1/2` (30/80) escalan el drama del choque
(flash, hitstop, shake, partículas); `FlamingNumberMinM` (2) prende el fuego;
`DiePitchStep` sube el tono por dado; toggles `EnableSfx/EnableParticles/
EnableShakeAndHitstop` para debug.

### Step ramp (2026-08-10 — aceleración por paso, estilo Balatro)
- Cada step resuelto (base del PJ, dado, proc, modificador global) consume un índice
  que recorta linealmente el tiempo del siguiente: `StepSpeedRampPerStep` (0.07) por
  paso hasta `StepSpeedFloor` (0.45). A ~8 steps la secuencia corre a menos de la
  mitad del tiempo por step.
- **El clash y la mitigación NO rampean** — el payoff mantiene su ritmo pleno; la
  única aceleración ahí sigue siendo el skip del jugador.
- Reemplaza al viejo ramp per-die (`DieSpeedRampPerStep`/`DieSpeedFloor`, eliminados):
  aquel solo tocaba dados, así que un breakdown cargado de procs/globales nunca
  aceleraba. `_dieIndex` sigue existiendo solo para pitch y juice.
- El factor multiplica ANTES de `D()` ⇒ compone con el skip (skip + ramp se apilan).

### Game speed (x1/x2/x4) — interacción
El selector (`docs/setup/game-speed.md`) NO toca `Time.timeScale`: `D()` del director
divide todas las duraciones de la secuencia por `GameSpeedPrefs.Multiplier` (compone
con skip y step-ramp). Flash / ghost trail / tick de roll-up corren en tiempo real y
dividen también para acompañar la secuencia comprimida.

### Accesibilidad / decisiones
- `DiceUiMotionPrefs.ReducedMotion` apaga wobble, stagger, pop-ins, slide-in, shake,
  hitstop, flash y bursts (los SFX y colores quedan).
- **Sin MMF_Players nuevos**: todo el juice nuevo es PrimeTween en código (patrón
  BossBarJuice); lo MMF existente (`DiceSlotJuice.PlayKeptPulse`, flourish) se reusa.
- Hitstop = `DiceHitstop` (timeScale, idiom del repo) — los tweens escalados se congelan
  con él a propósito; el flash y el camera shake son unscaled y siguen.
- Cleanup garantizado: `BreakdownJuice.OnSequenceEnd` corre en los 3 caminos de cierre
  (normal/abort/timeout) + `OnDisable` idempotente (fuego, duck, dims de dados).

## Checklist de smoke (Play Mode, run vía BootstrapRunOverride)
- [ ] Combate normal: combo con enchants + items → secuencia completa, daño aplicado ==
      total mostrado (comparar con `DamageDebugLogger` en consola).
- [ ] Sin combo (dado más alto): el fallback anuncia y la secuencia muestra la cara UNA vez.
- [ ] Chain multi-fase: fase Attack con secuencia; fase Shield con secuencia N×M de
      escudo (sin paso de mitigación; total del choque == escudo ganado).
- [ ] Action roll (Heal / Forzar Puerta): threshold intacto, sin breakdown.
- [ ] Skip: 1 click acelera, 2 clicks saltan al choque con el total correcto (slam).
- [ ] Player muere / combate se corta a mitad de secuencia: sin soft-lock (gate liberado).
- [ ] Dados bloqueados por boss: "+N" solo en contribuyentes.
- [ ] Salir a exploración y volver: preview N×M reaparece bien (bind/unbind limpio).

### Smoke del feel (nuevo)
- [ ] Colores: N azul / M gris→rojo por valor; vuelos tintados según destino.
- [ ] Cadena larga de dados: acelera sola y el pitch sube por dado.
- [ ] Clash: wind-up → flash + hitstop + shake + burst + roll-up del total; golpe grande
      (≥80) notablemente más dramático que uno chico (<30).
- [ ] M ≥ 2: fuego sobre el total + loop de audio; se APAGA al liberar la secuencia.
- [ ] Mitigación: clank + "-X" azul-gris + tick-down; weakness: flash dorado.
- [ ] Spam de toggles de hold: roll-up sin spam de ticks (rate-limit), sin parpadeos.
- [ ] Timeout forzado (`MaxSequenceSeconds` = 1 temporal): sin fuego colgado, música
      des-duckeada, dados sin dim.
- [ ] ReducedMotion ON: sin wobble/stagger/shake/hitstop/flash, secuencia funcional.
- [ ] Toggles del SO en false: no-ops limpios.
- [ ] Spinner: spin-in de la primera entrada al poblar; por cada global pop → vuelo al
      contador → el tambor rota con freno al siguiente (texto rota con el slot, clipping
      limpio bajo el frame); la última entrada rota a vacío; con ReducedMotion, swap
      instantáneo sin tambor.

### Smoke del step ramp + game speed (nuevo)
- [ ] Breakdown largo (~10 steps) a x1: los últimos steps visiblemente más rápidos que
      los primeros; el clash mantiene su ritmo completo (wind-up + roll-up enteros).
- [ ] Skip durante un breakdown rampeado: compone (todo aún más rápido), total correcto.
- [ ] Game speed x4: secuencia comprimida, flash y trail acompañan (no quedan "largos"),
      `Time.timeScale` sigue en 1 y tras el hitstop del clash vuelve a 1.
- [ ] Cambiar velocidad desde la pausa en medio de un combate: aplica al toque.
