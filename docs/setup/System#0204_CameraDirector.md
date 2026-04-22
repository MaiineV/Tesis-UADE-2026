# System#0204 — Camera Director

> **Sprint 04 / FP closure.** Cámara isométrica scripteada con PrimeTween —
> follow, rotate 45° snapped, pan, zoom, recenter, wall occlusion y floor
> view. Cierra el último gap de feel del FP. Spec completo en
> `TECHNICAL.md §17.E`.

---

## 0. Compile check + tests

1. Unity abre sin errores. Esperar a que el PrimeTween installer bakee el
   paquete (`Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween.tgz`
   ya está en `Packages/manifest.json`).
2. Test Runner → EditMode → Run All.
3. Esperar ~20 tests nuevos (`Rollgeon.Camera.Tests` + 2 extras en
   `Rollgeon.Dungeon.Tests` para `GetFloorBounds` / `GetCurrentRoomOccluders`).

---

## 1. SOs a crear

### `CameraConfig.asset`

- Path: `Assets/Rollgeon/Camera/CameraConfig.asset`
- Menu: `Create → Rollgeon → Camera → Camera Config`
- Defaults razonables (todos overrideable en inspector):
  - `DistanceFromTarget` = 12, `PitchDegrees` = 45, `StartingFacing` = NE.
  - Rotation: enabled, 45° step, 50px drag por step, tween 0.25s OutQuad.
  - Pan: enabled, speed 18, clamp to floor bounds ON, lerp 0.08s.
  - Zoom: enabled, min 6, max 22, step 1.5, tween 0.18s OutQuad,
    ortográfica por default.
  - Wall occlusion: enabled, fade 0.2s, map simétrico (1 pared en
    cardinales, 2 en diagonales).
  - Floor view: enabled, threshold 18, tween 0.3s, shell color semi-opaco.

### `CameraServiceBootstrap.asset` (opcional — recomendado)

- Path: `Assets/Rollgeon/Bootstrap/CameraServiceBootstrap.asset`.
- Menu: `Create → Rollgeon → Camera → Camera Service Bootstrap`.
- Slots:
  - `_config` — dropear el `CameraConfig.asset` creado arriba.
  - `_inputActions` — dropear `Assets/InputSystem_Actions.inputactions`
    (ya trae el map `Camera` agregado). Si lo dejás null, el router queda
    inerte y la cámara sólo responde a llamadas directas al
    `ICameraService`.
  - `_mapName` — "Camera" (default, no tocar a menos que renombres el map).

Priority 45 — corre antes que los Run-scope services y deja el
`CameraConfigSO` + `CameraInputConfig` disponibles en el `ServiceLocator`
para cuando la `CameraService` de la gameplay scene despierte.

### Registrar en `ServiceBootstrap.asset`

Dos alternativas:

**Alt A — via CameraServiceBootstrap (recomendado):**
En **Extra Runtime Services**, agregar `CameraServiceBootstrap.asset`.
El wrapper registra `CameraConfigSO` + `CameraInputConfig` en el
`ServiceLocator` global. El `CameraService` MonoBehaviour los consume al
despertar en la scene de gameplay.

**Alt B — directo como settings asset:**
Si no querés input bindings, dropeá el `CameraConfig.asset` directo en
`ServiceBootstrap.SettingsAssets`. Saltea el wrapper.

---

## 2. Escena `02_Gameplay.unity`

- **Main Camera**:
  1. Asegurarte que tiene tag `MainCamera` (ya lo está por default).
  2. Add Component → `Rollgeon/Camera/Camera Service`. En `Awake` resuelve
     el `CameraConfigSO` desde el `ServiceLocator` (o desde el override
     serializado `_configOverride` si preferís hardcodear por-scene),
     se inicializa y se registra como `ICameraService` en `ServiceScope.Run`.
  3. (Opcional) Add Component → `Rollgeon/Camera/Camera Input Router`.
     Si el `CameraServiceBootstrap` tiene `_inputActions` asignado, el
     router los resuelve automáticamente — no hace falta setear nada en
     el inspector del router.

- **Importante**: la cámara DEBE ser ortográfica (Projection = Orthographic)
  o dejar que el service la fuerce al zoomear. El
  `CameraConfigSO.IsOrthographic` default = true se asegura de eso cuando
  `ApplyZoomImmediate` corre.

---

## 3. Input — action map `Camera`

Ya está agregado a `Assets/InputSystem_Actions.inputactions`:

| Action | Binding | Semántica |
|---|---|---|
| `RotateModifier` | Mouse / rightButton | Mientras sostenido activa drag de rotación. |
| `RotateDrag` | Mouse / delta (Vector2) | Acumula pixels ⇒ cada `DragPixelsPerStep` dispara RotateBy45. |
| `PanModifier` | Mouse / middleButton | Gate del pan. |
| `PanDrag` | Mouse / delta (Vector2) | PanBy(delta) mientras modifier sostenido. |
| `Zoom` | Mouse / scroll (Vector2) | ZoomBy(sign(y)) por notch. |
| `Recenter` | Keyboard / F | RecenterOnPlayer(instant:false). |

