# Setup — UI#0013b Floor Transition Screen

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 10-15 min (crear GameObjects + cablear Inspector).
> **Prerrequisito merge:** UI#0102 (ScreenManager/BaseScreen/ScreenHost),
> Foundation#0010 (RunContext/RunBootstrapper) ya en `develop`.

Este instructivo describe como dejar el **Floor Transition Screen** operativo en el
editor. Ningun paso esta automatizado: el Canvas, los labels y el boton los crea
el usuario manualmente en Unity.

---

## 0. Componentes entregados por el PR

```
Assets/Scripts/Rollgeon/UI/
├── Screens/
│   ├── FloorTransitionPayload.cs      (payload con FloorNumber y FloorTitle)
│   └── FloorTransitionScreen.cs       (screen principal)
└── Tests/
    └── FloorTransitionScreenTests.cs   (6 tests EditMode)
```

---

## 1. Prerequisitos en escena

1. **ScreenManager** registrado en `ServiceLocator` (viene de UI#0102).
2. **IRunContextService** registrado en `ServiceLocator` (viene de Foundation#0010)
   — usado como fallback si el payload es null.

---

## 2. Crear el GameObject

En la escena donde vive el Canvas de UI (ej. escena de run):

1. Crear un **GameObject hijo del Canvas** llamado `FloorTransitionScreen`.
2. Agregar el componente **`FloorTransitionScreen`** (`Rollgeon/UI/Screens/Floor Transition Screen`
   en el menu AddComponent).
3. El GameObject debe arrancar **desactivado** (`SetActive(false)`) — el ScreenManager
   lo activa al pushear.

---

## 3. Jerarquia de hijos sugerida

```
FloorTransitionScreen (FloorTransitionScreen.cs)
├── FloorNumberLabel     (TextMeshProUGUI — "Piso 1")
├── FloorTitleLabel      (TextMeshProUGUI — "Catacumbas Profundas")
└── ContinueButton       (Button + TextMeshProUGUI hijo "Continuar")
```

---

## 4. Cablear en Inspector

En el componente `FloorTransitionScreen`:

| Field                 | Arrastrar                             |
|-----------------------|---------------------------------------|
| `_floorNumberLabel`   | FloorNumberLabel (TMP)                |
| `_floorTitleLabel`    | FloorTitleLabel (TMP)                 |
| `_continueButton`     | ContinueButton (Button)               |
| `_nextScreenStringId` | Dejar default "ExplorationHUD" o cambiar segun flujo |

---

## 5. Registrar en ScreenHost

El `ScreenHost` de la escena debe tener registrado el `FloorTransitionScreen`.
Agregar la referencia en la lista de screens del `ScreenHost` Inspector.

---

## 6. Navegar a esta screen

Desde el caller (ej. DungeonManager o ExplorationHUD al avanzar de piso):

```csharp
if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
{
    screens.PushByStringId("FloorTransitionScreen", new FloorTransitionPayload
    {
        FloorNumber = floorIndex + 1,
        FloorTitle  = floorLayout?.DisplayName
    });
}
```

---

## 7. Smoke test

1. Play la escena.
2. Provocar navegacion al FloorTransitionScreen (via caller o test manual).
3. Debe mostrar:
   - "Piso N" en el label de numero.
   - Titulo del piso si se paso en el payload, o label oculto si esta vacio/null.
4. Click en Continuar debe navegar a ExplorationHUD (o el screen configurado
   en `_nextScreenStringId`).

---

## 8. Tests

Correr los tests EditMode:

```
Unity > Window > General > Test Runner > EditMode > Rollgeon.UI.Tests
> FloorTransitionScreenTests (6 tests)
```

Todos deben pasar en verde.
