using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// MVP: la compra en sí (cobrar → entregar → marcar purchased) vive en
    /// <see cref="ShopPurchase"/>; este componente solo resuelve los servicios, decide
    /// la severidad del log y maneja prompt/hover. Cuando §7.7 y §8 aterricen, se migra
    /// a <c>InteractableComponent</c> + behavior con <c>EffDeductGold</c> /
    /// <c>EffAddItemToInventory</c> / <c>EffConsumeProp</c>.
    /// </para>
    /// <para>
    /// <b>Hover feedback.</b> En vez de llamar al <c>ItemInspectView</c>
    /// directamente (no existe aún), publica <c>OnShopItemTargetChanged</c> en
    /// <see cref="OnHoverEnter"/> / <see cref="OnHoverExit"/>. Payload per enum:
    /// <c>[bool hasTarget, string itemName, string description, int price, Sprite icon]</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Shop/Shop Item Pedestal Interactable")]
    public sealed class ShopItemPedestalInteractable : MonoBehaviour, Rollgeon.UI.Cursor.ICursorHoverable
    {
        private const string LogPrefix = "[ShopItemPedestalInteractable] ";

        [Tooltip("Label mostrado en el prompt. Se rellena desde Configure — NO editar en prefab.")]
        public string InteractLabel;

        [Tooltip("Distancia (en world units) a la que el jugador puede comprar este pedestal " +
                 "presionando la tecla configurada. Default 1.5 = aprox 1 tile. " +
                 "0 = desactiva la interacción.")]
        [SerializeField]
        private float _interactRange = 1.5f;

        [Tooltip("Tecla del Input System que dispara la compra cuando el jugador está dentro " +
                 "de InteractRange. Default Key.F.")]
        [SerializeField]
        private Key _interactKey = Key.F;

        private Guid _roomInstanceId;
        private ShopSlot _slot;
        private IShopManagerService _service;
        private bool _playerInRangeLastTick;
        private bool _lastCanAfford;

        /// <summary>Llamado por <see cref="ShopManagerService"/> al instanciar el pedestal.</summary>
        public void Configure(Guid roomInstanceId, ShopSlot slot, IShopManagerService service)
        {
            _roomInstanceId = roomInstanceId;
            _slot = slot;
            _service = service;
            InteractLabel = BuildLabel(slot);
        }

        /// <summary>
        /// Arma el contenido del <see cref="InteractionPromptView"/> — precio y color
        /// de "afford" salen de <see cref="IEconomyService"/> (mismo chequeo que usa
        /// <see cref="Interact"/> para la compra real, vía <c>CanAfford</c>).
        /// </summary>
        private InteractionPromptContent BuildPromptContent()
        {
            string title = _slot?.Item != null
                ? Rollgeon.Localization.LocalizedContent.Name(_slot.Item.EntryId, !string.IsNullOrEmpty(_slot.Item.DisplayName) ? _slot.Item.DisplayName : _slot.Item.EntryId)
                : string.Empty;
            string description = _slot?.Item != null ? Rollgeon.Localization.LocalizedContent.Description(_slot.Item.EntryId, _slot.Item.Description ?? string.Empty) : string.Empty;
            int price = _slot?.Price ?? 0;

            bool canAfford = !ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null
                || economy.CanAfford(price);

            return new InteractionPromptContent(_interactKey.ToString(), "Comprar", title, description, price, canAfford);
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

            ServiceLocator.TryGetService<IEconomyService>(out var economy);
            ServiceLocator.TryGetService<IInventoryService>(out var inventory);

            var result = ShopPurchase.Buy(economy, inventory, _service, _roomInstanceId, _slot);
            if (!result.Success)
            {
                // Gold insuficiente es flujo normal (el jugador puede spamear F sin plata);
                // el resto son bugs de wiring y merecen ruido.
                if (result.Failure == ShopPurchaseFailure.NotEnoughGold)
                    Debug.Log(LogPrefix + result.Message + " — compra rechazada.");
                else
                    Debug.LogError(LogPrefix + result.Message);
                return;
            }

            if (!string.IsNullOrEmpty(result.Message)) Debug.LogWarning(LogPrefix + result.Message);

            // BUG fix: Update() early-returns por _slot.Purchased ANTES de llegar al
            // chequeo de rango que dispara UpdatePromptVisibility(false) — sin este Hide
            // explícito el prompt quedaba pegado en pantalla tras comprar.
            InteractionPromptView.Hide(GetInstanceID());
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
                Rollgeon.Localization.LocalizedContent.Name(_slot.Item.EntryId, _slot.Item.DisplayName),
                Rollgeon.Localization.LocalizedContent.Description(_slot.Item.EntryId, _slot.Item.Description),
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
            string name = Rollgeon.Localization.LocalizedContent.Name(slot.Item.EntryId, !string.IsNullOrEmpty(slot.Item.DisplayName)
                ? slot.Item.DisplayName
                : slot.Item.EntryId);
            return $"[F] Comprar {name} ({slot.Price}G)";
        }

        // -----------------------------------------------------------------
        // MVP de input: cuando el jugador está dentro de InteractRange y presiona
        // InteractKey (F por default), se dispara la compra. El sistema canónico
        // (§7.7 IInteractionService) no aterrizó; este es el bridge interino.
        // -----------------------------------------------------------------
        private void Update()
        {
            if (_interactRange <= 0f) return;
            if (_slot == null || _slot.Purchased) return;

            bool inRange = IsPlayerInRange();
            if (inRange != _playerInRangeLastTick)
            {
                _playerInRangeLastTick = inRange;
                UpdatePromptVisibility(inRange);
            }
            else if (inRange)
            {
                // El gold pudo cambiar mientras el jugador está parado en rango (ej.
                // vendió algo, o recogió oro de otro pedestal) — re-Show refresca el
                // color del precio sin esperar a un cambio de rango.
                bool canAfford = BuildPromptContent().CanAfford;
                if (canAfford != _lastCanAfford) UpdatePromptVisibility(true);
            }

            if (!inRange) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_interactKey].wasPressedThisFrame) return;

            Interact();
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (!visible)
            {
                InteractionPromptView.Hide(GetInstanceID());
                return;
            }
            var content = BuildPromptContent();
            _lastCanAfford = content.CanAfford;
            // El botón "[F] Comprar" del prompt y la tecla física convergen en Interact().
            InteractionPromptView.Show(GetInstanceID(), content, Interact);
        }

        private void OnDisable()
        {
            // Esconde el prompt si el pedestal se desactiva (ej. compra cerró el visual).
            _playerInRangeLastTick = false;
            InteractionPromptView.Hide(GetInstanceID());
        }

        private bool IsPlayerInRange()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null) return false;
            var playerGuid = playerService.PlayerGuid;
            if (playerGuid == Guid.Empty) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!grid.TryGetPosition(playerGuid, out var playerCoord)) return false;

            // Distancia en el plano XZ — un spawn point elevado no debe comerse el
            // presupuesto de rango (mismo fix que EnchantmentAltarInteractable: el
            // altar a y=1 dejaba las diagonales exactamente en el borde del rango).
            var playerWorld = grid.GridToWorld(playerCoord);
            var delta = playerWorld - transform.position;
            float distSq = delta.x * delta.x + delta.z * delta.z;
            return distSq <= _interactRange * _interactRange;
        }
    }
}
