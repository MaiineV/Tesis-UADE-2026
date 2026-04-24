# System #0207 — Full Floor Loading, NavGraph, Bake Tool & SpawnPointConfig

> Checklist de configuración en Unity Editor para los nuevos sistemas de
> generación de piso completo, NavGraph con bake tool, y enemy spawn sets
> por spawn point. El código ya está en `develop`.

---

## Resumen de cambios

| Sistema | Qué cambió |
|---------|-----------|
| Floor loading | Todas las salas se instancian al generar el piso (no una a la vez) |
| CellSpacing | De 2f a 0f — las salas quedan edge-to-edge, puertas flush |
| Shell material | Configurable desde `CameraConfigSO.ShellMaterial` (no hardcodeado) |
| NavGraph | Reemplaza `GridSnapshot` como estructura de pathfinding |
| Bake tool | Botón "Bake NavGraph" en el Inspector de `RoomLayout` |
| SpawnPointConfig | Enemy sets configurables por spawn point GO |

---

## Paso 1 — Shell Material (CameraConfigSO)

### 1.1 Crear material URP para shells

1. Project Window → clic derecho → `Create > Material`
2. Nombre: `FloorShell_Mat`
3. Ubicación sugerida: `Assets/Rollgeon/Materials/FloorShell_Mat.mat`
4. En Inspector del material:
   - Shader: `Universal Render Pipeline/Unlit`
   - Surface Type: `Transparent`
   - Color: elegir color oscuro semi-transparente (ej. `RGBA 25, 25, 38, 217` ≈ el default `0.1, 0.1, 0.15, 0.85`)

### 1.2 Asignar en CameraConfigSO

1. Buscar el asset `CameraConfig` (debería estar en `Assets/Rollgeon/Camera/`)
2. En la sección **Floor View**:
   - [ ] `Shell Material` → drag del material `FloorShell_Mat`
   - [ ] `Shell Color` → sigue disponible como fallback si `Shell Material` queda null

> **Nota**: Si dejás `Shell Material` en null, se crea uno dinámico con
> `URP/Unlit` + `ShellColor`. Ya no debería verse magenta.

---

## Paso 2 — Room Prefabs (puertas flush)

### 2.1 Verificar bounds de sala

Con `CellSpacing = 0`, las salas se colocan edge-to-edge usando el bounds
máximo de todas las salas. Para que las puertas estén pegadas:

1. Abrir cada room prefab (`Room_Combat01`, etc.)
2. Verificar que `RoomLayout.LocalBounds` se calcula correctamente:
   - Debe haber al menos un `Renderer` hijo (floor mesh)
   - El bounds debe cubrir toda la geometría de la sala
3. **Los door anchors deben estar en el borde EXACTO del LocalBounds**:
   - `DoorAnchor_N` → en el borde norte (Z máximo del bounds)
   - `DoorAnchor_S` → en el borde sur (Z mínimo del bounds)
   - `DoorAnchor_E` → en el borde este (X máximo del bounds)
   - `DoorAnchor_W` → en el borde oeste (X mínimo del bounds)

### 2.2 Uniformar tamaño de salas

- [ ] **Recomendado**: todas las salas deben tener el mismo `LocalBounds.size`
- Si una sala es más chica que el máximo, quedará centrada con espacio vacío
  alrededor (las puertas de la sala vecina no coincidirán exactamente)
- Alternativa: usar salas del mismo tamaño y variar solo el contenido interior

### 2.3 Verificar en Play Mode

1. Entrar en gameplay → verificar que todas las salas aparecen instanciadas
2. Caminar entre salas → las puertas deben estar pegadas sin gaps visibles
3. Las salas vecinas deben ser visibles todo el tiempo (no se destruyen al salir)

---

## Paso 3 — NavGraph Bake (por cada room prefab)

### 3.1 Construir sala con tiles

Los room prefabs ahora deben construirse con tiles (GameObjects con
`Renderer`). Cada tile = 1 nodo en el NavGraph.

Estructura recomendada:
```
Room_Combat01 (root)
├── [RoomLayout component]
├── Tiles
│   ├── Tile_0_0  ← Cube/Quad en localPos (0, 0, 0)
│   ├── Tile_1_0  ← Cube/Quad en localPos (1, 0, 0)
│   ├── Tile_0_1  ← Cube/Quad en localPos (0, 0, 1)
│   ├── Tile_1_1  ← Cube/Quad en localPos (1, 0, 1)
│   ├── Tile_2_0  ← posición elevada: (2, 0.5, 0)
│   └── ...
├── Walls
│   ├── Wall_N    ← geometría visual, NO tiles de pathfinding
│   └── ...
├── GridOrigin
├── PlayerSpawn
├── EnemySpawns
│   ├── EnemySpawn_0
│   └── EnemySpawn_1
└── Doors (N/S/E/W como antes)
```

**Reglas de tiles**:
- Cada tile debe tener un `Renderer` (Cube, Quad, o mesh custom)
- La posición local del tile respecto al root determina su `GridCoord`:
  - `X = Round(localPos.x / TileSize)`
  - `Y = Round(localPos.z / TileSize)`
  - `Height = localPos.y`
- Tiles a distancia Manhattan > 1 no se conectan
- Tiles con diferencia de altura > `HeightThreshold` no se conectan

### 3.2 Configurar BakeSettings

En el Inspector de `RoomLayout`:

