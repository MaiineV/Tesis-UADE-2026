using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Economy;
using Rollgeon.Heroes;
using Rollgeon.Grid;
using Rollgeon.Input;
using Rollgeon.Meta;
using Rollgeon.Movement;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.Run;
using Rollgeon.Shop;
using Rollgeon.Tutorial.UI;
using Rollgeon.UI;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.CharacterFrame;
using Rollgeon.UI.Screens;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoroutineHost = Rollgeon.Patterns.CoroutineHost;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Orquestador del Tutorial Mode: step machine dirigida por eventos del bus,
    /// locks de salas del recorrido, gating progresivo de acciones, señalización
    /// vía <see cref="ITutorialOverlayService"/>, y teardown a fresh run al cruzar
    /// la puerta exit. Run-scoped — lo crea <c>RunController.OnRunStart</c> cuando
    /// <c>PendingRunRequest.IsTutorial</c> (ÚLTIMO, para que sus handlers corran
    /// después de los del DungeonManager), y muere con el <c>ClearScope(Run)</c>.
    /// <para>
    /// Recorrido: A Start → B combate 1v1 (unlock Heal al primer golpe recibido)
    /// → C combate 2v2 (unlock ForceDoor, escapar a B) → C se lockea, se abre
    /// D tienda (mejora de Par) → C se re-abre, matar a los 2 → E encantamientos
    /// (1 encantamiento, exit gateado hasta encantar) → exit → fresh run limpia.
    /// </para>
    /// </summary>
    public sealed class TutorialFlowController : IDisposable
    {
        private const string LogPrefix = "[TutorialFlowController] ";

        private readonly TutorialConfigSO _config;
        private readonly Guid _runId;

        private TutorialActionGateService _gate;
        private TutorialStep _step = TutorialStep.Movement;
        private bool _disposed;
        private bool _healTaught;

        // BUG-019: ventana exclusiva por paso — durante un paso guiado que pide UNA
        // acción, el resto se lockea temporalmente y se restaura con el snapshot.
        // null = sin ventana activa. Regla de orden: los Unlock de progresión
        // permanente corren ANTES de BeginExclusiveStep (así el restore los conserva).
        private HashSet<HeroBehaviorSlot> _exclusiveSnapshot;

        // El enemigo golpeó al jugador mientras corría la secuencia de enseñanza
        // del combate 1 — la lección de curar se difiere al cierre de esa secuencia.
        private bool _pendingHealTeach;

        // Popups diferidos al PRÓXIMO turno del jugador: mostrarlos en el instante
        // en que cede el turno los ponía encima de la animación de ataque enemiga
        // (feedback playtest). _playerTurnActive traquea entre OnTurnStarted del
        // jugador y el de cualquier enemigo.
        private bool _playerTurnActive;
        private bool _pendingCombat1Intro;

        // HandleAttackChainDone es alcanzable por OnChainCompleted Y por el
        // safety-net OnBehaviorExecuted — el guard evita el doble popup si ambos
        // llegan para la misma acción. Se rearma al arrancar la acción siguiente.
        private bool _attackChainDoneHandled;

        // Marco del jugador (arriba-izquierda): en el tutorial arranca sin íconos
        // y se revelan uno a uno (contrato → inventario → bolsa de dados).
        private CharacterFrameController _characterFrame;
        private readonly HashSet<CharacterFrameIcon> _unlockedIcons = new();

        // Defensa y curar se enseñan la PRIMERA vez que suceden, sea durante la
        // secuencia guiada o después, en el loop libre de cualquiera de los dos
        // combates (feedback playtest: podían no dispararse nunca si la primera
        // acción guiada no las tocaba).
        private bool _defenseTaught;
        private bool _defenseFromFreeLoop;

        // El presupuesto de re-rolls se enseña en el primer re-roll del jugador.
        private bool _rerollTaught;

        // true si el tutorial abrió el contrato en la primera tirada — solo en ese
        // caso lo cierra al terminar la acción (si lo abrió el jugador, es suyo).
        private bool _openedContractDrawer;

        // Paso subyacente del loop libre (Combat1 o Combat2) al que vuelven las
        // lecciones interceptadas (defensa/curar) cuando cierran.
        private TutorialStep _freeLoopStep = TutorialStep.Combat1;

        // Provider default de initiative, pisado por TutorialInitiativeProvider
        // (registro Global) — Dispose lo restaura.
        private IInitiativeProvider _initiativeToRestore;
        private IMovementService _movementService;

        // Vista de la mesa de encantamientos mientras esperamos su cierre
        // (paso CloseEnchantTable) — para desuscribir OnPanelClosed.
        private EnchantmentAltarView _enchantAltarView;

        // Roles del recorrido, resueltos por tipo+adyacencia (no por celdas hardcodeadas).
        private Guid _roomA; // Start
        private Guid _roomB; // Combat 1v1
        private Guid _roomC; // Combat 2v2
        private Guid _roomD; // Shop
        private Guid _roomE; // Enchantment

        private EventManager.EventReceiver _onRoomEntered;
        private EventManager.EventReceiver _onCombatTriggered;
        private EventManager.EventReceiver _onCombatEnd;
        private EventManager.EventReceiver _onShopItemPurchased;
        private EventManager.EventReceiver _onEnchantmentApplied;
        private EventManager.EventReceiver _onFloorExitRequested;
        private EventManager.EventReceiver _onActionSelectionStarted;
        private EventManager.EventReceiver _onHeroBehaviorClicked;
        private EventManager.EventReceiver _onChainTargetSelectionStarted;
        private EventManager.EventReceiver _onChainStarted;
        private EventManager.EventReceiver _onDiceRolled;
        private EventManager.EventReceiver _onChainPhaseStarted;
        private EventManager.EventReceiver _onChainCompleted;
        private EventManager.EventReceiver _onTurnStarted;
        private EventManager.EventReceiver _onTurnFinished;
        private EventManager.EventReceiver _onBehaviorExecuted;
        private EventManager.EventReceiver _onEnchantmentAltarActivated;

        private TutorialFlowController(TutorialConfigSO config, Guid runId)
        {
            _config = config;
            _runId = runId;
        }

        /// <summary>
        /// Factory: crea el controller, lo registra en <see cref="ServiceScope.Run"/>
        /// y arranca el primer paso. Llamar DESPUÉS de que el dungeon y los servicios
        /// de run estén registrados.
        /// </summary>
        public static TutorialFlowController CreateAndRegister(TutorialConfigSO config, Guid runId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var controller = new TutorialFlowController(config, runId);
            ServiceLocator.AddService<TutorialFlowController>(controller, ServiceScope.Run);
            controller.Begin();
            return controller;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onRoomEntered != null) EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEntered);
            if (_onCombatTriggered != null) EventManager.UnSubscribe(EventName.OnCombatTriggered, _onCombatTriggered);
            if (_onCombatEnd != null) EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
            if (_onShopItemPurchased != null) EventManager.UnSubscribe(EventName.OnShopItemPurchased, _onShopItemPurchased);
            if (_onEnchantmentApplied != null) EventManager.UnSubscribe(EventName.OnEnchantmentApplied, _onEnchantmentApplied);
            if (_onFloorExitRequested != null) EventManager.UnSubscribe(EventName.OnFloorExitRequested, _onFloorExitRequested);
            if (_onActionSelectionStarted != null) EventManager.UnSubscribe(EventName.OnActionSelectionStarted, _onActionSelectionStarted);
            if (_onHeroBehaviorClicked != null) EventManager.UnSubscribe(EventName.OnHeroBehaviorClicked, _onHeroBehaviorClicked);
            if (_onChainTargetSelectionStarted != null) EventManager.UnSubscribe(EventName.OnChainTargetSelectionStarted, _onChainTargetSelectionStarted);
            if (_onChainStarted != null) EventManager.UnSubscribe(EventName.OnChainStarted, _onChainStarted);
            if (_onDiceRolled != null) EventManager.UnSubscribe(EventName.OnDiceRolled, _onDiceRolled);
            if (_onChainPhaseStarted != null) EventManager.UnSubscribe(EventName.OnChainPhaseStarted, _onChainPhaseStarted);
            if (_onChainCompleted != null) EventManager.UnSubscribe(EventName.OnChainCompleted, _onChainCompleted);
            if (_onTurnStarted != null) EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStarted);
            if (_onTurnFinished != null) EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinished);
            if (_onBehaviorExecuted != null) EventManager.UnSubscribe(EventName.OnBehaviorExecuted, _onBehaviorExecuted);
            if (_onEnchantmentAltarActivated != null) EventManager.UnSubscribe(EventName.OnEnchantmentAltarActivated, _onEnchantmentAltarActivated);
            TypedEvent<DamageResolvedPayload>.Unsubscribe(OnDamageResolved);

            if (_movementService != null)
            {
                _movementService.OnEntityMoved -= OnEntityMoved;
                _movementService = null;
            }

            if (_enchantAltarView != null)
            {
                _enchantAltarView.OnPanelClosed -= OnEnchantTableClosed;
                _enchantAltarView = null;
            }

            // Los íconos lockeados del marco son estado del tutorial — restaurarlos
            // ante cualquier teardown (el fin normal recarga escena, pero un abort no).
            var frame = ResolveCharacterFrame();
            if (frame != null)
            {
                frame.SetAllIconsLocked(false);
                frame.SetPinned(false);
            }
            _characterFrame = null;

            // Restaurar el initiative provider default (la entry Global fue pisada
            // por TutorialInitiativeProvider en Begin).
            if (_initiativeToRestore != null)
            {
                ServiceLocator.AddService<IInitiativeProvider>(_initiativeToRestore, ServiceScope.Global);
                _initiativeToRestore = null;
            }
        }

        // ==================================================================
        // Setup
        // ==================================================================

        private void Begin()
        {
            if (!TryResolveRoomRoles())
            {
                Debug.LogError(LogPrefix + "El piso del tutorial no matchea el recorrido esperado " +
                               "(Start + 2 Combat encadenadas + Shop + Enchantment). El tutorial no se orquesta.");
                return;
            }

            // Servicios del tutorial (Run-scoped, mueren con el teardown).
            _gate = TutorialActionGateService.CreateAndRegister(_config.TutorialHero);
            TutorialInvulnerabilityService.CreateAndRegister();

            // El jugador SIEMPRE actúa primero en los combates del tutorial. El
            // decorator pisa la entry Global (TurnOrderService resuelve el provider
            // en cada BuildForCombat) y Dispose restaura el default.
            if (ServiceLocator.TryGetService<IInitiativeProvider>(out var initiative) && initiative != null)
            {
                _initiativeToRestore = initiative;
                ServiceLocator.AddService<IInitiativeProvider>(
                    new TutorialInitiativeProvider(initiative), ServiceScope.Global);
            }

            // El melee del primer combate aparece a pocas casillas del jugador,
            // para que la lección de MOVER se resuelva con un solo movimiento.
            TutorialEnemySpawnPlacement.CreateAndRegister(_roomB);

            // Economía y tienda controladas.
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy))
                economy.ResetTo(_config.TutorialStartingGold);
            if (ServiceLocator.TryGetService<IShopManagerService>(out var shop))
                shop.SetTutorialOverride(_config.ShopConfig, _config.ShopPool);

            // Locks iniciales del recorrido: D (tienda) nace Locked según el plan;
            // E nace Uncleared para gatear su puerta exit hasta encantar.
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                foreach (var entry in _config.FloorPlan.Entries)
                {
                    if (entry == null || !entry.StartLocked) continue;
                    var id = FindInstanceByCell(dungeon, entry.Cell);
                    if (id != Guid.Empty) dungeon.SetRoomState(id, RoomState.Locked);
                }

                dungeon.SetRoomState(_roomE, RoomState.Uncleared);

                // E no tiene puerta exit autorada: marcamos en runtime la puerta opuesta
                // a su entrada como salida de piso (mismo truco que la boss room, #158).
                MarkTutorialExitDoor(dungeon);

                // Los prefabs ya se instanciaron con los estados pre-lock — refrescar
                // los visuales de las salas cuyas puertas reflejan estos cambios.
                dungeon.ResyncDoorVisuals(_roomA);
                dungeon.ResyncDoorVisuals(_roomB);
                dungeon.ResyncDoorVisuals(_roomC);
                dungeon.ResyncDoorVisuals(_roomE);
            }

            Subscribe();

            // El marco del jugador arranca sin íconos: se revelan uno a uno durante
            // el recorrido (contrato → inventario → bolsa). Dispose restaura todo.
            ApplyFrameIconLocks();

            ShowMovementStep();
            Debug.Log(LogPrefix + $"Tutorial iniciado (runId={_runId}).");
        }

        private void Subscribe()
        {
            _onRoomEntered = OnRoomEntered;
            _onCombatTriggered = OnCombatTriggered;
            _onCombatEnd = OnCombatEnd;
            _onShopItemPurchased = OnShopItemPurchased;
            _onEnchantmentApplied = OnEnchantmentApplied;
            _onFloorExitRequested = OnFloorExitRequested;
            _onActionSelectionStarted = OnActionSelectionStarted;
            _onHeroBehaviorClicked = OnHeroBehaviorClicked;
            _onChainTargetSelectionStarted = OnChainTargetSelectionStarted;
            _onChainStarted = OnChainStarted;
            _onDiceRolled = OnDiceRolled;
            _onChainPhaseStarted = OnChainPhaseStarted;
            _onChainCompleted = OnChainCompleted;
            _onTurnStarted = OnTurnStarted;
            _onTurnFinished = OnTurnFinished;
            _onBehaviorExecuted = OnBehaviorExecuted;
            _onEnchantmentAltarActivated = OnEnchantmentAltarActivated;

            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEntered);
            EventManager.Subscribe(EventName.OnCombatTriggered, _onCombatTriggered);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);
            EventManager.Subscribe(EventName.OnShopItemPurchased, _onShopItemPurchased);
            EventManager.Subscribe(EventName.OnEnchantmentApplied, _onEnchantmentApplied);
            EventManager.Subscribe(EventName.OnFloorExitRequested, _onFloorExitRequested);
            EventManager.Subscribe(EventName.OnActionSelectionStarted, _onActionSelectionStarted);
            EventManager.Subscribe(EventName.OnHeroBehaviorClicked, _onHeroBehaviorClicked);
            EventManager.Subscribe(EventName.OnChainTargetSelectionStarted, _onChainTargetSelectionStarted);
            EventManager.Subscribe(EventName.OnChainStarted, _onChainStarted);
            EventManager.Subscribe(EventName.OnDiceRolled, _onDiceRolled);
            EventManager.Subscribe(EventName.OnChainPhaseStarted, _onChainPhaseStarted);
            EventManager.Subscribe(EventName.OnChainCompleted, _onChainCompleted);
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinished);
            EventManager.Subscribe(EventName.OnBehaviorExecuted, _onBehaviorExecuted);
            EventManager.Subscribe(EventName.OnEnchantmentAltarActivated, _onEnchantmentAltarActivated);
            TypedEvent<DamageResolvedPayload>.Subscribe(OnDamageResolved);

            if (ServiceLocator.TryGetService<IMovementService>(out var movement) && movement != null)
            {
                _movementService = movement;
                _movementService.OnEntityMoved += OnEntityMoved;
            }
        }

        /// <summary>
        /// Resuelve qué instancia es cada sala del recorrido por tipo + adyacencia:
        /// A = Start; B = Combat adyacente a A; C = Combat adyacente a B;
        /// D = Shop; E = Enchantment. Robusto a mover celdas en el plan.
        /// </summary>
        private bool TryResolveRoomRoles()
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return false;

            var all = dungeon.GetAllRoomInstances();
            foreach (var kv in all)
            {
                var type = kv.Value.Template != null ? kv.Value.Template.Type : RoomType.Combat;
                if (type == RoomType.Start) _roomA = kv.Key;
                else if (type == RoomType.Shop) _roomD = kv.Key;
                else if (type == RoomType.Enchantment) _roomE = kv.Key;
            }
            if (_roomA == Guid.Empty || _roomD == Guid.Empty || _roomE == Guid.Empty) return false;

            // B = combat conectada a A; C = combat conectada a B (que no es A).
            foreach (var neighborId in all[_roomA].Connections.Values)
            {
                if (all.TryGetValue(neighborId, out var n)
                    && n.Template != null && n.Template.Type == RoomType.Combat)
                {
                    _roomB = neighborId;
                    break;
                }
            }
            if (_roomB == Guid.Empty) return false;

            foreach (var neighborId in all[_roomB].Connections.Values)
            {
                if (neighborId == _roomA) continue;
                if (all.TryGetValue(neighborId, out var n)
                    && n.Template != null && n.Template.Type == RoomType.Combat)
                {
                    _roomC = neighborId;
                    break;
                }
            }
            return _roomC != Guid.Empty;
        }

        // ==================================================================
        // Event handlers — transiciones de la step machine
        // ==================================================================

        private void OnRoomEntered(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not Guid roomId) return;

            // El canvas del marco puede re-instanciarse entre salas — re-aplicar
            // el estado de reveal vigente (barato e idempotente).
            ApplyFrameIconLocks();

            if (_step == TutorialStep.Movement && roomId == _roomB)
            {
                // El combate lo anuncia OnCombatTriggered — acá solo apagamos el paso.
                Overlay()?.Hide();
            }
            else if (_step == TutorialStep.Shop && roomId == _roomD)
            {
                ShowStep(TutorialStep.Shop, new TutorialStepDisplayRequest
                {
                    AnchorKind = TutorialAnchorKind.WorldPosition,
                    WorldPosition = ResolvePedestalPosition(),
                    Text = LocalizedContent.Ui(TutorialTextKeys.ShopPedestal,
                        "Esta es la tienda. Acércate al pedestal y presiona F para comprar la mejora: tu combo PAR hará +50 de daño."),
                });
            }
            else if (_step == TutorialStep.Combat2Prep && roomId == _roomB)
            {
                // De vuelta en B camino a C: dejar de señalar la puerta de la tienda
                // y apuntar a la de la revancha (feedback playtest).
                ShowStep(TutorialStep.Combat2Prep, new TutorialStepDisplayRequest
                {
                    AnchorKind = TutorialAnchorKind.WorldPosition,
                    WorldPosition = ResolveDoorPosition(_roomB, _roomC),
                    Text = LocalizedContent.Ui(TutorialTextKeys.Combat2Door,
                        "La sala se desbloqueó: entra y termina lo que empezaste."),
                });
            }
            else if (_step == TutorialStep.GoToE && roomId == _roomE)
            {
                _step = TutorialStep.Enchant;
                ShowStep(TutorialStep.Enchant, new TutorialStepDisplayRequest
                {
                    AnchorKind = TutorialAnchorKind.WorldPosition,
                    WorldPosition = ResolveAltarPosition(),
                    Text = LocalizedContent.Ui(TutorialTextKeys.EnchantRoom,
                        "Esta es la mesa de encantamientos: mejora un dado a cambio de oro. Acércate al altar y presiona F."),
                });
            }
        }

        private void OnCombatTriggered(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not Guid roomId) return;

            if (_step == TutorialStep.Movement && roomId == _roomB)
            {
                ShowEnemiesIntro();
            }
            else if (_step == TutorialStep.GoToC && roomId == _roomC)
            {
                _step = TutorialStep.EscapeTeach;
                _gate?.Unlock(HeroBehaviorSlot.ForceDoor);

                // El player entra ya adyacente a la puerta — si se mueve pierde la
                // adyacencia y la lección se rompe (feedback playtest): durante el
                // escape solo queda FORZAR PUERTA disponible (BUG-019: ventana
                // exclusiva con snapshot, en vez de locks manuales por slot).
                BeginExclusiveStep(HeroBehaviorSlot.ForceDoor);

                // Señala el BOTÓN de la acción, no la puerta: el player entra ya
                // adyacente a ella (feedback playtest). Al clickearlo, la lección
                // retargetea a los dados (OnHeroBehaviorClicked).
                ShowStep(TutorialStep.EscapeTeach, ButtonStepRequest(HeroBehaviorSlot.ForceDoor,
                    LocalizedContent.Ui(TutorialTextKeys.EscapeTeach,
                        "¡Son demasiados! Se desbloqueó FORZAR PUERTA ({0}): úsala para escapar por donde viniste. Ya estás junto a la puerta."),
                    GameplayHotkey.ForceDoor));
            }
            else if (_step == TutorialStep.Combat2Prep && roomId == _roomC)
            {
                _step = TutorialStep.Combat2;
                _freeLoopStep = TutorialStep.Combat2;
                ShowStep(TutorialStep.Combat2, new TutorialStepDisplayRequest
                {
                    AnchorKind = TutorialAnchorKind.None,
                    Text = LocalizedContent.Ui(TutorialTextKeys.Combat2,
                        "¡Ahora sí! Con la mejora de PAR estás listo: acaba con los dos enemigos."),
                    InputPolicy = TutorialInputPolicy.BlockUntilContinue,
                }, () => Overlay()?.Hide());
            }
        }

        private void OnCombatEnd(params object[] args)
        {
            if (args == null || args.Length < 2 || args[0] is not Guid roomId) return;
            if (args[1] is not CombatOutcome outcome) return;

            // BUG-019: los locks del escape se levantan ante CUALQUIER outcome del
            // combate C — antes solo en la rama Aborted, y una victoria dejaba
            // Movement/BaseAttack lockeados para siempre (softlock).
            if (_step == TutorialStep.EscapeTeach && roomId == _roomC)
                EndExclusiveStep();

            if ((IsCombat1TeachStep(_step) || _step == TutorialStep.Combat1 || _step == TutorialStep.HealUnlocked)
                && roomId == _roomB && outcome == CombatOutcome.Victory)
            {
                // BUG-019: el enemigo puede morir en medio de la secuencia guiada de
                // ataque — cerrar la ventana exclusiva antes de pasar a exploración
                // (idempotente; sin esto Movement quedaría lockeado = softlock nuevo).
                EndExclusiveStep();
                ClearTurnDeferrals();
                CloseContractDrawerIfWeOpenedIt();

                _step = TutorialStep.GoToC;
                ShowStep(TutorialStep.GoToC, new TutorialStepDisplayRequest
                {
                    AnchorKind = TutorialAnchorKind.WorldPosition,
                    WorldPosition = ResolveDoorPosition(_roomB, _roomC),
                    Text = LocalizedContent.Ui(TutorialTextKeys.GoToC,
                        "¡Bien hecho! Sigue por la puerta señalada. La otra está bloqueada — la abrirás más adelante."),
                });
            }
            else if (_step == TutorialStep.EscapeTeach && roomId == _roomC && outcome == CombatOutcome.Aborted)
            {
                // El restore de las acciones ya corrió arriba (EndExclusiveStep).
                // Escapó: C queda gateada y se abre la tienda. El jugador está en B.
                ClearTurnDeferrals();
                if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
                {
                    dungeon.SetRoomState(_roomC, RoomState.Locked);
                    dungeon.SetRoomState(_roomD, RoomState.Cleared);
                    dungeon.ResyncDoorVisuals(_roomB);
                }

                // Recién salió de su segundo combate y está de vuelta en exploración:
                // explicar las consecuencias del escape y después la cámara/mapa,
                // antes de mandarlo a la tienda.
                ShowEscapeAftermath();
            }
            else if ((_step == TutorialStep.Combat2
                      // La victoria puede llegar con una lección interceptada visible
                      // (curar/defensa enseñadas en el loop libre del combate 2).
                      || (_freeLoopStep == TutorialStep.Combat2
                          && _step is TutorialStep.HealUnlocked or TutorialStep.DefenseTeach)
                      // BUG-019 (borde casi inalcanzable): victoria durante EscapeTeach —
                      // C quedó clareada, seguimos a E salteando la lección de tienda.
                      || _step == TutorialStep.EscapeTeach)
                     && roomId == _roomC && outcome == CombatOutcome.Victory)
            {
                // La victoria puede llegar con la ventana exclusiva de una lección
                // interceptada (curar) todavía abierta — restaurar (idempotente).
                EndExclusiveStep();
                ClearTurnDeferrals();
                _step = TutorialStep.GoToE;
                ShowStep(TutorialStep.GoToE, new TutorialStepDisplayRequest
                {
                    AnchorKind = TutorialAnchorKind.WorldPosition,
                    WorldPosition = ResolveDoorPosition(_roomC, _roomE),
                    Text = LocalizedContent.Ui(TutorialTextKeys.GoToE,
                        "¡Excelente! Pasa a la última sala: la de encantamientos."),
                });
            }
        }

        // Escapar no limpia la sala: los enemigos quedan con su vida guardada (y en
        // el juego real se curan un porcentaje). El jugador va a volver a C, así que
        // conviene decirlo ahora.
        private void ShowEscapeAftermath()
        {
            _step = TutorialStep.EscapeAftermath;
            ShowStep(TutorialStep.EscapeAftermath, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.EscapeAftermath,
                    "Escapar no los elimina: los enemigos se quedan en la sala, recuperan algo de vida y te esperan si vuelves."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, ShowCameraTeach);
        }

        private void ShowCameraTeach()
        {
            _step = TutorialStep.CameraTeach;
            ShowStep(TutorialStep.CameraTeach, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.CameraControls,
                    "Gira la cámara con el botón derecho. Arrastra el mapa con la rueda presionada. Zoom: rueda. Pruébalo ahora."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, ShowMapTeach);
        }

        // La cámara sigue viva detrás del dim — el jugador puede alejar el zoom
        // con este paso visible y ver las salas vecinas mientras lee.
        private void ShowMapTeach()
        {
            _step = TutorialStep.MapTeach;
            ShowStep(TutorialStep.MapTeach, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.MapRooms,
                    "Aleja el zoom para ver las salas adyacentes: sus íconos te dicen cuáles son especiales (tienda, encantamiento...)."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, ShowShopDoorStep);
        }

        /// <summary>Paso de la puerta de la tienda — cierra la cadena post-escape.</summary>
        private void ShowShopDoorStep()
        {
            _step = TutorialStep.Shop;
            ShowStep(TutorialStep.Shop, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.WorldPosition,
                WorldPosition = ResolveDoorPosition(_roomB, _roomD),
                Text = LocalizedContent.Ui(TutorialTextKeys.ShopDoor,
                    "¡Escapaste! Se abrió la puerta de la tienda. Entra: te espera algo que te dará ventaja."),
            });
        }

        /// <summary>El combate cerró — los popups diferidos al próximo turno ya no
        /// tienen turno al que diferirse.</summary>
        private void ClearTurnDeferrals()
        {
            _pendingCombat1Intro = false;
            _playerTurnActive = false;
        }

        /// <summary>
        /// BUG-019: abre la ventana exclusiva — snapshotea el estado del gate (una
        /// sola vez si se encadena) y lockea todo salvo <paramref name="allowed"/>.
        /// </summary>
        private void BeginExclusiveStep(HeroBehaviorSlot allowed)
        {
            if (_gate == null) return;
            if (_exclusiveSnapshot == null) _exclusiveSnapshot = _gate.SnapshotLocked();
            _gate.LockAllExcept(allowed);
        }

        /// <summary>
        /// BUG-019: cierra la ventana exclusiva restaurando el snapshot. Idempotente —
        /// se llama incondicionalmente en todos los exit paths de una secuencia guiada.
        /// </summary>
        private void EndExclusiveStep()
        {
            if (_exclusiveSnapshot == null) return;
            _gate?.RestoreTo(_exclusiveSnapshot);
            _exclusiveSnapshot = null;
        }

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            if (!TryGetPlayerGuid(out var playerGuid)) return;

            // (El cierre de la lección de ataque NO va acá: después del daño puede
            // venir la fase de defensa del chain — lo maneja OnChainPhaseStarted /
            // OnRollResolved al final de la acción.)

            // Primer golpe recibido → se desbloquea Curar. Inmediato en el loop
            // libre; diferido si todavía corre la secuencia de enseñanza (el enemigo
            // pega entre nuestros pasos porque el juego no se pausa detrás del dim).
            if (_healTaught || payload.FinalDamage <= 0 || payload.TargetGuid != playerGuid) return;

            if (_step == TutorialStep.Combat1 || _step == TutorialStep.Combat2)
            {
                // El golpe casi siempre llega durante el turno ENEMIGO — mostrar la
                // lección ahí la ponía encima de su animación de ataque (feedback
                // playtest): se difiere al próximo OnTurnStarted del jugador.
                if (_playerTurnActive) ShowHealTeach();
                else _pendingHealTeach = true;
            }
            else if (IsCombat1TeachStep(_step)) _pendingHealTeach = true;
        }

        // Traquea de quién es el turno y flushea los popups diferidos cuando
        // vuelve a ser el del jugador (la animación enemiga ya terminó).
        private void OnTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not Guid entity) return;
            if (!TryGetPlayerGuid(out var playerGuid)) return;

            _playerTurnActive = entity == playerGuid;
            if (!_playerTurnActive) return;

            if (_pendingHealTeach && _step is TutorialStep.Combat1 or TutorialStep.Combat2)
            {
                // La lección de curar pisa a la intro del loop libre: es la más útil
                // de las dos y el jugador ya vio cómo se pelea.
                _pendingCombat1Intro = false;
                ShowHealTeach();
            }
            else if (_pendingCombat1Intro)
            {
                _pendingCombat1Intro = false;
                ShowCombat1FreeIntro();
            }
        }

        // ==================================================================
        // Secuencia de enseñanza del combate 1 (sala B)
        // ==================================================================

        /// <summary>Pasos guiados del combate 1, previos al loop libre.</summary>
        private static bool IsCombat1TeachStep(TutorialStep step) =>
            step is TutorialStep.EnemiesIntro or TutorialStep.TurnOrderIntro or TutorialStep.ContractTeach
                or TutorialStep.MoveTeach or TutorialStep.MoveTiles
                or TutorialStep.StatsHp or TutorialStep.StatsRolls or TutorialStep.AttackTeach
                or TutorialStep.TargetTeach or TutorialStep.ThrowTeach or TutorialStep.DiceTeach
                or TutorialStep.DefenseTeach or TutorialStep.EndTurnTeach;

        /// <summary>Pasos de la lección de ataque que pueden reintentarse: si el
        /// jugador cancela la acción (EndTurn a mitad de camino), el próximo click
        /// en ATACAR re-abre la selección de objetivo y la lección re-arranca ahí.</summary>
        private static bool IsAttackLessonStep(TutorialStep step) =>
            step is TutorialStep.AttackTeach or TutorialStep.TargetTeach
                or TutorialStep.ThrowTeach or TutorialStep.DiceTeach;

        private void ShowEnemiesIntro()
        {
            _step = TutorialStep.EnemiesIntro;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.EnemiesIntro,
                    "¡Combate! Este es tu enemigo. En el tutorial tú siempre actúas primero."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            };
            var enemy = FirstEnemyOf(_roomB);
            if (enemy != Guid.Empty)
            {
                request.AnchorKind = TutorialAnchorKind.WorldEntity;
                request.EntityGuid = enemy;
            }
            ShowStep(TutorialStep.EnemiesIntro, request, ShowTurnOrderIntro);
        }

        // La cola de turnos (esquina sup. der.) ya está poblada: OnTurnQueueBuilt
        // corre en el setup del combate, frames antes del click de continue.
        private void ShowTurnOrderIntro()
        {
            _step = TutorialStep.TurnOrderIntro;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.TurnOrderIntro,
                    "Arriba a la derecha está el orden de turnos: quién actúa ahora y quién sigue."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            };
            var hud = FindCombatHud();
            if (hud != null && hud.TryGetTurnQueueRect(out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            ShowStep(TutorialStep.TurnOrderIntro, request, ShowContractTeach);
        }

        // Primer reveal del marco del jugador: el contrato, para que pueda ver los
        // combos antes de su primer ataque. Se pinnea la ruleta mientras se señala
        // (el anchor resolver cuenta rects con alpha 0 — sin pin la flecha
        // apuntaría a un ícono invisible).
        private void ShowContractTeach()
        {
            _step = TutorialStep.ContractTeach;
            UnlockFrameIcon(CharacterFrameIcon.Contract);
            ResolveCharacterFrame()?.SetPinned(true);
            ShowStep(TutorialStep.ContractTeach, IconStepRequest(CharacterFrameIcon.Contract,
                LocalizedContent.Ui(TutorialTextKeys.ContractIcon,
                    "Este es tu CONTRATO: los combos de generala y su daño. Consúltalo cuando quieras.")),
                () =>
                {
                    ResolveCharacterFrame()?.SetPinned(false);
                    ShowMoveTeach();
                });
        }

        private void ShowMoveTeach()
        {
            _step = TutorialStep.MoveTeach;
            ShowStep(TutorialStep.MoveTeach, ButtonStepRequest(HeroBehaviorSlot.Movement,
                LocalizedContent.Ui(TutorialTextKeys.MoveTeach,
                    "Tu héroe pelea cuerpo a cuerpo: primero acércate. Selecciona MOVER ({0})."),
                GameplayHotkey.Move));
        }

        // Movement comprometió su selección de tile → señalar las casillas alcanzables.
        private void OnActionSelectionStarted(params object[] args)
        {
            // Arrancó una acción nueva — rearmar el guard del cierre de ataque.
            _attackChainDoneHandled = false;

            if (_step != TutorialStep.MoveTeach) return;
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            _step = TutorialStep.MoveTiles;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.WorldEntity,
                EntityGuid = playerGuid,
                Text = LocalizedContent.Ui(TutorialTextKeys.MoveTiles,
                    "Las casillas iluminadas son tu alcance este turno. Haz clic en una casilla junto al enemigo."),
                CutoutRadiusPx = 320f,
                ShowArrow = false,
            };
            ShowStep(TutorialStep.MoveTiles, request);
        }

        // La lección de mover se cumple por ADYACENCIA, no por "hubo un movimiento":
        // si el player quedó lejos, se reintenta; si el enemigo se acercó solo,
        // también cuenta (feedback playtest: moverse lejos bugeaba la secuencia).
        private void OnEntityMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (_step != TutorialStep.MoveTeach && _step != TutorialStep.MoveTiles) return;
            if (!TryGetPlayerGuid(out var playerGuid)) return;

            if (IsPlayerAdjacentToRoomBEnemy(playerGuid))
            {
                ShowStatsHp();
                return;
            }

            if (_step == TutorialStep.MoveTiles && entity == playerGuid)
            {
                // Quedó lejos y ya gastó su movida del turno (solo se puede mover
                // UNA vez por turno en combate): enseñar FINALIZAR TURNO — el
                // enemigo se acerca solo en su turno, y esa adyacencia también
                // completa la lección. El paso vuelve a MoveTeach por si el turno
                // siguiente necesita moverse de nuevo.
                _step = TutorialStep.MoveTeach;
                ShowStep(TutorialStep.MoveTeach, EndTurnAnchoredRequest(
                    LocalizedContent.Ui(TutorialTextKeys.MoveTooFar,
                        "Quedaste lejos y solo puedes moverte una vez por turno. " +
                        "Pulsa FINALIZAR TURNO ({0}) y deja que se acerque.")));
            }
        }

        private bool IsPlayerAdjacentToRoomBEnemy(Guid playerGuid)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!grid.TryGetPosition(playerGuid, out var playerCoord)) return false;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                || !dungeon.GetAllRoomInstances().TryGetValue(_roomB, out var room)) return false;

            foreach (var enemy in room.SpawnedEnemies)
            {
                if (!grid.TryGetPosition(enemy, out var enemyCoord)) continue;
                int distance = Math.Abs(enemyCoord.X - playerCoord.X) + Math.Abs(enemyCoord.Y - playerCoord.Y);
                if (distance <= 1) return true;
            }
            return false;
        }

        private void ShowStatsHp()
        {
            _step = TutorialStep.StatsHp;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.StatsHp,
                    "Esta es tu VIDA: si llega a cero, la run se termina. Cuídala."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            };
            var hud = FindCombatHud();
            if (hud != null && hud.TryGetHealthBarRect(out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            ShowStep(TutorialStep.StatsHp, request, ShowStatsRolls);
        }

        private void ShowStatsRolls()
        {
            _step = TutorialStep.StatsRolls;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.StatsRolls,
                    "Este es tu POOL DE ROLLS: cada tirada de dados consume 1. Al terminar tu turno recuperas 5 (máximo 15)."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            };
            var hud = FindCombatHud();
            if (hud != null && hud.TryGetRollPoolRect(out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            ShowStep(TutorialStep.StatsRolls, request, ShowAttackTeach);
        }

        private void ShowAttackTeach()
        {
            _step = TutorialStep.AttackTeach;
            _gate?.Unlock(HeroBehaviorSlot.BaseAttack);

            // BUG-019: durante la secuencia guiada de ataque (AttackTeach → … →
            // EndTurnTeach) solo ATACAR queda disponible — sin esto el jugador podía
            // moverse/curarse en vez de atacar y estancar el paso (el unlock de arriba
            // es la progresión permanente y corre antes del snapshot a propósito).
            BeginExclusiveStep(HeroBehaviorSlot.BaseAttack);

            ShowStep(TutorialStep.AttackTeach, ButtonStepRequest(HeroBehaviorSlot.BaseAttack,
                LocalizedContent.Ui(TutorialTextKeys.AttackTeach,
                    "Se desbloqueó ATACAR ({0}). Selecciónalo para elegir a quién golpear."),
                GameplayHotkey.Attack));
        }

        // Click efectivo en un botón de acción del HUD. OJO con el orden: para el
        // ataque este evento llega DESPUÉS de OnChainTargetSelectionStarted (el
        // handoff corre dentro del mismo click, antes del Trigger del view), así que
        // la lección de ataque la encadena aquel handler — acá solo las acciones que
        // NO abren selección (Heal / ForceDoor van directo a los dados).
        private void OnHeroBehaviorClicked(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not HeroBehaviorSlot slot) return;

            if (_step == TutorialStep.HealUnlocked && slot == HeroBehaviorSlot.Healing)
            {
                ShowStep(TutorialStep.HealUnlocked, DiceZoneRequest(
                    LocalizedContent.Ui(TutorialTextKeys.HealDice,
                        "Curar también usa los dados: lánzalos y supera el umbral para recuperar vida. Bloquea los altos y confirma.")));
            }
            else if (_step == TutorialStep.EscapeTeach && slot == HeroBehaviorSlot.ForceDoor)
            {
                ShowStep(TutorialStep.EscapeTeach, DiceZoneRequest(
                    LocalizedContent.Ui(TutorialTextKeys.EscapeDice,
                        "Forzar la puerta se resuelve con los dados: lánzalos y supera el umbral para escapar del combate.")));
            }
            else if (_step == TutorialStep.Combat1)
            {
                // Ya está en el loop libre — la guía deja de estorbar.
                Overlay()?.Hide();
            }
        }

        // Eligió ATACAR y el chain abrió la selección interactiva de objetivo (la
        // selección va ANTES de la tirada) → señalar al enemigo. También re-engancha
        // la lección si el jugador la había cancelado a mitad de camino (EndTurn).
        private void OnChainTargetSelectionStarted(params object[] args)
        {
            if (!IsAttackLessonStep(_step)) return;
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            _step = TutorialStep.TargetTeach;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.TargetTeach,
                    "Primero elige el objetivo: haz clic en el enemigo iluminado en rojo."),
            };
            var enemy = FirstEnemyOf(_roomB);
            if (enemy != Guid.Empty)
            {
                request.AnchorKind = TutorialAnchorKind.WorldEntity;
                request.EntityGuid = enemy;
            }
            ShowStep(TutorialStep.TargetTeach, request);
        }

        // Objetivo clickeado → el chain arrancó y los dados aparecieron SIN tirar
        // (modo manual: esperan el agarre del jugador) → enseñar a arrojarlos.
        private void OnChainStarted(params object[] args)
        {
            // Arrancó una acción nueva — rearmar el guard del cierre de ataque.
            _attackChainDoneHandled = false;

            // AttackTeach cubre el caso de una selección no interactiva (AutoResolve/
            // Self): sin click de target, el chain arranca directo desde el botón.
            if (_step != TutorialStep.TargetTeach && _step != TutorialStep.AttackTeach) return;
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            _step = TutorialStep.ThrowTeach;
            ShowStep(TutorialStep.ThrowTeach, DiceZoneRequest(
                LocalizedContent.Ui(TutorialTextKeys.ThrowTeach,
                    "¡Objetivo marcado! Estos son tus dados: sujétalos con clic izquierdo " +
                    "y lánzalos con un movimiento rápido del mouse.")));
        }

        // Los dados asentaron y se reveló la primera tirada → enseñar combos,
        // holds y el re-tiro. En la primera tirada se abre el CONTRATO a la fuerza
        // (feedback playtest: el jugador tiene que VER los combos y su daño mientras
        // decide qué bloquear, no solo saber que el ícono existe). La primera
        // re-tirada enseña el presupuesto de rolls y su costo.
        private void OnDiceRolled(params object[] args)
        {
            if (_step != TutorialStep.ThrowTeach
                && !(_step == TutorialStep.DiceTeach && !_rerollTaught)) return;
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            if (_step == TutorialStep.ThrowTeach)
            {
                _step = TutorialStep.DiceTeach;
                OpenContractDrawerForRoll();
                ShowStep(TutorialStep.DiceTeach, DiceZoneRequest(
                    LocalizedContent.Ui(TutorialTextKeys.DiceTeach,
                        "Arma combos (par, trío, escalera). Clic bloquea un dado; re-tira el resto " +
                        "por 1 Roll cada tirada. Luego CONFIRMA.")));
                return;
            }

            // Segunda tirada de la misma acción = su primer re-roll. Es un advice,
            // no un paso: cualquier click lo descarta (feedback playtest — quedaba
            // pegado tapando el botón hasta el paso siguiente).
            _rerollTaught = true;
            var rerollRequest = RollButtonRequest(
                LocalizedContent.Ui(TutorialTextKeys.RerollTeach,
                    "Puedes volver a tirar sin tope: cada tirada consume 1 Roll de tu pool."));
            rerollRequest.InputPolicy = TutorialInputPolicy.DismissOnClick;
            ShowStep(TutorialStep.DiceTeach, rerollRequest);
        }

        // El chain siguió a su fase 2 (defensa post-ataque) — solo sucede si al
        // jugador le SOBRARON tiradas libres del ataque → explicar el escudo
        // sobre la zona de dados. Si no sobraron, el chain completa directo y
        // OnChainCompleted muestra la lección de fin de turno. Se enseña la
        // PRIMERA vez que sucede: puede ser en el ataque guiado o mucho después,
        // en el loop libre de cualquiera de los dos combates.
        private void OnChainPhaseStarted(params object[] args)
        {
            if (_defenseTaught) return;
            bool guided = _step is TutorialStep.TargetTeach or TutorialStep.ThrowTeach or TutorialStep.DiceTeach;
            bool freeLoop = _step is TutorialStep.Combat1 or TutorialStep.Combat2
                or TutorialStep.HealUnlocked;
            if (!guided && !freeLoop) return;
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            _defenseTaught = true;
            _defenseFromFreeLoop = freeLoop;
            _step = TutorialStep.DefenseTeach;
            ShowStep(TutorialStep.DefenseTeach, DiceZoneRequest(
                LocalizedContent.Ui(TutorialTextKeys.DefenseTeach,
                    "Te sobraron tiradas: fase de DEFENSA. Lanza los dados y arma un combo — " +
                    "tu ESCUDO absorbe el próximo golpe.")));
        }

        // La acción completa terminó (con o sin fase de defensa) → enseñar el fin
        // de turno. OJO: OnRollResolved NO sirve acá — dispara al final de CADA
        // fase del chain, así que con defensa pendiente pisaba esa lección
        // (feedback playtest). OnChainCompleted dispara exactamente una vez, en
        // FinishChain. phasesCompleted == 0 = pass total sin atacar (End Turn en
        // medio de la lección de dados): no avanzamos, el jugador puede reintentar
        // el ataque en su próximo turno.
        private void OnChainCompleted(params object[] args)
        {
            if (_step != TutorialStep.DiceTeach && _step != TutorialStep.TargetTeach
                && _step != TutorialStep.ThrowTeach && _step != TutorialStep.DefenseTeach) return;
            if (args == null || args.Length < 2 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;
            if (args[1] is int phasesCompleted && phasesCompleted == 0) return;

            HandleAttackChainDone();
        }

        /// <summary>
        /// La acción de ataque cerró con alguna lección de la secuencia visible.
        /// Guiado → sigue el fin de turno. Defensa interceptada en el loop libre →
        /// la lección cierra y devuelve el paso subyacente (el fin de turno ya se
        /// enseñó en la secuencia guiada), flusheando una lección de curar pendiente.
        /// </summary>
        private void HandleAttackChainDone()
        {
            // OnChainCompleted y el safety-net OnBehaviorExecuted pueden llegar
            // ambos para la misma acción — procesar solo el primero.
            if (_attackChainDoneHandled) return;
            _attackChainDoneHandled = true;

            if (_step == TutorialStep.DefenseTeach && _defenseFromFreeLoop)
            {
                if (_pendingHealTeach)
                {
                    // ShowHealTeach re-abre la ventana exclusiva; si venía una activa
                    // (defensa interceptada durante la guía de curar) el snapshot
                    // original se conserva — no hace falta cerrarla acá.
                    ShowHealTeach();
                    return;
                }
                // La defensa pudo interceptar el chain de la guía de curar — si esa
                // ventana sigue abierta, restaurarla (no-op en el ataque libre común).
                EndExclusiveStep();
                _step = _freeLoopStep;
                Overlay()?.Hide();
                return;
            }

            ShowEndTurnTeach();
        }

        // El jugador cerró su turno con la lección de fin de turno visible —
        // secuencia guiada completada.
        private void OnTurnFinished(params object[] args)
        {
            if (_step != TutorialStep.EndTurnTeach) return;
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            CompleteAttackTeach();
        }

        private void ShowEndTurnTeach()
        {
            CloseContractDrawerIfWeOpenedIt();
            _step = TutorialStep.EndTurnTeach;
            // No bloquea input: el jugador tiene que poder apretar el botón real.
            // La lección se cierra con OnTurnFinished.
            ShowStep(TutorialStep.EndTurnTeach, EndTurnAnchoredRequest(
                LocalizedContent.Ui(TutorialTextKeys.EndTurnTeach,
                    "¡Golpe completado! Cuando no quieras hacer nada más, pulsa " +
                    "FINALIZAR TURNO ({0}) para ceder el turno.")));
        }

        /// <summary>
        /// Paso no bloqueante anclado al botón FINALIZAR TURNO del HUD. Degrada a
        /// popup centrado si el HUD no está disponible.
        /// </summary>
        private TutorialStepDisplayRequest EndTurnAnchoredRequest(string text)
        {
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = text,
                HotkeyHint = GameplayHotkey.EndTurn,
            };
            var hud = FindCombatHud();
            if (hud != null && hud.TryGetEndTurnRect(out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            return request;
        }

        private void OnBehaviorExecuted(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not Guid source) return;
            if (!TryGetPlayerGuid(out var playerGuid) || source != playerGuid) return;

            if (_step is TutorialStep.DiceTeach or TutorialStep.TargetTeach
                or TutorialStep.ThrowTeach or TutorialStep.DefenseTeach)
            {
                // Red de seguridad para un ataque que resuelva sin pasar por el
                // chain — en el path chain OnChainCompleted ya corrió (FinishChain
                // lo emite antes) y el step ya salió de estas lecciones.
                HandleAttackChainDone();
            }
            else if (_step == TutorialStep.HealUnlocked)
            {
                // Ejecutó una acción con la guía de curar visible — lección cerrada,
                // restaurar la ventana exclusiva de la lección.
                EndExclusiveStep();
                _step = _freeLoopStep;
                Overlay()?.Hide();
            }
        }

        /// <summary>
        /// Cierra la secuencia guiada del combate 1: si el enemigo ya golpeó al
        /// jugador durante la enseñanza, la lección de curar sale acá; sino queda
        /// el loop libre (curar se enseña con el primer golpe recibido).
        /// </summary>
        private void CompleteAttackTeach()
        {
            // BUG-019: fin de la secuencia guiada — restaurar antes de cualquier
            // lección siguiente (ShowHealTeach desbloquea Healing de forma permanente
            // y debe correr FUERA de la ventana para que el restore no lo pise).
            EndExclusiveStep();

            // Esto corre en el fin de turno del jugador y el enemigo ataca a
            // continuación — mostrar acá ponía el popup encima de su animación
            // (feedback playtest). Ambas lecciones se difieren a OnTurnStarted.
            _step = TutorialStep.Combat1;
            Overlay()?.Hide();
            if (!_pendingHealTeach) _pendingCombat1Intro = true;
        }

        private void ShowCombat1FreeIntro()
        {
            _step = TutorialStep.Combat1;
            ShowStep(TutorialStep.Combat1, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.Combat1Free,
                    "¡Así se pelea! Repite el proceso — moverte, atacar, armar combos — hasta vencer al enemigo."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, () => Overlay()?.Hide());
        }

        private void ShowHealTeach()
        {
            _pendingHealTeach = false;
            _healTaught = true;
            _step = TutorialStep.HealUnlocked;
            _gate?.Unlock(HeroBehaviorSlot.Healing);

            // Mientras la guía de CURAR está visible, el resto de las acciones se
            // lockea (mismo patrón BUG-019 que ataque/escape): apretar ATACAR acá
            // rompía la step machine. FINALIZAR TURNO no es un slot — sigue libre,
            // así que diferir la cura al próximo turno no softlockea.
            BeginExclusiveStep(HeroBehaviorSlot.Healing);

            ShowStep(TutorialStep.HealUnlocked, ButtonStepRequest(HeroBehaviorSlot.Healing,
                LocalizedContent.Ui(TutorialTextKeys.HealUnlocked,
                    "¡Te golpearon! Se desbloqueó CURAR ({0}): úsala en tu turno cuando te falte vida."),
                GameplayHotkey.Heal));
        }

        private void OnShopItemPurchased(params object[] args)
        {
            if (_step != TutorialStep.Shop) return;

            // C se re-abre (Uncleared, NO Cleared: los ObjectStates intactos hacen
            // que el re-entry respawnee a los 2 enemigos con su HP guardado).
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                dungeon.SetRoomState(_roomC, RoomState.Uncleared);
                dungeon.ResyncDoorVisuals(_roomB);
                dungeon.ResyncDoorVisuals(_roomD);
            }

            // Antes de mandarlo de vuelta: se reveló el inventario — señalarlo.
            ShowBackpackTeach();
        }

        private void ShowBackpackTeach()
        {
            _step = TutorialStep.BackpackTeach;
            UnlockFrameIcon(CharacterFrameIcon.Backpack);
            ResolveCharacterFrame()?.SetPinned(true);
            ShowStep(TutorialStep.BackpackTeach, IconStepRequest(CharacterFrameIcon.Backpack,
                LocalizedContent.Ui(TutorialTextKeys.BackpackIcon,
                    "Se desbloqueó tu INVENTARIO: tu compra vive aquí, junto a todo lo que consigas en la run.")),
                () =>
                {
                    ResolveCharacterFrame()?.SetPinned(false);
                    ShowShopPurchasedStep();
                });
        }

        private void ShowShopPurchasedStep()
        {
            _step = TutorialStep.Combat2Prep;
            ShowStep(TutorialStep.Combat2Prep, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.WorldPosition,
                WorldPosition = ResolveDoorPosition(_roomD, _roomB),
                Text = LocalizedContent.Ui(TutorialTextKeys.ShopPurchased,
                    "¡Mejora comprada! Ahora estás preparado: vuelve por donde viniste y acaba con esos enemigos."),
            });
        }

        // Abrió la mesa (interact sobre el altar) → explicar cómo se encanta.
        // BlockUntilContinue: un click cierra el popup y deja usar la UI de la
        // mesa (feedback playtest: sin esto quedaba pegado tapando la pantalla).
        private void OnEnchantmentAltarActivated(params object[] args)
        {
            if (_step != TutorialStep.Enchant) return;

            ShowStep(TutorialStep.Enchant, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.EnchantTable,
                    "Elige un dado y un cupo, y confirma: el encantamiento sale al azar — y puede ser malo."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, ShowEnchantRerollHint);
        }

        // Segundo popup de la mesa: el resultado es una apuesta, y el reemplazo
        // (pagando cada vez más) es la forma de arreglar uno malo. Solo se explica —
        // la economía del tutorial alcanza para exactamente un encantamiento.
        private void ShowEnchantRerollHint()
        {
            ShowStep(TutorialStep.Enchant, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.EnchantReroll,
                    "Si no te gusta el resultado, puedes reemplazarlo pagando de nuevo: cada reemplazo cuesta más oro."),
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, () => Overlay()?.Hide());
        }

        private void OnEnchantmentApplied(params object[] args)
        {
            if (_step != TutorialStep.Enchant) return;

            // Encantó: E se clarea y su puerta exit se abre.
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                dungeon.SetRoomState(_roomE, RoomState.Cleared);
                dungeon.ResyncDoorVisuals(_roomE);
            }

            // Primero guiar al botón de cerrar la mesa; la puerta de salida se
            // señala recién al cerrarse el panel (feedback playtest: la puerta
            // no se ve con la mesa abierta). Sin vista → directo a la puerta.
            _enchantAltarView = UnityEngine.Object.FindFirstObjectByType<EnchantmentAltarView>(
                FindObjectsInactive.Include);
            if (_enchantAltarView == null)
            {
                ShowExitStep();
                return;
            }

            _enchantAltarView.OnPanelClosed += OnEnchantTableClosed;
            _step = TutorialStep.CloseEnchantTable;
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = LocalizedContent.Ui(TutorialTextKeys.EnchantDone,
                    "¡Dado encantado! Ya sabes todo lo que necesitas — cierra la mesa."),
            };
            if (_enchantAltarView.TryGetCloseButtonRect(out var closeRect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = closeRect;
            }
            ShowStep(TutorialStep.CloseEnchantTable, request);
        }

        private void OnEnchantTableClosed()
        {
            if (_enchantAltarView != null)
            {
                _enchantAltarView.OnPanelClosed -= OnEnchantTableClosed;
                _enchantAltarView = null;
            }
            if (_step != TutorialStep.CloseEnchantTable) return;

            // Con la mesa cerrada el marco vuelve a verse: último reveal, la bolsa.
            ShowDiceBagTeach();
        }

        private void ShowDiceBagTeach()
        {
            _step = TutorialStep.DiceBagTeach;
            UnlockFrameIcon(CharacterFrameIcon.DiceBag);
            ResolveCharacterFrame()?.SetPinned(true);
            ShowStep(TutorialStep.DiceBagTeach, IconStepRequest(CharacterFrameIcon.DiceBag,
                LocalizedContent.Ui(TutorialTextKeys.DiceBagIcon,
                    "Tu dado encantado vive en la BOLSA DE DADOS: revisa ahí tus dados y encantamientos.")),
                () =>
                {
                    ResolveCharacterFrame()?.SetPinned(false);
                    ShowExitStep();
                });
        }

        private void ShowExitStep()
        {
            _step = TutorialStep.Exit;
            ShowStep(TutorialStep.Exit, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.WorldPosition,
                WorldPosition = ResolveExitDoorPosition(_roomE),
                Text = LocalizedContent.Ui(TutorialTextKeys.Exit,
                    "Cruza la puerta señalada para empezar tu aventura de verdad."),
            });
        }

        private void OnFloorExitRequested(params object[] args)
        {
            if (_step != TutorialStep.Exit) return;
            _step = TutorialStep.Done;
            CoroutineHost.Run(EndTutorialRoutine());
        }

        // ==================================================================
        // Teardown → fresh run
        // ==================================================================

        private IEnumerator EndTutorialRoutine()
        {
            Debug.Log(LogPrefix + "Tutorial completado — teardown a fresh run.");

            Overlay()?.Hide();

            if (ServiceLocator.TryGetService<IMetaProgressionService>(out var meta))
                meta.MarkTutorialCompleted();

            // Bloquear input y tapar el swap con la pantalla de carga (mismo patrón
            // que FloorProgressionService.TransitionRoutine).
            ServiceLocator.TryGetService<IPhaseService>(out var phase);
            phase?.ReplacePhase(GamePhase.Loading);
            ServiceLocator.TryGetService<ILoadingScreenService>(out var loading);
            loading?.Show();
            loading?.ReportProgress(0.25f);
            yield return null;

            // Overrides de servicios GLOBAL — se limpian a mano (ClearScope no los toca).
            if (ServiceLocator.TryGetService<IShopManagerService>(out var shop))
                shop.ClearTutorialOverride();

            // EndRun → ClearScope(Run): descarta inventario (item), RuntimeDiceBag
            // (encantamiento), combo passives, dungeon del tutorial y ESTE controller.
            // runCompleted: true — la run del tutorial es descartable; borra el save
            // para que el Continue del menú no resuma un tutorial a medio desarmar.
            RunBootstrapper.EndRun(_runId, runCompleted: true);
            loading?.ReportProgress(0.5f);

            // Reset Global explícito: el oro no es run-scoped.
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy))
                economy.ResetTo(_config.PostTutorialStartingGold);

            // De vuelta al menú, directo a la selección de clase para la run real.
            PostTutorialRouting.AutoOpenClassSelection = true;
            var op = SceneManager.LoadSceneAsync("01_MainMenu");
            while (op != null && !op.isDone)
            {
                loading?.ReportProgress(0.5f + op.progress * 0.5f);
                yield return null;
            }
            loading?.ReportProgress(1f);
        }

        // ==================================================================
        // Overlay helpers
        // ==================================================================

        private void ShowMovementStep()
        {
            ShowStep(TutorialStep.Movement, new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.WorldPosition,
                WorldPosition = ResolveDoorPosition(_roomA, _roomB),
                Text = LocalizedContent.Ui(TutorialTextKeys.Movement,
                    "Haz clic en una casilla para moverte. Camina hasta la puerta señalada para salir de la sala."),
            });
        }

        private void ShowStep(TutorialStep step, TutorialStepDisplayRequest request, Action onContinue = null)
        {
            var overlay = Overlay();
            if (overlay == null)
            {
                Debug.LogWarning(LogPrefix + $"Sin ITutorialOverlayService — paso {step} sin señalización.");
                // Sin overlay no hay click de continue — avanzar igual para no
                // dejar la step machine colgada en un paso BlockUntilContinue.
                onContinue?.Invoke();
                return;
            }
            overlay.Show(request, onContinue);
        }

        private static ITutorialOverlayService Overlay()
        {
            ServiceLocator.TryGetService<ITutorialOverlayService>(out var overlay);
            return overlay;
        }

        /// <summary>
        /// Paso anclado a un botón del HUD de combate. Si el HUD no está disponible
        /// todavía, degrada a popup centrado (AnchorKind.None con UiTarget null).
        /// </summary>
        private TutorialStepDisplayRequest ButtonStepRequest(
            HeroBehaviorSlot slot, string text, GameplayHotkey hotkey)
        {
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = text,
                HotkeyHint = hotkey,
            };

            var view = UnityEngine.Object.FindFirstObjectByType<PlayerActionButtonsView>(
                FindObjectsInactive.Include);
            if (view != null && view.TryGetButtonRect(slot, out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            return request;
        }

        /// <summary>
        /// Paso no bloqueante anclado al botón Roll/Reroll del HUD. Degrada a
        /// popup centrado si el HUD no está disponible.
        /// </summary>
        private TutorialStepDisplayRequest RollButtonRequest(string text)
        {
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = text,
            };
            var hud = FindCombatHud();
            if (hud != null && hud.TryGetRollButtonRect(out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            return request;
        }

        // El contrato vive en Canvas_PlayerStatus como SlidingDrawer; abrirlo cierra
        // los otros drawers solo (registro Live de SlidingDrawer).
        private void OpenContractDrawerForRoll()
        {
            var drawer = FindContractDrawer();
            if (drawer == null || drawer.IsOpen) return;
            drawer.Open();
            _openedContractDrawer = true;
        }

        private void CloseContractDrawerIfWeOpenedIt()
        {
            if (!_openedContractDrawer) return;
            _openedContractDrawer = false;
            var drawer = FindContractDrawer();
            if (drawer != null && drawer.IsOpen) drawer.Close();
        }

        private static SlidingDrawer FindContractDrawer()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<Rollgeon.UI.HUD.Contract.ContractDrawerView>(
                FindObjectsInactive.Include);
            return view != null ? view.GetComponent<SlidingDrawer>() : null;
        }

        /// <summary>
        /// Paso anclado a la zona de dados del HUD de combate — el recorte abarca
        /// también los botones Roll y Confirmar (feedback playtest). Degrada a
        /// popup centrado si el HUD no está disponible.
        /// </summary>
        private TutorialStepDisplayRequest DiceZoneRequest(string text)
        {
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = text,
            };

            var hud = FindCombatHud();
            if (hud != null && hud.TryGetDiceZoneRect(out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;

                var extras = new List<RectTransform>(2);
                if (hud.TryGetRollButtonRect(out var rollRect)) extras.Add(rollRect);
                if (hud.TryGetConfirmButtonRect(out var confirmRect)) extras.Add(confirmRect);
                if (extras.Count > 0) request.UiTargetsExtra = extras.ToArray();
            }
            return request;
        }

        private static CombatHUDView FindCombatHud()
            => UnityEngine.Object.FindFirstObjectByType<CombatHUDView>(FindObjectsInactive.Include);

        // ==================================================================
        // Marco del jugador — reveal progresivo de íconos
        // ==================================================================

        /// <summary>Resuelve (y cachea) el controller del marco. El canvas puede
        /// re-instanciarse entre salas — el fake-null de Unity dispara el re-find.</summary>
        private CharacterFrameController ResolveCharacterFrame()
        {
            if (_characterFrame == null)
                _characterFrame = UnityEngine.Object.FindFirstObjectByType<CharacterFrameController>(
                    FindObjectsInactive.Include);
            return _characterFrame;
        }

        private void UnlockFrameIcon(CharacterFrameIcon icon)
        {
            _unlockedIcons.Add(icon);
            ResolveCharacterFrame()?.SetIconLocked(icon, false);
        }

        /// <summary>Aplica el estado de reveal vigente: lockea todo lo que el
        /// recorrido todavía no desbloqueó. Idempotente.</summary>
        private void ApplyFrameIconLocks()
        {
            var frame = ResolveCharacterFrame();
            if (frame == null) return;
            foreach (CharacterFrameIcon icon in Enum.GetValues(typeof(CharacterFrameIcon)))
                frame.SetIconLocked(icon, !_unlockedIcons.Contains(icon));
        }

        /// <summary>Paso bloqueante anclado a un ícono del marco del jugador.
        /// Degrada a popup centrado si el marco no está disponible.</summary>
        private TutorialStepDisplayRequest IconStepRequest(CharacterFrameIcon icon, string text)
        {
            var request = new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.None,
                Text = text,
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            };
            var frame = ResolveCharacterFrame();
            if (frame != null && frame.TryGetIconRect(icon, out var rect))
            {
                request.AnchorKind = TutorialAnchorKind.RectTransform;
                request.UiTarget = rect;
            }
            return request;
        }

        private static bool TryGetPlayerGuid(out Guid guid)
        {
            guid = Guid.Empty;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player) || player == null)
                return false;
            guid = player.PlayerGuid;
            return true;
        }

        /// <summary>Primer enemigo spawneado de la sala (para el spotlight). Los
        /// spawns ya corrieron: CombatHandoffService maneja OnCombatTriggered antes
        /// que este controller (suscripto último).</summary>
        private static Guid FirstEnemyOf(Guid roomId)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return Guid.Empty;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)) return Guid.Empty;
            if (room.SpawnedEnemies == null || room.SpawnedEnemies.Count == 0) return Guid.Empty;
            return room.SpawnedEnemies[0];
        }

        // ==================================================================
        // World anchors
        // ==================================================================

        private static Guid FindInstanceByCell(IDungeonService dungeon, Vector2Int cell)
        {
            foreach (var kv in dungeon.GetAllRoomInstances())
            {
                if (kv.Value.GridCell == cell) return kv.Key;
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Marca como salida de piso una puerta sin vecino de la sala E — la opuesta
        /// a la entrada si está autorada, sino cualquier slot libre con DoorRoot.
        /// ConfigureDoorSlots ya corrió (puerta desactivada + wall plug activo), así
        /// que acá revertimos ese estado; SyncDoorVisualStates la maneja como exit
        /// genérica de ahí en más (Open al pasar E a Cleared).
        /// </summary>
        private void MarkTutorialExitDoor(IDungeonService dungeon)
        {
            if (!dungeon.GetAllRoomInstances().TryGetValue(_roomE, out var room)
                || room.SpawnedPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "Sala E sin prefab — no se puede marcar la puerta exit.");
                return;
            }

            var layout = room.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout?.DoorSlots == null) return;

            DoorDirection entrance = DoorDirection.North;
            foreach (var dir in room.Connections.Keys) { entrance = dir; break; }
            var preferred = entrance.Opposite();

            DoorSlotRef chosen = null;
            foreach (var slot in layout.DoorSlots)
            {
                if (slot?.DoorRoot == null) continue;
                if (room.Connections.ContainsKey(slot.Direction)) continue;
                if (slot.Direction == preferred) { chosen = slot; break; }
                chosen ??= slot;
            }

            if (chosen == null)
            {
                Debug.LogWarning(LogPrefix + "Sala E sin DoorSlot libre con DoorRoot — sin puerta exit.");
                return;
            }

            var controller = chosen.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
            if (controller == null)
            {
                Debug.LogWarning(LogPrefix + $"DoorSlot {chosen.Direction} de E sin DoorController — sin puerta exit.");
                return;
            }

            controller.IsExit = true;
            controller.OwnerRoomInstanceId = room.InstanceId;
            if (chosen.WallPlug != null) chosen.WallPlug.SetActive(false);
            chosen.DoorRoot.SetActive(true);
        }

        /// <summary>Posición world de la puerta de <paramref name="fromRoom"/> que da a <paramref name="toRoom"/>.</summary>
        private Vector3 ResolveDoorPosition(Guid fromRoom, Guid toRoom)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return Vector3.zero;
            var all = dungeon.GetAllRoomInstances();
            if (!all.TryGetValue(fromRoom, out var from) || !all.TryGetValue(toRoom, out var to))
                return Vector3.zero;

            DoorDirection? direction = null;
            foreach (var kv in from.Connections)
            {
                if (kv.Value == toRoom) { direction = kv.Key; break; }
            }
            if (direction == null) return from.WorldPosition;

            var layout = from.SpawnedPrefab != null ? from.SpawnedPrefab.GetComponent<RoomLayout>() : null;
            if (layout?.DoorSlots != null)
            {
                foreach (var slot in layout.DoorSlots)
                {
                    if (slot == null || slot.Direction != direction.Value) continue;
                    if (slot.Anchor != null) return slot.Anchor.position;
                    if (slot.DoorRoot != null) return slot.DoorRoot.transform.position;
                }
            }

            // Fallback: punto medio entre ambas salas (la puerta está en el borde).
            return (from.WorldPosition + to.WorldPosition) * 0.5f;
        }

        private Vector3 ResolveExitDoorPosition(Guid roomId)
        {
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                && dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)
                && room.SpawnedPrefab != null)
            {
                foreach (var controller in room.SpawnedPrefab.GetComponentsInChildren<DoorController>(includeInactive: true))
                {
                    if (controller.IsExit) return controller.transform.position;
                }
                return room.WorldPosition;
            }
            return Vector3.zero;
        }

        // El pedestal/altar los spawnean ShopManagerService / EnchantmentRoomService
        // en su propio handler de OnRoomEntered — suscriptos ANTES que este controller,
        // así que al llegar acá ya existen en escena.
        private Vector3 ResolvePedestalPosition()
        {
            var pedestal = UnityEngine.Object.FindFirstObjectByType<ShopItemPedestalInteractable>();
            return pedestal != null ? pedestal.transform.position : Vector3.zero;
        }

        private Vector3 ResolveAltarPosition()
        {
            var altar = UnityEngine.Object.FindFirstObjectByType<EnchantmentAltarInteractable>();
            return altar != null ? altar.transform.position : Vector3.zero;
        }
    }
}
