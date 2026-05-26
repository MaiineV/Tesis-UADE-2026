using System;

namespace Rollgeon.Items
{
    [Serializable]
    public class InventorySlotSnapshot
    {
        public string ItemId;
        public int CurrentCooldown;
    }
}
