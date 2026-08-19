using System;
using System.Collections.Generic;  // IReadOnlyList<Guid>
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Status;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.FSM
{
    /// <summary>
    /// MonoBehaviour driver de <see cref="CombatTurnFSM"/>. Resuelve servicios
    /// via <see cref="ServiceLocator"/>, crea el contexto + FSM, y expone API
    /// publica para UI/scene scripts.
    /// Plan §3.3 / §4.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Single instance.</b> No instanciar dos controllers en la misma
    /// scene — cada uno se suscribe a <c>OnOverlayPushed/Popped</c> y el freeze
    /// flag es local; duplicados no comparten estado y el segundo captura
    /// overlays que no le corresponden.
    /// </para>
    /// <para>
    /// <b>Freeze por overlay.</b> Hook preparatorio: <c>OnOverlayPushed</c> setea
    /// <c>_phaseFrozen = true</c> y los ticks <c>Update/LateUpdate/FixedUpdate</c>
    /// saltean el tick de la FSM. En este worktree ningun publisher emite
    /// <c>OnOverlayPushed</c> — el flag nace <c>false</c> y solo cambia por tests
    /// o por el futuro <c>IPhaseService</c>.
    /// </para>
    /// </remarks>
    public sealed class CombatController : MonoBehaviour
    {
        /// <summary>
        /// Grace period (CNF-006) usado cuando no hay <see cref="RulesetSO"/>
        /// resuelto, o cuando el asset tiene <c>TurnOrder.EnemyActionDelaySeconds</c>
        /// en 0 (ej. un ruleset viejo, guardado antes de que este campo existiera).
        /// </summary>
        public const float DefaultEnemyActionDelaySeconds = 0.8f;

        [Required]
        [SerializeField]
        [InfoBox("Anchor al ServiceBootstrapSO global. No se usa runtime — solo valida que " +
                 "el bootstrap haya corrido antes de arrancar el combate.")]
        private ServiceBootstrapSO _bootstrap;

        [SerializeField, ReadOnly]
        [InfoBox("Debug-only: refleja el player activo. Nunca se serializa.")]
        private string _playerIdDebug;

        [SerializeField, ReadOnly]
        [InfoBox("Debug-only: estado actual de la FSM (nombre de la clase).")]
        private string _currentStateDebug;

        private TurnOrderService _turnOrder;
        private TurnManager _turnManager;
        private IRollPoolService _rolls;

        private CombatTurnFSM _fsm;
        private CombatContext _context;

        private EventManager.EventReceiver _onOverlayPushedHandler;
        private EventManager.EventReceiver _onOverlayPoppedHandler;

        // Skip de turno por stun. Vive mientras el controller exista (no per-combate):
        // OnTurnStarted solo sale con un combate corriendo, y el skipper es inerte si
        // IStunService no esta registrado.
        private StunTurnSkipper _stunSkipper;

        // Freeze flag: stub. Ver class remarks.
        private bool _phaseFrozen;

        /// <summary>Se dispara tras entrar a <see cref="CombatExitState"/> con el outcome final.</summary>
        public event Action<CombatOutcome> OnCombatFinished;

        /// <summary>FSM subyacente. Null antes de <see cref="StartCombat"/>.</summary>
        public CombatTurnFSM FSM => _fsm;

        /// <summary><c>true</c> si el freeze stub esta activo (overlay push).</summary>
        public bool IsFrozen => _phaseFrozen;

        // ======================================================================
        // Unity lifecycle
        // ======================================================================

        private void Awake()
        {
            if (_bootstrap == null)
            {
                Debug.LogError(
                    "[CombatController] _bootstrap anchor es null. Asigna el ServiceBootstrapSO " +
                    "en el inspector. El combate no arrancara.",
                    this);
                enabled = false;
                return;
            }

            // Resolver servicios. Si faltan, logueamos y dejamos el controller inerte —
            // no queremos NullRef runtime.
            if (!ServiceLocator.TryGetService<TurnOrderService>(out _turnOrder))
            {
                Debug.LogError("[CombatController] TurnOrderService no esta registrado. " +
                               "Revisa que TurnOrderServiceBootstrap este en ServiceBootstrapSO.ExtraServices.",
                               this);
            }
            if (!ServiceLocator.TryGetService<TurnManager>(out _turnManager))
            {
                Debug.LogWarning("[CombatController] TurnManager no registrado — action economy deshabilitado.", this);
            }
            if (!ServiceLocator.TryGetService<IRollPoolService>(out _rolls))
            {
                Debug.LogError("[CombatController] IRollPoolService no esta registrado. " +
                               "Revisa que RollPoolServiceBootstrap este en ServiceBootstrapSO.ExtraServices.",
                               this);
            }

            // Suscripcion al freeze hook (stub — ningun publisher emite aun).
            _onOverlayPushedHandler = OnOverlayPushed;
            _onOverlayPoppedHandler = OnOverlayPopped;
            EventManager.Subscribe(EventName.OnOverlayPushed, _onOverlayPushedHandler);
            EventManager.Subscribe(EventName.OnOverlayPopped, _onOverlayPoppedHandler);

            // Stun: si el actor que arranca turno esta stuneado, se consume 1 turno de stun y
            // el turno se cierra por el input normal. IStunService se resuelve lazy porque su
            // bootstrap puede correr despues de este Awake; si no existe (escenas viejas,
            // tests sin bootstrap) el skipper queda inerte y el flujo de turnos no cambia.
            _stunSkipper = new StunTurnSkipper(
                ResolveStunService,
                () => _context?.PlayerId ?? Guid.Empty,
                EndPlayerTurn,
                SendEnemyDone);
            _stunSkipper.Attach();

            // Expone este controller como ICombatStarter / ICombatSignaller para que
            // CombatHandoffService + RunController (scope Run, registrados en
            // GameplayBootstrapper.Start) los resuelvan sin stubs. AddService es upsert
            // — si el user vuelve al menu y arranca otra run, Awake del nuevo
            // CombatController sobrescribe la entry sin romper.
            var adapter = new CombatControllerAdapter(this);
            ServiceLocator.AddService<ICombatStarter>(adapter, ServiceScope.Global);
            ServiceLocator.AddService<ICombatSignaller>(adapter, ServiceScope.Global);
            ServiceLocator.AddService<IPlayerCombatActions>(adapter, ServiceScope.Global);
        }

        private void OnDestroy()
        {
            if (_onOverlayPushedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnOverlayPushed, _onOverlayPushedHandler);
                _onOverlayPushedHandler = null;
            }
            if (_onOverlayPoppedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnOverlayPopped, _onOverlayPoppedHandler);
                _onOverlayPoppedHandler = null;
            }

            _stunSkipper?.Dispose();
            _stunSkipper = null;

            if (_fsm != null && _fsm.IsRunning)
            {
                _fsm.Stop();
            }
            _fsm = null;
            _context = null;

            // Limpia las entries para no dejar refs apuntando a un MonoBehaviour destruido
            // (p.ej. al hacer LoadScene("01_MainMenu") desde Victory/Defeat).
            ServiceLocator.RemoveService<ICombatStarter>();
            ServiceLocator.RemoveService<ICombatSignaller>();
        }

        private void Update()
        {
            if (_phaseFrozen) return;
            _fsm?.Update();
            UpdateDebugLabels();
        }

        private void LateUpdate()
        {
            if (_phaseFrozen) return;
            _fsm?.LateUpdate();
        }

        private void FixedUpdate()
        {
            if (_phaseFrozen) return;
            _fsm?.FixedUpdate();
        }

        private void UpdateDebugLabels()
        {
            if (_fsm != null && _fsm.Current != null)
            {
                _currentStateDebug = _fsm.Current.Name;
            }
        }

        // ======================================================================
        // API publica
        // ======================================================================

        /// <summary>
        /// Arranca un nuevo combate. Crea el context + FSM, setea participantes, y
        /// dispara <c>StartCombat</c>.
        /// </summary>
        /// <param name="playerId">Guid del player activo.</param>
        /// <param name="participants">Lista de participantes (player + enemies). Debe contener <paramref name="playerId"/>.</param>
        /// <param name="roomInstanceId">Guid del room instance.</param>
        /// <param name="enemyActionHandler">Delegate que la AI del enemy (o test) implementa.</param>
        public void StartCombat(
            Guid playerId,
            IReadOnlyList<Guid> participants,
            Guid roomInstanceId,
            Action<Guid> enemyActionHandler)
        {
            if (_turnOrder == null || _rolls == null)
            {
                Debug.LogError("[CombatController] StartCombat abortado: servicios no resueltos en Awake.", this);
                return;
            }
            if (_fsm != null && _fsm.IsRunning)
            {
                Debug.LogWarning("[CombatController] Ya hay un combate corriendo — ignoro StartCombat.", this);
                return;
            }

            // CNF-006: resolver el grace period del ruleset activo. Un asset viejo
            // sin este campo seteado queda en 0 — en ese caso usamos el default en
            // vez de "sin delay", para no regresar silenciosamente al comportamiento
            // sincrono legacy en runs con rulesets desactualizados.
            float enemyActionDelay = DefaultEnemyActionDelaySeconds;
            if (ServiceLocator.TryGetService<RulesetSO>(out var ruleset) && ruleset != null)
            {
                float configured = Mathf.Max(0f, ruleset.TurnOrder.EnemyActionDelaySeconds);
                if (configured > 0f)
                {
                    enemyActionDelay = configured;
                }
            }

            _context = new CombatContext(
                _turnOrder,
                _turnManager,
                _rolls,
                playerId,
                roomInstanceId,
                enemyActionHandler,
                enemyActionDelaySeconds: enemyActionDelay,
                // Solo lo consume el countdown del grace period de EnemyTurnState:
                // multiplicar acá hace que el pacing enemigo siga al game speed en
                // vivo (incluso si se cambia desde la pausa a mitad de combate).
                deltaTimeProvider: () => Time.deltaTime * Rollgeon.Timing.GameSpeedPrefs.Multiplier);

            _fsm = new CombatTurnFSM(_context);
            _fsm.OnFinished += HandleFsmFinished;
            _fsm.SetParticipants(participants);

            _playerIdDebug = playerId.ToString();

            _fsm.Start();
            _fsm.SendInput(CombatInput.StartCombat);
        }

        /// <summary>Equivalente a <c>SendInput(PlayerActionDone)</c>.</summary>
        public void SendPlayerAction() => _fsm?.SendInput(CombatInput.PlayerActionDone);

        /// <summary>
        /// Cierra el turno del player. Unica via legitima (Revision 2) —
        /// la UI llama esto al clickear "End Turn".
        /// </summary>
        public void EndPlayerTurn() => _fsm?.SendInput(CombatInput.PlayerEndTurn);

        /// <summary>Anuncia que el enemy termino su turno (desde AI / test).</summary>
        public void SendEnemyDone() => _fsm?.SendInput(CombatInput.EnemyDone);

        /// <summary>
        /// Setea <see cref="CombatContext.PendingOutcome"/> y dispara <c>CombatEnded</c>.
        /// El combate transiciona a <see cref="CombatExitState"/>.
        /// </summary>
        public void NotifyCombatEnded(CombatOutcome outcome)
        {
            if (_fsm == null || _context == null) return;
            _context.PendingOutcome = outcome;
            _fsm.SendInput(CombatInput.CombatEnded);
        }

        /// <summary>Atajo: <c>NotifyCombatEnded(Aborted)</c>.</summary>
        public void AbortCombat() => NotifyCombatEnded(CombatOutcome.Aborted);

        /// <summary>Passthrough al FSM para tests o casos edge (overlay simulation).</summary>
        public void SendInput(CombatInput input) => _fsm?.SendInput(input);

        // ======================================================================
        // Internals
        // ======================================================================

        private void HandleFsmFinished(CombatOutcome outcome)
        {
            // CombatExitState delega al caller la decisión de cerrar la FSM.
            // Sin este teardown, _fsm.IsRunning queda en true entre combates y
            // el próximo StartCombat aborta con "Ya hay un combate corriendo".
            if (_fsm != null)
            {
                _fsm.OnFinished -= HandleFsmFinished;
                if (_fsm.IsRunning) _fsm.Stop();
            }
            _fsm = null;
            _context = null;

            OnCombatFinished?.Invoke(outcome);
        }

        /// <summary>
        /// Resolucion lazy (y sin cache) del <see cref="IStunService"/>: el bootstrap puede
        /// registrarlo despues del Awake de este controller, y en tests el ServiceLocator se
        /// limpia entre casos. Es un lookup de diccionario por turno — no vale cachear.
        /// </summary>
        private static IStunService ResolveStunService()
            => ServiceLocator.TryGetService<IStunService>(out var svc) ? svc : null;

        private void OnOverlayPushed(params object[] args)
        {
            _phaseFrozen = true;
        }

        private void OnOverlayPopped(params object[] args)
        {
            _phaseFrozen = false;
        }
    }
}
