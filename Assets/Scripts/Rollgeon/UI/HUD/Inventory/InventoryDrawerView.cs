using System.Collections.Generic;
using Patterns;
using Rollgeon.Items;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Inventory
{
    /// <summary>
    /// Contenido del drawer de inventario: los items comprados (pasivos + activos) en el
    /// grid rombo de <see cref="InventoryDiamondLayout"/> — filas de 5 alternadas, 20
    /// celdas mínimas. Con más items se agregan filas y el panel crece en alto.
    /// </summary>
    /// <remarks>
    /// Misma estructura que <c>DiceBagView</c>: se repuebla al abrir (evento
    /// <see cref="SlidingDrawer.Opened"/>), así que no escucha <c>OnItemChanged</c> — los
    /// consumibles usados simplemente no están en la próxima apertura. Las celdas se
    /// posicionan a mano con la math del layout (sin LayoutGroup).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Inventory Drawer View")]
    [RequireComponent(typeof(SlidingDrawer))]
    public class InventoryDrawerView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _panel;
        [SerializeField, Required] private RectTransform _grid;
        [SerializeField, Required] private InventoryItemSlotView _slotPrefab;

        [Title("Textos")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        // Entrada de display: items comprados y boss rewards (BUG-85) comparten celda.
        private readonly struct DisplayEntry
        {
            public readonly Sprite Icon;
            public readonly System.Func<string> Tooltip;

            public DisplayEntry(Sprite icon, System.Func<string> tooltip)
            {
                Icon = icon;
                Tooltip = tooltip;
            }
        }

        private readonly List<InventoryItemSlotView> _slots = new();
        private readonly List<DisplayEntry> _items = new();

        private SlidingDrawer _drawer;

        private void Awake()
        {
            if (_drawer == null) TryGetComponent(out _drawer);
            if (_drawer != null) _drawer.Opened += Rebuild;

            RefreshCaptions();
            LocalizationRefresh.Subscribe(RefreshCaptions);
        }

        private void OnDestroy()
        {
            if (_drawer != null) _drawer.Opened -= Rebuild;
            LocalizationRefresh.Unsubscribe(RefreshCaptions);
        }

        private void RefreshCaptions()
        {
            if (_titleLabel != null)
                _titleLabel.text = LocalizedContent.Ui(InventoryTextKeys.Title, "Inventario");
        }

        /// <summary>
        /// Repuebla con el inventario actual, posiciona las celdas según el patrón rombo
        /// y redimensiona el panel según las filas. Corre en cada apertura, así que
        /// refleja las compras sin escuchar nada.
        /// </summary>
        public void Rebuild()
        {
            CollectItems();

            int rows = InventoryDiamondLayout.Rows(_items.Count);
            int cells = InventoryDiamondLayout.VisibleCells(_items.Count);

            EnsureSlots(cells);
            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < cells;
                _slots[i].gameObject.SetActive(used);
                if (!used) continue;

                var rt = (RectTransform)_slots[i].transform;
                rt.anchoredPosition = InventoryDiamondLayout.CellPosition(i);
                if (i < _items.Count) _slots[i].Bind(_items[i].Icon, _items[i].Tooltip);
                else _slots[i].Bind(null, null);
            }

            // Solo el alto: la X la maneja SlidingDrawer y el ancho es fijo.
            if (_panel != null)
            {
                _panel.sizeDelta = new Vector2(
                    InventoryDiamondLayout.PanelWidth,
                    InventoryDiamondLayout.PanelHeight(rows));
            }
        }

        // Los services tienen scope Run/Global: en escenas sin run no están registrados
        // y el drawer se muestra vacío en vez de romper.
        private void CollectItems()
        {
            _items.Clear();

            if (ServiceLocator.TryGetService<IInventoryService>(out var inv) && inv != null)
            {
                AppendItems(inv.PassiveItems);
                AppendItems(inv.ActiveItems);
            }

            // BUG-85: los boss rewards reclamados también se listan — no entran al
            // InventoryService (no son ItemSO) pero el jugador tiene que poder ver
            // qué mejoras de personaje lleva la run.
            if (ServiceLocator.TryGetService<Rollgeon.Upgrades.Character.ICharacterRewardService>(
                    out var rewards) && rewards != null)
            {
                AppendRewards(rewards.ClaimedRewards);
            }
        }

        private void AppendItems(IReadOnlyList<InventorySlot> slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++)
            {
                var item = slots[i]?.Item;
                if (item == null) continue;
                var captured = item;
                _items.Add(new DisplayEntry(item.Icon, () => BuildItemTooltip(captured)));
            }
        }

        private void AppendRewards(IReadOnlyList<Rollgeon.Upgrades.Character.CharacterRewardSO> claimed)
        {
            if (claimed == null) return;
            for (int i = 0; i < claimed.Count; i++)
            {
                var reward = claimed[i];
                if (reward == null) continue;
                _items.Add(new DisplayEntry(reward.Icon, () => BuildRewardTooltip(reward)));
            }
        }

        private static string BuildItemTooltip(ItemSO item)
        {
            string fallbackName = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : item.ItemId;
            string name = LocalizedContent.Name(item.ItemId, fallbackName);
            string body = LocalizedContent.Description(item.ItemId, item.Description ?? string.Empty);
            return string.IsNullOrEmpty(body) ? $"<b>{name}</b>" : $"<b>{name}</b>\n{body}";
        }

        private static string BuildRewardTooltip(Rollgeon.Upgrades.Character.CharacterRewardSO reward)
        {
            string fallbackName = !string.IsNullOrEmpty(reward.DisplayName) ? reward.DisplayName : reward.UpgradeId;
            string name = LocalizedContent.Name(reward.UpgradeId, fallbackName);
            string body = LocalizedContent.Description(reward.UpgradeId, reward.Description ?? string.Empty);
            return string.IsNullOrEmpty(body) ? $"<b>{name}</b>" : $"<b>{name}</b>\n{body}";
        }

        // El pool se reusa y solo se apaga: el panel se repuebla en cada apertura.
        // Anchor top-left + pivot centrado para que CellPosition sea el centro exacto.
        private void EnsureSlots(int needed)
        {
            while (_slots.Count < needed)
            {
                var slot = Instantiate(_slotPrefab, _grid);
                var rt = (RectTransform)slot.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.one * InventoryDiamondLayout.DiamondSize;
                _slots.Add(slot);
            }
        }
    }
}
