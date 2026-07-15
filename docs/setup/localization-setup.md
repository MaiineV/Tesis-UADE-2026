# Setup — Localización ES/EN (Unity Localization)

> Idioma Español/Inglés con el package oficial `com.unity.localization` (1.5.12).
> Selector ESP/ING en el menú principal, persistencia entre sesiones y refresco en
> vivo sin reiniciar.
>
> **Estado:** Infraestructura + menú principal + flujo de menús + **todos los
> read-sites de contenido** + preload de tablas, **funcionando y validados en Play**.
> Pendiente deliberado: chrome dentro de la escena `02_Gameplay` (se trabaja en otra
> rama; se evita para no generar conflictos) y el pulido de la traducción EN del
> contenido masivo (ver *Alcance / pendientes*).

## Arquitectura (dos carriles)

- **Chrome estático** (labels fijos de UI): componente `LocalizeStringEvent` por TMP,
  apuntando a la String Table **`UI`**. Refresca solo al cambiar idioma.
- **Contenido data-driven** (`DisplayName`/`Description` de SOs): resolver central
  `LocalizedContent` contra la String Table **`Content`**, keyeada por el id de la
  entidad (`<id>.name` / `<id>.desc`), con **fallback al valor autor del SO**.

## Qué se agregó (código)

| Archivo | Rol |
|---|---|
| `Assets/Scripts/Rollgeon/Localization/ILocalizationService.cs` | Interfaz del servicio de idioma. |
| `Assets/Scripts/Rollgeon/Localization/LocalizationService.cs` | Envuelve `LocalizationSettings`; reemite `LanguageChanged`. |
| `Assets/Scripts/Rollgeon/Localization/LocalizationServiceBootstrap.cs` | `IPreloadableService` (Priority −100). Registra el servicio en el bootstrap. |
| `Assets/Scripts/Rollgeon/Localization/LanguageSelector.cs` | MonoBehaviour de los botones ESP/ING. |
| `Assets/Scripts/Rollgeon/Localization/LocalizedContent.cs` | Resolver: `Name/Description/Ui/Resolve/FromTable`. |
| `Assets/Scripts/Editor/Tools/Localization/LocalizationSetupTools.cs` | Utilidades de editor: `UpsertEntry`, `BindTMP`. |
| `Assets/Scripts/Rollgeon/Localization/Tests/*` | Tests EditMode (fallback del resolver + delegación del selector). |

Referencias de assembly agregadas: `Rollgeon.asmdef` → `Unity.Localization`;
`Rollgeon.Editor.asmdef` → `Unity.Localization`, `Unity.Localization.Editor`,
`Unity.TextMeshPro`.

## Wiring (ya aplicado por Unity MCP)

- **Package** `com.unity.localization` 1.5.12 instalado (`Packages/manifest.json`).
- **Assets** bajo `Assets/Localization/`: `LocalizationSettings.asset` (activo),
  locales `es`/`en`, y las String Table Collections `UI` y `Content` (bajo `Tables/`).
- **Startup selectors** (orden): `PlayerPrefLocaleSelector` → `SystemLocaleSelector`
  → `SpecificLocaleSelector(es)`. Es decir: elección guardada del jugador → idioma
  del sistema → fallback Español.
- **Bootstrap**: `LocalizationServiceBootstrap` agregado a `ExtraServices` en
  `Assets/Rollgeon/ServiceBootstrap.asset`.
- **Escena `01_MainMenu`**:
  - `LocalizeStringEvent` en los labels de: Play, Continue, Unlocks, Delete, Quit
    (tabla `UI`, keys `menu.*`); y en ClassSelection/BuildSelection/UnlockScreen:
    Confirm, Back, Clear, título de Unlocks, fallback de bolsa de dados.
  - Dos botones **`SpanishButton`/`EnglishButton`** (clonados de Quit) bajo el
    `MainMenuScreen`, con el componente `LanguageSelector` cableado.
- **Tabla `Content`** poblada para ~89 entidades: contenido real y jugable con ES+EN
  autorados (héroes, pasiva, 9 combos, enemigos/bosses, poción, unlocks); el resto
  (upgrades/enchantments, salas) auto-generado con ES=EN=valor autor como primer
  pase (pendiente de traducción).

## Cómo funciona (resumen)

1. En el arranque, `BootstrapRunner` invoca `LocalizationServiceBootstrap.Register()`,
   que registra `ILocalizationService` y dispara la init de Localization (aplica el
   idioma: guardado → sistema → ES).
