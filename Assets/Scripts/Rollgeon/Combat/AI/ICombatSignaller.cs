namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Narrow interface for enemy AI to signal turn completion to the combat FSM.
    /// Decouples AI from <see cref="FSM.CombatController"/> (S#0012b).
    /// </summary>
    public interface ICombatSignaller
    {
        void SignalEnemyDone();
    }
}
