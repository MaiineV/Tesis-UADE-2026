using System;
using Rollgeon.Dice;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Identidad del slot que <i>carga</i> un encantamiento: en qué dado del
    /// bag vive y en cuál de los cupos de ese dado. Pasado por separado al
    /// <c>EffectContext</c> en cada hook para que los triggers sepan sobre qué
    /// dado están operando.
    /// </summary>
    /// <remarks>
    /// Struct readonly — los campos no mutan después de aplicado el encantamiento
    /// (si se re-encanta, se crea un nuevo SlotRef con otro <see cref="Enchantment"/>).
    /// </remarks>
    [Serializable]
    public readonly struct EnchantmentSlotRef
    {
        /// <summary>Tipo del dado que carga (D3..D20).</summary>
        public readonly DiceType Type;

        /// <summary>Índice del dado dentro del <c>DiceBagSO</c> (0..4).</summary>
        public readonly int BagSlotIndex;

        /// <summary>
        /// Índice del cupo de encantamiento dentro del dado
        /// (0..<c>DiceType.MaxEnchantmentSlots()-1</c>).
        /// </summary>
        public readonly int EnchantmentSlotIndex;

        public EnchantmentSlotRef(DiceType type, int bagSlotIndex, int enchantmentSlotIndex)
        {
            Type = type;
            BagSlotIndex = bagSlotIndex;
            EnchantmentSlotIndex = enchantmentSlotIndex;
        }
    }
}
