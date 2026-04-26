namespace Rollgeon.Shop
{
    /// <summary>
    /// Proyección runtime de un slot de shop — un <see cref="ShopItemState"/>
    /// hidratado con la <see cref="ShopItemDef"/> resuelta desde el pool, más
    /// la referencia al spawn point del prefab donde va el visual. TECHNICAL.md §17.F.1.
    /// </summary>
    /// <remarks>
    /// Los slots son la vista que consume el <c>ShopManagerService</c> y los
    /// interactables. El <c>ShopItemState</c> sigue siendo la fuente de verdad
    /// persistida en <c>RoomInstance.ObjectStates</c> — este objeto es la
    /// conveniencia para no andar re-resolviendo la def cada frame.
    /// </remarks>
    public sealed class ShopSlot
    {
        public string SpawnPointId;
        public ShopItemDef Item;
        public int Price;
        public bool Purchased;
        public UnityEngine.GameObject SpawnedVisual;
    }
}
