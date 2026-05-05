using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Player;
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

        [Tooltip("Distancia (en world units) a la que el jugador puede comprar este pedestal " +
                 "presionando la tecla configurada. Default 1.5 = aprox 1 tile. " +
                 "0 = desactiva la interacción.")]
        [SerializeField]
        private float _interactRange = 1.5f;

        [Tooltip("Tecla del Input System que dispara la compra cuando el jugador está dentro " +
                 "de InteractRange. Default Key.F.")]
        [SerializeField]
        private Key _interactKey = Key.F;

        [Tooltip("Prompt opcional que se activa cuando el jugador entra en InteractRange y " +
                 "se desactiva al salir. Suele ser un Canvas hijo con un TMP. Si null, no " +
                 "se muestra prompt.")]
        [SerializeField]
        private GameObject _promptVisual;

        [Tooltip("Label TMP opcional dentro del prompt. Si está cableado, se rellena con " +
                 "el InteractLabel ('[F] Comprar Poción (8G)') al entrar en rango.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI _promptLabel;

        private Guid _roomInstanceId;
        private ShopSlot _slot;
        private IShopManagerService _service;
        private bool _playerInRangeLastTick;

        /// <summary>Llamado por <see cref="ShopManagerService"/> al instanciar el pedestal.</summary>
        public void Configure(Guid roomInstanceId, ShopSlot slot, IShopManagerService service)
        {
            _roomInstanceId = roomInstanceId;
            _slot = slot;
            _service = service;
            InteractLabel = BuildLabel(slot);
            EnsurePromptRefs();
            UpdatePromptVisibility(false);
        }

        /// <summary>
        /// Auto-resolve del prompt si los SerializeFields están null. Convención:
        /// hijo llamado "Prompt" (GameObject) con un TextMeshProUGUI descendiente.
        /// Si no existe, lo crea en runtime con un Canvas worldspace + TMP visible
        /// arriba del pedestal — así el prefab no requiere wiring manual.
        /// </summary>
        private void EnsurePromptRefs()
        {
            if (_promptVisual == null)
            {
                var t = transform.Find("Prompt");
                if (t != null) _promptVisual = t.gameObject;
            }
            if (_promptVisual == null)
            {
                _promptVisual = BuildAutoPrompt();
            }
            if (_promptLabel == null && _promptVisual != null)
            {
                _promptLabel = _promptVisual.GetComponentInChildren<TMPro.TextMeshProUGUI>(includeInactive: true);
            }
        }

        /// <summary>
        /// Construye un prompt minimal: GameObject "Prompt" con un Canvas worldspace
        /// + un TMP child. Posicionado arriba del pedestal (Y=2.5 local). Se devuelve
        /// inactivo — <see cref="UpdatePromptVisibility"/> lo activa al entrar en rango.
        /// </summary>
        private GameObject BuildAutoPrompt()
        {
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(transform, worldPositionStays: false);
            promptGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            promptGo.transform.localRotation = Quaternion.identity;
            promptGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var canvas = promptGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1;
            promptGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(promptGo.transform, worldPositionStays: false);
            var rt = labelGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 80f);
            rt.localPosition = Vector3.zero;

            var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = 32f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = string.Empty;
            tmp.raycastTarget = false;

            promptGo.SetActive(false);
            return promptGo;
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

            TryDeliverItemToInventory(_slot.Item != null ? _slot.Item.ItemId : null);

            _service.NotifyItemPurchased(_roomInstanceId, _slot.SpawnPointId, _slot.Price);
        }

        /// <summary>
        /// Resuelve el <see cref="ItemSO"/> en el catálogo por <paramref name="shopItemId"/>
        /// (debe matchear el <c>ItemSO.ItemId</c>) y lo agrega al inventario. Si el catálogo
        /// o el inventario no están registrados, sólo loggea — el oro ya se cobró y el
        /// pedestal queda como purchased.
        /// </summary>
        private static void TryDeliverItemToInventory(string shopItemId)
        {
            if (string.IsNullOrEmpty(shopItemId))
            {
                Debug.LogWarning(LogPrefix + "ShopItemDef sin ItemId — no se puede entregar al inventario.");
                return;
            }

            if (!ServiceLocator.TryGetService<ItemCatalogSO>(out var catalog) || catalog == null)
            {
                Debug.LogWarning(LogPrefix + "ItemCatalogSO no registrado — el ítem comprado no se entrega.");
                return;
            }

            var itemSo = catalog.GetById(shopItemId);
            if (itemSo == null)
            {
                Debug.LogWarning(LogPrefix + $"ItemSO con ItemId='{shopItemId}' no existe en ItemCatalog. " +
                                              "Verificá que el ShopItemDef.ItemId matchee el ItemSO.ItemId del catálogo.");
                return;
            }

            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null)
            {
                Debug.LogWarning(LogPrefix + "IInventoryService no registrado — el ítem comprado no se entrega.");
                return;
            }

            if (!inventory.AddItem(itemSo))
            {
                Debug.LogWarning(LogPrefix + $"AddItem('{shopItemId}') rechazado (inventario lleno?).");
            }
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

            if (!inRange) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_interactKey].wasPressedThisFrame) return;

            Interact();
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (_promptVisual != null) _promptVisual.SetActive(visible);
            if (_promptLabel != null && visible) _promptLabel.text = InteractLabel ?? string.Empty;
        }

        private void OnDisable()
        {
            // Esconde el prompt si el pedestal se desactiva (ej. compra cerró el visual).
            _playerInRangeLastTick = false;
            if (_promptVisual != null) _promptVisual.SetActive(false);
        }

        private bool IsPlayerInRange()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null) return false;
            var playerGuid = playerService.PlayerGuid;
            if (playerGuid == Guid.Empty) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!grid.TryGetPosition(playerGuid, out var playerCoord)) return false;

            var playerWorld = grid.GridToWorld(playerCoord);
            float distSq = (playerWorld - transform.position).sqrMagnitude;
            return distSq <= _interactRange * _interactRange;
        }
    }
}
