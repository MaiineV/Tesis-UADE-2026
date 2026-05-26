namespace Rollgeon.Shop
{
    /// <summary>
    /// Resultado atómico de un roll sobre un <see cref="ShopPoolSO"/>. El
    /// <see cref="Item"/> es la def polimórfica (<see cref="IShopRewardEntry"/>) —
    /// puede ser <see cref="ShopItemDef"/> o
    /// <see cref="Rollgeon.Upgrades.Combos.ComboPassiveSO"/>. El <see cref="BasePrice"/>
    /// viene del entry pesado y aún no tiene aplicado el
    /// <see cref="ShopConfigSO.PriceMultiplier"/> ni la varianza — eso lo resuelve
    /// <c>ShopManagerService</c>.
    /// </summary>
    public struct ShopRollResult
    {
        public IShopRewardEntry Item;
        public int BasePrice;
    }
}
