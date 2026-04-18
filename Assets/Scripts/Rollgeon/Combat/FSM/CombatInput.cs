namespace Rollgeon.Combat.FSM
{
    /// <summary>
    /// Inputs legales que acepta la <see cref="CombatTurnFSM"/>.
    /// Plan §4.2 y §10 R3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reserva de <c>None = 0</c>.</b> <c>default(CombatInput) == None</c>.
    /// La <see cref="Patterns.FSM.StateMachine{TContext, TInput}"/> pasa
    /// <c>default(TInput)</c> en <c>Start</c> y <c>Stop</c>; por contrato ningun
    /// estado reacciona a <c>None</c> en su <c>CheckInput</c>.
    /// </para>
    /// </remarks>
    public enum CombatInput
    {
        /// <summary>Sentinel inerte. Nunca dispara transicion.</summary>
        None = 0,
        /// <summary>Arranque del combate: <c>CombatEnterState</c> -> <c>Player|EnemyTurn</c>.</summary>
        StartCombat = 1,
        /// <summary>Una accion del player termino (self-loop en PlayerTurn).</summary>
        PlayerActionDone = 2,
        /// <summary>El player decide cerrar su turno (UI button "End Turn"). Revision 2: unica via.</summary>
        PlayerEndTurn = 3,
        /// <summary>AI del enemy (o test) anuncia fin de su turno.</summary>
        EnemyDone = 4,
        /// <summary>Cualquier estado -> CombatExit. Se dispara desde el controller tras NotifyCombatEnded.</summary>
        CombatEnded = 5,
    }
}
