using System;
using System.Collections.Generic;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Items;

namespace Rollgeon.Chests.Tests
{
    /// <summary>Economía fake: contador simple, sin eventos.</summary>
    internal sealed class FakeEconomyService : IEconomyService
    {
        public int CurrentGold { get; private set; }
        public List<int> AddedAmounts { get; } = new List<int>();

        public void Add(int amount)
        {
            if (amount < 0) return;
            CurrentGold += amount;
            AddedAmounts.Add(amount);
        }

        public bool Spend(int amount)
        {
            if (amount > CurrentGold) return false;
            CurrentGold -= amount;
            return true;
        }

        public bool CanAfford(int amount) => CurrentGold >= amount;
        public void ResetTo(int amount) => CurrentGold = amount;
    }

    /// <summary>Inventario fake: acepta o rechaza según <see cref="RejectAdds"/>.</summary>
    internal sealed class FakeInventoryService : IInventoryService
    {
        public bool RejectAdds;
        public List<ItemSO> Added { get; } = new List<ItemSO>();

        public IReadOnlyList<InventorySlot> PassiveItems => Array.Empty<InventorySlot>();
        public IReadOnlyList<InventorySlot> ActiveItems => Array.Empty<InventorySlot>();
        public int MaxActiveSlots => 4;
        public void AddActiveSlotBonus(int amount) { }

#pragma warning disable 67
        public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore 67

        public bool AddItem(ItemSO item)
        {
            if (RejectAdds) return false;
            Added.Add(item);
            return true;
        }

        public bool RemoveItem(string itemId) => false;
        public bool HasItem(string itemId) => false;
        public ItemSO GetItem(string itemId) => null;
        public bool ActivateItem(int activeSlotIndex, EffectContext ctx) => false;
        public int GetComboDamageBonusPreview(string comboId) => 0;
        public void TickCooldowns() { }
    }
}
