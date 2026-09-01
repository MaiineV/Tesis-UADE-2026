using Patterns;
using Rollgeon.Player;
using Rollgeon.Shop;

namespace Rollgeon.Items
{
    /// <summary>
    /// Gate estilo Isaac para pools (tienda, loot): un item con
    /// <see cref="ItemSO.UniquePerRun"/> que el jugador YA posee — en el inventario o
    /// como innato de su clase (<c>ClassHeroSO.InnateItemIds</c>, ej. el Warrior "tiene"
    /// Instinto de Supervivencia de nacimiento) — no vuelve a aparecer en la run.
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
            if (item == null || !item.UniquePerRun) return false;

            if (ServiceLocator.TryGetService<IInventoryService>(out var inv) && inv != null
                && inv.HasItem(item.ItemId))
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

            return false;
        }
    }
}
