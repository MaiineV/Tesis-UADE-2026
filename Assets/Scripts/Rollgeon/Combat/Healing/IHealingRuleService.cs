namespace Rollgeon.Combat.Healing
{
    /// <summary>
    /// Reglas que alteran la curación del jugador mientras un item las sostenga
    /// (patrón <c>IComboRuleService</c>: fuentes por ItemId, la regla vale mientras haya al
    /// menos una). Primera regla: "Ayuno" — las curas que vienen de items PASIVOS se ignoran.
    /// </summary>
    public interface IHealingRuleService
    {
        /// <summary>True si alguna fuente bloquea la curación proveniente de items pasivos.</summary>
        bool PassiveItemHealingBlocked { get; }

        void AddPassiveHealingBlock(string sourceId);

        void RemovePassiveHealingBlock(string sourceId);
    }
}
