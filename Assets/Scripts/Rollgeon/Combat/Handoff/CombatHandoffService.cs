using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combat.FSM;
using Rollgeon.Combat.FSM.States;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.PreConditions;
using Rollgeon.Player;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Listens for <see cref="EventName.OnCombatTriggered"/>, resolves enemy
    /// spawns, pushes the CombatHUD screen, and kicks off the combat FSM.
    /// </summary>
    /// <remarks>
    /// Spawn count heuristic: 1 enemy for <see cref="RoomType.Boss"/>,
    /// 2 for <see cref="RoomType.Combat"/>. The resolver handles the actual
    /// weighted roll from the room's <see cref="EnemyPoolSO"/>.
    /// </remarks>
    public sealed class CombatHandoffService : ICombatHandoffService
    {
        /// <summary>
        /// Path en <c>Resources/</c> al asset de fallback que se carga cuando
        /// <see cref="IPlayerService.DiceBag"/> es <c>null</c>. Asset autoreado por el
        /// usuario (Round3-Checklist) — 5 × D6 para el Guerrero hasta que la pantalla
        /// de build (Fase 2) construya bags reales.
        /// </summary>
        public const string FallbackBagResourcePath = "Dice/AD_Warrior_StartingBag";

        private readonly IDungeonService _dungeon;
        private readonly IPlayerService _player;
        private readonly IEnemySpawnResolver _resolver;
        private readonly IEnemyAIHandler _aiHandler;
        private readonly IScreenManager _screenManager;
        private readonly ICombatStarter _combatStarter;
        private readonly IPlayerCombatActions _playerActions;

        private EventManager.EventReceiver _onCombatTriggeredHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private bool _disposed;

        private int[] _lastFaces;
        private HeroActionBehavior _selectedBehavior;
        private bool _awaitingFirstRoll;

        // True mientras una accion sin tirada (Movement) espera que el jugador elija el
        // tile destino y todavia se puede cancelar+reembolsar (BUG-013). Lo setea el
        // playerState path de DoConfirm y lo limpia el callback de RequestAction al
        // completarse o cancelarse la accion.
        private bool _awaitingPlayerSelection;
        private EffChain _activeChain;
        private int _chainPhaseIndex;
        private TargetSelectionResult _chainPhaseSelectionResult;
        private ISelectionController _chainSelectionController;
        private Action _pendingChainCallback;

        public bool IsHandoffInProgress { get; private set; }

        public CombatHandoffService(
            IDungeonService dungeon,
            IPlayerService player,
            IEnemySpawnResolver resolver,
            IEnemyAIHandler aiHandler,
            IScreenManager screenManager,
            ICombatStarter combatStarter,
            IPlayerCombatActions playerActions)
        {
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _aiHandler = aiHandler ?? throw new ArgumentNullException(nameof(aiHandler));
            _screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));
            _combatStarter = combatStarter ?? throw new ArgumentNullException(nameof(combatStarter));
            _playerActions = playerActions ?? throw new ArgumentNullException(nameof(playerActions));

            _onCombatTriggeredHandler = OnCombatTriggered;
            EventManager.Subscribe(EventName.OnCombatTriggered, _onCombatTriggeredHandler);

            _onCombatEndHandler = OnCombatEnd;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
        }

        /// <summary>
        /// Factory: resolves dependencies from <see cref="ServiceLocator"/>, creates
        /// an instance, and registers it as <see cref="ICombatHandoffService"/> in
        /// <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static CombatHandoffService CreateAndRegister()
        {
            var dungeon = ServiceLocator.GetService<IDungeonService>();
            var player = ServiceLocator.GetService<IPlayerService>();
            var resolver = ServiceLocator.GetService<IEnemySpawnResolver>();
            var aiHandler = ServiceLocator.GetService<IEnemyAIHandler>();
            var screenManager = ServiceLocator.GetService<IScreenManager>();
            var combatStarter = ServiceLocator.GetService<ICombatStarter>();
            var playerActions = ServiceLocator.GetService<IPlayerCombatActions>();

            var service = new CombatHandoffService(
                dungeon, player, resolver, aiHandler, screenManager, combatStarter, playerActions);

            ServiceLocator.AddService<ICombatHandoffService>(service, ServiceScope.Run);
            return service;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onCombatTriggeredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatTriggered, _onCombatTriggeredHandler);
                _onCombatTriggeredHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
        }

        private void OnCombatTriggered(params object[] args)
        {
            // args: [Guid roomInstanceId, string roomId, RoomType roomType]
            if (args == null || args.Length < 3) return;

            var roomInstanceId = (Guid)args[0];
            var roomType = (RoomType)args[2];

            if (IsHandoffInProgress) return;
            IsHandoffInProgress = true;

            try
            {
                var instance = _dungeon.CurrentRoomInstance;
                var room = instance?.Template;
                if (room == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[CombatHandoffService] CurrentRoom is null — aborting handoff.");
                    return;
                }

                var rng = new System.Random(roomInstanceId.GetHashCode());
                var spawned = _resolver.Resolve(instance, rng);

                // Build participant list: player + enemies.
                var participants = new List<Guid> { _player.PlayerGuid };
                Guid firstEnemyId = Guid.Empty;

                foreach (var (id, _) in spawned)
                {
                    participants.Add(id);
                    if (firstEnemyId == Guid.Empty)
                        firstEnemyId = id;
                }

                // Push combat HUD screen.
                _screenManager.PushByStringId("CombatHUD",
                    new CombatHUDPayload
                    {
                        RoomInstanceId = roomInstanceId,
                        EncounterDisplayName = room.DisplayName
                    });

                // Cablea delegates del HUD contra IPlayerCombatActions + reroll budget
                // (setup doc UI#0095b §8.7). Sin esto, los clicks del HUD loggean
                // "no cableado" y combat se stucka.
                WireCombatHUDDelegates(firstEnemyId);

                // Start the combat FSM.
                _combatStarter.StartCombat(
                    _player.PlayerGuid,
                    participants,
                    roomInstanceId,
                    _aiHandler.HandleEnemyTurn);
            }
            finally
            {
                IsHandoffInProgress = false;
            }
        }

        // Limpia todo el estado de fase de combate. Lo invocan tanto el wiring del
        // proximo combate como el handler de OnCombatEnd — el chain puede haber
        // quedado abierto si el enemigo muere antes de que el player consuma todas
        // las fases (ej. ataque de 1 phase mata al enemy, sobran phases del chain;
        // sin este reset, _activeChain queda non-null y el RerollBudgetService
        // global preserva _current, asi que el primer StartBudget del proximo
        // combate tira InvalidOperationException).
        private void ResetCombatPhaseState()
        {
            if (_chainSelectionController != null)
            {
                if (_chainSelectionController.IsSelecting)
                    _chainSelectionController.CancelSelection();
                _chainSelectionController.OnSelectionCompleted -= OnChainSelectionDone;
                _chainSelectionController = null;
            }
            _pendingChainCallback = null;

            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                budget.EndBudget();

            _lastFaces = null;
            _selectedBehavior = null;
            _awaitingFirstRoll = false;
            _awaitingPlayerSelection = false;
            _activeChain = null;
            _chainPhaseIndex = 0;
            _chainPhaseSelectionResult = null;

            // Si quedó un ActionRoll abierto (ej. user nunca apretó Confirm pero el combat
            // termina por otra vía), cancelarlo para que el panel se cierre.
            if (ServiceLocator.TryGetService<ActionRolls.IActionRollService>(out var roll)
                && roll != null && roll.IsActive)
            {
                roll.Cancel();
            }
        }

        private void OnCombatEnd(params object[] args)
        {
            ResetCombatPhaseState();
        }

        private void WireCombatHUDDelegates(Guid firstEnemyId)
        {
            if (_screenManager.Current is not CombatHUDView hud) return;

            var playerGuid = _player.PlayerGuid;
            ResetCombatPhaseState();

            hud.OnEndTurnRequested = () =>
            {
                // BUG-013: si hay un Movement pendiente de selección, End Turn lo cancela
                // (con reembolso) en vez de quedar inerte. El jugador puede volver a apretar
                // End Turn para cerrar el turno.
                if (_awaitingPlayerSelection)
                {
                    CancelPlayerSelection();
                    return;
                }

                // BUG-015: simétrico, End Turn cancela un ActionRoll abierto antes de
                // cerrar el turno. Si el flow ya estaba en AwaitingRerollDecision la
                // energía base se cobró (cancel resuelve con la tirada actual, sin
                // bonus); si estaba en AwaitingConfirm, Cancel es "limpio" (no cobro).
                if (ServiceLocator.TryGetService<IActionRollService>(out var rsET)
                    && rsET != null && rsET.IsActive)
                {
                    rsET.Cancel();
                    return;
                }

                if (_activeChain != null)
                {
                    if (_chainSelectionController != null && _chainSelectionController.IsSelecting)
                        _chainSelectionController.CancelSelection();

                    if (ServiceLocator.TryGetService<IRerollBudgetService>(out var chainBudget) && chainBudget != null)
                        chainBudget.EndBudget();
                    var chainResolved = _lastFaces ?? Array.Empty<int>();
                    EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)chainResolved);
                    FinishChain(hud, playerGuid, true);
                    _playerActions.EndPlayerTurn();
                    return;
                }

                if (_selectedBehavior != null) return;

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                    budget.EndBudget();

                var resolved = _lastFaces ?? Array.Empty<int>();
                EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)resolved);

                _lastFaces = null;
                hud.ClearBehaviorForFormula();
                _playerActions.EndPlayerTurn();
            };

            void DoConfirm()
            {
                if (_selectedBehavior == null) return;

                if (_activeChain != null)
                {
                    var phase = _activeChain.Phases[_chainPhaseIndex];
                    var afterRoll = FindPhaseSelectionAt(phase, SelectionTiming.AfterRoll);

                    if (afterRoll != null)
                    {
                        _chainPhaseSelectionResult = null;
                        BeginChainSelection(afterRoll, playerGuid, () =>
                        {
                            ExecuteChainPhase(hud, firstEnemyId, playerGuid);
                        });
                    }
                    else
                    {
                        _chainPhaseSelectionResult = null;
                        ExecuteChainPhase(hud, firstEnemyId, playerGuid);
                    }
                    return;
                }

                var hero = ResolveHero();
                BaseComboSO combo = null;
                ComboDetectionResult? comboResult = null;

                if (hero != null && _lastFaces != null)
                {
                    var keptDice = FilterKeptDice(_lastFaces, hud.GetCurrentKeep());
                    combo = hero.Sheet?.MatchBest(keptDice);
                    if (combo != null)
                        comboResult = combo.Detect(keptDice);
                }

                bool hasBeforeRoll = _selectedBehavior.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll);
                Debug.Log($"[CombatHandoff] OnConfirm — behavior='{_selectedBehavior.ActionName}' hasBeforeRoll={hasBeforeRoll}");

                // BUG-013 (cobrar al ejecutar): el Movement (sin tirada + selección before-roll)
                // NO está prepago — la energía se cobra cuando la acción corre de verdad, es decir
                // al clickear la celda (TurnManager.TryExecute dentro de PlayerExecutingSubState).
                // Cancelar antes del click no cuesta nada. El resto sí está prepago: las acciones
                // con tirada ya cobraron en el primer roll, y las instantáneas sin selección
                // cobraron al ejecutarse (que es inmediato).
                bool chargeOnExecute = !_selectedBehavior.NeedsDiceRoll && hasBeforeRoll;

                var behaviorCtx = new HeroBehaviorContext
                {
                    DiceResult = _lastFaces,
                    MatchedComboResult = comboResult,
                    TargetGuid = firstEnemyId,
                    EnergyPrepaid = !chargeOnExecute,
                };

                // Capturamos info del behavior antes de nullarlo, para emitir el evento
                // OnBehaviorExecuted con payload consistente en todos los paths.
                var executedActionName = _selectedBehavior.ActionName;
                var executedBlockOnRepeat = _selectedBehavior.BlockOnRepeat;

                if (hasBeforeRoll
                    && ServiceLocator.TryGetService<ICombatStarter>(out var starter))
                {
                    var controller = starter as CombatControllerAdapter;
                    Debug.Log($"[CombatHandoff] ICombatStarter resolved={starter != null}, adapter={controller != null}, " +
                              $"controller={controller?.Controller != null}, fsm={controller?.Controller?.FSM != null}, " +
                              $"player={controller?.Controller?.FSM?.Player != null}");
                    var playerState = controller?.Controller?.FSM?.Player;
                    if (playerState != null)
                    {
                        if (ServiceLocator.TryGetService<IRerollBudgetService>(out var b) && b != null)
                            b.EndBudget();

                        var r = _lastFaces ?? Array.Empty<int>();
                        EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)r);

                        // BUG-013: estas acciones (ej. Movement) ejecutan de forma asíncrona —
                        // el jugador todavía tiene que clickear el tile destino. Si soltáramos
                        // el lock acá (OnBehaviorExecuted) y nulláramos _selectedBehavior, los
                        // demás botones volverían a estar disponibles y el guard de re-entrada
                        // desaparecería, dejando que el jugador dispare un ataque EN PARALELO al
                        // movimiento pendiente. En vez de eso lockeamos la UI ahora y diferimos
                        // el OnBehaviorExecuted + el clear al callback que el sub-FSM invoca
                        // recién cuando la acción terminó de ejecutarse.
                        //
                        // Sólo las acciones sin tirada (Movement) son cancelables: la energía ya
                        // se cobró pero el movimiento aún no ocurrió, así que re-clickear el slot
                        // (o End Turn) lo cancela y reembolsa vía CancelPlayerSelection.
                        _awaitingPlayerSelection = !_selectedBehavior.NeedsDiceRoll;
                        EventManager.Trigger(EventName.OnActionSelectionStarted, playerGuid);

                        Debug.Log("[CombatHandoff] → RequestAction on PlayerTurnState");
                        playerState.RequestAction(_selectedBehavior, behaviorCtx, () =>
                        {
                            _awaitingPlayerSelection = false;
                            EventManager.Trigger(EventName.OnBehaviorExecuted, playerGuid, executedActionName, executedBlockOnRepeat);
                            _lastFaces = null;
                            _selectedBehavior = null;
                            hud.ClearBehaviorForFormula();
                        });
                        return;
                    }
                    Debug.LogWarning("[CombatHandoff] playerState is null — falling through to TurnManager path");
                }

                if (ServiceLocator.TryGetService<TurnManager>(out var tm) && tm != null)
                    tm.TryExecuteEnergyPrepaid(_selectedBehavior, playerGuid, behaviorCtx);

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                    budget.EndBudget();

                var resolved = _lastFaces ?? Array.Empty<int>();
                EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)resolved);
                EventManager.Trigger(EventName.OnBehaviorExecuted, playerGuid, executedActionName, executedBlockOnRepeat);

                _lastFaces = null;
                _selectedBehavior = null;
                hud.ClearBehaviorForFormula();
            }

            hud.OnBehaviorSelected = (int index) =>
            {
                // BUG-013 (cancelar + reembolsar): si hay un Movement esperando su tile,
                // cualquier click de slot lo cancela y devuelve la energía. Durante la
                // selección sólo el slot de Movement queda interactuable (los demás están
                // lockeados por _awaitingSelection), así que este click es "deseleccionar".
                if (_awaitingPlayerSelection)
                {
                    CancelPlayerSelection();
                    return;
                }

                // BUG-015: si hay un ActionRoll activo (Heal/Forzar Puerta con panel
                // abierto), cualquier click de slot lo cancela. La energía la maneja
                // ActionRollService.Cancel() — si todavía no cobró (estaba en
                // AwaitingConfirm) no hay charge; si ya cobró (post-roll inicial) el
                // outcome retorna Cancelled y resolvemos sin reembolso (el user pagó
                // por la tirada que vio).
                if (ServiceLocator.TryGetService<IActionRollService>(out var rsActive)
                    && rsActive != null && rsActive.IsActive)
                {
                    rsActive.Cancel();
                    return;
                }

                // Cancel-by-reselection: si hay una accion seleccionada pero el primer
                // roll todavia no se ejecuto, dejamos que el user cambie de opinion sin
                // perder energia (la energia aun no se cobro). Si ya rolaron, la accion
                // queda comprometida hasta Confirm/EndTurn.
                if (_selectedBehavior != null)
                {
                    if (!_awaitingFirstRoll) return;
                    CancelAwaitingSelection(hud);
                }

                var hero = ResolveHero();
                if (hero == null) return;

                var phaseBehaviors = hero.GetBehaviorsForPhase(GamePhase.Combat);
                HeroActionBehavior behavior = index < phaseBehaviors.Count
                    ? phaseBehaviors[index]
                    : null;

                if (behavior == null)
                {
                    Debug.LogWarning($"[CombatHandoffService] Behavior index {index} not found.");
                    return;
                }

                if (ServiceLocator.TryGetService<TurnManager>(out var tm) && tm != null)
                {
                    if (!tm.CanExecute(behavior, playerGuid, out var reason))
                    {
                        Debug.Log($"[CombatHandoffService] Cannot execute '{behavior.ActionName}': {reason}");
                        return;
                    }
                }

                if (!behavior.HasUsableEffectGroup(playerGuid, firstEnemyId, out var effectReason))
                {
                    Debug.Log($"[CombatHandoffService] No usable effect group for '{behavior.ActionName}': {effectReason}");
                    return;
                }

                // Acciones secundarias con IActionRollEffect (Forzar Puerta, Curarse) usan el
                // flow especifico: ActionRollPanelView con confirm/holds/reroll por energia.
                // El cost lo cobra IActionRollService via el spec — saltamos el flow normal de
                // Generala de combate y ejecutamos el behavior con TryExecuteEnergyPrepaid tras
                // el outcome (igual semantica que ExplorationBehaviorService).
                if (TryFindActionRollEffect(behavior, out var rollEffect)
                    && rollEffect.TryGetRollSpec(playerGuid, out var rollSpec)
                    && ServiceLocator.TryGetService<IActionRollService>(out var actionRollService)
                    && actionRollService != null)
                {
                    var rollBag = ResolvePlayerBag();
                    if (rollBag == null)
                    {
                        Debug.LogError("[CombatHandoffService] No se pudo resolver bag para ActionRoll — abortado.");
                        return;
                    }

                    _selectedBehavior = behavior;
                    hud.SetBehaviorForFormula(behavior);

                    // BUG-015: simétrico al path Movement, lockear los demás slots
                    // mientras el ActionRoll está abierto. Sin esto, el usuario podía
                    // disparar otra acción (ej. Heal pisar Movement, o viceversa) y
                    // las dos correrían en paralelo. La UI lo limpia al recibir
                    // OnBehaviorExecuted (que emitimos abajo en el outcome handler).
                    EventManager.Trigger(EventName.OnActionSelectionStarted, playerGuid);
                    var executedActionName = behavior.ActionName;
                    var executedBlockOnRepeat = behavior.BlockOnRepeat;

                    actionRollService.StartFlow(rollSpec, playerGuid, rollBag, outcome =>
                    {
                        var resolvedBehavior = _selectedBehavior;
                        _selectedBehavior = null;
                        hud.ClearBehaviorForFormula();

                        if (outcome.Cancelled || resolvedBehavior == null)
                        {
                            // BUG-015: incluso en Cancelled, soltar el lock de la UI —
                            // si no la screen queda gateada con los slots Locked.
                            EventManager.Trigger(EventName.OnBehaviorExecuted, playerGuid,
                                executedActionName, executedBlockOnRepeat);
                            return;
                        }

                        Combos.ComboDetectionResult? combo = outcome.HasCombo
                            ? Combos.ComboDetectionResult.Match(outcome.EffectiveTotal,
                                outcome.FinalRoll != null ? outcome.FinalRoll.Length : 0)
                            : (Combos.ComboDetectionResult?)null;

                        var behaviorCtx = new HeroBehaviorContext
                        {
                            SourceEntity = new Entity { Guid = playerGuid },
                            SelectionResult = null,
                            DiceResult = outcome.FinalRoll,
                            MatchedComboResult = combo,
                            ActionRollEffectiveTotal = outcome.EffectiveTotal,
                        };

                        // Energia ya cobrada por IActionRollService — TryExecuteEnergyPrepaid
                        // solo ejecuta + trackea repeticion.
                        if (ServiceLocator.TryGetService<TurnManager>(out var tmgr) && tmgr != null)
                        {
                            tmgr.TryExecuteEnergyPrepaid(resolvedBehavior, playerGuid, behaviorCtx);
                            // BUG-018: en combate, TODA acción que entra al ActionRoll flow
                            // (Heal, Forzar Puerta) consumió energía y debe ser once-per-turn,
                            // tenga éxito o falle el threshold. TryExecuteEnergyPrepaid solo
                            // marca usado si el behavior.BlockOnRepeat=true en el asset; algunos
                            // assets legacy lo tienen en 0 y permitían retry tras fallo. Forzamos
                            // la marca acá para que el gate de WasUsedThisTurn aplique siempre.
                            tmgr.MarkBehaviorUsed(executedActionName);
                        }

                        EventManager.Trigger(EventName.OnBehaviorExecuted, playerGuid,
                            executedActionName, executedBlockOnRepeat);
                    });
                    return;
                }

                _selectedBehavior = behavior;
                hud.SetBehaviorForFormula(behavior);

                var chain = behavior.FindChainEffect();
                if (chain != null && chain.PhaseCount > 0)
                {
                    if (behavior.NeedsDiceRoll)
                    {
                        _activeChain = chain;
                        _chainPhaseIndex = 0;
                        // OnChainStarted se emite recien cuando arranca el primer roll
                        // del chain (no aca). Si lo emitieramos al seleccionar, los
                        // demas botones quedarian lockeados antes de que el jugador
                        // pueda usar cancel-by-reselection.
                    }
                    else
                    {
                        Debug.LogWarning("[CombatHandoffService] EffChain requires NeedsDiceRoll=true — falling back to linear execution.");
                    }
                }

                if (_activeChain != null)
                {
                    var phase0BeforeRoll = FindPhaseSelectionAt(_activeChain.Phases[0], SelectionTiming.BeforeRoll);
                    if (phase0BeforeRoll != null)
                    {
                        _chainPhaseSelectionResult = null;
                        BeginChainSelection(phase0BeforeRoll, playerGuid, () =>
                        {
                            // OnChainSelectionDone invoca este callback tanto al completar como al
                            // cancelar la selección. Si se canceló (ej. EndTurn durante el target
                            // select), no cobramos ni rolamos — la limpieza la hace el caller
                            // (EndTurn → FinishChain). Sin este guard cobraríamos al cancelar.
                            if (_chainPhaseSelectionResult != null && _chainPhaseSelectionResult.WasCancelled)
                                return;

                            // BUG-013 (cobrar al ejecutar): selección before-roll → la energía se
                            // cobra al COMPLETAR la selección (el click del target), antes del
                            // primer roll. Cancelar la selección antes de clickear no cuesta nada.
                            if (!SpendEnergyNow(behavior, playerGuid))
                            {
                                FinishChain(hud, playerGuid, false);
                                return;
                            }
                            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var b) && b != null)
                            {
                                var w = BuildBudgetAction(behavior);
                                if (w != null)
                                {
                                    b.StartBudget(w);
                                    // El chain rola automaticamente tras la seleccion;
                                    // consumimos el primer roll del budget para preservar
                                    // la cuenta "rerolls disponibles" igual que antes.
                                    b.TryExtraRoll(playerGuid);
                                }
                            }
                            var selBag = ResolvePlayerBag();
                            var selRoller = ResolveRoller();
                            if (selBag == null || selRoller == null)
                            {
                                FinishChain(hud, playerGuid, false);
                                return;
                            }
                            // Path chain con BeforeRoll: emitimos OnChainStarted recien
                            // ahora (no al seleccionar) para que la UI mantenga los demas
                            // slots Available durante la fase de target selection.
                            EventManager.Trigger(EventName.OnChainStarted, playerGuid);
                            _lastFaces = selRoller.RollAll(selBag);
                            EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
                        });
                        return;
                    }
                }

                if (!behavior.NeedsDiceRoll)
                {
                    // BUG-013 (cobrar al ejecutar): si la acción necesita elegir un tile/target
                    // ANTES (Movement), NO cobramos acá — la energía se cobra al clickear la
                    // celda (DoConfirm marca EnergyPrepaid=false y PlayerExecutingSubState cobra
                    // vía TurnManager.TryExecute). Cancelar antes del click no cuesta nada. Las
                    // instantáneas sin selección se ejecutan ya mismo, así que cobrar acá ES
                    // "al ejecutar".
                    bool hasBeforeSelection = behavior.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll);
                    if (!hasBeforeSelection && !SpendEnergyNow(behavior, playerGuid))
                    {
                        _selectedBehavior = null;
                        hud.ClearBehaviorForFormula();
                        return;
                    }
                    DoConfirm();
                    return;
                }

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                {
                    var wrapper = BuildBudgetAction(behavior);
                    if (wrapper != null)
                        budget.StartBudget(wrapper);
                }

                // Flow manual: no se rola ni se cobra energia aca. El usuario debe
                // apretar el boton Roll del HUD (-> hud.OnRollRequested). La energia
                // se cobra en ese momento, dentro del handler de Roll. Si el user
                // re-selecciona otra accion antes, CancelAwaitingSelection limpia
                // budget y selected behavior — la energia no se perdio.
                _awaitingFirstRoll = true;
            };

            hud.OnRollRequested = () =>
            {
                if (!_awaitingFirstRoll || _selectedBehavior == null) return;

                var bag = ResolvePlayerBag();
                var roller = ResolveRoller();
                if (bag == null || roller == null)
                {
                    Debug.LogError("[CombatHandoffService] No se pudo resolver bag/roller — Roll abortado.");
                    return;
                }

                if (!SpendEnergyNow(_selectedBehavior, playerGuid))
                {
                    CancelAwaitingSelection(hud);
                    return;
                }

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var rb) && rb != null)
                    rb.TryExtraRoll(playerGuid);

                _awaitingFirstRoll = false;
                // Path chain sin BeforeRoll: el chain quedo activo en OnBehaviorSelected
                // pero recien ahora arranca su primer roll. Emitimos OnChainStarted aqui
                // para preservar cancel-by-reselection (mientras _awaitingFirstRoll era
                // true, los demas slots quedaron Available).
                if (_activeChain != null)
                    EventManager.Trigger(EventName.OnChainStarted, playerGuid);
                _lastFaces = roller.RollAll(bag);
                EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
            };

            hud.OnConfirmRequested = () => DoConfirm();

            hud.OnChainPassRequested = () =>
            {
                if (_activeChain == null) return;

                if (_chainSelectionController != null && _chainSelectionController.IsSelecting)
                    _chainSelectionController.CancelSelection();

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var chainBudget) && chainBudget != null)
                    chainBudget.EndBudget();

                var chainResolved = _lastFaces ?? Array.Empty<int>();
                EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)chainResolved);

                Debug.Log($"[CombatHandoff] Chain pass at phase {_chainPhaseIndex}");
                FinishChain(hud, playerGuid, true);
            };

            hud.OnEnergyRerollRequested = () =>
            {
                if (_selectedBehavior != null && !_selectedBehavior.NeedsDiceRoll) return;

                // BUG-014: si todos los dados están holdeados, el reroll no movería
                // ningún dado — bail antes de consumir budget/energía. El botón
                // debería estar deshabilitado por la UI, esto es el guard defensivo.
                var keep = hud.GetCurrentKeep();
                if (AllDiceHeld(keep))
                {
                    Debug.LogWarning("[CombatHandoffService] Reroll bloqueado — todos los dados están holdeados.");
                    return;
                }

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                    budget.TryExtraRoll(playerGuid);

                var bag = ResolvePlayerBag();
                var roller = ResolveRoller();
                if (bag == null || roller == null)
                {
                    Debug.LogError("[CombatHandoffService] No se pudo resolver bag/roller — Reroll abortado.");
                    return;
                }

                _lastFaces = roller.Reroll(bag, _lastFaces, keep);
                EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
            };
        }

        // ======================================================================
        // Chain execution
        // ======================================================================

        private void ExecuteChainPhase(CombatHUDView hud, Guid firstEnemyId, Guid playerGuid)
        {
            if (_activeChain == null || _chainPhaseIndex >= _activeChain.PhaseCount) return;

            var phase = _activeChain.Phases[_chainPhaseIndex];
            var hero = ResolveHero();

            var effCtx = new EffectContext
            {
                SourceGuid = playerGuid,
                SourceEntity = new Entity { Guid = playerGuid },
                TargetGuid = firstEnemyId,
                DiceResult = _lastFaces,
                lastResult = true,
                SourceBehavior = _selectedBehavior,
                SelectionResult = _chainPhaseSelectionResult,
            };

            if (hero != null && _lastFaces != null)
            {
                var keptDice = FilterKeptDice(_lastFaces, hud.GetCurrentKeep());
                var combo = hero.Sheet?.MatchBest(keptDice);
                if (combo != null)
                    effCtx.ComboResult = combo.Detect(keptDice);
            }

            var preCtx = new PreConditionContext
            {
                OwnerGuid = playerGuid,
                OpponentGuid = firstEnemyId,
            };

            int remainingFreeRolls = 0;
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget?.Current != null)
                remainingFreeRolls = budget.Current.FreeRollsRemaining;

            if (phase?.Effects != null)
                phase.Effects.TryExecute(effCtx, preCtx);

            budget?.EndBudget();

            var resolved = _lastFaces ?? Array.Empty<int>();
            EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)resolved);
            _lastFaces = null;

            _chainPhaseIndex++;

            if (_chainPhaseIndex >= _activeChain.PhaseCount)
            {
                FinishChain(hud, playerGuid, false);
                return;
            }

            int currentEnergy = 0;
            if (ServiceLocator.TryGetService<IEnergyService>(out var energy) && energy != null)
                currentEnergy = energy.GetCurrent(playerGuid);

            // [CHAIN-DIAG] bug "fase de escudo no sucede".
            Debug.Log($"[CHAIN-DIAG] ExecuteChainPhase done — index now={_chainPhaseIndex}/{_activeChain.PhaseCount} " +
                      $"remainingFreeRolls={remainingFreeRolls} energy={currentEnergy}");

            // BUG-019: la phase siguiente (típicamente defensa post-attack) requiere
            // rolls libres sobrantes del pool de la phase anterior. Si el jugador
            // gastó los 3 free rolls atacando, la tirada de defensa no debe ocurrir
            // — aunque tenga energía suficiente para reroll. La energía sola no
            // habilita la phase: necesita haber pool libre del attack.
            if (remainingFreeRolls == 0)
            {
                Debug.Log($"[CHAIN-DIAG] Chain auto-terminated at phase {_chainPhaseIndex}: " +
                          $"freeRolls={remainingFreeRolls} → no quedan rolls libres del attack, " +
                          $"no se dispara la phase siguiente (defensa)");
                FinishChain(hud, playerGuid, false);
                return;
            }

            PrepareNextChainPhase(hud, playerGuid, remainingFreeRolls + 1);
        }

        private void StartNextChainPhase(CombatHUDView hud, Guid playerGuid, int freeRollCount)
        {
            // [CHAIN-DIAG]
            Debug.Log($"[CHAIN-DIAG] StartNextChainPhase phase={_chainPhaseIndex} freeRollCount={freeRollCount} " +
                      $"behavior={(_selectedBehavior != null ? _selectedBehavior.ActionName : "NULL")}");

            var wrapper = UnityEngine.ScriptableObject.CreateInstance<ActionDefinitionSO>();
            wrapper.ActionId = $"{_selectedBehavior.ActionName}.chain.phase{_chainPhaseIndex}";
            wrapper.EnergyCost = 0;
            wrapper.FreeRollCount = freeRollCount;
            wrapper.AllowsEnergyReroll = _selectedBehavior.AllowsEnergyReroll;

            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
            {
                budget.StartBudget(wrapper);
                // El budget cuenta TODOS los rolls incl. el primero. En el flow de
                // chain el primer roll dispara automaticamente (no hay boton Roll
                // entre fases), asi que consumimos una unidad para preservar la
                // semantica "FreeRollsRemaining = rerolls disponibles tras el roll".
                budget.TryExtraRoll(playerGuid);
            }

            var bag = ResolvePlayerBag();
            var roller = ResolveRoller();
            if (bag == null || roller == null)
            {
                Debug.LogError("[CombatHandoffService] Cannot resolve bag/roller for chain phase — finishing chain.");
                FinishChain(hud, playerGuid, false);
                return;
            }

            _lastFaces = roller.RollAll(bag);
            EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
            EventManager.Trigger(EventName.OnChainPhaseStarted, playerGuid, _chainPhaseIndex, _activeChain.PhaseCount);
        }

        private void FinishChain(CombatHUDView hud, Guid playerGuid, bool wasPass)
        {
            int phasesCompleted = _chainPhaseIndex;
            int totalPhases = _activeChain?.PhaseCount ?? 0;

            // Capturamos antes de nullar para poder emitir OnBehaviorExecuted con payload
            // valido. Si fue un pass total (wasPass && phasesCompleted==0) la accion no se
            // considera ejecutada — la UI debe poder rehabilitar el slot.
            string executedActionName = _selectedBehavior?.ActionName;
            bool executedBlockOnRepeat = _selectedBehavior?.BlockOnRepeat ?? false;

            _activeChain = null;
            _chainPhaseIndex = 0;
            _lastFaces = null;
            _selectedBehavior = null;

            _chainPhaseSelectionResult = null;
            if (_chainSelectionController != null)
            {
                _chainSelectionController.OnSelectionCompleted -= OnChainSelectionDone;
                _chainSelectionController = null;
            }
            _pendingChainCallback = null;

            hud.ClearBehaviorForFormula();

            EventManager.Trigger(EventName.OnChainCompleted, playerGuid, phasesCompleted, totalPhases, wasPass);

            // [DIAG temporal] bug "botón sigue activo tras usar".
            Debug.Log($"[CombatHandoff-DIAG] FinishChain — action='{executedActionName ?? "null"}' " +
                      $"blockOnRepeat={executedBlockOnRepeat} phasesCompleted={phasesCompleted} " +
                      $"→ marcaUsado={(!string.IsNullOrEmpty(executedActionName) && phasesCompleted > 0 && executedBlockOnRepeat)}");

            if (!string.IsNullOrEmpty(executedActionName) && phasesCompleted > 0)
            {
                // El chain path ejecuta effects via phase.Effects.TryExecute (línea ~666)
                // sin pasar por TurnManager.TryExecuteEnergyPrepaid → BlockOnRepeat nunca
                // se trackeaba para attacks. Lo marcamos acá para que el slot bloquee.
                if (executedBlockOnRepeat
                    && ServiceLocator.TryGetService<TurnManager>(out var tm) && tm != null)
                {
                    tm.MarkBehaviorUsed(executedActionName);
                }

                EventManager.Trigger(EventName.OnBehaviorExecuted, playerGuid, executedActionName, executedBlockOnRepeat);
            }
        }

        // ======================================================================
        // Chain — per-phase selection
        // ======================================================================

        private static SelectionSettings FindPhaseSelectionAt(ChainPhase phase, SelectionTiming timing)
        {
            if (phase?.Effects?.Effects == null) return null;
            foreach (var eff in phase.Effects.Effects)
            {
                if (eff != null && eff.RequiresSelectionAt(timing))
                    return eff.GetSelection();
            }
            return null;
        }

        private void BeginChainSelection(SelectionSettings settings, Guid playerGuid, Action onComplete)
        {
            // [CHAIN-DIAG]
            Debug.Log($"[CHAIN-DIAG] BeginChainSelection slotState={settings.SlotState} " +
                      $"autoResolve={settings.AutoResolve} entityFilter={settings.EntityFilter}");

            if (settings.SlotState == SlotState.Self)
            {
                if (ServiceLocator.TryGetService<IGridManager>(out var g) && g.TryGetPosition(playerGuid, out var pos))
                {
                    _chainPhaseSelectionResult = new TargetSelectionResult
                    {
                        WasCompleted = true,
                        SelectedTargets = new List<TargetRef> { TargetRef.At(pos) },
                    };
                }
                onComplete();
                return;
            }

            GridCoord ownerPos = default;
            if (ServiceLocator.TryGetService<IGridManager>(out var grid))
                grid.TryGetPosition(playerGuid, out ownerPos);

            if (settings.AutoResolve)
            {
                _chainPhaseSelectionResult = settings.AutoResolveTargets(ownerPos, playerGuid);
                onComplete();
                return;
            }

            var validTargets = settings.ResolveValidTiles(ownerPos, playerGuid);

            // Si no hay targets válidos (ej. el único enemigo murió en la fase de daño del
            // chain), NO abrimos una selección que el jugador no puede completar — eso
            // colgaba el chain: la fase siguiente (escudo) nunca arrancaba, nunca se llamaba
            // FinishChain, _selectedSlot quedaba pegado y el botón seguía habilitado.
            // Proseguimos sin target: la parte self-cast (escudo) igual aplica y el chain cierra.
            if (validTargets == null || validTargets.Count == 0)
            {
                Debug.LogWarning("[CombatHandoff] Chain phase sin targets válidos — proceeding sin selección " +
                                 "para no colgar el chain (enemigo muerto / fuera de rango).");
                _chainPhaseSelectionResult = null;
                onComplete();
                return;
            }

            if (!ServiceLocator.TryGetService<ISelectionController>(out _chainSelectionController))
            {
                Debug.LogWarning("[CombatHandoff] ISelectionController not registered — skipping chain selection");
                onComplete();
                return;
            }

            _pendingChainCallback = onComplete;
            _chainSelectionController.OnSelectionCompleted += OnChainSelectionDone;
            _chainSelectionController.BeginSelection(new SelectionRequest
            {
                Settings = settings,
                ValidTargets = validTargets,
                OwnerGuid = playerGuid,
                HighlightStyle = "move",
            });
        }

        private void OnChainSelectionDone(TargetSelectionResult result)
        {
            if (_chainSelectionController != null)
            {
                _chainSelectionController.OnSelectionCompleted -= OnChainSelectionDone;
                _chainSelectionController = null;
            }
            _chainPhaseSelectionResult = result;
            var cb = _pendingChainCallback;
            _pendingChainCallback = null;
            cb?.Invoke();
        }

        private void PrepareNextChainPhase(CombatHUDView hud, Guid playerGuid, int freeRollCount)
        {
            var nextPhase = _activeChain.Phases[_chainPhaseIndex];
            var beforeRoll = FindPhaseSelectionAt(nextPhase, SelectionTiming.BeforeRoll);

            // [CHAIN-DIAG]
            Debug.Log($"[CHAIN-DIAG] PrepareNextChainPhase phase={_chainPhaseIndex} " +
                      $"beforeRollSel={(beforeRoll != null ? beforeRoll.SlotState.ToString() : "null")} freeRollCount={freeRollCount}");

            if (beforeRoll != null)
            {
                _chainPhaseSelectionResult = null;
                BeginChainSelection(beforeRoll, playerGuid, () =>
                {
                    StartNextChainPhase(hud, playerGuid, freeRollCount);
                });
            }
            else
            {
                _chainPhaseSelectionResult = null;
                StartNextChainPhase(hud, playerGuid, freeRollCount);
            }
        }

        // Bag del jugador con fallback al asset Resources (Fase 1 — hasta que el
        // BuildSelectionScreen de Fase 2 popule el bag desde un pool por clase).
        private DiceBagSO ResolvePlayerBag()
        {
            var bag = _player?.DiceBag;
            if (bag != null) return bag;

            var fallback = Resources.Load<DiceBagSO>(FallbackBagResourcePath);
            if (fallback == null)
            {
                Debug.LogError($"[CombatHandoffService] IPlayerService.DiceBag null y fallback " +
                               $"'{FallbackBagResourcePath}' no encontrado en Resources/. " +
                               $"Crear el asset (Create → Rollgeon → Dice Bag) o popular DiceBag " +
                               $"vía Fase 2.");
                return null;
            }
            Debug.LogWarning($"[CombatHandoffService] Usando bag fallback '{fallback.name}' — " +
                             $"Fase 2 debería popular IPlayerService.DiceBag desde el build screen.");
            return fallback;
        }

        private ClassHeroSO ResolveHero()
        {
            var hero = _player?.CurrentHero;
            if (hero == null)
            {
                Debug.LogWarning("[CombatHandoffService] IPlayerService.CurrentHero is null — " +
                                 "cannot resolve hero behaviors.");
            }
            return hero;
        }

        private ActionDefinitionSO BuildBudgetAction(HeroActionBehavior behavior)
        {
            var wrapper = UnityEngine.ScriptableObject.CreateInstance<ActionDefinitionSO>();
            wrapper.ActionId = behavior.ActionName;
            wrapper.EnergyCost = behavior.EnergyCost;

            if (behavior.AllowsReroll)
            {
                wrapper.FreeRollCount = behavior.FreeRollCount;
                wrapper.AllowsEnergyReroll = behavior.AllowsEnergyReroll;
            }
            else
            {
                wrapper.FreeRollCount = 1;
                wrapper.AllowsEnergyReroll = false;
            }
            return wrapper;
        }

        private IDiceRoller ResolveRoller()
        {
            if (ServiceLocator.TryGetService<IDiceRoller>(out var roller) && roller != null)
                return roller;
            Debug.LogError("[CombatHandoffService] IDiceRoller no registrado. " +
                           "Agregar DiceRollerBootstrap a ServiceBootstrapSO.ExtraServices.");
            return null;
        }

        /// <summary>
        /// BUG-014: True si <paramref name="keep"/> existe y todos sus elementos
        /// son <c>true</c>. Usado por el guard de reroll para no consumir budget/
        /// energía cuando no hay ningún dado para re-tirar.
        /// </summary>
        public static bool AllDiceHeld(bool[] keep)
        {
            if (keep == null || keep.Length == 0) return false;
            for (int i = 0; i < keep.Length; i++) if (!keep[i]) return false;
            return true;
        }

        public static int[] FilterKeptDice(int[] faces, bool[] keep)
        {
            if (faces == null) return Array.Empty<int>();
            if (keep == null || keep.Length == 0) return faces;

            int count = 0;
            int len = Math.Min(faces.Length, keep.Length);
            for (int i = 0; i < len; i++)
                if (keep[i]) count++;

            if (count == 0) return Array.Empty<int>();
            if (count == faces.Length) return faces;

            var result = new int[count];
            int idx = 0;
            for (int i = 0; i < len; i++)
                if (keep[i]) result[idx++] = faces[i];
            return result;
        }

        /// <summary>
        /// Limpia el state interno cuando el usuario cancela la accion seleccionada
        /// antes del primer roll (eligiendo otra accion). No hay refund de energia
        /// porque <c>SpendEnergyNow</c> se difirio al boton Roll.
        /// </summary>
        private void CancelAwaitingSelection(CombatHUDView hud)
        {
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                budget.EndBudget();

            _selectedBehavior = null;
            _awaitingFirstRoll = false;
            hud.ClearBehaviorForFormula();
        }

        /// <summary>
        /// Cancela un Movement (acción sin tirada) que está esperando que el jugador
        /// clickee el tile destino (BUG-013). Con cobro-al-ejecutar la energía recién se
        /// cobra al clickear la celda, así que cancelar antes NO cuesta nada — no hay nada
        /// que reembolsar. Cancelar la selección dispara el unwind del sub-FSM
        /// (<c>PlayerSelectingSubState → PlayerExecutingSubState</c> con <c>WasCancelled</c>),
        /// que skipea la ejecución (no cobra) e invoca el callback de <c>RequestAction</c>:
        /// ese callback limpia <see cref="_selectedBehavior"/> /
        /// <see cref="_awaitingPlayerSelection"/> y emite <c>OnBehaviorExecuted</c> para
        /// liberar la UI.
        /// </summary>
        private void CancelPlayerSelection()
        {
            if (!_awaitingPlayerSelection) return;

            if (ServiceLocator.TryGetService<ISelectionController>(out var sel)
                && sel != null && sel.IsSelecting)
            {
                sel.CancelSelection();
                return;
            }

            // Defensa: si no había una selección activa que cancelar, liberamos el estado
            // a mano para no dejar la UI lockeada.
            var name = _selectedBehavior?.ActionName ?? "Movement";
            var block = _selectedBehavior?.BlockOnRepeat ?? false;
            _awaitingPlayerSelection = false;
            _selectedBehavior = null;
            _lastFaces = null;
            EventManager.Trigger(EventName.OnBehaviorExecuted, _player.PlayerGuid, name, block);
        }

        private static bool SpendEnergyNow(HeroActionBehavior behavior, Guid playerGuid)
        {
            if (behavior.EnergyCost <= 0) return true;
            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
                return true;
            if (!energy.SpendEnergy(playerGuid, behavior.EnergyCost))
            {
                Debug.Log($"[CombatHandoffService] SpendEnergy failed for '{behavior.ActionName}' at selection time.");
                return false;
            }
            return true;
        }

        // Recorre los effects del behavior buscando el primer IActionRollEffect.
        // Mismo patron que ExplorationBehaviorService.TryFindActionRollEffect.
        private static bool TryFindActionRollEffect(HeroActionBehavior behavior,
            out IActionRollEffect rollEffect)
        {
            rollEffect = null;
            if (behavior?.Effects == null) return false;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is IActionRollEffect candidate)
                    {
                        rollEffect = candidate;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
