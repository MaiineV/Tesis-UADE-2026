# Setup — UI#0013a Build Selection Screen

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 20-30 min (crear GameObjects + cablear Inspector).
> **Prerrequisito merge:** UI#0098 (ClassSelectionScreen), UI#0102 (ScreenManager/
> BaseScreen/ScreenHost), Foundation#0010 (RunContext/RunBootstrapper) ya en `develop`.

Este instructivo describe como dejar el **Build Selection Screen** operativo en el
editor. Ningun paso esta automatizado: el Canvas, los labels, las imagenes y los
botones los crea el usuario manualmente en Unity.

---

## 0. Componentes entregados por el PR

```
Assets/Scripts/Rollgeon/UI/
├── Screens/
│   ├── BuildSelectionPayload.cs      (payload con hero, runId, rulesetId)
│   └── BuildSelectionScreen.cs       (screen principal)
├── HUD/
│   └── DiceSlotView.cs               (sub-view para un slot de dado)
└── Tests/
    └── BuildSelectionScreenTests.cs   (5 tests EditMode)
```

**Modificado:**
- `ClassSelectionScreen.cs` — ahora pasa `BuildSelectionPayload` en vez de
  disparar `OnRunStart` directamente.

---

## 1. Prerequisitos en escena

1. **ScreenManager** registrado en `ServiceLocator` (viene de UI#0102).
2. **ClassSelectionScreen** operativo y con `_nextScreenStringId = "BuildSelectionScreen"`.
3. **RunBootstrapper** dependencies: `IPlayerService` registrado en ServiceLocator
   (viene de Foundation#0006).

---

## 2. Crear el GameObject

En la escena `01_MainMenu.unity` (o la escena donde vive el Canvas de UI):

1. Crear un **GameObject hijo del Canvas** llamado `BuildSelectionScreen`.
2. Agregar el componente **`BuildSelectionScreen`** (`Rollgeon/UI/Screens/Build Selection Screen`
   en el menu AddComponent).
3. El GameObject debe arrancar **desactivado** (`SetActive(false)`) — el ScreenManager
   lo activa al pushear.

---

## 3. Jerarquia de hijos sugerida

```
BuildSelectionScreen (BuildSelectionScreen.cs)
├── HeroNameLabel         (TextMeshProUGUI)
├── HeroDescriptionLabel  (TextMeshProUGUI)
├── HeroPortrait          (Image)
├── DiceContainer         (Transform vacio — los slots se instancian aca)
├── DiceBagFallbackLabel  (TextMeshProUGUI — "No dice bag configured")
├── ConfirmButton         (Button + TextMeshProUGUI hijo "Confirm")
└── BackButton            (Button + TextMeshProUGUI hijo "Back")
```

---

## 4. Cablear en Inspector

En el componente `BuildSelectionScreen`:

| Field                    | Arrastrar                              |
|--------------------------|----------------------------------------|
| `_heroNameLabel`         | HeroNameLabel (TMP)                    |
| `_heroDescriptionLabel`  | HeroDescriptionLabel (TMP)             |
| `_heroPortrait`          | HeroPortrait (Image)                   |
| `_diceContainer`         | DiceContainer (Transform)              |
| `_diceSlotPrefab`        | Prefab de DiceSlotView (ver paso 5)    |
| `_diceBagFallbackLabel`  | DiceBagFallbackLabel (TMP)             |
| `_confirmButton`         | ConfirmButton (Button)                 |
| `_backButton`            | BackButton (Button)                    |

---

## 5. Crear el DiceSlotView prefab

1. Crear un GameObject con un `TextMeshProUGUI` hijo llamado `DiceLabel`.
2. Agregar el componente `DiceSlotView` al root.
3. Cablear `_diceLabel` al `DiceLabel` TMP.
4. Guardar como prefab en `Assets/Prefabs/UI/` (o donde corresponda).
5. Arrastrarlo al campo `_diceSlotPrefab` del `BuildSelectionScreen`.

---

## 6. Registrar en ScreenHost

El `ScreenHost` de la escena debe tener registrado el `BuildSelectionScreen`.
Agregar la referencia en la lista de screens del `ScreenHost` Inspector.

---

## 7. Smoke test

1. Play la escena.
2. En ClassSelectionScreen, seleccionar Guerrero y clickear Confirm.
3. Debe navegar a BuildSelectionScreen mostrando:
   - Nombre del heroe ("Guerrero").
   - Descripcion del heroe.
   - Fallback label visible (porque StartingDiceBagRef es null en MVP).
4. Click Back debe volver a ClassSelectionScreen.
5. Click Confirm en BuildSelectionScreen debe:
   - Llamar `RunBootstrapper.StartRun` (requiere IPlayerService registrado).
   - Navegar a ExplorationHUD (si esta registrado).

---

## 8. Tests

Correr los tests EditMode:

```
Unity > Window > General > Test Runner > EditMode > Rollgeon.UI.Tests
> BuildSelectionScreenTests (5 tests)
```

Todos deben pasar en verde.
