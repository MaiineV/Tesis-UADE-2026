using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Remueve un ítem del inventario por <see cref="ItemId"/>. Devuelve <c>false</c>
    /// (corta la cadena §8.8) si el ítem no estaba en el inventario; devuelve <c>true</c>
    /// si la remoción se efectuó. Usar combinado con <c>PCHasInventoryItem</c> para
    /// mostrar/ocultar el botón antes de gastar energía.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffRemoveInventoryItem : BaseEffect
    {
        [ValueDropdown(nameof(GetItemIds))]
        public string ItemId;

        public override string GetEffectName() => "Remove Item From Inventory";

        public override bool ApplyEffect(EffectContext context)
        {
            if (string.IsNullOrEmpty(ItemId)) return false;

            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null)
            {
                Debug.LogWarning("[EffRemoveInventoryItem] IInventoryService no registrado.");
                return false;
            }

            if (!inventory.RemoveItem(ItemId))
            {
                Debug.Log($"[EffRemoveInventoryItem] Item '{ItemId}' no estaba en el inventario — no-op.");
                return false;
            }

            return true;
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetItemIds() => ItemCatalogSO.GetEditorAllIds();
#endif
    }
}
