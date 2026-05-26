using System;
using System.Collections.Generic;
using Rollgeon.Effects;

namespace Rollgeon.Items
{
    public interface IInventoryService
    {
        IReadOnlyList<InventorySlot> PassiveItems { get; }
        IReadOnlyList<InventorySlot> ActiveItems { get; }

        bool AddItem(ItemSO item);
        bool RemoveItem(string itemId);
        bool HasItem(string itemId);
        ItemSO GetItem(string itemId);

        bool ActivateItem(int activeSlotIndex, EffectContext ctx);

        void TickCooldowns();

        int MaxActiveSlots { get; }

        event Action<ItemSO, bool> OnItemChanged;
    }
}
