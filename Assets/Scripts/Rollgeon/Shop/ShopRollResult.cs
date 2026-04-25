namespace Rollgeon.Shop
{
    /// <summary>
    /// Resultado atómico de un roll sobre un <see cref="ShopPoolSO"/>. El
    /// <see cref="Item"/> es la def específica; el <see cref="BasePrice"/>
    /// viene del entry pesado y aún no tiene aplicado el
    /// <see cref="ShopConfigSO.PriceMultiplier"/> ni la varianza — eso lo
    /// resuelve <c>ShopManagerService</c>.
    /// </summary>
    public struct ShopRollResult
    {
        public ShopItemDef Item;
        public int BasePrice;
    }
}
