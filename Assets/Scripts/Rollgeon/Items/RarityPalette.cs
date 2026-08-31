using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Paleta canónica por <see cref="ItemRarity"/> — realineada al GDD de Ítems
    /// Pasivos (item-editor-spec.md §5.2): Normal/Raro/Épico/Legendario/Dios.
    /// La comparten el tint del cofre en mundo y el marco de rareza del reveal
    /// gacha; si aparece otro consumidor de rareza (tooltips, shop), leer de acá
    /// y no duplicar hexas.
    ///
    /// El GDD de pasivas no da hexes, solo emoji — se reciclan 3 de los 4 que ya
    /// existían: Rare (violeta) y Legendary (dorado) quedan igual; Uncommon pasa
    /// de rojo a azul (Raro 🔵) y el rojo se corre a God (Dios 🔴), que es el tier
    /// para el que el GDD lo pide. Common (Normal ⚪) y Uncommon (Raro 🔵) son
    /// hexes nuevos. Ver §5.3: el GDD del Cofre (hexes propios, 4 tiers) queda
    /// desactualizado a propósito hasta que se comunique el conflicto — esta
    /// paleta sigue al GDD de pasivas como fuente de verdad.
    /// </summary>
    public static class RarityPalette
    {
        public static readonly Color32 Common = new Color32(0xD6, 0xD3, 0xCE, 0xFF);    // #D6D3CE hueso — Normal ⚪
        public static readonly Color32 Uncommon = new Color32(0x3A, 0x6E, 0xA5, 0xFF);  // #3A6EA5 azul — Raro 🔵
        public static readonly Color32 Rare = new Color32(0x5C, 0x4A, 0x7A, 0xFF);      // #5C4A7A violeta — Épico 🟣
        public static readonly Color32 Legendary = new Color32(0xD9, 0xA4, 0x4E, 0xFF); // #D9A44E dorado — Legendario 🟠
        public static readonly Color32 God = new Color32(0xB3, 0x3A, 0x1F, 0xFF);       // #B33A1F rojo — Dios 🔴

        /// <summary>Herrajes/bisagras/cerradura del cofre — común a todos los tiers.</summary>
        public static readonly Color32 Fittings = new Color32(0x5F, 0x73, 0x7A, 0xFF);  // #5F737A

        public static Color32 BodyColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return Common;
                case ItemRarity.Uncommon: return Uncommon;
                case ItemRarity.Rare: return Rare;
                case ItemRarity.Legendary: return Legendary;
                case ItemRarity.God: return God;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(rarity), rarity, "ItemRarity sin color definido en RarityPalette.");
            }
        }

        /// <summary>
        /// Etiqueta de display en el vocabulario del GDD de pasivas — NO es un
        /// rename del enum (§5.1): Common/Uncommon/Rare/Legendary/God quedan
        /// igual en código, esto es solo el mapeo a texto para la tool, la tienda
        /// y los tooltips. Español fijo (así está el GDD); si hace falta EN,
        /// pasa por localización aparte, no por acá.
        /// </summary>
        public static string DisplayName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return "Normal";
                case ItemRarity.Uncommon: return "Raro";
                case ItemRarity.Rare: return "Épico";
                case ItemRarity.Legendary: return "Legendario";
                case ItemRarity.God: return "Dios";
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(rarity), rarity, "ItemRarity sin etiqueta de display en RarityPalette.");
            }
        }
    }
}
