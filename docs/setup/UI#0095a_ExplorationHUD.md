# Setup — UI#0095a Exploration HUD

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 30-45 min (crear canvas + widgets + cablear prefabs).
> **Prerrequisito merge:** Foundation#0001-0005, T100a (IEnergyService), T102
> (ScreenManager/BaseScreen/ScreenHost) ya en `develop`.

Este instructivo describe como dejar el **Exploration HUD** operativo en el editor.
Ningun paso esta automatizado: el Canvas, los Sliders, los TextMeshPro y los
GameObjects de los slots de item los crea el usuario manualmente en Unity
(TECHNICAL.md §17.D.1 prohibe construir UI programaticamente). El worktree solo
entrega el codigo C# de las views + el stub de `IPlayerService`.

Cualquier duda sobre el *por que*: ver `plan.md` (worktree raiz) y `TECHNICAL.md`
§17.D + §G.

---

## 0. Decisiones de infra

- **No hay `Rollgeon.UI.asmdef` runtime.** Consistente con UI#0102 — todo
  `Rollgeon.UI.*` y `Rollgeon.Player.*` vive en `Assembly-CSharp` default.
- **Tests EditMode** en `Rollgeon.UI.Tests.asmdef` (ya existente — se agrega una
  suite: `ExplorationHUDViewTests`).
- **Sub-views vs HUD monolitico:** este HUD se compone de **5 sub-views**
  independientes (`HealthBarView`, `EnergyBarView`, `GoldCounterView`,
  `ActiveItemsView`, `MinimapView`). Plan §2.1 justifica.

---

## 1. Componentes entregados por el PR

```
Assets/Scripts/Rollgeon/
├── Player/
│   └── IPlayerService.cs                          [STUB — §G minimo]
└── UI/
    ├── HUD/
    │   ├── ActiveItemState.cs                     (enum Inactive/Active/Depleted)
    │   ├── ActiveItemSlotView.cs
    │   ├── ActiveItemsView.cs
    │   ├── EnergyBarView.cs
    │   ├── GoldCounterView.cs
    │   ├── HealthBarView.cs
    │   └── MinimapView.cs
    ├── Screens/
    │   └── ExplorationHUDView.cs                  (BaseScreen subclass, ScreenStringId = "ExplorationHUD")
    └── Tests/
        └── ExplorationHUDViewTests.cs
```

**Ningun prefab, ninguna escena, ningun asset `.unity`/`.prefab` es parte del PR.**
El layout, colores, fuentes y sprites los autorea el diseñador (ver §8).

---

## 2. Pre-requisitos

1. **Foundation#0001-0005 + T100a + T102 mergeadas** a `develop`. Verificar que existen:
   - `Assets/Scripts/Rollgeon/Patterns/EventManager.cs`.
   - `Assets/Scripts/Rollgeon/Patterns/ServiceLocator.cs`.
   - `Assets/Scripts/Rollgeon/UI/BaseScreen.cs`.
   - `Assets/Scripts/Rollgeon/UI/ScreenHost.cs`.
   - `Assets/Scripts/Rollgeon/UI/IScreenManager.cs`.
   - `Assets/Scripts/Rollgeon/Combat/Energy/IEnergyService.cs`.
2. **Odin Inspector instalado** (Sirenix). Los scripts usan `[Title]`,
   `[Required]`, `[Tooltip]`, `[InfoBox]`, `[ShowInInspector]`.
3. **TextMeshPro instalado** (los labels del HUD usan `TextMeshProUGUI`).
4. Abrir el proyecto en Unity, esperar recompilacion, confirmar **0 errores** en
   la consola.

---

## 3. Crear / abrir la escena de Gameplay

Este HUD se pushea desde la escena **gameplay** (la que carga el `BootstrapRunner`
o el `ClassSelectionScreen` cuando T98 mergee). Para el MVP del worktree se
puede armar en cualquier escena que tenga un `ScreenHost`:

1. Abrir la escena donde va a vivir el HUD (ej: `02_Gameplay.unity` — crearla si
   no existe siguiendo la misma plantilla de `01_MainMenu`).
2. Confirmar que hay un `EventSystem` + un `ScreenHost` en la jerarquia.
3. El HUD se cuelga como hijo de un `Canvas` (el mismo u otro diferente al del
   menu — es decision del diseñador).

---

## 4. Estructura de jerarquia del HUD

Armar debajo del Canvas gameplay:

