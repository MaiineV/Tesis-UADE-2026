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

        /// <summary>
        /// BUG-078 (guard defensivo): <c>true</c> cuando <see cref="Enter"/> detectó que
        /// <see cref="CombatContext.CachedParticipants"/> no tiene ningún combatiente
        /// además del player (ej. <c>DefaultEnemySpawnResolver</c> no pudo resolver al
        /// boss en el resume). En ese caso no armamos la cola de turnos — <see cref="CheckInput"/>
        /// desvía <c>StartCombat</c> directo a <see cref="ExitRef"/> en vez de Player/Enemy,
        /// evitando el softlock de un player solo ciclando su propio turno.
        /// </summary>
        private bool _noValidCombatants;

        public CombatEnterState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            _noValidCombatants = false;

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

            // BUG-078: la sala iba a combate pero no hay NINGÚN combatiente además del
            // player (típicamente el spawn del boss falló en el resume/re-entry). Arrancar
            // la FSM así deja al player como único participante de la cola: su turno
            // termina y vuelve a empezar el suyo, ciclando para siempre — y en boss room
            // EffForceDoor bloquea el escape, así que es un softlock real. Cerramos el
            // combate en vez de construir la cola.
            bool hasNonPlayerCombatant = false;
            foreach (var id in Context.CachedParticipants)
            {
                if (id != Context.PlayerId) { hasNonPlayerCombatant = true; break; }
            }
            if (!hasNonPlayerCombatant)
            {
                UnityEngine.Debug.LogWarning(
                    "[CombatEnterState] CachedParticipants solo contiene al player (sin enemigos) — " +
                    "cerrando el combate como Aborted en vez de arrancar la FSM de turnos (BUG-078).");
                _noValidCombatants = true;
                Context.PendingOutcome = CombatOutcome.Aborted;
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
                    // BUG-078: Enter ya decidió cerrar el combate (sin combatientes
                    // válidos) — StartCombat no debe llevar a Player/Enemy con una cola
                    // que nunca se armó.
                    if (_noValidCombatants)
                    {
                        next = ExitRef;
                        return true;
                    }

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
