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
        /// Chequeo read-only de los mismos gates que aplica <see cref="ActivateItem"/>
        /// (cooldown, action economy, precondiciones del <c>OnActivate</c>). Sin efectos
        /// secundarios: lo llama el HUD para pintar el slot y explicar el rechazo antes
        /// del click. <see cref="ItemActivationBlock.None"/> = usable.
        /// </summary>
        ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx);

        /// <summary>
        /// Bono de daño at-played (EffAddComboBonus) que los items passive aportarían al
        /// combo dado. Para el preview de daño — el bono real se aplica en ComboPlayed.
        /// </summary>
        int GetComboDamageBonusPreview(string comboId);

        void TickCooldowns();

        int MaxActiveSlots { get; }

        event Action<ItemSO, bool> OnItemChanged;
    }
}
