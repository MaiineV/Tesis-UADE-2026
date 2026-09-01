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

        /// <summary>
        /// Bono de daño at-played (EffAddComboBonus) que los items passive aportarían al
        /// combo dado. Para el preview de daño — el bono real se aplica en ComboPlayed.
        /// </summary>
        int GetComboDamageBonusPreview(string comboId);

        void TickCooldowns();

        /// <summary>
        /// Slots de items activos disponibles: base del bootstrap + bonus de items
        /// (Mochila Grande). Nunca menor que la base.
        /// </summary>
        int MaxActiveSlots { get; }

        /// <summary>
        /// Suma (o resta, con negativo) al bonus de slots activos. Lifecycle de items:
        /// entra con el item, se revierte al perderlo.
        /// </summary>
        void AddActiveSlotBonus(int amount);

        event Action<ItemSO, bool> OnItemChanged;
    }
}
