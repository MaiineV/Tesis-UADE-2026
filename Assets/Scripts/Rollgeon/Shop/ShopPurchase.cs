using System;
using Rollgeon.Economy;
using Rollgeon.Items;

namespace Rollgeon.Shop
{
    /// <summary>Por qué se rechazó una compra. El caller decide la severidad del log.</summary>
    public enum ShopPurchaseFailure
    {
        None,
        NoSlot,
        AlreadyPurchased,
        NotEnoughGold,
        ServiceMissing
    }

    /// <summary>
    /// Resultado de <see cref="ShopPurchase.Buy"/>. Mismo patrón que
    /// <c>EnchantmentRollResult</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Warning"/> existe porque la compra puede cerrarse (oro cobrado, slot
    /// marcado) y la entrega igual no aterrizar — inventario lleno, o un tipo de reward
    /// que todavía no sabemos entregar. Eso no revierte la compra (era el comportamiento
    /// del pedestal desde el MVP) pero el caller tiene que poder decirlo.
    /// </remarks>
    public readonly struct ShopPurchaseResult
    {
        public bool Success { get; }
        public ShopPurchaseFailure Failure { get; }
        public string Message { get; }
        public int GoldPaid { get; }

        private ShopPurchaseResult(bool success, ShopPurchaseFailure failure, string message, int goldPaid)
        {
            Success = success;
            Failure = failure;
            Message = message;
            GoldPaid = goldPaid;
        }

        public static ShopPurchaseResult Ok(int goldPaid, string warning = null)
            => new ShopPurchaseResult(true, ShopPurchaseFailure.None, warning, goldPaid);

        public static ShopPurchaseResult Fail(ShopPurchaseFailure failure, string message)
            => new ShopPurchaseResult(false, failure, message, 0);
    }

    /// <summary>
    /// La compra de un slot de shop, sin UI y sin escena: cobrar → entregar → marcar
    /// el slot como purchased. TECHNICAL.md §17.F.4.
    /// </summary>
    /// <remarks>
    /// Vivía inline en <see cref="ShopItemPedestalInteractable.Interact"/>. Se extrajo
    /// para que la dev console pueda comprar por el mismo camino que el pedestal en vez
    /// de reimplementar el cobro — dos copias del flujo se despegan en cuanto alguien
    /// agregue un tipo nuevo de <see cref="IShopRewardEntry"/>.
    /// <para>
    /// Los servicios entran <b>por parámetro</b>, no por <c>ServiceLocator</c>: así los
    /// tests y los comandos de consola pueden pasar sus fakes.
    /// </para>
    /// </remarks>
    public static class ShopPurchase
    {
        public static ShopPurchaseResult Buy(IEconomyService economy, IInventoryService inventory,
                                             IShopManagerService shop, Guid roomInstanceId, ShopSlot slot)
        {
            if (slot == null)
                return ShopPurchaseResult.Fail(ShopPurchaseFailure.NoSlot, "No hay slot que comprar.");
            if (slot.Purchased)
                return ShopPurchaseResult.Fail(ShopPurchaseFailure.AlreadyPurchased, "El slot ya está comprado.");
            if (economy == null)
                return ShopPurchaseResult.Fail(ShopPurchaseFailure.ServiceMissing,
                    "IEconomyService no registrado — no se puede procesar la compra.");
            if (shop == null)
                return ShopPurchaseResult.Fail(ShopPurchaseFailure.ServiceMissing,
                    "IShopManagerService no registrado — no se puede cerrar la compra.");

            if (!economy.Spend(slot.Price))
                return ShopPurchaseResult.Fail(ShopPurchaseFailure.NotEnoughGold,
                    $"Gold insuficiente ({economy.CurrentGold} < {slot.Price}).");

            string warning = Deliver(slot.Item, inventory);
            shop.NotifyItemPurchased(roomInstanceId, slot.SpawnPointId, slot.Price);
            return ShopPurchaseResult.Ok(slot.Price, warning);
        }

        /// <summary>
        /// Dispatch polimórfico de la entrega. El reward canónico es <see cref="ItemSO"/>
        /// (activo o passive) → va al inventario, que aplica sus <c>PassiveItemHook</c> al
        /// obtenerlo. Cuando se agreguen otros tipos de <see cref="IShopRewardEntry"/>,
        /// sumar el case acá. Devuelve el warning si no se pudo entregar, o <c>null</c>.
        /// </summary>
        private static string Deliver(IShopRewardEntry entry, IInventoryService inventory)
        {
            switch (entry)
            {
                case ItemSO item:
                    if (inventory == null)
                        return "IInventoryService no registrado — el ítem comprado no se entrega.";
                    return inventory.AddItem(item)
                        ? null
                        : $"AddItem('{item.ItemId}') rechazado (inventario lleno?).";
                case null:
                    return "Entry null — no se entrega nada.";
                default:
                    return $"Tipo de reward no soportado: {entry.GetType().Name}.";
            }
        }
    }
}
