namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Marker base de los triggers de combo passive (pasivas de combo que viven
    /// en la run y reaccionan al matcheo de un combo específico). Los concretos
    /// implementan uno o más de los <c>IOn*</c> hooks de este archivo. El service
    /// dispatcha los hooks vía cast por tipo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Triggers vs flat bonus.</b> El campo <c>FlatDamageBonus</c> del
    /// <see cref="ComboPassiveSO"/> cubre el caso simple (+X daño plano). Los
    /// triggers de este archivo cubren condiciones más ricas: "+3 oro cada vez
    /// que matchea escalera", "shield si el combo se activa con dado par", etc.
    /// </para>
    /// <para>
    /// <b>Readers cross-system.</b> Mismo patrón que enchantments — los concretos
    /// usan <c>EffectIntReader</c> para todos los valores numéricos.
    /// </para>
    /// </remarks>
    public interface IComboPassiveTrigger
    {
    }

    /// <summary>
    /// Dispara cuando el combo target del passive matchea sobre el roll final.
    /// El <c>ComboPassiveContext.Effect</c> contiene <c>DiceResult</c> +
    /// <c>ComboResult</c>; <c>ComboPassiveContext.ComboId</c> es el id del combo
    /// matched. Side effects van al <c>Scratch</c>.
    /// </summary>
    public interface IOnComboPassiveMatchedTrigger : IComboPassiveTrigger
    {
        void OnComboMatched(ComboPassiveContext ctx);
    }
}