```
Canvas (Screen Space - Overlay, Scaler 1920x1080)
└── ExplorationHUDView                              (Create Empty + componente ExplorationHUDView.cs)
    ├── HealthBarRoot
    │   └── HealthBarView                           (Create Empty + HealthBarView.cs)
    │       ├── Slider (UI → Slider)                → cablear en `_slider`
    │       └── HPText (UI → Text - TMP)            → cablear en `_text`
    ├── EnergyBarRoot
    │   └── EnergyBarView                           (Create Empty + EnergyBarView.cs)
    │       ├── Slider                              → `_slider`
    │       └── EnergyText (UI → Text - TMP)        → `_text`
    ├── GoldCounterRoot
    │   └── GoldCounterView                         (Create Empty + GoldCounterView.cs)
    │       ├── GoldIcon (Image, opcional)
    │       └── GoldText (UI → Text - TMP)          → `_text`
    ├── ActiveItemsRoot
    │   └── ActiveItemsView                         (Create Empty + ActiveItemsView.cs)
    │       ├── ArcoSlot                            (Create Empty + ActiveItemSlotView.cs)
    │       │   ├── Icon (UI → Image)               → `_icon`
    │       │   ├── InactiveOverlay (Image gris)    → `_inactiveOverlay`  (disabled)
    │       │   └── DepletedOverlay (Image X)       → `_depletedOverlay`  (disabled)
    │       └── PocionSlot                          (Create Empty + ActiveItemSlotView.cs)
    │           ├── Icon, InactiveOverlay, DepletedOverlay (igual que Arco)
    └── MinimapRoot
        └── MinimapView                             (Create Empty + MinimapView.cs)
            ├── MapPivot (Create Empty)             → `_mapPivot`
            │   └── Placeholder (UI → Raw Image)    → `_placeholder`
```

