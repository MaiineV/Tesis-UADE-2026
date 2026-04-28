using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Precondición que pasa si el <see cref="IInventoryService"/> tiene al menos un slot
    /// con el <see cref="ItemId"/> indicado. Usada típicamente en behaviors que consumen
    /// un ítem (ej. Heal con poción).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCHasInventoryItem : BasePreCondition
    {
        [ValueDropdown(nameof(GetItemIds))]
        [Tooltip("ItemId que debe estar presente en el inventario para que la precondición pase.")]
        public string ItemId;

        public PCHasInventoryItem()
        {
            // Depende de runtime state — no es constante.
            _isConstantValue = false;
        }

        public override string ConditionName =>
            string.IsNullOrEmpty(ItemId) ? "HasInventoryItem(<unset>)" : $"HasInventoryItem({ItemId})";

        public override bool Evaluate(PreConditionContext context)
        {
            if (string.IsNullOrEmpty(ItemId)) return false;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null)
            {
                Debug.LogWarning("[PCHasInventoryItem] IInventoryService no registrado.");
                return false;
            }
            return inventory.HasItem(ItemId);
        }

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
