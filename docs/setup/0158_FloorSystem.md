# #158 — Sistema de pisos: setup de engine

Guía para terminar el wiring de engine del sistema de pisos. El **código y los
tests están completos y compilando**; esta guía cubre lo que necesita tu mano en
el editor (un prefab espacial) más el playtest end-to-end.

---

## Lo que ya está hecho (código + MCP)

**Código (rama `Feature#0009_FloorSystem`):**
- Sistema de **tiers** de enemigos: `EnemyDataSO.ExtraTiers` (Multiplicador / Manual
  por-stat), `CreateRuntimeStats(int tier)`, probabilidad de tier por spawner
  (`SpawnPointConfig.SetTierWeights`, `EnemySetupSO.SetupSlot.TierWeights`,
  `EnemyPoolSO.EntryTierWeights`). Aplicado en `DefaultEnemySpawnResolver`.
- `floorDepth` real desde `IRunContextService.FloorIndex` en shop y rewards;
  `RunContext` implementa `ISaveable`.
- **Puerta de salida física**: `DoorController.IsExit`. La puerta se abre al
  derrotar al boss (vía `SyncDoorVisualStates` en `OnCombatEnd(Victory)`); al pisar
  su tile-frente, `ExplorationBehaviorService` dispara `OnFloorExitRequested`.
- `FloorProgressionService` (run-scoped): fade → `AdvanceFloor()` → `GenerateFloor`
  del piso siguiente → `FloorTransitionScreen`. Victoria desacoplada a `OnRunVictory`
  (solo en el piso terminal).

**Contenido (configurado por MCP):**
- Cadena de pisos: `FloorLayout` (Piso 1) → `Floor2_Layout` (Piso 2) →
  `Floor3_Layout` (Piso 3, terminal). El run arranca en `FloorLayout`
  (`RunControllerBootstrapper._defaultLayout`).
- Tiers de ejemplo: `ED_Goblin` T2 multiplicador (HP ×1.3, Atk ×1.2, Spd ×1.1);
  `ED_Healer` T2 mixto (HP manual 50, Spd manual 2, Atk ×1.5).
- `EP_01` (pool de la boss room) reparte el Goblin **80% T1 / 20% T2**.

---

## Falta: puerta de salida en el prefab de la boss room

**Prefab:** `Assets/Prefabs/Rooms/Boss_Room03.prefab` (lo usa `Room_Boss01`, la única
boss room). Ya tiene 4 puertas cardinales `Door` (clones de
`Assets/Prefabs/Tiles/Door.prefab`) y un `RoomLayout` en la raíz.

La puerta de salida es un **5º `DoorController` con `IsExit = true`**, independiente de
las 4 cardinales (no usa los `DoorSlots` ni necesita vecino). Pasos:

1. Abrí `Boss_Room03.prefab` en modo prefab.
2. Duplicá uno de los `Door` existentes (o instanciá `Assets/Prefabs/Tiles/Door.prefab`)
   como hijo de la raíz. Renombralo `ExitDoor`.
3. En su `DoorController`:
   - **`IsExit` = true** (lo demás lo setea el `DungeonManager` en runtime).
   - **`Direction`** = la cardinal cuyo `InwardOffset` apunte hacia adentro de la sala
     desde donde lo coloques (ej. si lo ponés sobre la pared Norte, `Direction = North`,
     y el tile-frente cae un paso al Sur, hacia el interior).
4. **Posición**: pegalo a un segmento de pared (los `TileWall`) en una dirección que
   quede **libre** visualmente, y verificá que el **tile interior de enfrente sea
   caminable** en el `NavGraph` (debe ser un tile por el que el player pueda pararse).
   El tile-frente = `WorldToGrid(door.position) + Direction.InwardOffset()`.
5. (Opcional, polish) Dale al hijo `MeshOpen` un material/emisivo distinto para que la
   salida sea **visualmente distinguible** del resto de puertas (lo pide el GDD).
6. Guardá el prefab. No hace falta tocar `RoomLayout.DoorSlots` — las puertas exit se
   resuelven por `GetComponentsInChildren<DoorController>()` con `IsExit`.

> La puerta arranca cerrada (`LockedCombat`) y pasa a `Open` sola al derrotar al boss.
> Si el `NavGraph` no expone el tile-frente como caminable, el player no podrá pisarlo
> — rebakealo desde el Room Editor incluyendo esa casilla.

---

## Playtest: 3 pisos lineales

1. Play desde `00_Bootstrap` → menú → build select → `02_Gameplay`.
2. **Piso 1**: limpiá el boss → la puerta de salida se abre (mesh open) → caminá hasta su
   tile-frente → fade → pantalla **"Piso 2"** → Continue → explorás el Piso 2 con
   **HP / oro / dados / inventario intactos** (verificá el HUD).
3. **Piso 2**: confirmá que los enemigos pueden salir en **T2** (healthbars más altas que
   en Piso 1, según el 80/20 del pool) → seguí al boss → salida → **Piso 3**.
4. **Piso 3 (terminal)**: limpiá el boss → salida → **VictoryScreen** (no otra
   transición — `Floor3_Layout.NextFloor` es null).
5. Console limpia (sin warnings de reciprocidad de puertas en los pisos nuevos).

**Para verificar `floorDepth`** (lo consume `ShopPoolSO`): poné un `WeightedShopItem`
con `MinFloorDepth = 1` en el pool de la tienda; debería aparecer recién desde el Piso 2.

---

## Fuera de alcance (follow-up, no en esta entrega)

Rutas secundarias / puertas alternativas configurables por sala, piso alternativo,
condición de salida `Special`, `AttackRange` como atributo runtime. El `DoorController.IsExit`
y `FloorLayoutSO` están diseñados para extenderse a eso sin reescritura.
