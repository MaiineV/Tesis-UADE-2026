using Patterns.FSM;

namespace Rollgeon.Combat.FSM.States
{
    /// <summary>
    /// Estado terminal. Resetea <c>TurnOrderService</c> y no acepta transiciones
    /// subsiguientes. Plan §3.2 / §4.3 + plan R2.
    /// </summary>
    /// <remarks>
    /// Este estado NO llama <c>Stop()</c> en su <c>Enter</c> ni emite
    /// <c>OnCombatEnd</c>. El <see cref="CombatTurnFSM"/> escucha
    /// <c>OnStateEntered</c> y, si detecta que entro a este estado, dispara
    /// primero el evento <c>OnFinished</c> publico (el caller externo decide si
    /// cerrar la FSM) y recien despues emite <c>OnCombatEnd</c> — ver el porque
    /// del orden en <see cref="CombatTurnFSM"/>.
    /// </remarks>
    public sealed class CombatExitState : BaseState<CombatContext, CombatInput>
    {
        public CombatExitState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            Context.TurnOrder.Reset();
        }

        // No CheckInput override: el estado es terminal. Cualquier input llega
        // y se descarta (return false default de BaseState).
    }
}
