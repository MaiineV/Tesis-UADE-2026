namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Acciones que la UI dispara sobre la FSM de combate durante el turno del
    /// player (Revision 2). Separado de <see cref="ICombatStarter"/> (lifecycle)
    /// y <see cref="Rollgeon.Combat.AI.ICombatSignaller"/> (AI) para mantener cada
    /// contrato con el minimo superficie.
    /// </summary>
    public interface IPlayerCombatActions
    {
        /// <summary>Una accion del player termino — FSM input <c>PlayerActionDone</c>.</summary>
        void SendPlayerAction();

        /// <summary>El player cierra su turno — FSM input <c>PlayerEndTurn</c>.</summary>
        void EndPlayerTurn();
    }
}
