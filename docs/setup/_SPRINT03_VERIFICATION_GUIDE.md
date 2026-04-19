# Sprint 03 FP — Guía de verificación

> **Generado automáticamente el 2026-04-18 por el orchestrator.**
> 15 tareas mergeadas a `develop` (47 commits ahead de `origin/develop`, sin push).
> Este documento te da el orden correcto para validar el trabajo en Unity antes de proceder con Phase 3.

---

## Paso 0 — Compile check global (CRÍTICO)

Antes de tocar nada:

1. Abrir el proyecto en Unity.
2. Esperar recompilación completa.
3. Verificar **0 errores** en Console.
4. Si hay errores de compile → reportar antes de seguir (es tiempo de bug fix, no de setup).

**Lo que debe compilar sin errores:**
- `Assets/Scripts/Rollgeon/Patterns/` — ServiceLocator, EventManager, EventName, TypedEvent, FSM, Catalogs, Bootstrap
- `Assets/Scripts/Rollgeon/Attributes/` — BaseAttribute, ModifiableAttributes, Modifier<T>, AttributesManager, Stats (Energy, Speed, Health, HealStrength), HiddenFromUIAttribute
- `Assets/Scripts/Rollgeon/Effects/` — IEffect, BaseEffect, EffectData, capability interfaces, concretos ejemplo (EffDamage, EffHeal), stubs
- `Assets/Scripts/Rollgeon/PreConditions/` — BasePreCondition, PCComposite
- `Assets/Scripts/Rollgeon/Combos/` — BaseComboSO, 8 concretos, ComboCatalogSO, Counters
- `Assets/Scripts/Rollgeon/Heroes/` — ClassHeroSO, ContractSheet, ContractWarriorFactory
- `Assets/Scripts/Rollgeon/Entities/` — BaseEntitySO, EnemyDataSO, EnemyCatalogSO, Behaviors, Bosses
- `Assets/Scripts/Rollgeon/Combat/` — Actions (TurnManager), Energy (EnergyService), ComboBlock, FSM (CombatTurnFSM + CombatController), Weakness
- `Assets/Scripts/Rollgeon/Dice/` — RerollBudget
- `Assets/Scripts/Rollgeon/UI/` — ScreenManager, BaseScreen, ScreenHost, MainMenuScreen, ClassSelectionScreen, ExplorationHUDView, CombatHUDView + sub-views
- `Assets/Scripts/Rollgeon/Balance/` — RulesetSO (Energy+TurnOrder+Weakness+Counters merge-hook)
- `Assets/Scripts/Rollgeon/Dungeon/` — FloorExitInteractable

**Tests EditMode esperados:** ~250+ tests. `Window → General → Test Runner → EditMode → Run All`. Todos deberían pasar verdes.

---

## Paso 1 — Setup foundations (orden importa)

Ejecutar los setup docs en **este orden exacto** (reflejan el orden de merge + dependencias):

### 1.1 Foundations (patrón de infraestructura — SO/scene creation)

| # | Doc | Qué hace | Bloqueante para |
|---|---|---|---|
| 1 | `Foundation#0001_ServiceLocatorEventManager.md` | Nada que setear — es solo código infra. | Todo. |
| 2 | `Foundation#0002_FSM.md` | Nada que setear. | Combate. |
| 3 | `Foundation#0003_AttributesAndModifiers.md` | Nada que setear (stats se crean por SOs downstream). | Todo lo que use stats. |
| 4 | `Foundation#0004_EffectsPreConditions.md` | Nada que setear. El EffectData vive inline en otros SOs. | Todo lo que use efectos. |
| 5 | `Foundation#0005_CatalogsAndBootstrap.md` | **CRÍTICO** — creás la bootstrap scene `00_Bootstrap.unity` + el asset `ServiceBootstrap.asset`. | Todo. |

### 1.2 Core systems (Combat)

