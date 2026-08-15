using Rollgeon.Items;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Contenido de una celda del reel gacha. <see cref="Rarity"/> es SIEMPRE la
    /// del contenido de la celda (el fondo por rareza la refleja): el ítem aporta
    /// la suya; el oro es <see cref="ItemRarity.Common"/> fijo — nunca el tier del
    /// cofre.
    /// </summary>
    public readonly struct ChestReelCellData
    {
        public ItemSO Item { get; }
        public int GoldAmount { get; }
        public ItemRarity Rarity { get; }
        public bool IsWinner { get; }
        public bool IsGold => Item == null;

        private ChestReelCellData(ItemSO item, int goldAmount, ItemRarity rarity, bool isWinner)
        {
            Item = item;
            GoldAmount = goldAmount;
            Rarity = rarity;
            IsWinner = isWinner;
        }

        public static ChestReelCellData ForItem(ItemSO item, bool isWinner = false) =>
            new ChestReelCellData(item, 0, item != null ? item.Rarity : ItemRarity.Common, isWinner);

        public static ChestReelCellData ForGold(int amount, bool isWinner = false) =>
            new ChestReelCellData(null, amount, ItemRarity.Common, isWinner);
    }
}
