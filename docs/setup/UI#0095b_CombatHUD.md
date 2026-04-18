# Setup — UI#0095b Combat HUD

> **Audiencia:** el usuario del proyecto, tras mergear este PR a `develop`.
> **Tiempo estimado:** 60-90 min (armar canvas + 2 prefabs + wirear 7 sub-views +
> cablear `CombatController`).
> **Prerrequisito merge:** T100d (`CombatTurnFSM` + `CombatController`), T102 (MainMenu
> + ScreenManager + BaseScreen), T95a (ExplorationHUDView — sub-view pattern), T97a/b
> (combos + contract), T99 (enemy spawn), T104 (IRerollBudgetService), Foundation
> #0001–0005.

Este instructivo deja el **Combat HUD** operativo dentro del editor. El worktree entrega
solo código C# (`Rollgeon.UI.Screens.CombatHUDView` + 6 sub-views en `Rollgeon.UI.HUD`
+ `FloatingDamageSpawner` + `FloatingDamageInstance` + payload + tests). La escena,
canvas y prefabs los arma el usuario manualmente.

Dudas sobre el *por qué* de un paso: ver `plan.md` (worktree raíz) y `TECHNICAL.md`
§17.D / §12.6 / §12.7 / §5.3-5.4 / §1.2.1.

---

## 8.0 Decisiones de infra confirmadas en este merge

- **No hay `Rollgeon.UI.asmdef` runtime.** Misma convención que T102 y T95a: la
  capa UI vive en `Assembly-CSharp` default. Modularizar es refactor dedicado.
- **`Rollgeon.UI.Tests.asmdef` (EditMode)** reutiliza la suite existente. No se
  agregan referencias nuevas — los tests usan fakes locales (`FakePlayerService`,
  `FakePositionResolver`) para aislarse de TurnManager / RerollBudgetService.
- **Escena** — se reutiliza la escena gameplay que ya tenga el `ScreenHost`. No
  se crea una escena dedicada para el combat HUD.
- **`CombatController` wiring** — el PR NO toca `CombatController.cs`. El wiring
  de delegates se hace **post-push** por el script de gameplay / escena (ver §8.7).
  Alternativa: si se decide integrar el push al `CombatController` en un follow-up,
  es un diff acotado (~10-15 líneas).

---

## 8.1 Pre-requisitos

1. **T100d mergeada** a `develop`. Verificar:
   - `Assets/Scripts/Rollgeon/Combat/FSM/CombatController.cs` existe.
   - `ServiceBootstrapSO` registra `TurnOrderService` + `TurnManager` + `IEnergyService`.
2. **T95a mergeada.** Existe:
   - `Assets/Scripts/Rollgeon/UI/Screens/ExplorationHUDView.cs` — patrón de sub-views.
   - `Assets/Scripts/Rollgeon/UI/ScreenHost.cs` — host de registro.
   - `Rollgeon.Player.IPlayerService` (stub o real).
3. **T97a/b + T104** mergeadas para features ancilares (opcional pero recomendado):
   - Weakness registry (T97b) — el `EnemyPanelView` consulta `IWeaknessRegistry` si
     está disponible; sin el servicio, el ícono queda oculto (fallback graceful).
   - `IRerollBudgetService` (T104) — el `RerollCountView` muestra fallback `-/-` si
     no está registrado.
4. **Odin + TMP** instalados (mismos requisitos que T102).
5. Abrir el proyecto en Unity, esperar recompile, confirmar **0 errores**.

---

## 8.2 Crear prefab `TurnSlot.prefab`

1. En la escena de gameplay, bajo el Canvas del Combat HUD (§8.4), crear un `Panel`
   (RectTransform 80×80 px).
2. Agregar como children:
   - `Image` (portrait) — sprite placeholder del enemy/player.
   - `TextMeshProUGUI` (label) — texto pequeño arriba a la izquierda (1,2,3…).
   - `ActiveHighlight` (GameObject con Image outline dorado — se activa en turno).
   - `DestroyedOverlay` (GameObject con Image de X roja — se activa al morir).
3. Attach `TurnSlotView.cs` al root del panel.
4. Cablear en Inspector:
   - `_portrait` → Image portrait.
   - `_label` → TMP label.
   - `_activeHighlight` → GO ActiveHighlight.
   - `_destroyedOverlay` → GO DestroyedOverlay.
   - `_highlightColor`, `_idleColor` según preferencia.
5. Drag a `Assets/Rollgeon/Prefabs/UI/TurnSlot.prefab`.
6. Eliminar la instancia de la escena (el `TurnQueueView` instanciará copias en runtime).

---

## 8.3 Crear prefab `FloatingDamage.prefab`

