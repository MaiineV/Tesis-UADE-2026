using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Elige qué encantamiento representa visualmente a un dado que puede tener varios.
    /// La elección vive acá para que sumar un nuevo efecto no toque call-sites.
    /// </summary>
    public static class DiceEnchantVisualResolver
    {
        /// <summary>
        /// Primer encantamiento MALDITO (<see cref="CapCursed"/>) si hay alguno; si no,
        /// primer encantamiento real; <c>null</c> si el dado no tiene ninguno.
        /// </summary>
        /// <remarks>
        /// La maldición gana siempre el visual: un dado con bendición + maldición debe
        /// leerse como maldito. La lista de <c>RuntimeDiceBag.GetEnchantments</c> está
        /// documentada como "puede contener nulls (cupos vacíos)", así que hay que
        /// saltearlos: un <c>list[0]</c> ingenuo devolvería null para un dado encantado
        /// en el segundo cupo.
        /// </remarks>
        public static EnchantmentSO ResolvePrimary(IReadOnlyList<EnchantmentSO> enchantments)
        {
            if (enchantments == null) return null;

            EnchantmentSO first = null;
            for (int i = 0; i < enchantments.Count; i++)
            {
                var enchantment = enchantments[i];
                if (enchantment == null) continue;
                if (enchantment.IsCursed()) return enchantment;
                if (first == null) first = enchantment;
            }

            return first;
        }
    }
}