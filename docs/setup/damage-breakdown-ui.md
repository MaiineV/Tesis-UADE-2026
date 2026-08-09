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
| `GlobalModifierCascade` (anchor der., −40, 260, 240×420) | `GlobalModifierCascadeView` + CanvasGroup (alpha 0) | `EntriesRoot` + `EntryTemplate` inactivo (Icon 48×48 + Label). **`_fallbackIcon` sin asignar** — sugerido `Assets/Art/UI/Inventory/ItemSlot.png` mientras los `ItemSO.Icon` estén vacíos. **Verificar solape con Confirm/Reroll en el editor.** |
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
2. **`_fallbackIcon`** del cascade y **`_mitigationSprite`** del director.
3. **Iconos de `ItemSO`**: los 22 items tienen `Icon` vacío — mientras tanto los popups
   muestran el fallback + el "+X".
4. Pasada de layout en editor: solape del cascade con `ConfirmButton`/`RerollCountView`,
   tamaños de fuente, posición del `ClashAnchor`.

## Cómo funciona (resumen para debugging)
1. Preview: `DiceZoneView.RunComboDetection` → `ComboMatchedPayload` → `DamageFormulaView`
   delega en `DamageBreakdownView` (N = base combo, M = perilla de habilidad) y pinta los
   "+N" por dado (cara + encantos aditivos del journal at-match).
2. Confirm: `BeginPlay` llena el journal at-played → `DamageBreakdownAnnouncer` emite
   `DamageBreakdownComputedPayload` → el director levanta `BreakdownUiGate` y reproduce:
   base PJ → dados (orden de slot) → procs por dado → cascade global (de abajo hacia
   arriba) → choque N/M → total crudo → mitigación visible (si el target mitiga) →
   libera el gate. Recién ahí `FeedbackManager` despacha la secuencia real del golpe
   (anim → "hit" → daño → floating numbers).
3. Anti soft-lock: timeout del director (8 s) + failsafe del FeedbackManager (10 s) +
   `Abort` en Unbind/OnDisable. Skip: 1er click acelera ×3, 2do salta al choque.
4. Fuera de alcance: fase Shield del chain (sin sequence propia — el label de escudo
   sigue como antes), action rolls y exploración (sin payload → path intacto).

## Checklist de smoke (Play Mode, run vía BootstrapRunOverride)
- [ ] Combate normal: combo con enchants + items → secuencia completa, daño aplicado ==
      total mostrado (comparar con `DamageDebugLogger` en consola).
- [ ] Sin combo (dado más alto): el fallback anuncia y la secuencia muestra la cara UNA vez.
- [ ] Chain multi-fase: fase Attack con secuencia; fase Shield intacta (label viejo).
- [ ] Action roll (Heal / Forzar Puerta): threshold intacto, sin breakdown.
- [ ] Skip: 1 click acelera, 2 clicks saltan al choque con el total correcto.
- [ ] Player muere / combate se corta a mitad de secuencia: sin soft-lock (gate liberado).
- [ ] Dados bloqueados por boss: "+N" solo en contribuyentes.
- [ ] Salir a exploración y volver: preview N×M reaparece bien (bind/unbind limpio).
