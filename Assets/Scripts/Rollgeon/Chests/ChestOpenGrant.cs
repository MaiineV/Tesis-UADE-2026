using Rollgeon.Economy;
using Rollgeon.Items;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Qué aterrizó efectivamente al abrir el cofre. Puede diferir del roll: un ítem
    /// rechazado por el inventario (slots activos llenos) degrada a oro de fallback.
    /// </summary>
    public readonly struct ChestGrantResult
    {
        public ItemSO Item { get; }
        public int GoldAmount { get; }
        public bool WasInventoryFullFallback { get; }
        public bool IsGold => Item == null;

        public ChestGrantResult(ItemSO item, int goldAmount, bool wasInventoryFullFallback)
        {
            Item = item;
            GoldAmount = goldAmount;
            WasInventoryFullFallback = wasInventoryFullFallback;
        }
    }

    /// <summary>
    /// Entrega de la recompensa del cofre, sin UI y sin escena. Mismo patrón que
    /// <c>ShopPurchase</c>: los servicios entran por parámetro para que tests y
    /// consola pasen fakes. La recompensa se suma automáticamente (GDD §15 — no se
    /// recoge del suelo); <c>InventoryService.AddItem</c> ya emite
    /// <c>OnItemObtained</c>, acá no se re-emite nada.
    /// </summary>
    public static class ChestOpenGrant
    {
        public static ChestGrantResult Grant(
            IInventoryService inventory,
            IEconomyService economy,
            ChestLootResult loot,
            ChestTierDef tierDef)
        {
            if (loot.IsGold)
            {
                economy?.Add(loot.GoldAmount);
                return new ChestGrantResult(null, loot.GoldAmount, wasInventoryFullFallback: false);
            }

            if (inventory != null && inventory.AddItem(loot.Item))
                return new ChestGrantResult(loot.Item, 0, wasInventoryFullFallback: false);

            // Inventario lleno (o ausente): el valor del cofre no se pierde — degrada
            // al oro de fallback del tier.
            int fallback = tierDef?.FallbackGold ?? 0;
            economy?.Add(fallback);
            return new ChestGrantResult(null, fallback, wasInventoryFullFallback: true);
        }
    }
}
