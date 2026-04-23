# System #0205 — Room Prefab Migration Setup

> Checklist de configuración en Unity Editor para el sistema de salas prefab-based
> (Isaac-style floor graph). Implementado en `sprint05/feature/0205-rooms-migration`.
> Solo cubre autoría de assets/prefabs — el código ya está en `develop`.

---

## Assets que necesitás crear

```
Assets/Rollgeon/
  Prefabs/
    Rooms/
      Room_Start.prefab
      Room_Combat01.prefab    ← mínimo 2-3 combat rooms
      Room_Boss.prefab
    Door/
      DoorPrefab.prefab
  ScriptableObjects/
    Dungeon/
      Room_Start.asset
      Room_Combat01.asset
      Room_Boss.asset
      EnemySetup_*.asset      ← opcional pero recomendado
      FloorLayout_Debug.asset
```

---

## Paso 1 — Prefab de sala

### Jerarquía del prefab (ej. `Room_Combat01.prefab`)

```
Room_Combat01 (root)
├── [RoomLayout component]
├── Floor_Mesh           ← Renderer (necesario para que OnValidate calcule LocalBounds)
├── GridOrigin           ← Empty Transform en la celda (0,0) del grid
├── PlayerSpawn          ← Empty Transform donde spawnea el héroe
├── EnemySpawns
│   ├── EnemySpawn_0     ← index 0
│   └── EnemySpawn_1     ← index 1
├── Door_N
│   ├── DoorAnchor_N     ← Empty Transform (pose del DoorPrefab instanciado)
│   ├── WallPlug_N       ← Cube/Quad tapando la pared Norte (activo por default)
│   └── DoorRoot_N       ← opcional: mesh de puerta autorada
├── Door_S
│   ├── DoorAnchor_S
│   ├── WallPlug_S
│   └── DoorRoot_S
├── Door_E
│   ├── DoorAnchor_E
│   ├── WallPlug_E
│   └── DoorRoot_E
└── Door_W
    ├── DoorAnchor_W
    ├── WallPlug_W
    └── DoorRoot_W
```

### Checklist del componente `RoomLayout` (Inspector del root)

- [ ] `Grid Origin` → drag del Transform `GridOrigin`
- [ ] `Tile Size` → float del tamaño de celda (default `1`)
- [ ] `Grid Override` → dejar vacío = rectángulo sin obstáculos
- [ ] `Player Spawn Point` → drag del Transform `PlayerSpawn`
- [ ] `Enemy Spawn Points` → drag de `EnemySpawn_0`, `EnemySpawn_1`, etc.
- [ ] `Door Slots` → 4 entradas (ver tabla abajo)
- [ ] `Local Bounds` → se recalcula solo al guardar el prefab (necesita al menos un Renderer hijo)

### Cada `DoorSlotRef` en la lista `Door Slots`

| Campo | Qué asignar |
|---|---|
| `Direction` | `North` / `South` / `East` / `West` |
| `Anchor` | Empty Transform de pose (`DoorAnchor_N`, etc.) |
| `Wall Plug` | Cube/Quad de pared tapiada (`WallPlug_N`, etc.) |
| `Door Root` | Mesh de puerta ya en el prefab — opcional |

> **Importante:** el `WallPlug` debe arrancar **activo** en el prefab. El `DungeonManager`
> lo desactiva cuando hay vecino en esa dirección; si no hay vecino, lo deja activo.

---

## Paso 2 — Prefab de puerta (`DoorPrefab`)

El `DungeonManager` instancia este prefab sobre el `Anchor` de cada slot conectado.

### Jerarquía

```
DoorPrefab (root)
├── [DoorController component]
├── MeshOpen      ← activo por default
├── MeshClosed    ← inactivo por default
└── WallPlug      ← inactivo por default
```

### Checklist del componente `DoorController`

- [ ] `Mesh Open` → drag de `MeshOpen`
- [ ] `Mesh Closed` → drag de `MeshClosed`
- [ ] `Wall Plug` → drag de `WallPlug`

> `Owner Room Instance Id`, `Direction` y `Spawn Point Id` los setea el `DungeonManager`
> en runtime — no hay que tocarlos en el prefab.

**Comportamiento de estados:**

