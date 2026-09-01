using System.Collections.Generic;
using Patterns;
using Rollgeon.Player;
using Rollgeon.Shop;

namespace Rollgeon.Items
{
    /// <summary>
    /// Gate estilo Isaac para pools (tienda, loot). Dos reglas independientes (OR):
    /// <list type="bullet">
    /// <item><see cref="ItemSO.UniquePerRun"/>: un item que el jugador YA posee — en el
    /// inventario o como innato de su clase (<c>ClassHeroSO.InnateItemIds</c>, ej. el
    /// Warrior "tiene" Instinto de Supervivencia de nacimiento) — no vuelve a aparecer
    /// en la run.</item>
    /// <item><see cref="ItemSO.FamilyExclusive"/>: poseer CUALQUIER item de la misma
    /// <see cref="ItemSO.FamilyId"/> bloquea al candidato (incluye duplicados de sí
    /// mismo). Para pares excluyentes por GDD (Corazón/Tesoro de la Fortuna). La rama
    /// no mira <c>InnateItemIds</c>: son item ids, no familias, y ningún innato actual
    /// pertenece a una familia.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Mismo patrón de gate estático que <c>MetaUnlockGate</c>: los pools lo consultan
    /// en la elegibilidad del roll. Degrada permisivo sin servicios (tests, tooling de
    /// editor): sin inventario no se puede afirmar posesión ⇒ el item sale.
    /// Solo afecta rolls FUTUROS — los slots de tienda ya generados quedan congelados
    /// por diseño (ShopItemState).
    /// </remarks>
    public static class UniquePerRunGate
    {
        public static bool IsBlocked(IShopRewardEntry entry) => IsBlocked(entry as ItemSO);

        public static bool IsBlocked(ItemSO item)
        {
            if (item == null) return false;
            if (!item.UniquePerRun && !item.FamilyExclusive) return false;

            var inv = ServiceLocator.TryGetService<IInventoryService>(out var found) ? found : null;

            if (item.UniquePerRun)
            {
                if (inv != null && inv.HasItem(item.ItemId))
                    return true;

                if (ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null)
                {
                    var innate = ps.CurrentHero != null ? ps.CurrentHero.InnateItemIds : null;
                    if (innate != null)
                    {
                        for (int i = 0; i < innate.Count; i++)
                            if (string.Equals(innate[i], item.ItemId, System.StringComparison.Ordinal))
                                return true;
                    }
                }
            }

            if (item.FamilyExclusive && !string.IsNullOrEmpty(item.FamilyId) && inv != null)
            {
                if (AnySlotSharesFamily(inv.PassiveItems, item.FamilyId)
                    || AnySlotSharesFamily(inv.ActiveItems, item.FamilyId))
                    return true;
            }

            return false;
        }

        private static bool AnySlotSharesFamily(IReadOnlyList<InventorySlot> slots, string familyId)
        {
            if (slots == null) return false;
            for (int i = 0; i < slots.Count; i++)
            {
                var owned = slots[i]?.Item;
                if (owned != null && string.Equals(owned.FamilyId, familyId, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
