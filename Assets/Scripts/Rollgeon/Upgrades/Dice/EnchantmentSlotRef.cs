using System;
using Rollgeon.Dice;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Identidad del slot que <i>carga</i> un encantamiento: en qué dado del
    /// bag vive y en qué posición de la lista de encantamientos de ese dado.
    /// Pasado por separado al <c>EffectContext</c> en cada hook para que los
    /// triggers sepan sobre qué dado están operando.
    /// </summary>
    /// <remarks>
    /// Struct readonly — el índice es el orden de append en el dado y es estable
    /// de por vida: remover un encantamiento deja un tombstone (null) en su
    /// posición en vez de compactar la lista.
    /// </remarks>
    [Serializable]
    public readonly struct EnchantmentSlotRef
    {
        /// <summary>Tipo del dado que carga (D3..D20).</summary>
        public readonly DiceType Type;

        /// <summary>Índice del dado dentro del <c>DiceBagSO</c> (0..4).</summary>
        public readonly int BagSlotIndex;

        /// <summary>
        /// Índice del encantamiento dentro de la lista del dado (orden de append,
        /// tombstones incluidos).
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
