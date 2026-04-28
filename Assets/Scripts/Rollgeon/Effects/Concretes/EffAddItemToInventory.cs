using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffAddItemToInventory : BaseEffect
    {
        [ValueDropdown(nameof(GetItemIds))]
        public string ItemId;

        public override string GetEffectName() => "Add Item To Inventory";

        public override bool ApplyEffect(EffectContext context)
        {
            if (string.IsNullOrEmpty(ItemId)) return false;

            if (!ServiceLocator.TryGetService<ItemCatalogSO>(out var catalog))
            {
                Debug.LogWarning("[EffAddItemToInventory] ItemCatalogSO not registered.");
                return false;
            }

            var item = catalog.GetById(ItemId);
            if (item == null)
            {
                Debug.LogWarning($"[EffAddItemToInventory] Item '{ItemId}' not found in catalog.");
                return false;
            }

            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory))
            {
                Debug.LogWarning("[EffAddItemToInventory] IInventoryService not registered.");
                return false;
            }

            if (!inventory.AddItem(item))
            {
                Debug.LogWarning($"[EffAddItemToInventory] Failed to add item '{ItemId}'.");
                return false;
            }

            EventManager.Trigger(EventName.OnItemObtained, context.SourceGuid, ItemId);
            return true;
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetItemIds() => ItemCatalogSO.GetEditorAllIds();
#endif
    }
}
