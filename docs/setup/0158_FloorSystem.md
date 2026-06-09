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

**La salida es DINÁMICA — NO hace falta una puerta de salida fija.** El `DungeonManager`
garantiza que la boss room sea un **dead-end (1 sola entrada)** y designa en runtime como
salida de piso (`IsExit`) la puerta **opuesta a la entrada**. O sea: cualquiera de las 4
puertas cardinales puede terminar siendo la salida según por dónde se entre; las 2
perpendiculares quedan tapiadas. No se rota la sala. (Resetea cualquier `IsExit` autoreado,
así que un `ExitDoor` fijo en el prefab **sobra** — conviene quitarlo.)

Lo único que necesita el prefab `Boss_Room03.prefab`:

1. Sus **4 puertas cardinales `Door`** (ya las tiene, clones de `Assets/Prefabs/Tiles/Door.prefab`),
   con sus `DoorSlots` autoreados (Auto-Populate en `RoomLayout`) — entrada/salida/walls los
   resuelve el runtime.
2. **Que el `NavGraph` exponga como caminable el tile-frente de las 4 puertas** (no solo el de
   la entrada): cualquiera puede ser la salida. El tile-frente =
   `WorldToGrid(door.position) + Direction.InwardOffset()`. Si falta alguno, rebakealo desde
   el Room Editor incluyendo esas casillas.
3. (Opcional, polish — pendiente) "Visualmente distinguible" del GDD: como la salida es
   dinámica, no hay un mesh fijo distinto. Se puede agregar un hijo `ExitGlow` al
   `Door.prefab` que se prenda cuando la puerta está `Open` **y** es `IsExit`, o swapear
   material en runtime. Queda como follow-up.

> La puerta de salida arranca cerrada (`LockedCombat`) y pasa a `Open` sola al derrotar al
> boss (mismo path que el unlock de puertas). La entrada (puerta de la conexión) también se
> abre al clearear — el player puede volver o tomar la salida.

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
