# Setup — Foundation#0005 Catalogs + ServiceBootstrapSO + BootstrapRunner

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 10–15 min.
> **Prerrequisito merge:** Foundation#0001 (ServiceLocator + EventManager + ServiceScope) ya en `develop`.

Este instructivo describe cómo dejar la infraestructura de bootstrap **operativa
dentro del editor de Unity**. Ningún paso está automatizado: todos los assets
(`.unity`, `.asset`) los crea el usuario manualmente por diseño — este worktree
sólo entrega código.

Cualquier duda sobre el *por qué* de un paso: ver `plan.md` y `TECHNICAL.md`
§1.1, §1.1.1, §1.1.2, §1.1.3.

---

## 8.1 Pre-requisitos

1. **Foundation#0001 mergeado** a `develop`: debe existir el namespace `Patterns`
   con las clases `ServiceLocator`, `EventManager`, `EventName`, `ServiceScope`.
   Verificar en `Assets/Scripts/Rollgeon/Patterns/`.
2. **Odin Inspector instalado** (Sirenix). Es dependencia dura: `ServiceBootstrapSO`
   y `BaseCatalogSO<T>` usan `SerializedScriptableObject` y `[OdinSerialize]`.
3. Abrir el proyecto en **Unity** (versión del proyecto), esperar a que recompile,
   confirmar **0 errores** en la consola.
4. Tener la vista **Project** con modo "One Column Layout" o "Two Column Layout"
   — todo lo siguiente usa rutas absolutas bajo `Assets/Rollgeon/`.

---

## 8.2 Crear la escena de Bootstrap

1. `File → New Scene` → elegir plantilla **Empty (URP)** o **Basic (Built-in RP)**
   según tu `RenderPipeline`.
2. Eliminar cualquier GameObject que el template agregue (Main Camera, Directional
   Light, etc.) — esta escena sólo hospeda el runner; no renderiza nada.
3. `File → Save As…` → guardar en `Assets/Scenes/00_Bootstrap.unity`
   (crear la carpeta `Scenes` si no existe).
4. En la jerarquía, `Create Empty` → renombrar a **`Bootstrap`**. Dejar su
   Transform en `(0,0,0)` por prolijidad (no hay renderizado, es irrelevante).
5. Con el GameObject `Bootstrap` seleccionado → `Add Component` → buscar
   **"Bootstrap Runner"** (namespace `Rollgeon.Patterns.Bootstrap`) y agregarlo.
6. Dejar los campos del componente vacíos por ahora — se wirean en §8.4.

---

## 8.3 Crear el asset `ServiceBootstrapSO`

1. En la vista **Project**, crear la carpeta `Assets/Rollgeon/Bootstrap/` si no
   existe.
2. Click derecho dentro de esa carpeta → `Create → Rollgeon → Service Bootstrap`.
3. Nombrar el asset exactamente **`ServiceBootstrap`** (sin sufijo `.asset` —
   Unity lo agrega automáticamente).
4. Seleccionar el asset. Odin Inspector muestra cuatro `[Title]` groups:
   - **Catalogs** — lista polimórfica de `BaseCatalogSO`.
   - **Settings Assets** — lista de `ScriptableObject` de configuración.
   - **Extra Runtime Services** — lista de implementadores de `IPreloadableService`.
   - **Scene Chaining** — campo `_nextSceneName`, default `01_MainMenu`.
5. Confirmar que `_nextSceneName` diga **`01_MainMenu`** (case-sensitive — debe
   coincidir con el nombre exacto del asset de escena).

---

## 8.4 Wirear Runner ↔ SO

1. Abrir `Assets/Scenes/00_Bootstrap.unity` (doble click).
2. Seleccionar el GameObject `Bootstrap` en la jerarquía.
3. En el Inspector, localizar el componente **Bootstrap Runner**.
4. Arrastrar el asset `Assets/Rollgeon/Bootstrap/ServiceBootstrap.asset` al slot
   **`_bootstrap`** (tiene un `[Required]` de Odin que indica en rojo si queda
   vacío).
5. Dejar **`_nextScene`** vacío (usa default del SO). Llenar sólo si se quiere
   overridear por-escena (ej. apuntar a una escena de test).
6. Dejar **`_dontDestroyOnLoad = false`** y **`_preloadCatalogs = true`** para
   Sprint 03.
7. Guardar con `Ctrl+S`.

---

## 8.5 Crear catálogos placeholder

Cada catálogo concreto (ComboCatalog, EntityCatalog, ActionCatalog, …) lo
entrega un downstream worktree (T97a, T99, T100b, T103…). **Este worktree no
crea ningún catálogo concreto.** Con la lista `Catalogs` vacía,
`RegisterAll()` corre sin error y loguea `Registered 0 catalogs, ...`.

Cuando un downstream mergee un catálogo, el flujo para enchufarlo es:

1. Ir a `Assets/Rollgeon/<subcarpeta-del-downstream>/` (p.ej. `Combos/`).
2. Click derecho → `Create → Rollgeon → <Nombre del catálogo>` (cada downstream
   define su `[CreateAssetMenu]`).