2. Los `LocalizeStringEvent` muestran el texto de la tabla `UI` en el locale activo.
3. El código que muestra contenido llama `LocalizedContent.Name/Description(id, fallback)`,
   que lee de `Content` o cae al valor autor del SO.
4. Los botones ESP/ING llaman `ILocalizationService.SetLanguage("es"|"en")`; el
   package refresca todos los `LocalizeStringEvent` y `PlayerPrefLocaleSelector`
   persiste la elección.

## Cómo probar (manual — requiere Play)

1. Play desde `00_Bootstrap`. El menú arranca en el idioma del sistema (o ES).
2. Click **ING** → los labels del menú pasan a inglés (el primer switch a un idioma
   carga su tabla y puede tardar ~1 frame extra). Click **ESP** → vuelven.
3. Entrar a selección de clase / build → nombre y descripción del héroe, combos y
   nombre de sala salen en el idioma activo.
4. Persistencia: setear ING, salir de Play, volver a entrar → arranca en ING
   (`PlayerPrefLocaleSelector`).

Tests: `run_tests` EditMode del assembly `Rollgeon.Localization.Tests` (5/5 en verde).

## Read-sites de contenido enrutados (código)

Ya llaman `LocalizedContent.Name/Description(id, fallback)` (o localizan al copiar al
payload): `BuildSelectionScreen`, `ClassSelectionScreen` (pasiva), `ComboRowView`,
`RoomNavigationView`, `UnlockToastView`, `EnchantmentAltarView`,
`CharacterRewardPedestalInteractable`, `ShopItemPedestalInteractable`, `UnlocksScreen`,
`UnlockResultsView`, `ActionRollPanelView`, `DamageFormulaView`, `ActionRollService`,
`DiceZoneView`, `MetaProgressionService`, `CombatHandoffService`. `ComboIndicatorView`
queda cubierto porque lee el payload ya localizado. Los botones de clase
(Warrior/Rogue/Mage) se localizan por `LocalizeStringEvent` → `UI/class.*`.

## Chrome de gameplay (Canvas prefabs)

La UI de gameplay vive en prefabs (`Assets/Prefabs/UI/Canvas/Canvas_*.prefab`), no
inline en `02_Gameplay.unity`. Localizado a nivel **prefab** (con
`PrefabUtility.LoadPrefabContents`, sin abrir ni modificar la escena): PauseMenu,
Victory, Defeat, FloorTransition, ActionRoll, EnchantmentAltar (headers/botones),
CombatHUD y ExplorationHUD (botones de acción, End Turn, Pass). Los labels
dinámicos/code-set (números de vida/energía/oro, costos, hotkeys `(Q)`, room
name/progress/type, tooltips, toasts, formula de daño) quedan fuera a propósito.

## Alcance / pendientes
- **Traducción EN del contenido masivo** (enchantments/salas): hoy ES=EN. Pulir con
  `UpsertEntry("Content", "<id>.name"|".desc", es, en)`.
- **Strings de chrome hardcodeados menores** en algunos views (`"Encantado:"`, `"Tomar"`,
  `"Rooms {n}/{m}"`, etc.): quedan en español; localizar con `LocalizedContent.Ui(key, fb)`
  + `UpsertEntry("UI", ...)` si se quiere cobertura total.

## Nota de testing (editor)

El refresco de los `LocalizeStringEvent` usa AsyncOperations. Con el editor **sin foco**
(ej. manejado por MCP) esas ops pueden tardar en tickear, así que un switch puede verse
demorado o parcial en el editor. Con el editor enfocado en Play y en build —con las
tablas en *preload*— el cambio es inmediato. Verificado que la cadena
referencia→listener→DB es correcta punta a punta.

## Preload

`LocalizationSettings.PreloadBehavior = PreloadAllLocales` y ambas colecciones (`UI`,
`Content`) marcadas para preload → al inicializar se cargan las tablas de ES y EN, así
el switch en cualquier dirección es instantáneo.

## Rollback

- Quitar `LocalizationServiceBootstrap` de `ServiceBootstrap.asset` → `ExtraServices`.
- Borrar `Assets/Localization/` y `Assets/Scripts/Rollgeon/Localization/`.
- Quitar `Unity.Localization` de `Rollgeon.asmdef` y remover el package del manifest.
- Los `LocalizeStringEvent` en escena quedan inertes si el package se va; borrarlos de
  los TMP afectados restaura el texto estático.
