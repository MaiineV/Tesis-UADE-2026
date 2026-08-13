using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Elige qué encantamiento representa visualmente a un dado que puede tener varios.
    /// Hoy el visual es uno solo (holo sí/no), pero la elección ya vive acá para que sumar
    /// un segundo efecto no toque call-sites.
    /// </summary>
    public static class DiceEnchantVisualResolver
    {
        /// <summary>
        /// Primer encantamiento real de la lista, o <c>null</c> si el dado no tiene ninguno.
        /// </summary>
        /// <remarks>
        /// La lista de <c>RuntimeDiceBag.GetEnchantments</c> está documentada como "puede
        /// contener nulls (cupos vacíos)", así que hay que saltearlos: un <c>list[0]</c> ingenuo
        /// devolvería null para un dado encantado en el segundo cupo.
        /// </remarks>
        public static EnchantmentSO ResolvePrimary(IReadOnlyList<EnchantmentSO> enchantments)
        {
            if (enchantments == null) return null;

            for (int i = 0; i < enchantments.Count; i++)
                if (enchantments[i] != null) return enchantments[i];

            return null;
        }
    }
}