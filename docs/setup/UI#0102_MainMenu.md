# Setup — UI#0102 Main Menu + ScreenManager minimo

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 15-20 min (crear escena + armar canvas + wirear botones).
> **Prerrequisito merge:** Foundation#0001 (`ServiceLocator`, `EventManager`) y Foundation#0005 (`BootstrapRunner`, escena `00_Bootstrap`) ya en `develop`.

Este instructivo describe como dejar el Main Menu **operativo dentro del editor**.
Ningun paso esta automatizado: la escena (`01_MainMenu.unity`), el Canvas y los
botones los crea el usuario manualmente por diseno — este worktree solo entrega
codigo C# (`Rollgeon.UI` namespace + `MainMenuScreen`).

Cualquier duda sobre el *por que* de un paso: ver `plan.md` (worktree raiz) y
`TECHNICAL.md` §17.D.

---

## 8.0 Decisiones de infra confirmadas en este merge

- **No hay `Rollgeon.UI.asmdef` runtime.** El proyecto no usa asmdef para runtime
  (solo para tests). `Rollgeon.UI.*` vive en el `Assembly-CSharp` default,
  consistente con `Rollgeon.Patterns.*` y `Rollgeon.Attributes.*`. Si en el
  futuro se decide modularizar, esto se refactorea en una tarea dedicada.
- **Si** hay `Rollgeon.UI.Tests.asmdef` (EditMode). Consistente con
  `Rollgeon.Attributes.Tests.asmdef`.

---

## 8.1 Pre-requisitos

1. **Foundation#0001 y #0005 mergeadas** a `develop`. Verificar que existen:
   - `Assets/Scripts/Rollgeon/Patterns/ServiceLocator.cs`.
   - `Assets/Scripts/Rollgeon/Patterns/Bootstrap/BootstrapRunner.cs`.
   - Escena `Assets/Rollgeon/Scenes/00_Bootstrap.unity` armada siguiendo
     `docs/setup/Foundation#0005_CatalogsAndBootstrap.md`.
2. **Odin Inspector instalado** (Sirenix). Los scripts de UI usan `[Title]`,
   `[Required]` y `[Tooltip]`.
3. **TextMeshPro instalado.** Al crear el primer `UI → Button - TextMeshPro`,
   Unity ofrece importar los TMP Essentials — aceptar.
4. Abrir el proyecto en Unity, esperar a que recompile, confirmar **0 errores**
   en la consola.

---

## 8.2 Crear la escena `01_MainMenu`

1. `File → New Scene` → plantilla **Basic (Built-in RP)** o **Basic (URP)** segun
   el render pipeline del proyecto.
2. `File → Save As…` → guardar en `Assets/Rollgeon/Scenes/01_MainMenu.unity`
   (crear la carpeta `Scenes` si no existe).
3. Dejar la `Main Camera` y `Directional Light` que vienen por default — no
   molestan (Canvas Overlay ignora la camara, pero es util tener una para el
   background).

---

## 8.3 Estructura de jerarquia

En la escena recien creada, armar:

```
01_MainMenu (scene root)
├── Main Camera                                 (dejar el que viene por default)
├── Directional Light                           (dejar el que viene por default)
├── EventSystem                                 (GameObject → UI → Event System)
├── ScreenHost                                  (GameObject → Create Empty)
│   └── [Component] ScreenHost.cs
│       └── Initial Screen String Id = "MainMenu"
└── Canvas                                      (GameObject → UI → Canvas)
    │   Render Mode: Screen Space - Overlay
    │   [Component] Canvas Scaler — UI Scale Mode: Scale With Screen Size
    │                                Reference Resolution: 1920 × 1080
    └── MainMenuScreen                          (Create Empty como hijo del Canvas)
        ├── [Component] MainMenuScreen.cs
        ├── Background                          (UI → Image, stretch full-screen, color oscuro)
        ├── TitleLabel                          (UI → Text - TextMeshPro — "Rollgeon" u otro)
        ├── PlayButton                          (UI → Button - TextMeshPro, label "Jugar")
        └── QuitButton                          (UI → Button - TextMeshPro, label "Salir")
```

### Notas sobre la jerarquia

