namespace Rollgeon.UI.ChestReveal
{
    /// <summary>Keys de la tabla UI para el reveal del cofre (patrón <c>InventoryTextKeys</c>).</summary>
    public static class ChestRevealTextKeys
    {
        public const string Title = "chest.reveal.title";
        public const string GoldAmount = "chest.reveal.gold_amount";   // "{0}" = monto
        public const string SkipHint = "chest.reveal.skip_hint";
        public const string ContinueHint = "chest.reveal.continue_hint";
        public const string RarityCommon = "chest.rarity.common";
        public const string RarityUncommon = "chest.rarity.uncommon";
        public const string RarityRare = "chest.rarity.rare";
        public const string RarityLegendary = "chest.rarity.legendary";
        public const string RarityGod = "chest.rarity.god";

        /// <summary>
        /// Switch exhaustivo A PROPÓSITO: el <c>default:</c> devolvía la key de
        /// Common para cualquier rareza no listada — con God agregado, el reveal de
        /// un cofre Dios mostraba la etiqueta "Common" en el panel, sin un solo error.
        /// </summary>
        public static string RarityKey(Items.ItemRarity rarity)
        {
            switch (rarity)
            {
                case Items.ItemRarity.Common: return RarityCommon;
                case Items.ItemRarity.Uncommon: return RarityUncommon;
                case Items.ItemRarity.Rare: return RarityRare;
                case Items.ItemRarity.Legendary: return RarityLegendary;
                case Items.ItemRarity.God: return RarityGod;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(rarity), rarity, "ItemRarity sin key de localización en ChestRevealTextKeys.");
            }
        }
    }
}
