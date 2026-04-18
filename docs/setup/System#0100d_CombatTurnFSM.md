# System#0100d_CombatTurnFSM — Setup

`CombatTurnFSM` + `CombatController` son la FSM minima viable del combate
para el FP (player + 1..N enemies, loop de turnos explicito).

Worktree: `D:/GitHub/TesisUade.worktrees/System#0100d_CombatTurnFSM`
Branch: `sprint03/system/0100d-combat-turn-fsm`
Namespace raiz: `Rollgeon.Combat.FSM`

## 1. Pre-requisitos merged en `develop`

- Foundation#0001..#0005 (ServiceLocator, EventManager, FSM, Attributes,
  Effects, Catalogs, Bootstrap).
- System#0100a (Energy + `IEnergyService`).
- System#0100b (ActionCatalogSO + `TurnManager` + `TurnManagerBootstrap`).
- System#0100c (`TurnOrderService` + Speed + `TurnOrderServiceBootstrap`).

Sin estos, el `CombatController.Awake` loguea error y se deshabilita.

## 2. Alcance (Revision 2)

- El turno del player termina **unicamente** cuando el usuario dispara
  `PlayerEndTurn` desde la UI. Energy == 0 NO auto-cierra el turno.
- `PlayerTurnState.CheckInput(PlayerActionDone)` es inerte; self-loop puro.
- El `TurnManager.CanExecute` sigue bloqueando acciones de costo > 0 cuando
  la energia es insuficiente (tarea de la UI mostrar el estado).

## 3. Archivos entregados

```
Assets/Scripts/Rollgeon/Combat/FSM/
  CombatContext.cs
  CombatInput.cs
  CombatOutcome.cs
  CombatTurnFSM.cs
  CombatController.cs
  States/
    CombatEnterState.cs
    PlayerTurnState.cs
    EnemyTurnState.cs
    CombatExitState.cs
  Tests/
    Rollgeon.Combat.FSM.Tests.asmdef
    FakeEnergyService.cs
    FakeInitiativeProvider.cs
    CombatTurnFSMTests.cs
    CombatControllerFreezeTests.cs
docs/setup/System#0100d_CombatTurnFSM.md
```

## 4. Unity steps

1. Abrir Unity, esperar recompilacion. Verificar 0 errores.
2. `Window -> General -> Test Runner -> EditMode -> Run All`. Los tests de
   `Rollgeon.Combat.FSM.Tests` deben pasar (incluyendo el nuevo
   `EnergyZero_DoesNotAutoEndTurn_FSMRemainsInPlayerTurnState`).
3. Abrir (o crear) una scene de sandbox (`Assets/Rollgeon/Scenes/Sandbox_Combat.unity`).
4. Scene: GameObject con `BootstrapRunner` apuntando al `ServiceBootstrapSO`
   global (que incluye `RulesetSO`, `EnergyService`, `TurnOrderServiceBootstrap`,
   `TurnManagerBootstrap`).
5. Crear GameObject vacio `CombatController`. Agregar componente
   `CombatController`. Asignar el `ServiceBootstrapSO` al campo
   `_bootstrap` (Odin `[Required]` lo marca en rojo si queda null).
6. Opcional: `SandboxCombatDriver.cs` (no commitear) con botones UI:
   - `Start Combat` -> poblar registry con `playerId` + `enemyId`, luego
     `controller.StartCombat(playerId, [playerId, enemyId], roomId, onEnemyTurn)`.
   - `End Turn` -> `controller.EndPlayerTurn()`.
   - `Enemy Done` -> `controller.SendEnemyDone()`.
   - `Victory / Defeat / Abort` -> `controller.NotifyCombatEnded(outcome)`.
7. Play. En consola deben aparecer, en orden:
   - `OnCombatStart(roomId)`
   - `OnTurnQueueBuilt(order, round=0)` (T100c)
   - `OnTurnStarted(playerId)`
   - (interacciones de UI)
   - `OnTurnFinished(playerId)` -> `OnTurnStarted(enemyId)` -> `OnTurnFinished(enemyId)` -> loop
   - `OnCombatEnd(roomId, outcome)` al `NotifyCombatEnded`.

## 5. Debug tips

- Suscribir un logger a `OnTurnStarted`/`OnTurnFinished` para ver payloads.
- `CombatTurnFSM.OnInputAccepted` es purely diagnostic — logear para ver
  que inputs se aceptan.
- Si el FSM queda stuck en `PlayerTurn`: la UI tiene que llamar
  `EndPlayerTurn()`. Recordar Revision 2: energia en 0 **no** lo saca del
  estado. Solo `PlayerEndTurn` explicito, `CombatEnded` (abort/victory), o
  `EnemyDone` (en `EnemyTurnState`).
- Si el FSM queda stuck en `EnemyTurn`: el `enemyActionHandler` debe
  eventualmente llamar `SendEnemyDone()`. Si el handler no lo hace
  (AI rota, test incompleto) el FSM queda colgado.

## 6. Pitfalls

- **Un solo controller por scene.** Dos controllers se suscriben ambos al
  bus `OnOverlayPushed/Popped` — shared state entre combates paralelos
  no es soportado.
- **`InitializeForEntity` del player debe haber corrido antes de `StartCombat`.**
  Si el player no tiene entry en el `IEnergyService`, `GetCurrent` devuelve 0
  y la UI via `TurnManager.CanExecute` no podra ejecutar ninguna accion.
- **El `enemyActionHandler` no puede ser null** si se esperan turnos de enemy —
  bueno, puede: la FSM queda en `EnemyTurnState` esperando un
  `SendEnemyDone()` externo. Para tests sin AI es valido; para runtime real
  siempre inyectar un handler.

## 7. Hooks futuros (NO en este worktree)

- `IPhaseService` real (§12.0.3) — los ticks de `Update/LateUpdate/FixedUpdate`
  ya se gate-an por `_phaseFrozen`; falta el publisher de `OnOverlayPushed/Popped`.
- AI del enemy (T99 / T103) — reemplaza el `enemyActionHandler` stub por
  logica real.
- `Roll` / `Reroll` / `ResolveCombo` sub-states (T97) — se interponen entre
  `PlayerTurnState.CheckInput(PlayerActionDone)` y el target state sin
  cambiar la shape externa del FSM.
- `CleanupPlayerModifiers` / `CleanupEnemyModifiers` — cuando `§3 lifecycle`
  aterrice con `TickEvent`.
- `CombatFSMInspector` debug window — suscribirse a
  `CombatTurnFSM.OnInputAccepted` + leer `Current` para visualizar el arbol live.
