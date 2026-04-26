using System;
using Patterns;
using Rollgeon.Economy;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Stub interactable del shop item — mismo patrón que
    /// <c>FloorExitInteractable</c>. Vive sobre el pedestal instanciado por el
    /// <see cref="ShopManagerService"/>. <see cref="Interact"/> es el hook del
    /// (futuro) <c>IInteractionService</c> (§7.7); hoy se invoca manualmente.
    /// TECHNICAL.md §17.F.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MVP: ejecuta la compra inline — checkea gold vía <see cref="IEconomyService"/>,
    /// descuenta, notifica al <see cref="IShopManagerService"/>. Cuando §7.7 y §8
    /// aterricen, se migra a <c>InteractableComponent</c> + behavior con
    /// <c>EffDeductGold</c> / <c>EffAddItemToInventory</c> / <c>EffConsumeProp</c>.
    /// </para>
    /// <para>
    /// <b>Hover feedback.</b> En vez de llamar al <c>ItemInspectView</c>
    /// directamente (no existe aún), publica <c>OnShopItemTargetChanged</c> en
    /// <see cref="OnHoverEnter"/> / <see cref="OnHoverExit"/>. Payload per enum:
    /// <c>[bool hasTarget, string itemName, string description, int price, Sprite icon]</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Shop/Shop Item Pedestal Interactable")]
    public sealed class ShopItemPedestalInteractable : MonoBehaviour
    {
        private const string LogPrefix = "[ShopItemPedestalInteractable] ";

        [Tooltip("Label mostrado en el prompt. Se rellena desde Configure — NO editar en prefab.")]
        public string InteractLabel;

        private Guid _roomInstanceId;
        private ShopSlot _slot;
        private IShopManagerService _service;

        /// <summary>Llamado por <see cref="ShopManagerService"/> al instanciar el pedestal.</summary>
        public void Configure(Guid roomInstanceId, ShopSlot slot, IShopManagerService service)
        {
            _roomInstanceId = roomInstanceId;
            _slot = slot;
            _service = service;
            InteractLabel = BuildLabel(slot);
        }

        /// <summary>
        /// Ejecuta la compra. En el MVP se llama a mano (no hay
        /// <c>IInteractionService</c>) — un test, un trigger collider o un
        /// handler temporal en el hero invoca esto cuando el jugador presiona
        /// el botón de interacción.
        /// </summary>
        public void Interact()
        {
            if (_slot == null || _service == null)
            {
                Debug.LogWarning(LogPrefix + "Interact invocado sin Configure previo — no-op.");
                return;
            }
            if (_slot.Purchased)
            {
                return;
            }

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
            {
                Debug.LogError(LogPrefix + "IEconomyService no registrado — no se puede procesar la compra.");
                return;
            }

            if (!economy.Spend(_slot.Price))
            {
                Debug.Log(LogPrefix + $"Gold insuficiente ({economy.CurrentGold} < {_slot.Price}) — compra rechazada.");
                return;
            }

            // TODO (§18): EffAddItemToInventory(slot.Item.ItemId). Hoy sin
            // IInventoryService, el ítem se descarta — el oro se cobró igual.
            // El evento OnItemObtained queda para cuando el inventario exista.

            _service.NotifyItemPurchased(_roomInstanceId, _slot.SpawnPointId, _slot.Price);
        }

        /// <summary>
        /// Llamado por el hover / target selector del (futuro) IInteractionService
        /// cuando el jugador se acerca al pedestal.
        /// </summary>
        public void OnHoverEnter()
        {
            if (_slot == null || _slot.Item == null) return;
            EventManager.Trigger(
                EventName.OnShopItemTargetChanged,
                true,
                _slot.Item.DisplayName,
                _slot.Item.Description,
                _slot.Price,
                _slot.Item.Icon);
        }

        public void OnHoverExit()
        {
            EventManager.Trigger(
                EventName.OnShopItemTargetChanged,
                false,
                string.Empty,
                string.Empty,
                0,
                (Sprite)null);
        }

        private static string BuildLabel(ShopSlot slot)
        {
            if (slot == null || slot.Item == null) return "[F] Comprar";
            return $"[F] Comprar {slot.Item.DisplayName} ({slot.Price}G)";
        }
    }
}
