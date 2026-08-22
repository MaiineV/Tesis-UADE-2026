namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Narrow interface for enemy AI to signal turn completion to the combat FSM.
    /// Decouples AI from <see cref="FSM.CombatController"/>.
    /// </summary>
    public interface ICombatSignaller
    {
        void SignalEnemyDone();
        void NotifyCombatEnded(FSM.CombatOutcome outcome);
    }
}
