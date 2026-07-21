namespace Rollgeon.Shop
{
    /// <summary>
    /// Proyección runtime de un slot de shop — un <see cref="ShopItemState"/>
    /// hidratado con la <see cref="IShopRewardEntry"/> resuelta desde el pool,
    /// más la referencia al spawn point del prefab donde va el visual. TECHNICAL.md §17.F.1.
    /// </summary>
    /// <remarks>
    /// <see cref="Item"/> es normalmente un <c>Rollgeon.Items.ItemSO</c> (activo o
    /// passive). El dispatch de "qué hacer al comprar" se hace en
    /// <c>ShopItemPedestalInteractable</c> por tipo concreto.
    /// </remarks>
    public sealed class ShopSlot
    {
        public string SpawnPointId;
        public IShopRewardEntry Item;
        public int Price;
        public bool Purchased;
        public UnityEngine.GameObject SpawnedVisual;
    }
}
