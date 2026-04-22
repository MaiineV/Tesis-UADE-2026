# System#0201 — Grid Foundation

> **Sprint 04 / FP closure.** Agrega la infraestructura de grilla y movimiento que el
> `TECHNICAL.md` describe en §11.3, §13.3, §17.§B y §17.§I.
> Todo lo de abajo son setup tasks para vos (agustin) — el código ya está en `Assets/Scripts/Rollgeon/Grid/`
> y `Assets/Scripts/Rollgeon/Movement/`.

---

## 0. Compile check + tests

1. Abrir Unity. Esperar recompile.
2. `Window → General → Test Runner → EditMode → Run All`.
3. Esperar **~30 nuevos tests verdes** sumados al baseline.

Si algo rompe: leer este doc + `Rollgeon/Grid/` + `Rollgeon/Movement/` antes de tocar otra cosa.

---

## 1. SOs a crear

### 1.1 `GridManagerBootstrap.asset`

- Path: `Assets/Rollgeon/Bootstrap/GridManagerBootstrap.asset`
- Create menu: `Rollgeon → Grid → Grid Manager Bootstrap`
- Sin campos que configurar. Priority = 75 (hardcoded).

### 1.2 `MovementServiceBootstrap.asset`

- Path: `Assets/Rollgeon/Bootstrap/MovementServiceBootstrap.asset`
- Create menu: `Rollgeon → Movement → Movement Service Bootstrap`
- Sin campos. Priority = 78. Depende de `IGridManager` ya registrado.

### 1.3 `RoomGridLoaderBootstrap.asset`

- Path: `Assets/Rollgeon/Bootstrap/RoomGridLoaderBootstrap.asset`
- Create menu: `Rollgeon → Dungeon → Room Grid Loader Bootstrap`
- Sin campos. Priority = 80. Depende de `IGridManager` + `IDungeonService`.

---

## 2. Registro en `ServiceBootstrap.asset`

Abrir `Assets/Rollgeon/Bootstrap/ServiceBootstrap.asset` y en **Extra Runtime Services**
agregar en este orden (entre los existentes):

| Priority | Asset |
|---|---|
| 70 | `RerollBudgetServiceBootstrap` (existente) |
| 72 | `DiceRollerBootstrap` (existente) |
| **75** | **`GridManagerBootstrap`** ← nuevo |
| **78** | **`MovementServiceBootstrap`** ← nuevo |
| **80** | **`RoomGridLoaderBootstrap`** ← nuevo |
| (…)   | resto |

> `RoomGridLoaderBootstrap` debe correr **después** de `RunController.OnRunStart`
> porque necesita `IDungeonService` ya registrado. Hoy `RunController` se crea en
> bootstrap global con priority alta; verificar corriendo Play que no hay warnings
> `[RoomGridLoaderBootstrap] IDungeonService no registrado todavia`.

---

## 3. Configurar layout en `RoomSO` assets

`RoomSO` ganó tres campos nuevos (visibles en inspector con Odin):

| Campo | Qué va |
|---|---|
| `GridLayout` (`GridSnapshot`) | Width, Height, Walkable[]. Default: dejarlo en `IsEmpty` (el `GridManager` trata eso como rectángulo walkable sin límites). Para un layout real, un futuro editor tool bakea la matriz. |
| `PlayerSpawn` (`GridCoord`) | Tile donde aparece el hero al entrar. Default `(0,0)`. |
| `EnemySpawnPoints` (`List<GridCoord>`) | Tiles donde aparecen enemigos. Si hay más enemigos que puntos, los extras roteán por la lista. |

### 3.1 Recomendación para FP

Para `Room_Combat01/02/03` (combat rooms):

```
GridLayout: vacío (IsEmpty = true) — grilla ilimitada walkable
PlayerSpawn: (0, 2)
EnemySpawnPoints:
  - (4, 1)
  - (4, 3)
  - (6, 2)
```

Para el boss room (generado runtime, ver `DungeonManager.GenerateFloor`): por ahora
queda sin layout custom — el `RoomGridLoader` detecta `IsEmpty` y trata el mapa como
walkable infinito, lo cual es suficiente para FP.

---

## 4. Smoke test manual

1. Play desde `00_Bootstrap`.
2. Menú → class → build → gameplay.
3. En Console esperar logs de bootstrap:
   ```
   [Bootstrap] Registered … IGridManager …
   [Bootstrap] Registered … IMovementService …
   ```
4. Entrar a la primera sala (combat). No debería haber errores rojos.
5. Worktree C va a agregar los GameObjects visuales — hasta entonces el grid
   funciona pero no hay pawns visibles.

---

## 5. Extender más adelante

- **§13.3 Prefab de sala.** Cuando se migre de `RoomSO` puro a `RoomSO + prefab`,
  usar `Dungeon/Components/RoomLayout.cs` + `SpawnPoint.cs` + `DoorSlot.cs` en el
  prefab. Un editor tool lee los SpawnPoints del prefab y bakea el `GridSnapshot`
  a partir de la geometría.
- **§11.3 Grid selection.** Queries tipo `TQ_ReachableTiles` se agregan cuando el
  player tenga un combo que targetee tiles (no hay en el FP).
- **Tweens visuales.** Cuando se agregue PrimeTween al proyecto, `MovementService`
  puede disparar tweens en `OnEntityMoved` (hoy la capa visual lo va a hacer en Worktree C).

---

*Generado 2026-04-21. Worktree A de `_SPRINT04_FP_CLOSURE`.*