- El `ScreenHost` **no tiene que ser hijo del Canvas**; puede ser root sibling.
  Lo unico que necesita es que las `BaseScreen` sean descendientes del Canvas
  (o sea — hijas del propio Canvas). El host hace
  `GetComponentsInChildren<BaseScreen>` desde su propio GameObject, asi que si
  colgas el host **dentro** del Canvas, el discovery tambien sirve.
- Variante igualmente valida: colgar `ScreenHost` como hijo root del Canvas y
  poner `MainMenuScreen` debajo del host. Resultado identico.
- Solo debe haber **un** `ScreenHost` por escena.

---

## 8.4 Wiring del `MainMenuScreen`

1. Seleccionar el GameObject `MainMenuScreen` en la jerarquia.
2. En el Inspector, en el componente `MainMenuScreen`:
   - Campo **Play Button**: arrastrar el GameObject `PlayButton` (el del Canvas).
   - Campo **Quit Button**: arrastrar el GameObject `QuitButton`.
3. **No** cablear `OnClick()` en el Inspector de los Button — el componente se
   suscribe programaticamente en `OnEnable()` y desuscribe en `OnDisable()`.
4. Campo **Screen String Id Override** (heredado de `BaseScreen`) puede dejarse
   vacio: `MainMenuScreen` overridea el getter para devolver `"MainMenu"`
   directamente. Si lo dejas vacio, queda el default.

---

## 8.5 Estetica

El codigo **no** impone paleta ni fuentes. El usuario autorea:

- **Fondo** (`Background`): sugerencia `#0E0E14` (gris muy oscuro). Stretch a
  ancho y alto completos del Canvas via anchors.
- **Acentos** en botones: dorado / rojo casino.
- **Titulo**: fuente serif/display si hay un asset TMP disponible, tamano ~120.
- **Layout**: titulo centrado arriba, botones en pila vertical centrada abajo.
- **Logo / decoracion**: el usuario agrega `Image`/`RawImage` con su sprite si
  quiere.

Ningun color/fuente esta forzado desde C#. Si se rompe la estetica, se arregla
en engine, no en codigo.

---

## 8.6 `ScreenHost` — configuracion

El componente `ScreenHost` tiene dos campos:

| Campo | Default | Uso |
|---|---|---|
| `Initial Screen String Id` | `"MainMenu"` | Id de la screen que se pushea automatico en `Awake`. Debe matchear el `ScreenStringId` de alguna `BaseScreen` hija. `MainMenuScreen` expone `"MainMenu"` — dejarlo asi. |
| `Include Inactive` | `true` | Si busca tambien entre GameObjects inactivos al descubrir screens. Dejar en `true` (el host desactiva todas al arrancar y re-activa solo la pusheada). |

Si dejas `Initial Screen String Id` vacio, el host solo registra las screens en
el `IScreenManager` y no pushea nada — util si algun otro sistema se encarga
del push inicial. Para `01_MainMenu`, **dejarlo en `"MainMenu"`**.

---

## 8.7 Build Settings — agregar la escena

