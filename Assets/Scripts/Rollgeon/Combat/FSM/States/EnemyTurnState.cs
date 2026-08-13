using System;
using Patterns;
using Patterns.FSM;

namespace Rollgeon.Combat.FSM.States
{
    /// <summary>
    /// Turno de un enemy. Dispara <c>OnTurnStarted</c>, invoca el
    /// <see cref="CombatContext.EnemyActionHandler"/>, y espera <c>EnemyDone</c>.
    /// Plan §3.2 / §4.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AI stub.</b> En el FP no hay AI real; el handler es provisto por la
    /// scene/test. Si el handler dispara <c>EnemyDone</c> sincrono, la FSM
    /// encola el input (reentrancy-safe) y termina el turno en el mismo frame.
    /// </para>
    /// <para>
    /// <b>Event ordering (plan R9).</b> Igual que <see cref="PlayerTurnState"/>:
    /// <c>OnTurnFinished</c> con Guid cacheado BEFORE <c>Advance()</c>.
    /// </para>
    /// <para>
    /// <b>Grace period (CNF-006).</b> <c>OnTurnStarted</c> sigue disparando
    /// inmediato en <c>Enter</c> — sólo se difiere la invocación del
    /// <see cref="CombatContext.EnemyActionHandler"/>, para darle al jugador un
    /// instante de lectura antes de que el enemigo actúe. Si
    /// <see cref="CombatContext.EnemyActionDelaySeconds"/> es <c>&lt;= 0</c> o no
    /// hay <see cref="CombatContext.DeltaTimeProvider"/> inyectado, el handler
    /// corre síncrono en <c>Enter</c> — comportamiento legacy byte-idéntico
    /// (necesario para los tests que no pasan estos parámetros opcionales).
    /// </para>
    /// </remarks>
    public sealed class EnemyTurnState : BaseState<CombatContext, CombatInput>
    {
        internal PlayerTurnState Player;
        internal CombatExitState ExitRef;
        // Self-ref para cadenas de enemies (enemy A -> enemy B sin player entre medio).
        internal EnemyTurnState Self;

        private Guid _actingGuid;

        // Countdown del grace period (CNF-006). _handlerPending sólo es true
        // mientras estamos esperando para invocar EnemyActionHandler.
        private float _delayRemaining;
        private bool _handlerPending;

        public EnemyTurnState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            // Cachear el actor ANTES de invocar el handler — el handler puede
            // gatillar side effects que muten TurnOrder.
            _actingGuid = Context.TurnOrder.Current;
            EventManager.Trigger(EventName.OnTurnStarted, _actingGuid);

            float delay = Context.EnemyActionDelaySeconds;
            if (delay <= 0f || Context.DeltaTimeProvider == null)
            {
                // Path legacy: sin grace period configurado (o sin reloj inyectado)
                // el handler corre sincrono, igual que antes de CNF-006.
                // [STUB] — T99/T103 will provide real AI via delegate injection.
                Context.EnemyActionHandler?.Invoke(_actingGuid);
                return;
            }

            _delayRemaining = delay;
            _handlerPending = true;
        }

        public override void Update()
        {
            if (!_handlerPending) return;

            _delayRemaining -= Context.DeltaTimeProvider();
            if (_delayRemaining > 0f) return;

            // Reset ANTES de invocar — el handler puede disparar EnemyDone
            // reentrante (via SendInput encolado), y no queremos que un update
            // posterior en el mismo frame re-dispare el handler.
            _handlerPending = false;
            Context.EnemyActionHandler?.Invoke(_actingGuid);
        }

        public override void Exit(CombatInput input)
        {
            // Si CombatEnded llega durante el grace period, invalidamos el
            // countdown — el handler jamas debe correr para un combate ya cerrado.
            _handlerPending = false;

            // OnTurnFinished con Guid cacheado — cursor aun no cambio.
            EventManager.Trigger(EventName.OnTurnFinished, _actingGuid);
            // Advance solo si el combate sigue (ver PlayerTurnState.Exit).
            if (input != CombatInput.CombatEnded)
            {
                Context.TurnOrder.Advance();
            }
        }

        public override bool CheckInput(CombatInput input, out BaseState<CombatContext, CombatInput> next)
        {
            switch (input)
            {
                case CombatInput.EnemyDone:
                    // Peek del proximo guid para decidir target antes de Exit (donde Advance corre).
                    var order = Context.TurnOrder.OrderForRound;
                    if (order == null || order.Count == 0)
                    {
                        next = ExitRef;
                        return true;
                    }

                    int curIndex = IndexOf(order, Context.TurnOrder.Current);
                    if (curIndex < 0)
                    {
                        next = Self;
                        return true;
                    }
                    int nextIndex = (curIndex + 1) % order.Count;
                    Guid nextGuid = order[nextIndex];

                    next = (nextGuid == Context.PlayerId)
                        ? (BaseState<CombatContext, CombatInput>)Player
                        : Self;
                    return true;

                case CombatInput.CombatEnded:
                    next = ExitRef;
                    return true;

                default:
                    next = null;
                    return false;
            }
        }

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<Guid> list, Guid g)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == g) return i;
            }
            return -1;
        }
    }
}
