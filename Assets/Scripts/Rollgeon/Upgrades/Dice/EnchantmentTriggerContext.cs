using Rollgeon.Effects;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper que se le pasa a cada hook de un <c>IEnchantmentTrigger</c>. Junta:
    /// </summary>
    /// <list type="bullet">
    /// <item><description><see cref="Effect"/> — combat / roll state via <c>EffectContext</c> (DiceResult, ComboResult, SourceGuid, etc.).</description></item>
    /// <item><description><see cref="Slot"/> — qué dado del bag carga este encantamiento y en qué cupo.</description></item>
    /// <item><description><see cref="Scratch"/> — buffer mutable para que el trigger escriba bonus damage / gold / shield. El service consume al final del dispatch.</description></item>
    /// </list>
    /// <remarks>
    /// Vivido sólo durante el dispatch de un evento. El service lo crea fresh, lo
    /// pasa por todos los triggers relevantes, aplica el scratch, y lo descarta
    /// (o lo reusa con <see cref="EnchantmentScratch.Reset"/>).
    /// </remarks>
    public sealed class EnchantmentTriggerContext
    {
        /// <summary>Combat / roll state. Nunca null durante el dispatch.</summary>
        public EffectContext Effect;

        /// <summary>Identidad del slot que carga el encantamiento.</summary>
        public EnchantmentSlotRef Slot;

        /// <summary>Scratch que el trigger escribe. Nunca null durante el dispatch.</summary>
        public EnchantmentScratch Scratch;

        /// <summary>
        /// ID del combo matcheado en este evento, si aplica (hooks <c>OnComboMatched</c>).
        /// Null/empty para hooks que no son combo-related (apply, dice rolled, turn finished).
        /// El service de Phase 4 lo setea desde el <c>BaseComboSO</c> matched.
        /// </summary>
        public string ComboId;
    }
}