1. `File → Build Settings…`.
2. Verificar / armar el orden:
   - Indice **0**: `Assets/Rollgeon/Scenes/00_Bootstrap.unity` (creado en
     Foundation#0005).
   - Indice **1**: `Assets/Rollgeon/Scenes/01_MainMenu.unity` ← **agregar
     ahora** con drag & drop desde Project.
3. Guardar (Unity persiste Build Settings automaticamente).

Por que importa: `BootstrapRunner` intenta cargar `"01_MainMenu"` por nombre.
Si no esta en Build Settings, loggea el warning `Scene '01_MainMenu' no esta en
Build Settings` y no carga nada.

---

## 8.8 Verificacion funcional — smoke test

1. Abrir `Assets/Rollgeon/Scenes/00_Bootstrap.unity`.
2. Presionar **Play** en el editor.
3. Esperado en consola:
   - Logs `[Bootstrap] ...` de `BootstrapRunner.Awake`.
   - `[Bootstrap] Loading scene 01_MainMenu`.
   - La escena `01_MainMenu` carga y aparece el menu con los botones **Jugar**
     y **Salir**.
4. **Click en "Salir"** → el editor sale de playmode (log
   `[MainMenuScreen] Quit requested.`).
5. Volver a Play desde `00_Bootstrap`.
6. **Click en "Jugar"** → en consola aparece:
   - `[MainMenuScreen] Play clicked.`
   - `[ScreenManager] 'ClassSelectionScreen' no esta registrada. Fallback
     graceful: el stack no cambia. Verificar que la screen exista en la escena
     (T98 puede no haber mergeado todavia).` ← **comportamiento esperado hoy**
     (T98 no mergeo).
7. El usuario queda en el menu — no hay crash, no hay pantalla negra.
8. **Cuando T98 mergee**: al agregar el `ClassSelectionScreen` como hija del
   Canvas (su `GetType().Name` matchea el string `"ClassSelectionScreen"`), el
   click en Jugar transiciona sin warning.

---

## 8.9 Build Player — verificacion adicional (opcional)

Para validar el path de `Application.Quit()` (no corre en editor):

1. `File → Build And Run` → plataforma Windows (Mono o IL2CPP, da igual).
2. En el build, click **Salir** → la app se cierra sin error.

Este paso es opcional; el smoke test del §8.8 ya cubre el flujo principal en
editor.

---

## 8.10 Tests EditMode

Opcional — el PR incluye `Rollgeon.UI.Tests.asmdef` con
`ScreenManagerTests.cs` (smoke test del `ScreenManager`).

1. `Window → General → Test Runner`.
2. Solapa **EditMode**.
3. Buscar la suite `Rollgeon.UI.Tests.ScreenManagerTests`.
4. Click **Run All** — los 8 tests deben pasar en verde.

Si un test falla, verificar que:
- Odin Inspector este instalado.
- No haya otro `ScreenManager` registrado en `ServiceLocator` de una sesion
  previa (los tests no tocan el locator, pero si algun otro test si, puede
  haber leakage — igualmente no deberia afectar a esta suite).

---

## 8.11 Troubleshooting rapido

| Sintoma | Causa probable | Fix |
|---|---|---|
| "No arranca al hacer Play desde `01_MainMenu` directo" | Correcto: el Bootstrap es pre-requisito. | Siempre correr desde `00_Bootstrap`. Un `BootstrapSkipper` para dev es tarea futura (no T102). |
| "Canvas no se ve" | Canvas Render Mode incorrecto, o el `Background` no stretchea. | Render Mode = Screen Space - Overlay. Background Image con anchors en los 4 corners (preset stretch-stretch). |
| "Click en Jugar no hace nada, sin ningun log" | `ScreenHost` no esta presente o no se registro. | Verificar que el GameObject `ScreenHost` existe en la escena con el componente attached. Solo uno. |
| "Click en Jugar loggea error `IScreenManager no esta registrado`" | `ScreenHost` no ejecuto `Awake` antes que `MainMenuScreen.OnEnable`. | No deberia pasar con execution order default (hijos se activan despues del parent). Si pasa, movemos el `ScreenHost` a execution order `-100`. |
| "Warning: 'ClassSelectionScreen' no esta registrada" | Esperado hasta que T98 mergee. No es un bug. | Ignorar hasta merge de T98. |
| "Warning: `_playButton` no esta cableado" | El usuario olvido arrastrar el Button en el Inspector (§8.4). | Arrastrar `PlayButton`/`QuitButton` a los slots correspondientes. |

---

## 8.12 Definicion de Done (validacion del PR)

- [ ] `01_MainMenu.unity` existe en `Assets/Rollgeon/Scenes/`.
- [ ] `01_MainMenu` esta en Build Settings en indice 1.
- [ ] Play desde `00_Bootstrap` → carga `01_MainMenu` automaticamente.
- [ ] Boton "Jugar" → warning graceful de `ClassSelectionScreen` (hoy) / navega
      a `ClassSelectionScreen` (cuando T98 mergee).
- [ ] Boton "Salir" → editor sale de playmode / build cierra.
- [ ] `Rollgeon.UI.Tests.ScreenManagerTests` pasan 100% en Test Runner.
- [ ] Cero archivos `.unity`/`.prefab`/`.asset` creados por el dev en este PR.
