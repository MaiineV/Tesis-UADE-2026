using Patterns;
using Patterns.FSM;

namespace Rollgeon.Combat.FSM.States
{
    /// <summary>
    /// Estado inicial: construye la cola de turno y decide el primer actor.
    /// Plan §3.2 / §4.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enter.</b> Dispara <c>OnCombatStart(roomInstanceId)</c>, llama
    /// <c>TurnOrder.BuildForCombat(CachedParticipants)</c> (que a su vez
    /// dispara <c>OnTurnQueueBuilt</c>).
    /// </para>
    /// <para>
    /// <b>CheckInput(StartCombat).</b> Si <c>TurnOrder.Current == PlayerId</c>
    /// transiciona a <see cref="PlayerTurnState"/>; si no, a
    /// <see cref="EnemyTurnState"/>. Responde tambien a <c>CombatEnded</c>
    /// (aborto temprano) transicionando a <see cref="CombatExitState"/>.
    /// </para>
    /// </remarks>
    public sealed class CombatEnterState : BaseState<CombatContext, CombatInput>
    {
        internal PlayerTurnState Player;
        internal EnemyTurnState Enemy;
        internal CombatExitState Exit;

        public CombatEnterState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            // 1) OnCombatStart BEFORE BuildForCombat — listeners de "combat init"
            //    (achievements, stats tracking) se suscriben al evento y esperan
            //    que corra antes del turn queue wiring.
            EventManager.Trigger(EventName.OnCombatStart, Context.RoomInstanceId);

            if (Context.CachedParticipants == null || Context.CachedParticipants.Count == 0)
            {
                UnityEngine.Debug.LogError(
                    "[CombatEnterState] CachedParticipants is null/empty. " +
                    "Call CombatTurnFSM.SetParticipants(...) before Start().");
                return;
            }

            // BuildForCombat internamente dispara OnTurnQueueBuilt (T100c).
            Context.TurnOrder.BuildForCombat(Context.CachedParticipants);
        }

        public override bool CheckInput(CombatInput input, out BaseState<CombatContext, CombatInput> next)
        {
            switch (input)
            {
                case CombatInput.StartCombat:
                    next = (Context.TurnOrder.Current == Context.PlayerId)
                        ? (BaseState<CombatContext, CombatInput>)Player
                        : Enemy;
                    return true;

                case CombatInput.CombatEnded:
                    next = Exit;
                    return true;

                default:
                    next = null;
                    return false;
            }
        }
    }
}