Layout sugerido (Figma #94):
- HP + Energy: esquina superior izquierda, stackeados verticalmente.
- Gold counter: justo debajo, mismo anchor.
- Active items: fila horizontal debajo de Gold, o en la esquina inferior izq.
- Minimap: esquina superior derecha.

---

## 5. Cableo detallado

### 5.1 `ExplorationHUDView` (root)

1. Seleccionar el GameObject `ExplorationHUDView` (hijo del Canvas).
2. En Inspector:
   - **Health Bar** ← arrastrar `HealthBarView` (hijo).
   - **Energy Bar** ← `EnergyBarView`.
   - **Gold Counter** ← `GoldCounterView`.
   - **Active Items** ← `ActiveItemsView`.
   - **Minimap** ← `MinimapView`.
3. **Screen String Id Override** puede quedar vacio: `ExplorationHUDView` expone
   `"ExplorationHUD"` por default.

### 5.2 `HealthBarView`

- **Slider** ← el Slider hijo. Configurarlo con Handle deshabilitado (queremos
  solo una Fill). Min = 0, Max = 1 (la view calcula `current / max`).
- **Text** ← el `TextMeshProUGUI` hijo.
- **Text Format** ← default `"{0}/{1}"`. Alternativas: `"{0} HP"`, `"HP {0}/{1}"`.

### 5.3 `EnergyBarView`

Identico a HealthBar. Fill sugerido azul/amarillo.

### 5.4 `GoldCounterView`

- **Text** ← el `TextMeshProUGUI` hijo.
- **Text Format** ← default `"{0}G"`. Ej: `"{0}"`, `"{0} oro"`.
- **Sin Slider.**

### 5.5 `ActiveItemsView`

1. Expandir el array **Bindings** (size = 2 para el FP).
2. Entrada 0:
   - **Item Id** = `item.arco`
   - **Slot** ← arrastrar `ArcoSlot`.
3. Entrada 1:
   - **Item Id** = `item.pocion`
   - **Slot** ← arrastrar `PocionSlot`.

Los strings deben matchear **exactamente** el catalogo. Cuando F#0010
(`ItemRegistry`) mergee, refactorizar a refs `ItemSO`.

### 5.6 `ActiveItemSlotView` (por cada slot — Arco y Pocion)

- **Icon** ← la `Image` hija principal.
- **Inactive Overlay** ← el GameObject del overlay (puede ser un Image gris que
  tape el icono, o una X roja).
- **Depleted Overlay** ← el GameObject del overlay de agotado.
- **Icon Active** (opcional): Sprite a usar cuando el slot esta Active.
- **Icon Inactive** (opcional): Sprite a usar cuando esta Inactive. Si ambos son
  null, el sprite del Image queda fijo y solo se togglean los overlays.

Dejar los dos overlays **deshabilitados por default** en el prefab: la view los
activa segun corresponda via `SetState`.

### 5.7 `MinimapView`

- **Map Pivot** ← arrastrar el GameObject `MapPivot`. Si se deja null, la view
  usa su propio Transform (funcional pero menos flexible).
- **Placeholder** ← la `RawImage` placeholder. Opcional.
- **Rotation Correction Euler** ← default `(0, 0, -45)`. Tunear hasta que el
  placeholder quede alineado visualmente con el mundo isometrico.

---

## 6. Registro en el `ScreenHost`

El `ScreenHost` de la escena descubre `BaseScreen` children automaticamente en
`Awake`, asi que **no hay que registrar manualmente** el HUD: basta con que
viva debajo del `ScreenHost` (o del Canvas que cuelga del host).

Para que el HUD se pushee automaticamente al cargar la escena:

- Opcion A — **auto-push**: setear `Initial Screen String Id = "ExplorationHUD"`
  en el `ScreenHost`. La HUD aparece al arrancar.
- Opcion B — **push manual** desde otro sistema (ej: al terminar
  `ClassSelectionScreen`): llamar
  ```csharp
  ServiceLocator.GetService<IScreenManager>().PushByStringId("ExplorationHUD");
  ```
  Para el MVP, lo mas simple es Opcion A.

---

## 7. Registrar `IPlayerService` (stub)

El HUD consume `IPlayerService` del `ServiceLocator` para resolver el
`PlayerGuid`. **Este worktree solo provee la interface minima** (solo
`Guid PlayerGuid { get; }`); la implementacion real la entrega F#0008 cuando
mergee. Opciones:

- **Opcion 1 — HUD sin player real (solo UI visual):** no registrar nada. El
  HUD loggea un warning y queda en default con `_playerGuid = Guid.Empty`.
  Al disparar eventos manualmente con `guid = Guid.Empty`, el HUD los atiende
  porque matchean el filtro.
- **Opcion 2 — registrar una implementacion minima:** hasta que F#0008 mergee,
  el usuario puede crear una clase en su bootstrap que implemente la interface
  y devuelva un `Guid` fijo. Ejemplo:
  ```csharp
  public class FakePlayerService : Rollgeon.Player.IPlayerService
  {
      public System.Guid PlayerGuid { get; } = System.Guid.NewGuid();
  }
  // En el bootstrap:
  ServiceLocator.AddService<IPlayerService>(new FakePlayerService());
  ```
  Eso desbloquea el filtrado por guid. Este worktree **no** entrega ese stub
  — es decision del bootstrap del proyecto.

Cuando F#0008 mergee con la `PlayerService` real y los hooks `OnPlayerSet`/
`OnPlayerCleared` existan, se puede enriquecer `ExplorationHUDView` para
suscribirse al ciclo de vida y hacer re-bind automatico al spawn del player.
Por ahora, si el HUD se pushea antes del spawn, **re-pushear** la screen tras
el spawn hace el trabajo (o invocar `BindAll(guid)` manualmente).

---

## 8. Estetica

El codigo **no** impone paleta, fuentes ni layout. El usuario autorea:

- **Fill colors** de HP / Energy en los Sliders (rojo / azul).
- **Sprites** de iconos de Arco / Pocion (placeholder mientras no haya arte).
- **Fuentes** TMP (default o asset custom).
- **Anchors** y posicionamiento (seguir Figma #94).

Ningun color / fuente esta forzado desde C#.

---

## 9. Smoke test manual (sin tests automatizados)

1. Con la escena gameplay abierta y el HUD pusheado, abrir **Window → Test
   Runner** (o una Console custom) y disparar manualmente:

   ```csharp
   var g = System.Guid.Empty; // o el guid registrado por el bootstrap
   Patterns.EventManager.Trigger(Patterns.EventName.OnPlayerHealthChanged, g, 50, 100);
   Patterns.EventManager.Trigger(Patterns.EventName.OnPlayerEnergyChanged, g, 3, 4);
   Patterns.EventManager.Trigger(Patterns.EventName.OnGoldChanged, 25, 25);
   Patterns.EventManager.Trigger(Patterns.EventName.OnItemObtained, g, "item.arco");
   Patterns.EventManager.Trigger(Patterns.EventName.OnActiveItemUsed, g, "item.pocion");
   ```

2. Esperado:
   - HP slider a 50%, label `"50/100"`.
   - Energy slider a 75%, label `"3/4"`.
   - Gold label `"25G"`.
   - Arco slot pasa a Active.
   - Pocion slot pasa a Depleted.
3. `OnItemRemoved` con `"item.arco"` vuelve el Arco a Inactive.

Hacer esto desde un EditorWindow dev-tool es mas comodo — fuera de scope este
worktree (ver plan §7).

---

## 10. Tests EditMode automatizados

El PR incluye `ExplorationHUDViewTests.cs` en
`Rollgeon.UI.Tests.asmdef`:

1. `Window → General → Test Runner` → solapa **EditMode**.
2. Buscar `Rollgeon.UI.Tests.ExplorationHUDViewTests`.
3. Run All — los 5 tests deben pasar en verde:
   - `BindAll_SubscribesHealthBar_EventUpdatesSlider`
   - `BindAll_SubscribesEnergyBar_EventUpdatesSlider`
   - `HealthBar_FiltersByGuid_IgnoresOtherEntities`
   - `UnbindAll_StopsReceivingEvents`
   - `BindAll_IsIdempotent_NoDoubleSubscription`

Si alguno falla, verificar:
- Odin Inspector instalado.
- `EventManager.ResetEventDictionary` no esta siendo llamado desde otra suite
  en paralelo (los tests de UI#0095a lo llaman en TearDown — no deberia haber
  cross-contamination).

---

## 11. Troubleshooting

| Sintoma | Causa probable | Fix |
|---|---|---|
| "HUD no aparece al cargar la escena" | `ScreenHost` no lo descubrio / `Initial Screen String Id` no matchea `"ExplorationHUD"`. | Verificar que el GameObject del HUD es descendiente del `ScreenHost` y que el string id del host es `"ExplorationHUD"`. |
| "Warning: `_healthBar` no esta cableado" | Olvido arrastrar la sub-view al slot en el Inspector. | §5.1. |
| "Slider no se mueve al disparar OnPlayerHealthChanged" | Guid del evento != `PlayerGuid` resuelto. | Verificar que `IPlayerService.PlayerGuid` matchea el guid del evento. Si no hay `IPlayerService` registrado, `_playerGuid = Guid.Empty` — disparar eventos con `Guid.Empty` para testear. |
| "Icono de Arco no togglea" | El `ItemId` cableado no matchea el string del evento. | §5.5 — verificar exact match case-sensitive. |
| "Minimap rotado incorrectamente" | `_rotationCorrectionEuler` mal tuneado para el mundo de la proyecto. | Ajustar en Inspector (ver §5.7). |
| "Warning: `IPlayerService no registrado`" | Comportamiento esperado hasta que F#0008 mergee. | Ver §7. El HUD queda funcional en modo "guid = Empty" para testing visual. |

---

## 12. Definicion de Done (validacion del PR)

- [ ] El GameObject `ExplorationHUDView` existe en la escena gameplay.
- [ ] Los 5 slots del Inspector (`_healthBar`, `_energyBar`, `_goldCounter`,
      `_activeItems`, `_minimap`) estan cableados sin warnings al Play.
- [ ] Disparar `OnPlayerHealthChanged(guid, 50, 100)` mueve el slider al 50%.
- [ ] Disparar `OnPlayerEnergyChanged(guid, 3, 4)` mueve el slider al 75%.
- [ ] Disparar `OnGoldChanged(25, delta)` updatea el label a `"25G"`.
- [ ] `OnItemObtained(guid, "item.arco")` → Arco a Active.
- [ ] `OnActiveItemUsed(guid, "item.pocion")` → Pocion a Depleted.
- [ ] `OnItemRemoved(guid, "item.arco")` → Arco a Inactive.
- [ ] El minimap placeholder se ve con la rotacion corregida (no en 45° puro).
- [ ] Al pop del HUD (cambio de escena / `PopCurrent`), disparar otro
      `OnPlayerHealthChanged` ya no afecta la UI (las sub-views hicieron Unbind).
- [ ] Los 5 tests de `ExplorationHUDViewTests` pasan 100% en Test Runner.
- [ ] Cero archivos `.unity`/`.prefab`/`.asset` creados por el dev en este PR.
