using System;
using System.Collections.Generic;
using Patterns.FSM;
using Rollgeon.Combat.FSM.States;

namespace Rollgeon.Combat.FSM
{
    /// <summary>
    /// Wrapper de conveniencia sobre <see cref="StateMachine{TContext, TInput}"/>
    /// que compone los 4 estados del combate, los wire-a entre si, y expone una
    /// API limpia (<see cref="Start"/>, <see cref="Stop"/>, <see cref="SendInput"/>,
    /// ticks, <see cref="Current"/>).
    /// Plan §3.3 / §4.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Single-thread, strict. Llamadas desde background threads son UB — el
    /// caller es responsable de marshallear al main thread (Unity).
    /// </para>
    /// <para>
    /// [Future-tool] Debug window — expose Current + history buffer for a per-frame visualizer.
    /// </para>
    /// </remarks>
    public sealed class CombatTurnFSM
    {
        private readonly StateMachine<CombatContext, CombatInput> _sm;

        /// <summary>Estado <c>CombatEnter</c> (entry point).</summary>
        public CombatEnterState Enter { get; }
        /// <summary>Estado <c>PlayerTurn</c>.</summary>
        public PlayerTurnState Player { get; }
        /// <summary>Estado <c>EnemyTurn</c>.</summary>
        public EnemyTurnState Enemy { get; }
        /// <summary>Estado <c>CombatExit</c> (terminal).</summary>
        public CombatExitState ExitState { get; }

        /// <summary>Estado activo de la FSM subyacente.</summary>
        public BaseState<CombatContext, CombatInput> Current => _sm.Current;

        /// <summary><c>true</c> entre <c>Start</c> y <c>Stop</c>.</summary>
        public bool IsRunning => _sm.IsRunning;

        /// <summary>Contexto compartido (conveniencia).</summary>
        public CombatContext Context => _sm.Context;

        /// <summary>
        /// Diagnostico: se dispara cuando un input es aceptado y produce una transicion.
        /// </summary>
        public event Action<CombatInput> OnInputAccepted;

        /// <summary>
        /// Se dispara tras entrar a <see cref="CombatExitState"/> con el outcome final.
        /// Consumido por <see cref="CombatController"/> para exponer su propio event publico.
        /// </summary>
        public event Action<CombatOutcome> OnFinished;

        public CombatTurnFSM(CombatContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            Enter = new CombatEnterState(context);
            Player = new PlayerTurnState(context);
            Enemy = new EnemyTurnState(context);
            ExitState = new CombatExitState(context);

            // Wire siblings (set siblings pattern — evita deps circulares en constructor).
            Enter.Player = Player;
            Enter.Enemy = Enemy;
            Enter.ExitRef = ExitState;

            Player.Enemy = Enemy;
            Player.ExitRef = ExitState;
            Player.Self = Player;

            Enemy.Player = Player;
            Enemy.ExitRef = ExitState;
            Enemy.Self = Enemy;

            _sm = new StateMachine<CombatContext, CombatInput>(context, Enter);

            _sm.OnTransition += (from, to, input) => OnInputAccepted?.Invoke(input);
            _sm.OnStateEntered += OnStateEnteredInternal;
        }

        private void OnStateEnteredInternal(BaseState<CombatContext, CombatInput> state)
        {
            if (state is CombatExitState)
            {
                var outcome = Context.PendingOutcome ?? CombatOutcome.Aborted;
                OnFinished?.Invoke(outcome);
            }
        }

        /// <summary>
        /// Cachea la lista de participantes que <see cref="CombatEnterState.Enter"/> pasara
        /// a <c>TurnOrder.BuildForCombat</c>. Debe llamarse antes de <see cref="Start"/>.
        /// </summary>
        public void SetParticipants(IReadOnlyList<Guid> participants)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (participants.Count == 0)
            {
                throw new ArgumentException("participants no puede estar vacia.", nameof(participants));
            }
            Context.CachedParticipants = participants;
        }

        /// <summary>Arranca la FSM en <see cref="CombatEnterState"/> con <c>None</c> input.</summary>
        public void Start() => _sm.Start(CombatInput.None);

        /// <summary>Detiene la FSM (<c>Exit(default)</c> del current state).</summary>
        public void Stop() => _sm.Stop();

        /// <summary>Envia un input (reentrancy-safe via queue).</summary>
        public void SendInput(CombatInput input) => _sm.SendInput(input);

        /// <summary>Tick por frame. No-op si <c>!IsRunning</c>.</summary>
        public void Update() => _sm.Update();

        /// <summary>Tick post-render. No-op si <c>!IsRunning</c>.</summary>
        public void LateUpdate() => _sm.LateUpdate();

        /// <summary>Tick fisico. No-op si <c>!IsRunning</c>.</summary>
        public void FixedUpdate() => _sm.FixedUpdate();
    }
}
