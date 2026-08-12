using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combat.FSM;
using Rollgeon.Combat.FSM.States;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Dice.Throw;
using Rollgeon.Dungeon;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Feedback;
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

        // True mientras una fase de chain espera su primer roll PAGO (sin rolls
        // sobrantes del pool anterior pero con energía): el prompt del board ofrece
        // "X Roll (1E)" y el botón Roll cobra 1E vía el paid path del budget.
        private bool _awaitingChainPaidRoll;

        // True mientras una accion sin tirada (Movement) espera que el jugador elija el
        // tile destino y todavia se puede cancelar+reembolsar (BUG-013). Lo setea el
        // playerState path de DoConfirm y lo limpia el callback de RequestAction al
        // completarse o cancelarse la accion.
        private bool _awaitingPlayerSelection;
        // BUG-018: lock de re-entrada del Confirm de chain. Los efectos de la fase corren
        // sincrónicos en ExecuteChainPhase, pero _chainPhaseIndex y _selectedBehavior se
        // actualizan recién en la continuación diferida por el feedback — sin este flag,
        // cada Confirm/Espacio durante la animación del golpe re-ejecutaba la misma fase.
        private bool _isResolvingChainPhase;
        private EffChain _activeChain;
        private int _chainPhaseIndex;
        private TargetSelectionResult _chainPhaseSelectionResult;
        private ISelectionController _chainSelectionController;
        private Action _pendingChainCallback;

        // CNF-002: guid del enemigo marcado con el crease de selección (objetivo del
        // ataque elegido antes de tirar). Guid.Empty = nadie marcado.
        private Guid _creaseTargetGuid;

        // BUG-030 (Torpe): true mientras hay un relanzamiento completo forzado
        // agendado y sin ejecutar. Congela Confirm/EnergyReroll/EndTurn durante la
        // ventana del delay para que el jugador no consuma la mano que está por volar.
        private bool _forcedRerollPending;

        // Seam de tests: los EditMode no tienen player loop — inyectan un scheduler
        // sincrónico o capturan el callback. Default (null): PrimeTween.Tween.Delay.
        internal Action<float, Action> ForcedRerollScheduler { get; set; }

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
                EnemyDataSO firstEnemyData = null;

                foreach (var (id, data) in spawned)
                {
                    participants.Add(id);
                    if (firstEnemyId == Guid.Empty)
                    {
                        firstEnemyId = id;
                        firstEnemyData = data;
                    }
                }

                // Push combat HUD screen.
                _screenManager.PushByStringId("CombatHUD",
                    new CombatHUDPayload
                    {
                        RoomInstanceId = roomInstanceId,
                        EncounterDisplayName = Rollgeon.Localization.LocalizedContent.Name(room.RoomId, room.DisplayName)
                    });

                // Encuentro de jefe: encender la barra de vida en pantalla (BossBarView).
                // Se bindea al primer enemigo (el jefe es el único spawn en RoomType.Boss)
                // y le pasa el nombre ya localizado. La barra lee el Health por su cuenta.
                if (roomType == RoomType.Boss && firstEnemyId != Guid.Empty && firstEnemyData != null)
                {
                    TypedEvent<BossEncounterStartedPayload>.Raise(new BossEncounterStartedPayload
                    {
                        BossGuid = firstEnemyId,
                        DisplayName = Rollgeon.Localization.LocalizedContent.Name(
                            firstEnemyData.EntityId, firstEnemyData.DisplayName),
                    });
                }

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
        // ------------------------------------------------------------------
        // CNF-008: gate de throw manual. Los rolls del jugador delegan en
        // IDiceThrowService — en modos manuales el reveal (OnDiceRolled) se
        // difiere hasta que el jugador arroje los dados y asienten. Sin el
        // servicio registrado (tests, escenas sin la UI) cae al path legacy
        // sincrónico, byte-idéntico al comportamiento anterior.
        // ------------------------------------------------------------------

        private void RollViaThrow(Guid playerGuid, DiceBagSO bag, IDiceRoller roller)
        {
            if (ServiceLocator.TryGetService<IDiceThrowService>(out var throwSvc) && throwSvc != null)
            {
                throwSvc.RequestRoll(playerGuid, bag, faces => _lastFaces = faces);
                return;
            }
            _lastFaces = roller.RollAll(bag);
            EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
        }

        private void RerollViaThrow(Guid playerGuid, DiceBagSO bag, IDiceRoller roller, bool[] keep)
        {
            if (ServiceLocator.TryGetService<IDiceThrowService>(out var throwSvc) && throwSvc != null)
            {
                throwSvc.RequestReroll(playerGuid, bag, _lastFaces, keep, faces => _lastFaces = faces);
                return;
            }
            _lastFaces = roller.Reroll(bag, _lastFaces, keep);
            EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
        }

        // Guard para entry points disparables por el jugador: mientras hay dados en
        // el aire no se acepta otro roll/reroll/reselección (evita dobles sesiones).
        private static bool ThrowBusy()
            => ServiceLocator.TryGetService<IDiceThrowService>(out var t) && t != null && t.IsBusy;

        // Un throw pendiente no puede sobrevivir a su contexto (fin de chain, fin de
        // combate, cancel) — se aborta SIN revelar (no reembolsa: decisión de diseño).
        private static void AbortThrow()
        {
            if (ServiceLocator.TryGetService<IDiceThrowService>(out var t) && t != null && t.IsBusy)
                t.Abort();
        }

        private void ResetCombatPhaseState()
        {
            AbortThrow();
            // El grab-reroll no sobrevive al combate — el handler apunta a un hud viejo.
            if (ServiceLocator.TryGetService<IDiceThrowService>(out var throwSvcReset) && throwSvcReset != null)
                throwSvcReset.GrabRerollHandler = null;
            ClearAttackTargetCrease();

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
            // BUG-030: un forced reroll agendado no sobrevive a su combate — el
            // callback del scheduler chequea el flag y hace no-op si llegó tarde.
            _forcedRerollPending = false;
            _activeChain = null;
            _chainPhaseIndex = 0;
            _chainPhaseSelectionResult = null;
            // El prompt del board se apaga en la MISMA operación que el flag: son el
            // mismo estado y no pueden divergir. Antes acá solo se reseteaba el flag,
            // asumiendo que el GO "se auto-esconde al desactivarse la zona" — falso:
            // desactivar un canvas ancestro no limpia el m_IsActive propio del hijo, así
            // que un combate que cerraba con la entrada paga pendiente (el enemigo muere
            // antes de que el player consuma todas las fases) se lo filtraba al siguiente
            // combate, encima del roll de ataque (PUL-016).
            _awaitingChainPaidRoll = false;
            _isResolvingChainPhase = false;
            if (_screenManager?.Current is CombatHUDView resetHud)
                resetHud.HideChainRollPrompt();

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
                // BUG-030: la ventana del forced reroll es corta (~0.35s) — cerrar el
                // turno en el medio dejaría el callback re-tirando sin turno activo.
                if (_forcedRerollPending) return;

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
                // BUG-030: con un relanzamiento forzado agendado, la mano actual está
                // por volar — confirmar ahora ejecutaría un combo que no va a existir.
                if (_forcedRerollPending) return;
                if (_selectedBehavior == null) return;

                if (_activeChain != null)
                {
                    // Entrada paga pendiente: sin tirada no hay nada que confirmar —
                    // Space no debe ejecutar la fase con DiceResult null.
                    if (_awaitingChainPaidRoll) return;

                    // BUG-018: la fase actual ya se despachó (selección abierta o efectos
                    // aplicados) y la continuación diferida todavía no avanzó el índice.
                    if (_isResolvingChainPhase) return;
                    _isResolvingChainPhase = true;

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
                        // CNF-002: NO limpiar _chainPhaseSelectionResult acá. Con la
                        // selección del ataque en BeforeRoll, el target elegido antes de
                        // tirar viaja en este campo hasta ExecuteChainPhase
                        // (effCtx.SelectionResult). Limpiarlo descartaba la elección del
                        // jugador y el daño caía al fallback firstEnemyId.
                        ExecuteChainPhase(hud, firstEnemyId, playerGuid);
                    }
                    return;
                }

                var hero = ResolveHero();
                BaseComboSO combo = null;
                ComboDetectionResult? comboResult = null;
                int[] keptDice = null;
                int[] keptDiceOriginalIndices = null;

                if (hero != null && _lastFaces != null)
                {
                    var keepMask = KeepExcludingBlockedDice(hud.GetCurrentKeep(), _lastFaces.Length);
                    keptDice = FilterKeptDice(_lastFaces, keepMask);
                    keptDiceOriginalIndices = FilterKeptIndices(keepMask, _lastFaces.Length);
                    var keptTypes = ResolveKeptTypes(keptDiceOriginalIndices, keptDice.Length);
                    combo = hero.Sheet?.MatchBest(keptDice, keptTypes);
                    if (combo != null)
                        comboResult = DetectWithContractMods(hero.Sheet, combo, keptDice, keptTypes);

                    // Boss 2 (§3): registrar el combo ejecutado en el ataque del jugador. Si no
                    // hubo combo (daño mínimo / dado más alto), Record(null) loguea el marcador
                    // "sin combo". Solo el ataque primario con tirada pasa por acá (el chain
                    // hace early-return arriba; Heal/Forzar Puerta van por ActionRollService).
                    if (ServiceLocator.TryGetService<Rollgeon.Combat.ComboLog.IComboLogService>(out var comboLog) && comboLog != null)
                        comboLog.Record(combo != null ? combo.ComboId : null);
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
                    KeptDice = keptDice,
                    KeptDiceOriginalIndices = keptDiceOriginalIndices,
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
                // CNF-008: con dados en el aire no se reselecciona — evita abrir una
                // segunda sesión de throw o cancelar el chain con el reveal pendiente.
                if (ThrowBusy()) return;

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
                        // SetBehaviorForFormula ya empujó el board del behavior; si la fase 0
                        // overridea, lo corregimos acá (vale para ambos entry points del primer
                        // roll: selección BeforeRoll y el botón Roll manual).
                        ApplyChainPhaseBoardSkin(hud, 0);
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
                            RollViaThrow(playerGuid, selBag, selRoller);
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
                // Entrada paga de fase de chain: el "primer roll" de la fase se cobra
                // recien aca, al click, via el paid path del budget (0 free ⇒ 1E). Si
                // el cobro falla (energia gastada en el medio) no hay nada que revertir
                // — el jugador sale con Pass o End Turn.
                if (_activeChain != null && _awaitingChainPaidRoll)
                {
                    if (ThrowBusy()) return;
                    var chainBag = ResolvePlayerBag();
                    var chainRoller = ResolveRoller();
                    if (chainBag == null || chainRoller == null) return;
                    if (!(ServiceLocator.TryGetService<IRerollBudgetService>(out var paidBudget)
                          && paidBudget != null && paidBudget.TryExtraRoll(playerGuid)))
                    {
                        return;
                    }
                    _awaitingChainPaidRoll = false;
                    hud.HideChainRollPrompt();
                    RollViaThrow(playerGuid, chainBag, chainRoller);
                    return;
                }

                if (!_awaitingFirstRoll || _selectedBehavior == null) return;
                if (ThrowBusy()) return;

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
                RollViaThrow(playerGuid, bag, roller);
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

            hud.OnEnergyRerollRequested = () => TryEnergyReroll(hud, playerGuid);

            // CNF-008 (grab-to-reroll): en modos manuales el reroll también se inicia
            // agarrando dados asentados y arrojándolos — el presenter arma el agarre
            // y pide el reroll acá con keep = los dados NO agarrados.
            if (ServiceLocator.TryGetService<IDiceThrowService>(out var grabThrowSvc) && grabThrowSvc != null)
                grabThrowSvc.GrabRerollHandler = keepOverride => TryEnergyReroll(hud, playerGuid, keepOverride);
        }

        // ======================================================================
        // BUG-030 (Torpe): relanzamiento completo forzado
        // ======================================================================

        /// <inheritdoc />
        public bool TryScheduleForcedFullHandReroll(Guid playerGuid, float delaySeconds = 0.35f)
        {
            if (_forcedRerollPending) return false;
            if (_selectedBehavior == null || !_selectedBehavior.NeedsDiceRoll) return false;
            if (_lastFaces == null || _lastFaces.Length == 0) return false;
            if (ThrowBusy()) return false;

            // Diferido fuera del dispatch de OnDiceRolled: un reroll sincrónico
            // anidaría el reveal nuevo dentro del evento viejo y los handlers
            // restantes (DiceZoneView) pintarían las caras viejas encima.
            _forcedRerollPending = true;
            var scheduler = ForcedRerollScheduler
                ?? ((delay, callback) => PrimeTween.Tween.Delay(delay, callback));
            scheduler(Mathf.Max(0f, delaySeconds), () => ExecuteForcedFullHandReroll(playerGuid));
            return true;
        }

        private void ExecuteForcedFullHandReroll(Guid playerGuid)
        {
            // ResetCombatPhaseState pudo bajar el pending (fin de combate, cancel):
            // el callback tardío no debe re-tirar sobre un contexto muerto.
            if (!_forcedRerollPending) return;
            _forcedRerollPending = false;
            if (_lastFaces == null || _selectedBehavior == null) return;
            if (ThrowBusy()) return;

            var bag = ResolvePlayerBag();
            var roller = ResolveRoller();
            if (bag == null || roller == null) return;

            // keep all-false = vuela la mano entera; los dados bloqueados (Boss 1)
            // quedan forzados a keep=true como en cualquier reroll.
            var keep = KeepForcingBlockedDice(new bool[_lastFaces.Length], _lastFaces.Length);

            // Holds marcados durante la ventana del delay quedarían fuera de sync
            // con el reveal nuevo — se limpian antes de re-tirar.
            if (_screenManager?.Current is CombatHUDView hud)
                hud.ClearDiceHolds();

            // Sin TryExtraRoll ni energía: el reroll del Torpe es gratis por diseño.
            RerollViaThrow(playerGuid, bag, roller, keep);
        }

        // Cuerpo compartido del reroll de combate: el botón Roll/Reroll lo invoca sin
        // keepOverride (usa los holds del HUD) y el grab-to-reroll con el mask armado
        // por el presenter. Devuelve true si el reroll efectivamente arrancó.
        private bool TryEnergyReroll(CombatHUDView hud, Guid playerGuid, bool[] keepOverride = null)
        {
            if (_forcedRerollPending) return false;
            if (_selectedBehavior != null && !_selectedBehavior.NeedsDiceRoll) return false;
            if (ThrowBusy()) return false;
            if (_lastFaces == null) return false; // nada rolleado todavía — no hay qué re-tirar

            // BUG-014: si todos los dados están holdeados, el reroll no movería
            // ningún dado — bail antes de consumir budget/energía. El botón
            // debería estar deshabilitado por la UI, esto es el guard defensivo.
            // Boss 1 (§2): forzamos keep=true en los dados bloqueados para que NO se re-rolleen.
            var keep = KeepForcingBlockedDice(keepOverride ?? hud.GetCurrentKeep(), _lastFaces?.Length ?? 0);
            if (AllDiceHeld(keep))
            {
                Debug.LogWarning("[CombatHandoffService] Reroll bloqueado — todos los dados están holdeados.");
                return false;
            }

            // Sin budget/energía el reroll no procede — clave para el grab-to-reroll,
            // que no tiene el gating visual del botón.
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null
                && !budget.TryExtraRoll(playerGuid))
            {
                return false;
            }

            var bag = ResolvePlayerBag();
            var roller = ResolveRoller();
            if (bag == null || roller == null)
            {
                Debug.LogError("[CombatHandoffService] No se pudo resolver bag/roller — Reroll abortado.");
                return false;
            }

            RerollViaThrow(playerGuid, bag, roller, keep);
            return true;
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
                var keepMask = hud.GetCurrentKeep();
                var keptDice = FilterKeptDice(_lastFaces, keepMask);
                effCtx.KeptDice = keptDice;
                effCtx.KeptDiceOriginalIndices = FilterKeptIndices(keepMask, _lastFaces.Length);
                var keptTypes = ResolveKeptTypes(effCtx.KeptDiceOriginalIndices, keptDice.Length);
                effCtx.ComboResult = DetectChainCombo(hero.Sheet, keptDice, keptTypes, _chainPhaseIndex);
            }

            var preCtx = new PreConditionContext
            {
                OwnerGuid = playerGuid,
                OpponentGuid = firstEnemyId,
            };

            int remainingFreeRolls = 0;
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget?.Current != null)
                remainingFreeRolls = budget.Current.FreeRollsRemaining;

            // El chain NO pasa por HeroActionBehavior.Execute() (que limpia stored values
            // en su primera línea): sin esta limpieza, los FloatingDamage/Shield/Heal que
            // los efectos appendean quedan acumulados en el behavior del ClassHeroSO
            // (compartido toda la run) y el feedback re-emite tiradas de casteos viejos —
            // un número extra por cada sala jugada.
            _selectedBehavior?.ClearBehaviorValues();

            // El chain no pasa por HeroActionBehavior.Execute, así que la ventana de combo
            // jugado se abre acá — una emisión POR FASE (cada fase consume su propia tirada).
            // EndPlay corre antes del OnRollResolved de más abajo: los hooks de roll-resolved
            // no deben ver la ventana abierta.
            if (phase?.Effects != null)
            {
                var play = ServiceLocator.TryGetService<IComboPlayService>(out var p) ? p : null;
                play?.BeginPlay(effCtx);
                // Anuncio acotado a los efectos de ESTA fase: daño si la fase pega,
                // escudo si la fase defiende — misma secuencia N×M para ambos.
                var phaseDmg = Rollgeon.Combat.Damage.DamageBreakdownAnnouncer.FindDealDamage(phase.Effects);
                if (phaseDmg != null)
                    Rollgeon.Combat.Damage.DamageBreakdownAnnouncer.Announce(effCtx, phaseDmg);
                else
                    Rollgeon.Combat.Damage.DamageBreakdownAnnouncer.AnnounceShield(
                        effCtx, Rollgeon.Combat.Damage.DamageBreakdownAnnouncer.FindAddShield(phase.Effects));
                try
                {
                    phase.Effects.TryExecute(effCtx, preCtx);
                }
                finally
                {
                    play?.EndPlay();
                }
            }

            // El resto del avance del chain se difiere hasta que baje el feedback en vuelo.
            // Los efectos de arriba pueden haber dejado consecuencias de gameplay atadas al
            // frame de impacto (§10.8, StepSource.InlineEffect): sin este gate el chain
            // avanzaría de fase ANTES de que el golpe conecte — la fase siguiente limpiaría
            // el bag y el FloatingDamage diferido saldría pegado al feedback equivocado.
            // Sin feedback en vuelo corre sincrónico, así que el flujo viejo no cambia.
            // ANTES de eso, esperar la secuencia de breakdown si está corriendo: la fase de
            // escudo no tiene feedback de golpe (EffAddShield aplica sincrónico), así que sin
            // esta espera el chain avanzaba en el mismo frame y el reset del board cortaba
            // la cuenta a mitad de la animación. El gate SIEMPRE baja (timeout del director
            // 8s + failsafe del FeedbackManager 10s + abort en teardown) — no hay soft-lock.
            RunAfterBreakdownSequence(() =>
            {
                if (ServiceLocator.TryGetService<TurnManager>(out var turnMgr) && turnMgr != null)
                    turnMgr.RunWhenFeedbackSettles(() => ContinueChainPhase(hud, playerGuid, remainingFreeRolls));
                else
                    ContinueChainPhase(hud, playerGuid, remainingFreeRolls);
            });
        }

        // Difiere la continuación hasta que la secuencia de breakdown libere su gate.
        // Sin secuencia en curso (fase sin combo, director sin bindear) corre sincrónico.
        private static void RunAfterBreakdownSequence(Action continuation)
        {
            if (!BreakdownUiGate.Pending)
            {
                continuation();
                return;
            }

            Action handler = null;
            handler = () =>
            {
                if (BreakdownUiGate.Pending) return; // ref-count: esperar la transición a 0
                BreakdownUiGate.Changed -= handler;
                continuation();
            };
            BreakdownUiGate.Changed += handler;
        }

        /// <summary>
        /// Segunda mitad de <see cref="ExecuteChainPhase"/>: cierra el budget, emite el roll
        /// resuelto y avanza de fase. Separada para poder diferirla hasta que termine el
        /// feedback del golpe (ver el gate en <see cref="ExecuteChainPhase"/>).
        /// </summary>
        private void ContinueChainPhase(CombatHUDView hud, Guid playerGuid, int remainingFreeRolls)
        {
            // BUG-018: el avance de fase corre sincrónico dentro de este método — recién
            // al salir queda estado consistente para aceptar otro Confirm.
            _isResolvingChainPhase = false;

            // CNF-002: el ataque ya resolvió sobre el objetivo — el crease de selección
            // se apaga acá (el Hit Flash del daño toma la posta en el mismo pawn).
            ClearAttackTargetCrease();

            // Ejecutar los efectos pudo terminar el combate: si el golpe mató al último
            // enemigo, ResetCombatPhaseState() ya limpió todo (incluido EndBudget) dejando
            // _activeChain en null. En ese caso no hay chain que seguir procesando — salimos
            // antes de volver a tocar _activeChain (sino NRE en _activeChain.PhaseCount).
            // Con el gate de feedback esto ahora también cubre el cierre diferido por la
            // secuencia de muerte, que antes llegaba después de este punto.
            if (_activeChain == null)
                return;

            // Se re-resuelve acá en vez de capturarse: entre el golpe y esta continuación
            // pudo pasar un frame largo, y el budget vive en el ServiceLocator.
            ServiceLocator.TryGetService<IRerollBudgetService>(out var budget);
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

            // La phase siguiente (típicamente defensa post-attack) corre con los rolls
            // libres sobrantes del pool anterior — su primer roll consume 1 de ellos.
            // Sin sobrantes, la energía habilita una entrada PAGA (1E por el primer
            // roll) si el behavior permite energy-reroll; sin rolls NI energía el
            // chain corta acá.
            var entryMode = ResolveChainPhaseEntry(remainingFreeRolls, currentEnergy,
                _selectedBehavior != null && _selectedBehavior.AllowsEnergyReroll);
            if (entryMode == ChainPhaseEntry.Finish)
            {
                FinishChain(hud, playerGuid, false);
                return;
            }

            PrepareNextChainPhase(hud, playerGuid, remainingFreeRolls);
        }

        private void StartNextChainPhase(CombatHUDView hud, Guid playerGuid, int freeRollCount)
        {
            var wrapper = UnityEngine.ScriptableObject.CreateInstance<ActionDefinitionSO>();
            wrapper.ActionId = $"{_selectedBehavior.ActionName}.chain.phase{_chainPhaseIndex}";
            wrapper.EnergyCost = 0;
            wrapper.FreeRollCount = freeRollCount;
            wrapper.AllowsEnergyReroll = _selectedBehavior.AllowsEnergyReroll;

            bool paidEntry = freeRollCount == 0;

            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
            {
                budget.StartBudget(wrapper);
                // El budget cuenta TODOS los rolls incl. el primero. En el flow de
                // chain la fase abre con su roll ya en curso (no hay boton Roll
                // entre fases), asi que consumimos una unidad para preservar la
                // semantica "FreeRollsRemaining = rerolls disponibles tras el roll".
                // En entrada paga no hay free rolls que consumir: el primer roll se
                // cobra (1E) recien cuando el jugador aprieta el boton Roll.
                if (!paidEntry)
                    budget.TryExtraRoll(playerGuid);
            }

            if (paidEntry)
            {
                // Entrada paga: la fase queda abierta SIN dados. El prompt del board
                // ofrece "X Roll (1E)" y hud.OnRollRequested cobra y tira al click;
                // Pass / End Turn siguen disponibles como salida sin costo.
                _awaitingChainPaidRoll = true;
                var paidPhase = _activeChain.Phases[_chainPhaseIndex];
                hud.ShowChainRollPrompt(paidPhase?.Label);
                EventManager.Trigger(EventName.OnChainPhaseStarted, playerGuid, _chainPhaseIndex, _activeChain.PhaseCount);
                return;
            }

            var bag = ResolvePlayerBag();
            var roller = ResolveRoller();
            if (bag == null || roller == null)
            {
                Debug.LogError("[CombatHandoffService] Cannot resolve bag/roller for chain phase — finishing chain.");
                FinishChain(hud, playerGuid, false);
                return;
            }

            // CNF-008: entre fases del chain ya no hay roll automático — los dados
            // aparecen sin tirar y la fase (ej. defensa) espera el throw del jugador.
            RollViaThrow(playerGuid, bag, roller);
            EventManager.Trigger(EventName.OnChainPhaseStarted, playerGuid, _chainPhaseIndex, _activeChain.PhaseCount);
        }

        /// <summary>Modo de entrada a la fase siguiente de un chain.</summary>
        public enum ChainPhaseEntry
        {
            /// <summary>Sin rolls sobrantes ni pago posible — el chain termina.</summary>
            Finish = 0,

            /// <summary>Quedan rolls libres del pool anterior — el primer roll de la fase consume 1.</summary>
            Free = 1,

            /// <summary>Sin rolls libres pero con energía — el primer roll se paga (1E) al apretar Roll.</summary>
            Paid = 2,
        }

        /// <summary>
        /// Decide la entrada a la fase siguiente del chain según el pool sobrante y la
        /// energía. La energía solo habilita la entrada paga si el behavior permite
        /// energy-reroll — mismo gate que aplica <see cref="RerollBudgetService"/> al cobrar.
        /// </summary>
        public static ChainPhaseEntry ResolveChainPhaseEntry(
            int remainingFreeRolls, int energy, bool allowsEnergyReroll)
        {
            if (remainingFreeRolls > 0) return ChainPhaseEntry.Free;
            if (energy > 0 && allowsEnergyReroll) return ChainPhaseEntry.Paid;
            return ChainPhaseEntry.Finish;
        }

        private void FinishChain(CombatHUDView hud, Guid playerGuid, bool wasPass)
        {
            // CNF-002: funnel terminal del chain (resolver, pass, End Turn, cancel) —
            // ningún path debe dejar el crease de selección huérfano.
            // CNF-008: ídem con un throw pendiente — se aborta sin revelar.
            AbortThrow();
            ClearAttackTargetCrease();

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
            _awaitingChainPaidRoll = false;
            _isResolvingChainPhase = false;

            _chainPhaseSelectionResult = null;
            if (_chainSelectionController != null)
            {
                _chainSelectionController.OnSelectionCompleted -= OnChainSelectionDone;
                _chainSelectionController = null;
            }
            _pendingChainCallback = null;

            hud.ClearBehaviorForFormula();
            hud.HideChainRollPrompt();

            EventManager.Trigger(EventName.OnChainCompleted, playerGuid, phasesCompleted, totalPhases, wasPass);

            // [DIAG temporal] bug "botón sigue activo tras usar".

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

        // Delegado a EffChain para compartir la MISMA búsqueda con el gate del botón
        // (HeroActionBehavior.HasUsableEffectGroup) y el hover preview — si divergieran,
        // el botón podría gatear con una selección distinta a la que el chain usa al jugar.
        private static SelectionSettings FindPhaseSelectionAt(ChainPhase phase, SelectionTiming timing)
            => EffChain.FindPhaseSelectionAt(phase, timing);

        private void BeginChainSelection(SelectionSettings settings, Guid playerGuid, Action onComplete)
        {

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
                // CNF-002: selecciones de enemigo (elegir a quién atacar) se pintan con
                // el estilo rojo "attack"; el resto conserva el celeste "move".
                HighlightStyle = settings.TargetsEnemies ? "attack" : "move",
                // Ataques: además del target rojo, pintar TODO el rango con el tinte base
                // "range" por debajo (solo visual; no altera qué casillas son clickeables).
                RangeTiles = settings.TargetsEnemies ? settings.ResolveRangeTiles(ownerPos, playerGuid) : null,
                RangeHighlightStyle = "range",
            });

            // Selección interactiva abierta: el jugador debe clickear el target.
            // El tutorial usa esta señal para el paso "pegale al enemigo".
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, playerGuid);
        }

        private void OnChainSelectionDone(TargetSelectionResult result)
        {
            if (_chainSelectionController != null)
            {
                _chainSelectionController.OnSelectionCompleted -= OnChainSelectionDone;
                _chainSelectionController = null;
            }
            _chainPhaseSelectionResult = result;

            // CNF-002: el objetivo quedó elegido ANTES de la tirada — marcarlo con el
            // crease para que el jugador vea a quién le está tirando.
            SetAttackTargetCrease(result);

            var cb = _pendingChainCallback;
            _pendingChainCallback = null;
            cb?.Invoke();
        }

        // ---- CNF-002: crease de selección sobre el pawn objetivo -----------------

        /// <summary>
        /// Marca con el crease "foco" al occupant de la celda elegida, si es un enemigo.
        /// Selecciones incompletas o sin occupant (celda vacía, self) limpian y no marcan.
        /// </summary>
        private void SetAttackTargetCrease(TargetSelectionResult result)
        {
            ClearAttackTargetCrease();

            if (result == null || !result.WasCompleted)
            {
                return;
            }
            if (result.SelectedTargets == null || result.SelectedTargets.Count == 0)
            {
                return;
            }
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!grid.TryGetOccupant(result.SelectedTargets[0].Coord, out var occupant)
                || occupant == Guid.Empty)
            {
                return;
            }
            if (occupant == _player.PlayerGuid) return;

            _creaseTargetGuid = occupant;
            PawnMaterialFeedback.SetSelectedFor(occupant, true);

            // El HUD previsualiza la mitigación real (weakness + escudo) del enemigo apuntado.
            EventManager.Trigger(EventName.OnCombatTargetChanged, _player.PlayerGuid, occupant);
        }

        private void ClearAttackTargetCrease()
        {
            if (_creaseTargetGuid == Guid.Empty) return;
            PawnMaterialFeedback.SetSelectedFor(_creaseTargetGuid, false);
            _creaseTargetGuid = Guid.Empty;
            EventManager.Trigger(EventName.OnCombatTargetChanged, _player.PlayerGuid, Guid.Empty);
        }

        // Empuja al board skin el tipo efectivo de la fase de chain indicada: el propio de la
        // fase si overridea, si no el del behavior. Cada fase tira aparte y puede querer un
        // board distinto (ej. daño=Attack, escudo=Defense).
        private void ApplyChainPhaseBoardSkin(CombatHUDView hud, int phaseIndex)
        {
            if (_activeChain == null || _selectedBehavior == null) return;
            if (phaseIndex < 0 || phaseIndex >= _activeChain.PhaseCount) return;

            var phase = _activeChain.Phases[phaseIndex];
            var type = phase != null
                ? phase.ResolveBoardType(_selectedBehavior.BoardType)
                : _selectedBehavior.BoardType;
            hud.ApplyBoardType(type);
        }

        private void PrepareNextChainPhase(CombatHUDView hud, Guid playerGuid, int freeRollCount)
        {
            var nextPhase = _activeChain.Phases[_chainPhaseIndex];
            // Board skin de la fase entrante antes de abrir su target-select, así el tablero
            // ya muestra el skin correcto (ej. Defense en la fase de escudo) mientras se apunta.
            ApplyChainPhaseBoardSkin(hud, _chainPhaseIndex);
            var beforeRoll = FindPhaseSelectionAt(nextPhase, SelectionTiming.BeforeRoll);


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

        // Detección de combo del path chain. Además, SOLO en la fase 0, registra el combo
        // en IComboLogService: los behaviors con chain nunca llegan al Record del path
        // primario (DoConfirm hace early-return con _activeChain activo), así que sin este
        // registro el historial queda vacío. El log alimenta al forbid-combo del jefe de
        // piso 2 (AINode_RotateBlock Target=Combo) y al snapshot de resume — no borrar el
        // Record aunque el combo del turno "no se use" acá. Las fases > 0 son continuaciones
        // con tirada nueva — matchean para su propio daño pero no redefinen "el combo del turno".
        internal static ComboDetectionResult? DetectChainCombo(
            Heroes.ContractSheet sheet, int[] keptDice, IReadOnlyList<DiceType> keptTypes,
            int chainPhaseIndex)
        {
            var combo = sheet?.MatchBest(keptDice, keptTypes);
            var result = combo != null
                ? DetectWithContractMods(sheet, combo, keptDice, keptTypes)
                : null;

            if (chainPhaseIndex == 0
                && ServiceLocator.TryGetService<Rollgeon.Combat.ComboLog.IComboLogService>(out var comboLog)
                && comboLog != null)
            {
                comboLog.Record(combo != null ? combo.ComboId : null);
            }

            return result;
        }

        // Capa 1 — tabla por clase (Spec Daño v2): Detect con el base plano de la clase.
        // Capa 2 — Boss 3 (§4): modificadores del Contrato encima del base de clase
        // (R01 ×2, R02 ×0.5, R03 prohibido → 0, R04/R05 valor del vecino). Devuelve el
        // ComboDetectionResult con el daño efectivo. Sin servicio/modificadores ⇒ el base de clase.
        private static ComboDetectionResult? DetectWithContractMods(
            Heroes.ContractSheet sheet, BaseComboSO combo, int[] keptDice,
            IReadOnlyList<DiceType> keptTypes)
        {
            if (combo == null) return null;
            var detected = combo.Detect(keptDice, keptTypes, sheet?.GetBaseDamageOverride(combo.ComboId));
            if (!detected.IsMatch) return detected;

            if (ServiceLocator.TryGetService<Rollgeon.Combat.ContractMod.IContractModifierService>(out var mods)
                && mods != null)
            {
                // Prohibido (Boss 2 memoria de combos / Boss 3 R03): el ataque NO scorea → 0 daño
                // TOTAL. Devolvemos NoMatch para que tampoco se sume el daño base del PJ (Attack)
                // ni los bonos de combo — "armás el combo bloqueado → 0".
                if (mods.IsForbidden(combo.ComboId))
                    return ComboDetectionResult.NoMatch();
                // Los mods escalan el base PLANO (Fix#0047): la parte dinámica de los combos
                // de base variable viaja aparte en DynamicBonus y las caras entran al daño
                // una sola vez vía Σcaras.
                int eff = mods.GetEffectiveBaseDamage(combo.ComboId, detected.BaseDamage);
                return ComboDetectionResult.Match(
                    detected.ComboId, eff, detected.CountUsed, detected.ContributingIndices,
                    detected.DynamicBonus);
            }
            return detected;
        }

        // Fuerza Bruta necesita el rango de cada dado DURANTE la detección (no después, como
        // multi_dmg_combo): tipos del subset holdeado vía el bag runtime, alineados 1:1 con
        // keptDice. Sin servicio/bag ⇒ null y el combo cae a su fallback d6.
        private static IReadOnlyList<DiceType> ResolveKeptTypes(
            IReadOnlyList<int> keptDiceOriginalIndices, int keptCount)
        {
            if (!ServiceLocator.TryGetService<Rollgeon.Upgrades.Dice.IDiceEnchantmentService>(out var enchants)
                || enchants?.Bag == null)
                return null;
            return Rollgeon.Combat.Damage.ContributingDiceResolver.ResolveKept(
                keptDiceOriginalIndices, enchants.Bag.Dice, keptCount);
        }

        // Boss 1 (§2): el dado bloqueado se ve pero queda EXCLUIDO del combo. Devuelve una
        // copia del keep con los índices bloqueados en false, sin tocar el array de la UI.
        private static bool[] KeepExcludingBlockedDice(bool[] keep, int diceLen)
        {
            var result = CopyKeep(keep, diceLen);
            if (ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db) && db != null)
                for (int i = 0; i < result.Length; i++)
                    if (db.IsBlocked(i)) result[i] = false;
            return result;
        }

        // Boss 1 (§2): el dado bloqueado NO se puede re-rollear. Fuerza keep=true en los índices
        // bloqueados para que el roller conserve su cara en el reroll.
        private static bool[] KeepForcingBlockedDice(bool[] keep, int diceLen)
        {
            var result = CopyKeep(keep, diceLen);
            if (ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db) && db != null)
                for (int i = 0; i < result.Length; i++)
                    if (db.IsBlocked(i)) result[i] = true;
            return result;
        }

        private static bool[] CopyKeep(bool[] keep, int diceLen)
        {
            int len = diceLen > 0 ? diceLen : (keep?.Length ?? 0);
            var result = new bool[len];
            if (keep != null)
                for (int i = 0; i < len && i < keep.Length; i++) result[i] = keep[i];
            return result;
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
        /// Contraparte de <see cref="FilterKeptDice"/>: en vez de los valores de cara, devuelve
        /// los índices ORIGINALES de bag slot que quedaron holdeados, en el mismo orden. Ej.
        /// <c>keep=[F,T,F,T,T]</c> → <c>[1,3,4]</c>. Necesario para mapear
        /// <see cref="ComboDetectionResult.ContributingIndices"/> (relativos al subset holdeado)
        /// de vuelta al <c>DiceType</c> real de cada dado (Spec de Daño v2).
        /// </summary>
        public static int[] FilterKeptIndices(bool[] keep, int diceLen)
        {
            if (keep == null || keep.Length == 0)
            {
                var all = new int[diceLen];
                for (int i = 0; i < diceLen; i++) all[i] = i;
                return all;
            }

            int len = Math.Min(diceLen, keep.Length);
            int count = 0;
            for (int i = 0; i < len; i++)
                if (keep[i]) count++;

            var result = new int[count];
            int idx = 0;
            for (int i = 0; i < len; i++)
                if (keep[i]) result[idx++] = i;
            return result;
        }

        /// <summary>
        /// Limpia el state interno cuando el usuario cancela la accion seleccionada
        /// antes del primer roll (eligiendo otra accion). No hay refund de energia
        /// porque <c>SpendEnergyNow</c> se difirio al boton Roll.
        /// </summary>
        private void CancelAwaitingSelection(CombatHUDView hud)
        {
            AbortThrow();
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
