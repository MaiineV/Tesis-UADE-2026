using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Items;

namespace Rollgeon.Movement.Die
{
    /// <summary>
    /// Mapea los modifiers de <see cref="MoveRange"/> del jugador a los items que los
    /// aplicaron, para que el dado de Movimiento pueda anunciar "Botas Ligeras +1".
    /// </summary>
    /// <remarks>
    /// Solo se atribuyen modifiers aditivos (<c>Add</c>/<c>Subtract</c>) cuyo <c>SourceId</c>
    /// sea el <see cref="ItemPassiveSourceId"/> de un item del inventario: es lo único que
    /// se puede mostrar como "+X" con nombre. Cualquier otra fuente (upgrades, pasivas de
    /// clase) sigue contando en el total que muestra el label agregado, pero no tiene chip.
    /// Un item con varios modifiers sobre el stat se colapsa en una sola entrada.
    /// </remarks>
    public static class MovementRangeAttribution
    {
        /// <summary>Lee stat e inventario del <see cref="ServiceLocator"/>; vacío si falta algo.</summary>
        public static void Resolve(Guid playerGuid, List<MovementRangeContribution> into)
        {
            into.Clear();
            if (playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null) return;

            var stat = attrs.GetAttribute<MoveRange>(playerGuid);
            if (stat == null) return;

            Resolve(stat.GetRawModifiers(), EnumerateItems(inventory), into);
        }

        /// <summary>Función pura: modifiers × items → contribuciones en orden de inventario.</summary>
        public static void Resolve(IReadOnlyList<Modifier<int>> modifiers, IEnumerable<ItemSO> items,
                                   List<MovementRangeContribution> into)
        {
            into.Clear();
            if (modifiers == null || modifiers.Count == 0 || items == null) return;

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                var sourceId = ItemPassiveSourceId.For(item.ItemId);
                int delta = 0;
                bool any = false;
                for (int i = 0; i < modifiers.Count; i++)
                {
                    var mod = modifiers[i];
                    if (mod == null || mod.SourceId != sourceId) continue;
                    if (mod.Operation == ModifierOperation.Add) { delta += mod.Amount; any = true; }
                    else if (mod.Operation == ModifierOperation.Subtract) { delta -= mod.Amount; any = true; }
                }
                if (any) into.Add(new MovementRangeContribution(item, delta));
            }
        }

        private static IEnumerable<ItemSO> EnumerateItems(IInventoryService inventory)
        {
            var seen = new HashSet<string>();
            foreach (var slot in inventory.PassiveItems)
                if (slot?.Item != null && seen.Add(slot.Item.ItemId)) yield return slot.Item;
            foreach (var slot in inventory.ActiveItems)
                if (slot?.Item != null && seen.Add(slot.Item.ItemId)) yield return slot.Item;
        }
    }
}
