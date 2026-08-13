using Patterns;
using Patterns.FSM;
using Rollgeon.Combat.Resume;

namespace Rollgeon.Combat.FSM.States
{
    /// <summary>
    /// Estado inicial: construye la cola de turno y decide el primer actor.
    /// Plan §3.2 / §4.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enter.</b> Dispara <c>OnCombatStart(roomInstanceId)</c>, llama
    /// <c>TurnOrder.BuildForCombat(CachedParticipants, Context.PlayerId)</c> (que a
    /// su vez dispara <c>OnTurnQueueBuilt</c>). Pasar <c>PlayerId</c> como
    /// <c>priorityGuid</c> es la política CNF-006 ("el jugador siempre tiene el
    /// primer turno del combate") — no hay flag de config, es incondicional.
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
        internal CombatExitState ExitRef;

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

            // Resume desde save (#0028 Fase 3): si hay estado de combate stageado para esta
            // sala, restaura la cola/cursor/round/energía exactos y NO armamos una fresca.
            // El coordinator ya filtró la cola a los participantes vivos.
            if (ServiceLocator.TryGetService<ICombatResumeCoordinator>(out var resume)
                && resume != null
                && resume.TryBeginResume(Context.TurnOrder, Context.CachedParticipants, Context.PlayerId))
            {
                return;
            }

            // BuildForCombat internamente dispara OnTurnQueueBuilt (T100c).
            // priorityGuid = PlayerId → CNF-006, el player siempre abre la cola.
            Context.TurnOrder.BuildForCombat(Context.CachedParticipants, Context.PlayerId);
        }

        public override bool CheckInput(CombatInput input, out BaseState<CombatContext, CombatInput> next)
        {
            switch (input)
            {
                case CombatInput.StartCombat:
                    // Remark (CNF-006): con el player forzado al frente de la cola en
                    // Enter, la rama Enemy es teóricamente inalcanzable mientras el
                    // player esté entre los participantes — se deja como fallback
                    // defensivo (ej. combates sin player, tests de FSM aislada).
                    next = (Context.TurnOrder.Current == Context.PlayerId)
                        ? (BaseState<CombatContext, CombatInput>)Player
                        : Enemy;
                    return true;

                case CombatInput.CombatEnded:
                    next = ExitRef;
                    return true;

                default:
                    next = null;
                    return false;
            }
        }
    }
}
