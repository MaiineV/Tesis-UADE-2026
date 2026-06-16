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

        // El add va por ItemId — no requiere selección de tile/entidad.
        protected override bool ShowSelection => false;
        public override bool HasSelectionRequirement() => false;
        public override bool RequiresSelectionAt(Selection.SelectionTiming timing) => false;

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

            // OnItemObtained lo dispara InventoryService.AddItem centralmente — no
            // re-disparar acá para evitar doble fire.
            return true;
        }

        // Método siempre compilado (player build incluido) porque [ValueDropdown(nameof(...))]
        // resuelve el nombre en compile-time. La lógica de scan vive dentro de
        // ItemCatalogSO.GetEditorAllIds que ya tiene su propio #if UNITY_EDITOR.
        private static IEnumerable<string> GetItemIds()
        {
#if UNITY_EDITOR
            return ItemCatalogSO.GetEditorAllIds();
#else
            return Array.Empty<string>();
#endif
        }
    }
}