Si querés otros bindings (gamepad, touch), extendé el map desde el Input
Actions editor.

---

## 4. Wall Occlusion — autoría (post-FP)

Cuando las salas se migren a prefab-based (§13.3), por cada pared del
`RoomPrefab`:

1. Seleccionar el mesh de la pared.
2. Add Component → `Rollgeon/Camera/Wall Occluder`.
3. En el dropdown `Direction`, elegir la `WallDirection` que corresponda
   (N/NE/E/SE/S/SW/W/NW).
4. En FP esto no aplica — no hay prefabs de sala autorados. El code path
   está listo pero devuelve una lista vacía.

El `CameraService.RefreshWallOcclusion` corre en cada `FacingChanged` y
consulta `IDungeonService.GetCurrentRoomOccluders()`; la política de
qué paredes ocultar por yaw vive en el `OcclusionMap` editable del config.

---

## 5. Floor View — shells (post-FP)

El floor view emite `FloorViewToggled(true)` al cruzar el umbral de zoom.
Para que visualmente aparezcan shells hay que completar §13.6 (per-room
`WorldPosition` + generación de shells procedurales). En el FP, el evento
dispara correctamente pero no se ve nada — está documentado y queda listo
para cuando el dungeon pipeline migre a prefabs.

---

## 6. Smoke test end-to-end

Play desde `00_Bootstrap.unity`:

1. Console: `[CameraServiceBootstrap] Registered on Main Camera (config=CameraConfig).`
   0 warnings rojos.
2. Menú → Class Selection → Build Selection → Gameplay carga.
3. **Follow**: hero aparece, cámara lo sigue con offset `DistanceFromTarget`.
4. **Rotate**: sostener RMB + drag horizontal → cada ~50px la cámara snapea
   45°. Log `OnCameraFacingChanged`.
5. **Pan**: sostener MMB + arrastrar → cámara se separa del hero. `IsPanning=true`.
6. **Recenter**: `F` → vuelve smooth en 0.4s al hero. Log `OnCameraRecentered`.
7. **Zoom**: scroll arriba/abajo → zoom clampeado a `[6, 22]`.
8. **Floor view**: zoomear out hasta cruzar 18. Log `OnCameraFloorViewToggled(true)`.
   (Shells no aparecen en FP — ver §5 arriba.)
9. **Proceed a combat room**: cámara hace recenter instantáneo al hero
   (via `RoomGridLoader → ICameraService.RecenterOnPlayer(instant:true)`).
10. **Return to menu**: cámara se deregistra con `ClearScope(Run)`.

---

## Troubleshooting

| Síntoma | Causa probable | Fix |
|---|---|---|
| `[CameraServiceBootstrap] Camera.main no encontrada` | El `Main Camera` de `02_Gameplay` no tiene tag `MainCamera`. | Seleccionarlo → Inspector → Tag → `MainCamera`. |
| `CameraConfigSO no asignado` | Slot `_config` vacío en `CameraServiceBootstrap.asset`. | Dropear el `CameraConfig.asset`. |
| Al rotar el hero no se ve — parece detrás de algo | Hero está dentro del frustum pero la rotación inicial deja paredes opacas. | Apagar temporalmente `EnableWallOcclusion` o meter `WallOccluder`s en las paredes (post-FP). |
| Scroll no zoomea | Map `Camera` no está enabled, o el `_inputActions` no está asignado. | Verificar que `CameraServiceBootstrap._inputActions` apunta al asset correcto y que el map `Camera` no está disabled por otro script. |
| F no recentra | El router detecta modifier pero no el tap de F — revisar que el binding de `Recenter` esté `<Keyboard>/f`. | Reimportar el asset. |

---

## Post-FP (diferido)

- **Shake**: `ICameraService.Shake(amplitude, duration)` ya existe y hace
  shake con `Tween.ShakeLocalPosition`. Falta wirearlo desde el
  `FeedbackManager` cuando se prenda la pipeline de feedback (§10).
- **Floor view shells procedurales**: depende de §13.3 (RoomLayout.Bounds
  bakeado por room prefab) + §13.6 (`RoomInstance.WorldPosition` y
  `DungeonManager.GenerateFloor` instanciando shells). Hoy el evento
  toggle dispara pero no hay nada que renderear.
- **CameraSettingsSO**: preferencias de usuario (sensitivity, invert axes)
  como SO separado del `CameraConfigSO`. Post-FP junto a §15 SaveSystem.

---

*Generado 2026-04-22. Sprint 04 Fase de Producción — camera director.*
