using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Wrapper que se le pasa a cada hook de un <see cref="IComboPassiveTrigger"/>.
    /// Análogo a <see cref="Rollgeon.Upgrades.Dice.EnchantmentTriggerContext"/>
    /// pero sin <c>SlotRef</c> (los passives no están atados a un dado específico).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scratch compartido.</b> Reusamos
    /// <see cref="Rollgeon.Upgrades.Dice.EnchantmentScratch"/> — semánticamente
    /// son los mismos campos (BonusComboDamage, ComboDamageMultiplier, BlockComboDamage,
    /// BonusGold, BonusShield). Cuando aterrice el AttackResolver consume ambos
    /// scratches (de DiceEnchantmentService y de ComboPassiveService) sumando.
    /// </para>
    /// </remarks>
    public sealed class ComboPassiveContext
    {
        /// <summary>Combat / roll state. Populado por el service antes de disparar el hook.</summary>
        public EffectContext Effect;

        /// <summary>ID del combo matched. Coincide con el <c>ComboPassiveSO.TargetComboId</c>.</summary>
        public string ComboId;

        /// <summary>Buffer mutable. Los triggers escriben aquí; el service consume al final.</summary>
        public EnchantmentScratch Scratch;
    }
}
