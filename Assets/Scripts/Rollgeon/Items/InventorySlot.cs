using System;

namespace Rollgeon.Items
{
    [Serializable]
    public class InventorySlot
    {
        public ItemSO Item;
        public int CurrentCooldown;
    }
}