| # | Doc | Qué hace |
|---|---|---|
| 6 | `System#0100a_EnergyAttributeAndRegen.md` | Crear `Ruleset.asset` (si no existe del F#0005) y setear EnergyConfig (Max=4, AtRunStart=2, RegenBase=2). |
| 7 | `System#0100b_ActionEconomyRepetition.md` | Crear `ActionCatalog.asset` + 6 `ActionDefinition*.asset` (Move, AttackBasic, AttackSpecial, Heal, ForceDoor, EndTurn). Registrar en `ServiceBootstrap`. |
| 8 | `System#0100c_TurnOrderHiddenSpeed.md` | En `Ruleset.asset` setear TurnOrderConfig (SpeedDieMin=1, SpeedDieMax=6). |
| 9 | `System#0100d_CombatTurnFSM.md` | Agregar `CombatController` MonoBehaviour en una scene de prueba (o en 01_MainMenu temporalmente). |

### 1.3 Content (Combos + Heroes)

| # | Doc | Qué hace |
|---|---|---|
| 10 | `Content#0097a_ComboBaseAndConcretes.md` | Crear `ComboCatalog.asset` + 8 `Combo_*.asset` con BaseDamage del contrato del Guerrero (Par=10, DoblePar=18, SumaX=25, Trio=28, Escalera=35, FullHouse=40, Poker=60, Generala=100). |
| 11 | `Content#0097b_WarriorContractAndWeakness.md` | Crear `ClassHero_Warrior.asset` + arrastrar 8 combos en Sheet.Combos por prioridad desc. En `Ruleset.asset` setear WeaknessConfig.DefaultMultiplier (=1.5). |
| 12 | `Content#0097c_ComboCountersAndStrike.md` | En `Ruleset.asset` setear Counters (PerUseBonus=0.02, MaxBonus=0.20). Strike queda TBD (§5.6 diferido). |
| 13 | `Content#0099_SupportEnemyAuditor.md` | Crear `EnemyData_Auditor.asset` (stats: HP=20, HealStrength=5, Speed=4, MaxEnergy=3), `EnemyCatalog.asset`, asignar SupportHealBehavior. |
| 14 | `Content#0103_BossFloorManager.md` | Crear `BossFloorManager.asset` con stats + 3 behaviors (ComboBlock, EnergyBuildup, Attack). ComboBlockInterval=3, Duration=2. |

### 1.4 UI screens

| # | Doc | Qué hace |
|---|---|---|
| 15 | `UI#0102_MainMenu.md` | Crear scene `01_MainMenu.unity` con ScreenHost + Canvas + 2 botones (Jugar/Salir) + MainMenuScreen. Agregar a Build Settings index 1. |
| 16 | `UI#0098_ClassSelectionScreen.md` | En `01_MainMenu.unity`: agregar ClassSelectionScreen child al ScreenHost + canvas extra para selector. Asignar `ClassHero_Warrior.asset` al slot. |
| 17 | `UI#0095a_ExplorationHUD.md` | Crear scene o canvas de exploration HUD con 5 sub-views wired (Health, Energy, Gold, ActiveItems, Minimap). |
| 18 | `UI#0095b_CombatHUD.md` | Crear canvas de combat HUD con 7 sub-views + FloatingDamage spawner + prefab TurnSlotView. |

### 1.5 Features extras

| # | Doc | Qué hace |
|---|---|---|
| 19 | `Feature#0104_EnergyReroll.md` | Registrar `RerollBudgetService` en `ServiceBootstrap.ExtraServices`. Setear FreeRollCount en cada `ActionDefinition*.asset` (attacks=3, heal/force=1). |

---

## Paso 2 — Smoke test playable

Orden:
1. **Bootstrap scene** → debería loguear `[Bootstrap] RegisterAll() invoked` + `Registered N catalogs...`.
2. **Main Menu** debería aparecer. Click "Jugar" → navega a Class Selection (o loguea warning si no wireaste la pantalla).
3. **Class Selection** → Warrior seleccionable, Mago/Picaro locked. Click Warrior → muestra ContractSheet (8 filas). Click Confirm → dispara `OnRunStart` (veres warning "BuildSelectionScreen not found" — esperado, T98 dejó stub).
4. **Test un combate manualmente** (temporal): en una scene de test, crear un MonoBehaviour que llame `CombatController.StartCombat(new[]{ playerGuid, enemyGuid })` con entidades dummy. Validar que PlayerTurn/EnemyTurn transicionan, `OnTurnStarted`/`OnTurnFinished` firean, `TurnManager._actionsUsedThisTurn` se limpia.

---

## Paso 3 — Revisar TBDs

Dejamos cosas flaggeadas para post-Sprint:
- **§5.6 Strike combos** — marcado TBD en TECHNICAL.md. No implementar.
- **§G IPlayerService** — stub con solo `PlayerGuid`. Extender cuando se agregue hero spawn real.
- **§12.0 IPhaseService overlays** — hooks presentes en CombatController, publisher no existe.
- **§15 SaveSystem** — interfaces `ISaveable` implementadas (stubs), service de save no existe.
- **`BuildSelectionScreen`** — T98 pushea por string id, no existe la screen aún.
- **`HealStrength` en `Rollgeon.Entities.Behaviors`** en lugar de `Attributes.Stats` — follow-up de relocation.
- **Damage pipeline real** — los combates hacen `AttributesManager.Modify<Health,int>` directo. Falta pipeline con mitigación/crit/etc.
- **Enemy AI delegate** — CombatContext.EnemyActionHandler es stub que siempre termina turno.

---

## Paso 4 — Cuando todo compile y corras un smoke test exitoso

Volvé a chatearme y ejecutamos **Phase 3** (en orden):

1. **T101 — Balance Inspector audit** (3h) — auditoría transversal de que TODAS las variables listadas en issue #101 están expuestas y con Tooltip/Range.
2. **Tools (5 en paralelo):**
   - `Tool#0100T_EnergyActionBalanceEditor` (3h)
   - `Tool#0097T_ContractEditor` (3h)
   - `Tool#0099T_EntityWizardSupport` (2h)
   - `Tool#0103T_BossSetupWizard` (2h)
   - `Tool#0095T_HUDPreview` (2h)

Total estimado Phase 3: ~15h de trabajo de agents.

---

## Resumen numérico

- **Worktrees ejecutados**: 15
- **Merge commits en develop**: ~22 (incluye merges develop→branch + feature→develop)
- **Archivos C# nuevos**: ~180+
- **Tests EditMode**: ~250+
- **Setup docs**: 19 (este incluido)
- **Commits ahead de origin/develop**: 47 (sin push — tu responsabilidad pushear cuando esté verificado)

Good luck. 🎲
