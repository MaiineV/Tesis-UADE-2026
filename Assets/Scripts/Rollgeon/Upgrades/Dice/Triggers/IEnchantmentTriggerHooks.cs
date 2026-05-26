namespace Rollgeon.Upgrades.Dice.Triggers
{
    /// <summary>
    /// Marker base de todos los triggers de encantamiento. Los triggers concretos
    /// implementan uno o más de los <c>IOn*</c> hooks de este archivo y son
    /// dispatchados por el <c>DiceEnchantmentService</c> (Phase 4) según qué
    /// interfaz declaren.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué interfaces separadas en vez de un phase enum.</b> Cada concrete
    /// declara explícitamente qué eventos consume — el dispatcher hace
    /// <c>foreach (var t in triggers) if (t is IOnDiceRolledTrigger d) d.OnDiceRolled(ctx)</c>.
    /// Más explícito en el código y permite a un mismo trigger reaccionar a varios eventos.
    /// </para>
    /// <para>
    /// <b>Readers cross-sistema.</b> Los concretos consumen <c>EffectIntReader</c>
    /// (Rollgeon.Effects.Readers) para los valores numéricos — designers configuran
    /// si el amount es literal, lee gold actual, lee combo counter, etc.
    /// </para>
    /// </remarks>
    public interface IEnchantmentTrigger
    {
    }

    /// <summary>
    /// Dispara <i>una vez</i> en el momento en que el encantamiento se aplica al
    /// slot. Útil para setup: registrar listeners externos, inicializar contadores
    /// internos (ej. "explota si no se usa en 3 turnos" inicializa el counter).
    /// </summary>
    public interface IOnEnchantmentAppliedTrigger : IEnchantmentTrigger
    {
        void OnEnchantmentApplied(EnchantmentTriggerContext ctx);
    }

    /// <summary>
    /// Dispara cuando el roll <i>crudo</i> sale (post-dice, pre-reroll).
    /// <c>ctx.Effect.DiceResult</c> está populado.
    /// </summary>
    public interface IOnDiceRolledTrigger : IEnchantmentTrigger
    {
        void OnDiceRolled(EnchantmentTriggerContext ctx);
    }

    /// <summary>
    /// Dispara cuando el roll <i>final</i> queda lockeado tras todos los rerolls
    /// permitidos. <c>ctx.Effect.DiceResult</c> es ya la versión final.
    /// </summary>
    public interface IOnRollResolvedTrigger : IEnchantmentTrigger
    {
        void OnRollResolved(EnchantmentTriggerContext ctx);
    }

    /// <summary>
    /// Dispara cuando el <c>ContractSheet</c> matchea un combo sobre el roll
    /// final. <c>ctx.Effect.ComboResult</c> tiene el combo matched. El trigger
    /// decide si su carrier participó y reacciona via <c>ctx.Scratch</c>.
    /// </summary>
    public interface IOnComboMatchedTrigger : IEnchantmentTrigger
    {
        void OnComboMatched(EnchantmentTriggerContext ctx);
    }

    /// <summary>
    /// Dispara al finalizar el turno del player. Útil para counters tipo
    /// "explota si no se usa en N turnos" — incrementan al fin de turno.
    /// </summary>
    public interface IOnTurnFinishedTrigger : IEnchantmentTrigger
    {
        void OnTurnFinished(EnchantmentTriggerContext ctx);
    }
}
