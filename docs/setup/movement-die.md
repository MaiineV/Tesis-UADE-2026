# Dado de Movimiento (§6.6) — entidad separada de la build de 5

> Estado al 2026-08-25: implementado y cableado vía MCP en `Feature#0054_MovementDie`.
>
> **Qué es**: Movimiento en combate ya no usa un rango fijo (`Range = 4`). Al arrastrar
> el chip de Mover, se tira un **dado propio** (`MovementDieSO`, D4 por defecto) y la
> cara revelada es la cantidad de casillas alcanzables (BFS por camino, como antes).
> El dado **no ocupa slot** del `DiceBagSO` de combate, no recibe encantamientos ni
> bloqueos de dados, y cambiar la build no lo modifica. Exploración sigue siendo
> click-to-move libre (Range 20, sin dado).

## El modelo

- **Entidad**: `Rollgeon.Movement.Die.MovementDieSO { DiceType Type = D4 }`. Cada clase
  lo referencia en `ClassHeroSO.StartingMovementDie` (null ⇒ D4).
- **Servicio**: `IMovementDieService` (Run scope, `MovementDieServiceBootstrap`,
  priority 79). Tira con un `System.Random` **propio** — no pasa por `IDiceRoller`:
  el roller registrado (`EnchantedDiceRoller`) aplica encantamientos por índice de slot
  del bag y `DiceRoller` consume la cola de rig del DevConsole; cualquiera de los dos
  acoplaría el dado a la build. El rango activo se publica **en el reveal** (no al pedir
  la tirada) para que el hover preview no spoilee la cara.
- **Rango**: `SelectionSettings.RangeFromMovementDie` (solo visible con
  `RangeMode = PathReachable`). `ResolveEffectiveRange(owner)`: tirada vigente → cara;
  servicio registrado sin tirada → cara máxima del dado (rango *potencial*, para que el
  gate del botón / hover / drag pre-tirada sigan funcionando); sin servicio o sin flag →
  `Range` autorado.
- **Flujo de combate** (`CombatHandoffService.OnBehaviorSelected`): Movement con el
  flag + servicio registrado ⇒ `SpendRollNow` (cobra **al tirar**, como toda acción
  con tirada) → `IMovementDieService.Roll` → reveal → gate post-reveal
  (`HasUsableEffectGroup`; si el tablero cambió y no hay destino, suelta la selección
  **sin reembolso**) → `DoConfirm` con `RollsPrepaid = true` (`TurnManager.TryExecute`
  no vuelve a cobrar) → selección de tile con el rango real.
- **Cambio de semántica vs BUG-013**: cancelar Movimiento **después** de ver la cara
  ya no reembolsa el roll (consistente con ataques/defensa). Antes de arrastrar sigue
  siendo gratis. Sin `IMovementDieService` registrado (escenas viejas, tests) el path
  legacy de cobro-al-ejecutar queda intacto.
- **Drag-and-drop**: `ActionDragPolicy.RequiresTileDrop` devuelve `false` con el flag
  — el drop en cualquier lado fuera de la UI dispara la tirada (mismo gesto que Heal /
  Forzar Puerta) y el tile se elige después, con el rango revelado.
  `ActionPlayDispatcher.Commit(..., feedTile: false)` no alimenta la selección con la
  celda del drop.
- **Evento**: `EventName.OnMovementDieRolled [Guid, int face, DiceType]` (al final del
  enum). NO dispara `OnDiceRolled` — el `DiceZoneView` no lo ve.

## UI — el dado usa la mesa como cualquier tirada

`MovementDieView` (`UI/HUD/MovementDieView.cs`) vive **centrado en el `RollArea` de
`Canvas_ActionRoll`** y está **oculto salvo durante su tirada**: al soltar Mover, el
servicio emite `OnMovementDieRollStarted` → la mesa se abre (`ActionRollExplorationVisibility`)
y los chips se apagan (`CombatHudZoneFlow`), igual que con `OnDiceRolled`; el dado gira en el
centro (`DiceSlotAnimator`, mismo spin y pacing por `GameSpeedPrefs` que los 5 de la build),
revela la cara, la deja leer `_revealHoldSeconds` (0.6 s / game speed) y se esconde; recién
ahí el servicio emite `OnMovementDieRolled` → la mesa se cierra, los chips vuelven y arranca
la selección de tile con el rango revelado. Es el `IMovementDiePresenter` del servicio; sin
la view cableada el dado igual se tira (reveal sincrónico, sin mesa). No toca los 5 slots
del `DiceZoneView`.

## Wiring (ya aplicado vía MCP, 2026-08-25)

1. `Assets/Rollgeon/Services/MovementDieServiceBootstrap.asset`
   (Create → Rollgeon → Movement → Movement Die Service Bootstrap) agregado a
   `Assets/Rollgeon/ServiceBootstrap.asset → ExtraServices`.
2. `Assets/Resources/Dice/AD_Warrior_MovementDie.asset`
   (Create → Rollgeon → Dice → Movement Die), `Type = D4`.
3. `Assets/Rollgeon/Classes/CH_Warrior.asset`:
   - `StartingMovementDie` → `AD_Warrior_MovementDie`.
   - Behavior `Movement` (combate) → `EffMove.Selection.RangeFromMovementDie = true`.
     `ExpMovement` queda en `false`.
   - Nota: el asset es Odin-serialized — setear el flag a mano en el YAML **no alcanza**
     (hay doble representación); hacerlo desde el Inspector o por código en el editor.
4. `Assets/Prefabs/UI/Canvas/Canvas_ActionRoll.prefab`:
   `DiceZoneView/RollArea/MovementDieView` (RectTransform 100×100 centrado en el
   RollArea) con `MovementDieView` + hijo `Slot` **inactivo** (instancia de
   `Assets/Prefabs/UI/DiceSlotView.prefab`). `CombatHUDView._movementDie` queda en
   None: es cross-canvas y `BindAll` lo resuelve con `FindFirstObjectByType` (mismo
   patrón que `HealthChipStackView`).

Para otra clase: crear su `MovementDieSO`, asignarlo en `StartingMovementDie` y activar
`RangeFromMovementDie` en el `EffMove` de su Movement de combate.

## Tests

- `Movement/Tests/MovementDieServiceTests.cs` — entidad separada (bag intacto,
  `SetDiceBag` no afecta, override no toca la build), RNG propio, reveal diferido,
  clear invalida reveals tardíos, fin de combate limpia.
- `Effects/Tests/SelectionSettingsMovementDieRangeTests.cs` — rango efectivo.
- `Combat/Handoff/Tests/CombatHandoffServiceTests.cs` (`MovementDie_*`) — cobro único,
  prepago, gate post-reveal sin reembolso, reveal tras fin de combate ignorado, path
  legacy sin servicio.
- `UI/Tests/ActionDragPolicyTests.cs` — `RequiresTileDrop` con el flag.

## Follow-ups (fuera de alcance)

- Throw manual 2D/3D del dado (anchor propio en `DiceThrow2DPresenter`).
- Rig del DevConsole para el dado de Movimiento.
- Encantamientos / upgrades del dado y ofrecerlo en el build screen.
- Skin `DiceBoardType.Movement`; preview de rango en hover muestra la cara máxima.