1. Bajo el canvas worldspace / overlay (ver §8.4), crear GameObject con:
   - `RectTransform`.
   - `CanvasGroup` (para animar alpha).
   - `TextMeshProUGUI` grande (tamaño 48+).
2. Attach `FloatingDamageInstance.cs`.
3. Cablear en Inspector:
   - `_text` → el TMP.
   - `_canvasGroup` → el CanvasGroup del mismo GO.
   - `_riseHeight` = 50, `_durationSeconds` = 1.2, `_fadeOutRatio` = 0.6 (defaults).
4. Drag a `Assets/Rollgeon/Prefabs/UI/FloatingDamage.prefab`.
5. Eliminar la instancia de la escena.

---

## 8.4 Montar la jerarquía del Combat HUD

Bajo el Canvas principal (ScreenSpace-Overlay) de la escena gameplay, crear la
estructura:

```
Canvas (Screen Space - Overlay)
├── ExplorationHUDView (ya existente — T95a)
└── CombatHUDView (GameObject nuevo)
    ├── DamageFlashGroup (Image full-screen rojo semi-transparente + CanvasGroup)
    ├── TurnQueueRoot
    │   └── TurnQueueView (container = self, slotPrefab = TurnSlot.prefab)
    ├── EnemyPanelRoot
    │   └── EnemyPanelView
    │       ├── PanelRoot (child GO — se oculta con Guid.Empty)
    │       │   ├── NameLabel (TMP)
    │       │   ├── HpSlider (Slider)
    │       │   ├── HpText (TMP)
    │       │   └── WeaknessRoot (GO)
    │       │       └── WeaknessIcon (Image)
    ├── ComboIndicatorRoot
    │   └── ComboIndicatorView
    │       ├── CurrentComboLabel (TMP)
    │       └── ContractRows (8 rows — uno por combo del guerrero)
    │           ├── ParRow (Label TMP + BlockedOverlay GO)
    │           ├── DoblePareRow
    │           ├── SumaXRow
    │           ├── TrioRow
    │           ├── EscaleraRow
    │           ├── FullHouseRow
    │           ├── PokerRow
    │           └── GeneralaRow
    ├── DiceZoneRoot
    │   └── DiceZoneView
    │       ├── RollArea (RectTransform)
    │       ├── HoldArea (RectTransform)
    │       └── DiceSlots (5 RectTransform children)
    ├── ActionButtonsRoot
    │   └── ActionButtonsView
    │       ├── AttackBtn (Button)
    │       ├── EnergyRerollBtn (Button)
    │       └── EndTurnBtn (Button)
    ├── RerollCountRoot
    │   └── RerollCountView
    │       ├── CountLabel (TMP "{used}/{cap}")
    │       └── ExtraRollBtn (Button)
    └── FloatingDamageOverlay (RectTransform)
        └── FloatingDamageSpawner (instancePrefab = FloatingDamage.prefab,
                                    overlayContainer = self)
```

**Nota sobre floating damage**: para el FP usamos ScreenSpace (no hace falta un
segundo canvas). El `_uiCamera` del spawner es opcional; si es null se usa
`Camera.main` solo cuando se resuelve una posición mundial. Sin `IEntityPositionResolver`
registrado, el spawner usa el centro de la pantalla como fallback.

---

## 8.5 Cablear refs del `CombatHUDView`

En el Inspector del GameObject raíz `CombatHUDView`:

| Campo | Drag target |
|---|---|
| `_turnQueue` | TurnQueueView |
| `_comboIndicator` | ComboIndicatorView |
| `_enemyPanel` | EnemyPanelView |
| `_actionButtons` | ActionButtonsView |
| `_diceZone` | DiceZoneView |
| `_rerollCount` | RerollCountView |
| `_floatingDamage` | FloatingDamageSpawner |
| `_damageFlashGroup` | CanvasGroup del DamageFlashGroup child |
| `_damageFlashSeconds` | 0.18 (default) |
| `_damageFlashAlpha` | 0.5 (default) |
| `_autoPopOnCombatEnd` | ✓ checked (default) |

---

## 8.6 Cablear refs de cada sub-view

### TurnQueueView
- `_slotPrefab` → `TurnSlot.prefab`.
- `_container` → Transform del TurnQueueRoot (self).

### ComboIndicatorView
- `_currentComboLabel` → TMP CurrentComboLabel.
- `_rows` → lista de 8 entradas, una por combo del guerrero:
  - Row 0: `ComboId = "combo.par"`, `Label` = ParRow label, `BlockedOverlay` = ParRow blocked GO.
  - … 7 más (DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala).
  - Los `ComboId` deben matchear con los IDs del catálogo T97a (verificar con
    `ComboCatalogSO.AllIds` en runtime).

