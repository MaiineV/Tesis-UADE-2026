# Dado de Movimiento (§6.6) — entidad separada de la build de 5

> Estado al 2026-08-25: implementado y cableado vía MCP en `Feature#0054_MovementDie`.
>
> **Qué es**: Movimiento en combate ya no usa un rango fijo (`Range = 4`). Al arrastrar
> el chip de Mover, se tira un **dado propio** (`MovementDieSO`, D6 por defecto) y la
> cara revelada es la cantidad de casillas alcanzables (BFS por camino, como antes).
> El dado **no ocupa slot** del `DiceBagSO` de combate, no recibe encantamientos ni
> bloqueos de dados, y cambiar la build no lo modifica. Exploración sigue siendo
> click-to-move libre (Range 20, sin dado).

## El modelo

- **Entidad**: `Rollgeon.Movement.Die.MovementDieSO { DiceType Type = D6 }`. Cada clase
  lo referencia en `ClassHeroSO.StartingMovementDie` (null ⇒ D6).
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

## UI — dado suelto detrás de la ficha de Mover

`MovementDieView` (`UI/HUD/MovementDieView.cs`) es hermano **anterior** a `MoveButton` en
`PlayerActionButtonsView` (se dibuja detrás), con los mismos anchors/pivot/tamaño que la
ficha (`_chip`), y está **oculto salvo durante su acción**:

1. Al soltar Mover: el dado aparece detrás de la ficha y **sube con fade-in mientras
   rolea** (escala `_startScale → 1`) hasta `_overshoot` por encima de su posición final.
   El roleo es **el mismo de la mesa**: no rota el transform, cicla las siluetas
   Front/SideA/SideB del `DiceShapeCatalog` con ticks que desaceleran a lo largo de todo el
   recorrido, leyendo `Resources/Dice/DiceUiAnimationSettings` (`SpinTickSeconds`,
   `SpinDecelerationPower`, `ShowPreviewFacesDuringSpin`) — helpers de
   `DiceAnimChoreographer`.
2. **Drop-in**: cae a la posición final (encima de la ficha, `_gap` de separación, misma X
   y ancho ⇒ simétrico) con ease-out-bounce y un squash de aterrizaje.
3. Al aterrizar muestra la cara real y **recién ahí** publica el rango (`onRevealed`).
4. Queda visible hasta que el jugador elige a dónde moverse (o End Turn / fin de combate):
   `OnCleared` → fade-out corto y se oculta.

Todo sigue al game speed. Es el `IMovementDiePresenter` del servicio; sin la view cableada
el dado igual se tira (reveal sincrónico). **No usa la mesa de dados**: los eventos
`OnMovementDieRollStarted`/`OnMovementDieRolled` se emiten pero ni
`ActionRollExplorationVisibility` ni `CombatHudZoneFlow` los escuchan.

### Cancel: la acción queda comprometida tras la tirada

Con el dado ya tirado (`_movementRollPrepaid`), `HasCancellableSelection` es `false`: el
click derecho y los clicks de slot (mismo u otro) **no cancelan** el Movement, y
`PlayerActionButtonsView` muestra los demás slots `Locked`. Antes de soltar Mover no hay
nada que cancelar. **End Turn** sigue soltando la selección (pierde el roll pagado) — es la
única salida si el jugador no quiere moverse. El Movement legacy (sin servicio) conserva el
cancel gratis de BUG-013.

## Wiring (ya aplicado vía MCP, 2026-08-25)

1. `Assets/Rollgeon/Services/MovementDieServiceBootstrap.asset`
   (Create → Rollgeon → Movement → Movement Die Service Bootstrap) agregado a
   `Assets/Rollgeon/ServiceBootstrap.asset → ExtraServices`.
2. `Assets/Resources/Dice/AD_Warrior_MovementDie.asset`
   (Create → Rollgeon → Dice → Movement Die), `Type = D6`.
3. `Assets/Rollgeon/Classes/CH_Warrior.asset`:
   - `StartingMovementDie` → `AD_Warrior_MovementDie`.
   - Behavior `Movement` (combate) → `EffMove.Selection.RangeFromMovementDie = true`.
     `ExpMovement` queda en `false`.
   - Nota: el asset es Odin-serialized — setear el flag a mano en el YAML **no alcanza**
     (hay doble representación); hacerlo desde el Inspector o por código en el editor.
4. `Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab`:
   `CombatHUDView/PlayerActionButtonsView/MovementDieView` — hermano **anterior** a
   `MoveButton` (sibling index 0), mismos anchors/pivot/posición/tamaño que la ficha
   (100×100 en `(-250, 5)`), `CanvasGroup` alpha 0 / sin raycast, `MovementDieView` con
   `_slot` → hijo `Slot` **inactivo** (instancia de `Assets/Prefabs/UI/DiceSlotView.prefab`,
   stretch al padre) y `_chip` → `MoveButton`. `CombatHUDView._movementDie` queda en None:
   `BindAll` lo resuelve con `FindFirstObjectByType`.

5. **Chips de item (`Feature#0068`, 2026-09-02, vía MCP)** — bajo `MovementDieView`:
   `ChipsRoot` (anclado arriba del dado, `VerticalLayoutGroup` LowerCenter con
   `reverseArrangement` para que el primer chip quede pegado al dado, `ContentSizeFitter`
   vertical) y `ChipTemplate` **inactivo** (`Image` fondo oscuro 0.88 alpha sin sprite —
   placeholder, no hay 9-slice en `Assets/Art/UI` —, `HorizontalLayoutGroup`, hijos `Icon`
   32×32 e `Label` TMP m6x11plus 24 auto-size, `ModifierEntryView` con `_icon`/`_label` y
   `_labelColor` pergamino claro). `MovementDieView._chipTemplate` → ese `ModifierEntryView`,
   `_chipsRoot` → `ChipsRoot`, `_procClip` → `Assets/Sounds/Breakdown/sfx_breakdown_proc_item.wav`
   (mismo placeholder del breakdown). `_chipFallbackIcon` queda en None: sin sprite el
   `Image` del icono se apaga solo. Sin `_chipTemplate` la view no muestra chips y no loguea.

Para otra clase: crear su `MovementDieSO`, asignarlo en `StartingMovementDie` y activar
`RangeFromMovementDie` en el `EffMove` de su Movement de combate.

## Tests

- `MovementRangeAttributionTests`: función pura (Add/Subtract, fuente desconocida, merge
  por item, Multiply ignorado, orden de inventario) y wrapper por `ServiceLocator`.
- `MovementDieServiceTests.Roll_WithPresenter_PassesEmptyContributions_WhenNoInventoryIsRegistered`.
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
