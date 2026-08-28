namespace Rollgeon.Items
{
    /// <summary>
    /// Precio base por rareza (GDD de Ítems Pasivos, item-editor-spec.md §2). Tabla
    /// de consulta pura — el asistente de creación de la Fase 2 la usa para
    /// prellenar el <c>BasePrice</c> del <c>WeightedShopItem</c> en <c>ShopPool</c>;
    /// no está cableada a nada todavía. Normal puede subir hasta 20 si el efecto es
    /// fuerte dentro del tier — eso es override manual por ítem, no lo resuelve esta
    /// tabla.
    /// </summary>
    public static class RarityPricing
    {
        public const int Common = 15;    // Normal
        public const int Uncommon = 35;  // Raro
        public const int Rare = 60;      // Épico
        public const int Legendary = 100; // Legendario
        public const int God = 120;      // Dios

        public static int BasePriceFor(ItemRarity rarity)
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
                        nameof(rarity), rarity, "ItemRarity sin precio base en RarityPricing.");
            }
        }
    }
}
