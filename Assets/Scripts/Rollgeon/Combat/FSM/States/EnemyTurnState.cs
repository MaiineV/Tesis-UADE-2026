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
    /// </remarks>
    public sealed class EnemyTurnState : BaseState<CombatContext, CombatInput>
    {
        internal PlayerTurnState Player;
        internal CombatExitState ExitRef;
        // Self-ref para cadenas de enemies (enemy A -> enemy B sin player entre medio).
        internal EnemyTurnState Self;

        private Guid _actingGuid;

        public EnemyTurnState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            // Cachear el actor ANTES de invocar el handler — el handler puede
            // gatillar side effects que muten TurnOrder.
            _actingGuid = Context.TurnOrder.Current;
            EventManager.Trigger(EventName.OnTurnStarted, _actingGuid);

            // [STUB] — T99/T103 will provide real AI via delegate injection.
            // Para el FP, el delegate puede ser null (tests) o disparar EnemyDone
            // sincrono. Reentrancy-safe: StateMachine encola inputs durante
            // dispatch y los drenara tras que Enter complete.
            Context.EnemyActionHandler?.Invoke(_actingGuid);
        }

        public override void Exit(CombatInput input)
        {
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
