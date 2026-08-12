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
    /// Contenido del drawer de inventario: los items comprados (pasivos + activos) en un
    /// grid de 6 por fila. Con más de 6 items wrapean a filas nuevas y la caja y el panel
    /// crecen en alto.
    /// </summary>
    /// <remarks>
    /// Misma estructura que <c>DiceBagView</c>: se repuebla al abrir (evento
    /// <see cref="SlidingDrawer.Opened"/>), así que no escucha <c>OnItemChanged</c> — los
    /// consumibles usados simplemente no están en la próxima apertura.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Inventory Drawer View")]
    [RequireComponent(typeof(SlidingDrawer))]
    public class InventoryDrawerView : MonoBehaviour
    {
        // Layout compartido con el installer (fuente única de verdad). La celda es x4 de
        // UI-sheet_2 (16px) — pixel art: mantener múltiplos enteros.
        public const int Columns = 6;
        public const float CellSize = 64f;
        public const float CellSpacing = 8f;
        public const float BoxPadding = 12f;
        public const float PanelPadding = 16f;
        public const float TitleHeight = 38f;
        public const float CaptionHeight = 26f;
        public const float SectionGap = 6f;

        public static readonly float GridWidth = Columns * CellSize + (Columns - 1) * CellSpacing;
        public static readonly float BoxWidth = GridWidth + 2f * BoxPadding;
        public static readonly float PanelWidth = BoxWidth + 2f * PanelPadding;

        [Title("Refs")]
        [SerializeField, Required] private RectTransform _panel;
        [SerializeField, Required] private RectTransform _itemsBox;
        [SerializeField, Required] private RectTransform _grid;
        [SerializeField, Required] private InventoryItemSlotView _slotPrefab;

        [Title("Textos")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _captionLabel;

        private readonly List<InventoryItemSlotView> _slots = new();
        private readonly List<ItemSO> _items = new();

        private SlidingDrawer _drawer;

        // ==================================================================
        // Fórmulas de layout (puras, testeables)
        // ==================================================================

        /// <summary>Celdas a dibujar: mínimo una fila completa, y la última fila se rellena.</summary>
        public static int VisibleCells(int itemCount)
        {
            int rows = Rows(itemCount);
            return rows * Columns;
        }

        public static int Rows(int itemCount)
        {
            if (itemCount <= 0) return 1;
            return (itemCount + Columns - 1) / Columns;
        }

        public static float BoxHeight(int rows)
            => 2f * BoxPadding + rows * CellSize + (rows - 1) * CellSpacing;

        public static float PanelHeight(int rows)
            => PanelPadding + TitleHeight + SectionGap + CaptionHeight + SectionGap
               + BoxHeight(rows) + PanelPadding;

        // ==================================================================
        // Ciclo de vida
        // ==================================================================

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
            if (_captionLabel != null)
                _captionLabel.text = LocalizedContent.Ui(InventoryTextKeys.ItemsCaption, "Objetos");
        }

        // ==================================================================
        // Rebuild
        // ==================================================================

        /// <summary>
        /// Repuebla con el inventario actual y redimensiona caja y panel según las filas.
        /// Corre en cada apertura, así que refleja las compras sin escuchar nada.
        /// </summary>
        public void Rebuild()
        {
            CollectItems();

            int rows = Rows(_items.Count);
            int cells = VisibleCells(_items.Count);

            EnsureSlots(cells);
            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < cells;
                _slots[i].gameObject.SetActive(used);
                if (used) _slots[i].Bind(i < _items.Count ? _items[i] : null);
            }

            // Solo el alto: la X la maneja SlidingDrawer y el ancho es fijo. Con más de
            // 8 filas el panel se saldría de 1080 — hoy el pool de items no llega ahí.
            if (_itemsBox != null)
                _itemsBox.sizeDelta = new Vector2(BoxWidth, BoxHeight(rows));
            if (_panel != null)
                _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight(rows));
        }

        // El service tiene scope Run: en escenas sin run no está registrado y el drawer
        // se muestra vacío en vez de romper.
        private void CollectItems()
        {
            _items.Clear();
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inv) || inv == null)
                return;

            AppendItems(inv.PassiveItems);
            AppendItems(inv.ActiveItems);
        }

        private void AppendItems(IReadOnlyList<InventorySlot> slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++)
            {
                var item = slots[i]?.Item;
                if (item != null) _items.Add(item);
            }
        }

        // El pool se reusa y solo se apaga: el panel se repuebla en cada apertura.
        private void EnsureSlots(int needed)
        {
            while (_slots.Count < needed)
                _slots.Add(Instantiate(_slotPrefab, _grid));
        }
    }
}
