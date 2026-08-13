using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Paleta canónica por <see cref="ItemRarity"/> — colores confirmados en el GDD
    /// del Cofre §21 (tier ↔ rareza 1:1). La comparten el tint del cofre en mundo y
    /// el marco de rareza del reveal gacha; si aparece otro consumidor de rareza
    /// (tooltips, shop), leer de acá y no duplicar hexas.
    /// </summary>
    public static class RarityPalette
    {
        public static readonly Color32 Common = new Color32(0xB0, 0x89, 0x68, 0xFF);    // #B08968 marrón
        public static readonly Color32 Uncommon = new Color32(0xB3, 0x3A, 0x1F, 0xFF);  // #B33A1F rojo
        public static readonly Color32 Rare = new Color32(0x5C, 0x4A, 0x7A, 0xFF);      // #5C4A7A violeta
        public static readonly Color32 Legendary = new Color32(0xD9, 0xA4, 0x4E, 0xFF); // #D9A44E dorado

        /// <summary>Herrajes/bisagras/cerradura del cofre — común a todos los tiers.</summary>
        public static readonly Color32 Fittings = new Color32(0x5F, 0x73, 0x7A, 0xFF);  // #5F737A

        public static Color32 BodyColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return Uncommon;
                case ItemRarity.Rare: return Rare;
                case ItemRarity.Legendary: return Legendary;
                default: return Common;
            }
        }
    }
}
