using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Paleta de títulos de encantamientos (mismo patrón que
    /// <c>Rollgeon.Items.RarityPalette</c>): maldiciones en rojo, bendiciones en
    /// dorado. Fuente de verdad: Rollgeon_Paleta_de_Color.md. Si aparece otro
    /// consumidor (shop, tooltips nuevos), leer de acá y no duplicar hexas.
    /// </summary>
    public static class EnchantmentPalette
    {
        /// <summary>#D1365A — títulos de maldiciones.</summary>
        public static readonly Color32 Cursed = new Color32(0xD1, 0x36, 0x5A, 0xFF);

        /// <summary>#D9A44E — títulos de encantamientos no malditos.</summary>
        public static readonly Color32 Blessed = new Color32(0xD9, 0xA4, 0x4E, 0xFF);

        public static Color32 TitleColor(EnchantmentSO enchantment)
            => enchantment.IsCursed() ? Cursed : Blessed;

        /// <summary>Hex RRGGBB (sin #) para rich text inline.</summary>
        public static string TitleHex(EnchantmentSO enchantment)
            => ColorUtility.ToHtmlStringRGB(TitleColor(enchantment));
    }
}