- [ ] `Bake Settings > Tile Size` → tamaño de celda (default `1`)
- [ ] `Bake Settings > Height Threshold` → diferencia máxima de altura
  para conectar nodos (default `0.5`)

### 3.3 Bakear el NavGraph

1. Seleccionar el root del room prefab
2. En el Inspector de `RoomLayout`, clic en **"Bake NavGraph"**
3. La consola muestra: `[NavGraphBaker] Baked N nodes, M edges.`
4. El HelpBox debajo del botón muestra el conteo
5. **Guardar el prefab** (Ctrl+S) para persistir el NavGraph

### 3.4 Verificar con Gizmos

1. Con el prefab seleccionado en la Scene:
   - **Esferas verdes** = nodos (uno por tile)
   - **Líneas verdes** = edges (conexiones entre nodos adyacentes)
   - **Labels** = altura de cada nodo (`h=0.0`, `h=0.5`, etc.)
2. Si un edge es incorrecto (conecta nodos que no deberían conectarse):
   - No hay editor visual para borrar edges todavía
   - En código: usar `NavGraph.RemoveBidirectionalEdge(coordA, coordB)`
   - O ajustar la altura del tile para que supere el threshold

### 3.5 Repetir para cada room prefab

- [ ] `Room_Start.prefab` → bake
- [ ] `Room_Combat01.prefab` → bake
- [ ] `Room_Combat02.prefab` → bake (si existe)
- [ ] `Room_Boss.prefab` → bake
- [ ] Otros rooms → bake

---

## Paso 4 — SpawnPointConfig (enemy sets por spawn point)

### 4.1 Agregar componente a spawn points

En cada room prefab, por cada `EnemySpawn_X` Transform:

1. Seleccionar el GameObject `EnemySpawn_0`
2. `Add Component > SpawnPointConfig`
3. En **Enemy Sets** (lista):
   - Element 0 (Set 01) → drag de un `EnemyDataSO` (ej. `Goblin`)
   - Element 1 (Set 02) → drag de otro `EnemyDataSO` (ej. `Skeleton`)
   - Element 2 (Set 03) → drag de otro (ej. `Archer`)
4. Repetir para `EnemySpawn_1`, `EnemySpawn_2`, etc.

### 4.2 Reglas de selección de sets

- Al iniciar una sala, se elige **UN set index al azar** (ej. Set 02)
- **TODOS** los spawn points de esa sala usan el mismo set index
- El rango de selección es el **mínimo** de `SetCount` entre todos los
  `SpawnPointConfig` de la sala
- Si un spawn point NO tiene `SpawnPointConfig`, se usa `EnemyPoolSO`
  como fallback para ese punto específico

### 4.3 Ejemplo

```
EnemySpawn_0 [SpawnPointConfig]
  EnemySets:
    [0] Goblin       ← Set 01
    [1] Skeleton      ← Set 02
    [2] Archer        ← Set 03

EnemySpawn_1 [SpawnPointConfig]
  EnemySets:
    [0] Goblin        ← Set 01
    [1] OrcWarrior    ← Set 02
    [2] DarkMage      ← Set 03
```

Si se elige Set 02 → spawnea Skeleton + OrcWarrior.

### 4.4 Compatibilidad con sistema legacy

- `RoomSO.PossibleSetups` sigue funcionando como fallback
- Si NINGÚN spawn point tiene `SpawnPointConfig`, se usa el flow anterior
  (PossibleSetups → EnemyPool)
- Podés migrar gradualmente sala por sala

---

## Paso 5 — Verificación final

### En Unity Play Mode:

- [ ] Entrar al gameplay → todas las salas visibles al mismo tiempo
- [ ] Puertas de salas adyacentes pegadas sin gaps
- [ ] Zoom out (floor view) → shells con material correcto (no magenta)
- [ ] Entrar a sala de combate → enemigos spawnean según SpawnPointConfig
- [ ] Confirmar que el set es uniforme (todos los spawn points usan el mismo set index)
- [ ] Caminar entre salas → pathfinding funciona sobre el NavGraph bakeado
- [ ] Salir del piso → verificar que no quedan GameObjects huérfanos

### En Scene View (Editor):

- [ ] Seleccionar room prefab → gizmos verdes muestran nodos y edges
- [ ] Tiles a diferente altura sin edge → confirmar que no se conectan
- [ ] Botón "Bake NavGraph" regenera el grafo correctamente

---

## Archivos de referencia

| Archivo | Qué hace |
|---------|----------|
| `CameraConfigSO.cs` | `ShellMaterial` field en sección Floor View |
| `DungeonManager.cs` | `CellSpacing = 0`, instancia todas las rooms en `GenerateFloor` |
| `FloorShellVisibilityController.cs` | Usa material del config o fallback URP Unlit |
| `NavGraph.cs` | Grafo de nodos con edges explícitos |
| `NavGraphBaker.cs` | Lógica de bake (scan renderers → nodos + edges) |
| `NavGraphBakeSettings.cs` | `HeightThreshold`, `TileSize` |
| `RoomLayoutEditor.cs` | Botón "Bake NavGraph" en Inspector |
| `NavGraphGizmoDrawer.cs` | Gizmos de nodos y edges en Scene View |
| `SpawnPointConfig.cs` | Componente con `List<EnemyDataSO> EnemySets` |
| `DefaultEnemySpawnResolver.cs` | Lee SpawnPointConfig si existe, fallback a legacy |