### EnemyPanelView
- `_panelRoot` → GO PanelRoot child.
- `_name`, `_hpSlider`, `_hpText` → widgets del panel.
- `_weaknessRoot`, `_weaknessIcon` → widgets del bloque de weakness.

### ActionButtonsView
- `_attackButton`, `_energyRerollButton`, `_endTurnButton` → los 3 Buttons.
- `_attackAction` → el `ActionDefinitionSO` del ataque básico del guerrero (debe
  estar autorado — ver T100d / §12.6.0). Null = botón siempre disabled.

### DiceZoneView
- `_rollArea`, `_holdArea` → los 2 RectTransforms.
- `_diceSlots` → lista de 5 RectTransforms (anchor points para cada dado).

### RerollCountView
- `_countLabel` → TMP del label.
- `_extraRollButton` → Button.
- `_countFormat` = "{0}/{1}" (default).
- `_fallbackText` = "-/-" (default).

### FloatingDamageSpawner
- `_instancePrefab` → `FloatingDamage.prefab`.
- `_overlayContainer` → RectTransform del FloatingDamageOverlay.
- `_uiCamera` → null (usa `Camera.main` como fallback).
- `_outgoingTint`, `_incomingTint`, `_healTint` → colores al gusto.
- `_screenOffset` = (0, 60, 0) (default).

---

## 8.7 Cablear el `CombatController` al HUD

El PR **no modifica** `CombatController.cs`. El wiring se hace desde un script de
escena. Recomendado: crear un componente hermano `CombatHUDBridge.cs` que enlace
ambos:

```csharp
// Ejemplo — NO se entrega con este PR. Colocar en la escena gameplay.
using System;
using Rollgeon.Combat.FSM;
using Rollgeon.Dice;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using Patterns;
using UnityEngine;

public class CombatHUDBridge : MonoBehaviour
{
    [SerializeField] private CombatController _controller;
    [SerializeField] private CombatHUDView _hud;

    public void PushCombatHUD(Guid enemyTarget, Guid roomId)
    {
        var payload = new CombatHUDPayload
        {
            EnemyTargetGuid = enemyTarget,
            RoomInstanceId = roomId,
            EncounterDisplayName = "Combat",
        };

        // El ScreenHost registro el IScreenManager en el ServiceLocator (Awake).
        if (!ServiceLocator.TryGetService<IScreenManager>(out var manager) || manager == null)
        {
            Debug.LogError("IScreenManager no registrado — abortando push.");
            return;
        }
        manager.Push<CombatHUDView>(payload);

        _hud.OnAttackRequested = () => _controller.SendPlayerAction();
        _hud.OnEnergyRerollRequested = () =>
        {
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var b) && b != null)
            {
                // TryExtraRoll requires player guid — resolve via IPlayerService.
                if (ServiceLocator.TryGetService<Rollgeon.Player.IPlayerService>(out var ps))
                    b.TryExtraRoll(ps.PlayerGuid);
            }
        };
        _hud.OnEndTurnRequested = () => _controller.EndPlayerTurn();
    }

    public void PopCombatHUD()
    {
        if (ServiceLocator.TryGetService<IScreenManager>(out var manager) && manager != null)
        {
            manager.PopOverlay();
        }
    }
}
```

Cablear en Inspector:
- `_controller` → el GO con `CombatController`.
- `_hud` → el GO con `CombatHUDView`.

En el ciclo real, `PushCombatHUD` se llama cuando el `CombatController.StartCombat`
exitoso se complete (suscribir a `EventName.OnCombatStart` o directo al callback del
controller), y `PopCombatHUD` cuando `CombatController.NotifyCombatEnded(...)` cierre
el combate (o `OnCombatEnd`).

**Alternativa** — si se decide modificar `CombatController.cs` en un follow-up, los
mismos 3 delegates se setean al final de `StartCombat(...)`. Es un diff contenido
(<20 líneas). El plan §R3 detalla el tradeoff.

---

## 8.8 Registrar la screen al `ScreenHost`

El `ScreenHost` de la escena ya tiene una lista `BaseScreen[]` que registra en
`Awake`. Agregar el GO `CombatHUDView` a esa lista (drag desde la jerarquía).
El `ScreenStringId` del view es `"CombatHUD"` (constante — no editar).

El HUD arranca **desactivado** (`gameObject.SetActive(false)` lo maneja el
`ScreenManager` al hacer `Push`).

---

## 8.9 Eventos stub declarados en este PR

Este PR agrega 3 entries al enum `EventName` con el flag `// [STUB]`:

- `OnComboBlocked` — `[Guid affectedGuid, string comboId, int turnsRemaining]` (T103).
- `OnComboUnblocked` — `[Guid affectedGuid, string comboId]` (T103).
- `OnRerollBudgetChanged` — `[Guid playerGuid, int used, int cap]` (T104).

