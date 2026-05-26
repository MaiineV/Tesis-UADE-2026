using System;
using System.Collections.Generic;

namespace Rollgeon.Items
{
    [Serializable]
    public class InventorySnapshot
    {
        public List<string> PassiveItemIds = new();
        public List<InventorySlotSnapshot> ActiveSlots = new();
    }
}