3. Nombrarlo — convención: el nombre del SO sin el sufijo `SO` (`ComboCatalog`).
4. Abrir `Assets/Rollgeon/Bootstrap/ServiceBootstrap.asset`.
5. En la sección **Catalogs**, click `+` para agregar un slot, arrastrar el
   asset recién creado.
6. El validator de Odin muestra rojo si hay nulls o duplicados en la lista.

Repetir por cada catálogo a medida que los worktrees mergean.

---

## 8.6 Settings placeholder (opcional Sprint 03)

Mismo patrón que §8.5 pero con la lista **Settings Assets**. Los slots típicos
(ver `plan.md` §6.2): `SaveSettingsSO`, `CameraConfigSO`, `AudioSettingsSO`,
`ShopConfigSO`, `PhaseTransitionMatrixSO`, `GameSettingsSO`, `MinimapIconsSO`.

Para Sprint 03 esta lista **puede quedar vacía** — no bloquea el smoke-test.

---

## 8.7 Build Settings

La escena `00_Bootstrap` debe quedar en el **índice 0** del build:

1. `File → Build Settings…`.
2. Con `00_Bootstrap.unity` abierta en el editor, click **`Add Open Scenes`**.
3. Asegurarse que aparezca arriba de todo con índice **0**. Arrastrarla ahí si
   no lo está.
4. Cuando T102 (MainMenu) mergee, agregar `01_MainMenu.unity` en índice **1**.
5. Cerrar el diálogo.

> **Regla de play-mode.** Siempre iniciar `Play` desde `00_Bootstrap.unity`. Si
> se hace `Play` desde otra escena, ningún servicio queda registrado y los
> downstream crashean con `KeyNotFoundException` en el primer `GetService<T>`.
> Ver §8.9 troubleshooting.

---

## 8.8 Smoke-test

1. Abrir `Assets/Scenes/00_Bootstrap.unity`.
2. Click en `Play`.
3. Verificar en la **Console** de Unity los siguientes logs (en orden):
   - `[Bootstrap] RegisterAll() invoked`
   - `[Bootstrap] Registered N catalogs, M settings, K extra services` (con
     `N/M/K` coherentes con lo que pusiste en el asset; `0/0/0` es valido).
   - `[Bootstrap] Preload complete` *(o `Preload skipped (0 catalogs)` si
     `Catalogs` esta vacia)*.
   - `[Bootstrap] Hooks installed (OnRunStart, OnRunEnd)`
   - `[Bootstrap] Loading scene 01_MainMenu`
4. Si `01_MainMenu` aún no existe (normal hasta que T102 mergee):
   - Aparece el warning `[Bootstrap] Scene '01_MainMenu' no esta en Build
     Settings. Esperado solo hasta que T102 MainMenu mergee.` y el runner se
     detiene sin crashear. Ese es el comportamiento esperado para Sprint 03.
5. Detener `Play`.

---

## 8.9 Troubleshooting

### `NullReferenceException` — "ServiceBootstrapSO reference is null"

El `BootstrapRunner` no tiene su campo `_bootstrap` asignado. Volver a §8.4 y
arrastrar el asset.

### `[Bootstrap] Scene 'XXX' no esta en Build Settings`

La escena destino (`_nextScene` del runner o `NextSceneName` del SO) no está
en `File → Build Settings`. Agregarla (§8.7). Hasta que T102 mergee, este
warning es esperado y se puede ignorar.

### `Service already registered` / entries duplicadas

`RegisterAll()` corrió dos veces en la misma sesión. Verificar que haya **un
solo** `BootstrapRunner` en `00_Bootstrap.unity` — si se duplicó el GameObject
`Bootstrap`, los segundos registros sobrescriben a los primeros (comportamiento
de `AddService` de Foundation#0001 es upsert).

### Odin no muestra la lista polimórfica de `Catalogs`/`ExtraServices`

El SO debe heredar de `SerializedScriptableObject` (no `ScriptableObject`) y
los campos deben tener `[OdinSerialize]` (no `[SerializeField]`). Si vino así
de este merge: confirmar que Odin esté importado sin errores de compile en
`Sirenix/`.

### `PreloadAllCatalogsAsync` tarda mucho / nunca termina

Algún catálogo downstream sobreescribió `PreloadAsync()` con una llamada a
Addressables / red que cuelga. Mitigación temporal: desactivar el toggle
`_preloadCatalogs` del `BootstrapRunner` para probar sin preload. Fix real:
reportar bug al owner del catálogo.

### Consola muestra excepciones dentro de `IPreloadableService.Register()`

Cada excepción es capturada y logueada con contexto sin abortar el bootstrap
(ver `ServiceBootstrapSO.RegisterAll`). El servicio fallido queda sin
registrar — reportar al owner del servicio.

### Play desde otra escena → `KeyNotFoundException`

Sprint 03 no incluye un `[RuntimeInitializeOnLoadMethod]` que fuerce la carga
de `00_Bootstrap` (se descartó porque complica el testing por-escena de otros
devs). Regla operativa: **siempre arrancar desde `00_Bootstrap.unity`**.