| Estado | MeshOpen | MeshClosed | WallPlug |
|---|---|---|---|
| `Open` | ✓ | — | — |
| `LockedCombat` | — | ✓ | — |
| `LockedSkillCheck` | — | ✓ | — |
| `Tapiada` | — | — | ✓ |

---

## Paso 3 — Assets `RoomSO`

**Create → Rollgeon / Dungeon / Room**

Por cada sala:

- [ ] `Room Id` → string único, ej. `"start"`, `"combat_01"`, `"boss_01"`
- [ ] `Display Name` → nombre legible
- [ ] `Type` → `Start` / `Combat` / `Boss` / `Shop` / `Potion`
- [ ] `Room Prefab` → drag del prefab del paso 1
- [ ] `Grid Size` → `(1,1)` para sala estándar
- [ ] `Possible Setups` → lista de `EnemySetupSO` (puede quedar vacía; cae al EnemyPool)
- [ ] `Enemy Pool` → drag del `EnemyPoolSO` existente como fallback

---

## Paso 4 — Assets `EnemySetupSO` (opcional)

**Create → Rollgeon / Dungeon / Enemy Setup**

- [ ] `Setup Name` → ej. `"2 Goblins"`
- [ ] `Slots` → lista de `SetupSlot`:
  - `Spawn Point Index` → entero que indexa `RoomLayout.EnemySpawnPoints` (0-based)
  - `Enemy` → drag del `EnemyDataSO`

Asignás cada `EnemySetupSO` a `RoomSO.Possible Setups`. Al entrar a la sala se elige
uno al azar; si la lista está vacía se usa el `EnemyPool` ponderado.

---

## Paso 5 — `FloorLayoutSO`

**Create → Rollgeon / Dungeon / Floor Layout**

- [ ] `Room Count Min` → ej. `4` (mínimo para prueba rápida)
- [ ] `Room Count Max` → ej. `6`
- [ ] `Combat Rooms` → lista de `RoomSO` de tipo Combat
- [ ] `Shop Rooms` → lista de `RoomSO` tipo Shop (puede estar vacía)
- [ ] `Potion Rooms` → idem
- [ ] `Default Boss Room Template` → `RoomSO` de tipo Boss con prefab y setups
- [ ] `Start Room` → `RoomSO` de tipo Start (sala inicial del run)

> **Mínimo viable:** 1 `StartRoom` + 2 `CombatRooms` + 1 `DefaultBossRoomTemplate`.

---

## Paso 6 — Asignar el `FloorLayoutSO` al bootstrapper

- [ ] En el `GameplayBootstrapper` (SO o componente de escena), asignar el `FloorLayoutSO`
  del paso 5 al campo que se pasa a `DungeonManager.GenerateFloor(layout, seed)`.

---

## Paso 7 — Verificación en Play Mode

1. **Play** → `DungeonManager.GenerateFloor` construye el grafo (random walk Isaac).
2. El héroe aparece en `PlayerSpawnPoint` de la `StartRoom`.
3. Las puertas sin vecino muestran `WallPlug` activo (pared tapiada).
4. Caminar hacia un `DoorAnchor` → `EnterRoomByDoor` → nueva sala instanciada, `OnRoomEntered` fired.
5. Sala de combate: las 4 puertas pasan a `LockedCombat`.
6. Matar todos los enemigos → `OnCombatEnd(Victory)` → puertas vuelven a `Open`, `OnRoomCleared` fired.
7. Retroceder a sala ya limpiada → los enemigos no vuelven a spawnear.
8. Zoom out hasta `FloorViewZoomThreshold` → shells del piso visibles, sala actual oculta.

---

## Checklist mínimo para primera prueba

- [ ] `Room_Start.prefab` — `RoomLayout` con 4 `DoorSlotRef`
- [ ] `Room_Combat01.prefab` — `RoomLayout` con ≥2 `EnemySpawnPoints`
- [ ] `Room_Boss.prefab` — `RoomLayout` con `EnemySpawnPoints`
- [ ] `DoorPrefab` — `DoorController` + `MeshOpen` / `MeshClosed` / `WallPlug`
- [ ] `RoomSO` para cada prefab con `Room Prefab` asignado
- [ ] `FloorLayoutSO` con `StartRoom` + ≥2 `CombatRooms` + `DefaultBossRoomTemplate`
- [ ] `FloorLayoutSO` asignado al `GameplayBootstrapper`