El `CombatHUDView` y sus sub-views los consumen inmediatamente. Cuando T103 / T104
mergeen sus publishers, los eventos se disparan sin cambios en la UI.

Si T103 / T104 **ya** mergearon con estos nombres, las entries del enum son
duplicadas (conflict de merge). El dev resuelve manualmente: dejar una sola entry
y eliminar los comentarios `// [STUB]`.

---

## 8.10 Interfaz `IEntityPositionResolver`

Nueva interfaz declarada en `Rollgeon.Entities.IEntityPositionResolver`:

```csharp
Vector3? TryGetWorldPosition(Guid entityId);
```

El `FloatingDamageSpawner` la consume para posicionar los números flotantes cerca
del target. Sin implementación registrada en `ServiceLocator`, el spawner usa el
centro de la pantalla como fallback.

La implementación real la provee el pipeline de enemy/boss spawn (T99 / T103). En
este PR solo se declara la interfaz.

---

## 8.11 Smoke test manual

1. Abrir la escena gameplay y entrar en play.
2. Confirmar que `ExplorationHUDView` aparece.
3. Disparar un combate — spawnear enemies y llamar
   `CombatController.StartCombat(playerId, participants, roomId, enemyAIHandler)` +
   `CombatHUDBridge.PushCombatHUD(enemyTarget, roomId)`.
4. Verificar:
   - `CombatHUDView` aparece encima del `ExplorationHUDView`.
   - `TurnQueueView` muestra N portraits en orden (dispara `OnTurnQueueBuilt` el
     `TurnOrderService.BuildForCombat`).
   - `EnemyPanelView` muestra nombre + HP + weakness icon (si hay registry).
   - Los 3 botones se habilitan cuando es turno del player (tras `OnTurnStarted`).
5. Click en **Attack** → dice/combo flow ocurre → el label del combo se highlightea
   → número de daño flota → HP del enemy baja.
6. Llamar `CombatController.NotifyCombatEnded(Victory)` + `PopCombatHUD()` → el HUD
   desaparece, el Exploration HUD vuelve visible.

---

## 8.12 Troubleshooting

**"`CombatHUDView` no aparece al pushear."**
Verificar que el GO está en la lista de `BaseScreen` del `ScreenHost`. El manager
busca por type → si no está registrado, `Push<CombatHUDView>` no hace nada.

**"Los botones no reaccionan al click."**
Verificar que el `CombatHUDBridge` (o equivalente) setea los 3 delegates
(`OnAttackRequested`, etc.) **tras** el push. Si quedan null, los clicks loggean
warning pero no crashean.

**"El `EnemyPanelView` no muestra nombre."**
El pipeline actual no tiene `IEntityNameService`. El `CombatController` debe llamar
`_hud._enemyPanel.SetNameText(string)` cuando setea el target. Fallback: el texto
del prefab queda como placeholder.

**"El floating damage aparece en el centro de la pantalla."**
`IEntityPositionResolver` no está registrado. Normal durante el FP — T99/T103 lo
registrarán al spawnear entidades. El fallback al centro es intencional.

**"`RerollCountView` muestra `-/-`."**
`IRerollBudgetService` no está registrado. Verificar que el bootstrap de T104 está
en `ServiceBootstrapSO.ExtraServices`.

---

## 8.13 Checklist DoD (§95 — block Combat HUD)

- [ ] Zona de dados + zona de hold visibles (`DiceZoneView`).
- [ ] Indicador de tiradas restantes (`RerollCountView`).
- [ ] Botón "tirada extra con energía" (`RerollCountView._extraRollButton` +
      `ActionButtonsView._energyRerollButton`).
- [ ] Nombre de combo detectado resaltado (`ComboIndicatorView._currentComboLabel`).
- [ ] Panel del enemigo: nombre + HP + weakness icon (`EnemyPanelView`).
- [ ] Cola de turnos (`TurnQueueView` + `TurnSlotView`).
- [ ] Botones de acción: Atacar / Energía extra / Terminar turno (`ActionButtonsView`).
- [ ] Número de daño flotante (`FloatingDamageSpawner` + `FloatingDamageInstance`).
- [ ] Indicación visual de combo logrado (highlight del `_currentComboLabel`).
- [ ] Feedback al recibir daño (`CombatHUDView._damageFlashGroup`).
- [ ] Indicación visual de combo BLOQUEADO (`ComboIndicatorView` — overlay por row,
      vía `OnComboBlocked`).
- [ ] Barra de energía del modo Craps — **stub deliberado**, out of scope §17.C.
- [ ] EditMode tests pasan (7 suites).
- [ ] 0 `.unity`/`.prefab`/`.asset` creados en el PR.
- [ ] Setup doc (este archivo) escrito y completo.
