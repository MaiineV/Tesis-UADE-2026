using System.Collections.Generic;
using System.Linq;
using Patterns.Save;

namespace Rollgeon.Items
{
    public class InventoryState : ISaveable
    {
        public string SaveKey => "run.inventory";

        public List<string> PassiveItemIds = new();
        public List<InventorySlotSnapshot> ActiveSlots = new();

        public object CaptureState()
        {
            return new InventorySnapshot
            {
                PassiveItemIds = new List<string>(PassiveItemIds),
                ActiveSlots = ActiveSlots.Select(s => new InventorySlotSnapshot
                {
                    ItemId = s.ItemId,
                    CurrentCooldown = s.CurrentCooldown,
                }).ToList(),
            };
        }

        public void RestoreState(object state)
        {
            if (state is not InventorySnapshot snapshot) return;

            PassiveItemIds = new List<string>(snapshot.PassiveItemIds);
            ActiveSlots = snapshot.ActiveSlots.Select(s => new InventorySlotSnapshot
            {
                ItemId = s.ItemId,
                CurrentCooldown = s.CurrentCooldown,
            }).ToList();
        }
    }
}
