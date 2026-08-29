using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Items;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Inventory;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Math del grid rombo (<see cref="InventoryDiamondLayout"/>), el resize del rebuild
    /// y los estados de la celda (con/sin item, hover, tooltip).
    /// </summary>
    /// <remarks>
    /// El rebuild se prueba sin <c>IInventoryService</c> registrado (scope Run): el drawer
    /// tiene que mostrar las 20 celdas vacías del mockup en vez de romper.
    /// </remarks>
    [TestFixture]
    public class InventoryDrawerViewTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // Math del layout rombo
        // ------------------------------------------------------------------

        [Test]
        public void should_show_twenty_empty_cells_at_minimum()
        {
            // Arrange + Act + Assert — el mockup arranca con 4 filas de 5 rombos.
            Assert.AreEqual(20, InventoryDiamondLayout.VisibleCells(0));
            Assert.AreEqual(4, InventoryDiamondLayout.Rows(0));
            Assert.AreEqual(20, InventoryDiamondLayout.VisibleCells(20));
            Assert.AreEqual(4, InventoryDiamondLayout.Rows(20));
        }

        [Test]
        public void should_grow_a_full_row_beyond_twenty_items()
        {
            // Arrange + Act + Assert — con 21 items aparece la fila 5 completa.
            Assert.AreEqual(25, InventoryDiamondLayout.VisibleCells(21));
            Assert.AreEqual(5, InventoryDiamondLayout.Rows(21));
            Assert.AreEqual(30, InventoryDiamondLayout.VisibleCells(26));
        }

        [Test]
        public void should_stagger_odd_rows_half_a_cell_to_the_right()
        {
            // Arrange — celda 0 (fila par) vs celda 5 (fila impar, misma columna).
            var even = InventoryDiamondLayout.CellPosition(0);
            var odd = InventoryDiamondLayout.CellPosition(5);

            // Assert — misma columna, X corrida el stagger; el patrón se repite en fila 2.
            Assert.AreEqual(even.x + InventoryDiamondLayout.RowStagger, odd.x, 0.001f);
            Assert.AreEqual(even.x, InventoryDiamondLayout.CellPosition(10).x, 0.001f);
        }

        [Test]
        public void should_place_cells_by_column_and_row_pitch()
        {
            // Arrange
            float half = InventoryDiamondLayout.DiamondSize / 2f;

            // Act
            var first = InventoryDiamondLayout.CellPosition(0);
            var second = InventoryDiamondLayout.CellPosition(1);
            var rowBelow = InventoryDiamondLayout.CellPosition(5);

            // Assert
            Assert.AreEqual(new Vector2(half, -half), first);
            Assert.AreEqual(first.x + InventoryDiamondLayout.ColPitch, second.x, 0.001f);
            Assert.AreEqual(first.y - InventoryDiamondLayout.RowPitch, rowBelow.y, 0.001f);
        }

        [Test]
        public void should_grow_panel_height_per_extra_row()
        {
            // Arrange + Act + Assert — una fila extra suma exactamente RowPitch.
            float fourRows = InventoryDiamondLayout.PanelHeight(4);
            float fiveRows = InventoryDiamondLayout.PanelHeight(5);
            Assert.AreEqual(InventoryDiamondLayout.RowPitch, fiveRows - fourRows, 0.001f);
        }

        // ------------------------------------------------------------------
        // Rebuild sin service
        // ------------------------------------------------------------------

        [Test]
        public void should_show_twenty_positioned_cells_when_the_service_is_missing()
        {
            // Arrange
            var view = MakeDrawer(out var panel, out var grid);

            // Act
            view.Rebuild();

            // Assert — 20 celdas activas, posicionadas por la math (sin LayoutGroup).
            Assert.AreEqual(20, CountActiveChildren(grid));
            Assert.AreEqual(InventoryDiamondLayout.PanelHeight(4), panel.sizeDelta.y, 0.001f);
            Assert.AreEqual(InventoryDiamondLayout.PanelWidth, panel.sizeDelta.x, 0.001f);

            var slot7 = (RectTransform)grid.GetChild(7);
            Assert.AreEqual(InventoryDiamondLayout.CellPosition(7), slot7.anchoredPosition);
        }

        // ------------------------------------------------------------------
        // Celda: estados con/sin item + hover
        // ------------------------------------------------------------------

        [Test]
        public void should_show_the_empty_sprite_and_no_tooltip_on_an_empty_cell()
        {
            // Arrange
            var slot = MakeSlot(out var cell, out var icon, out var tooltip, out var sprites);

            // Act
            slot.Bind(null);

            // Assert
            Assert.AreSame(sprites.Empty, cell.sprite);
            Assert.IsFalse(icon.enabled);
            Assert.IsFalse(slot.IsOccupied);
            Assert.IsNotNull(tooltip.TextProvider, "vacío explícito, no null — null cae en AutoResolve");
            Assert.AreEqual(string.Empty, tooltip.TextProvider());
        }

        [Test]
        public void should_show_the_filled_sprite_and_centered_icon_when_bound()
        {
            // Arrange
            var item = MakeItem("moneda.suerte.par", "Moneda de la suerte", "Cada Par da +2 oro.");
            item.Icon = MakeSprite("icon");
            var slot = MakeSlot(out var cell, out var icon, out _, out var sprites);

            // Act
            slot.Bind(item);

            // Assert
            Assert.AreSame(sprites.Filled, cell.sprite);
            Assert.IsTrue(icon.enabled);
            Assert.AreSame(item.Icon, icon.sprite);
            Assert.IsTrue(slot.IsOccupied);
        }

        [Test]
        public void should_swap_to_hover_sprites_on_pointer_enter_and_back_on_exit()
        {
            // Arrange — celda ocupada.
            var item = MakeItem("test.hover.item", "Item", "desc");
            var slot = MakeSlot(out var cell, out _, out _, out var sprites);
            slot.Bind(item);
            var pointer = new PointerEventData(EventSystem.current);

            // Act + Assert — hover con item.
            slot.OnPointerEnter(pointer);
            Assert.AreSame(sprites.FilledHover, cell.sprite);
            slot.OnPointerExit(pointer);
            Assert.AreSame(sprites.Filled, cell.sprite);

            // Act + Assert — hover sin item.
            slot.Bind(null);
            slot.OnPointerEnter(pointer);
            Assert.AreSame(sprites.EmptyHover, cell.sprite);
            slot.OnPointerExit(pointer);
            Assert.AreSame(sprites.Empty, cell.sprite);
        }

        [Test]
        public void should_provide_name_and_description_through_the_tooltip()
        {
            // Arrange — id que no existe en las tablas: LocalizedContent devuelve el
            // fallback (los campos del asset) sin importar si la localización cargó.
            var item = MakeItem("test.tooltip.item", "Moneda de la suerte", "Cada Par da +2 oro.");
            var slot = MakeSlot(out _, out _, out var tooltip, out _);

            // Act
            slot.Bind(item);
            string text = tooltip.TextProvider();

            // Assert
            Assert.AreEqual("<b>Moneda de la suerte</b>\nCada Par da +2 oro.", text);
        }

        [Test]
        public void should_fall_back_to_the_item_id_when_there_is_no_display_name()
        {
            // Arrange
            var item = MakeItem("test.tooltip.nameless", displayName: "", description: "");
            var slot = MakeSlot(out _, out _, out var tooltip, out _);

            // Act
            slot.Bind(item);

            // Assert
            Assert.AreEqual("<b>test.tooltip.nameless</b>", tooltip.TextProvider());
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private sealed class SlotSprites
        {
            public Sprite Filled, Empty, FilledHover, EmptyHover;
        }

        private InventoryDrawerView MakeDrawer(out RectTransform panel, out RectTransform grid)
        {
            var go = new GameObject("InventoryDrawer", typeof(RectTransform));
            _spawned.Add(go);

            panel = AddRectChild(go.transform, "Panel");
            grid = AddRectChild(panel, "Grid");

            var slotPrefab = MakeSlot(out _, out _, out _, out _);

            go.AddComponent<SlidingDrawer>();
            var view = go.AddComponent<InventoryDrawerView>();
            SetPrivate(view, "_panel", panel);
            SetPrivate(view, "_grid", grid);
            SetPrivate(view, "_slotPrefab", slotPrefab);
            return view;
        }

        private InventoryItemSlotView MakeSlot(out Image cell, out Image icon,
            out UITooltipTrigger tooltip, out SlotSprites sprites)
        {
            var go = new GameObject("Slot", typeof(RectTransform));
            _spawned.Add(go);

            cell = go.AddComponent<Image>();
            icon = AddImageChild(go, "ItemIcon");
            tooltip = go.AddComponent<UITooltipTrigger>();

            sprites = new SlotSprites
            {
                Filled = MakeSprite("filled"),
                Empty = MakeSprite("empty"),
                FilledHover = MakeSprite("filledHover"),
                EmptyHover = MakeSprite("emptyHover"),
            };

            var slot = go.AddComponent<InventoryItemSlotView>();
            SetPrivate(slot, "_cellBg", cell);
            SetPrivate(slot, "_icon", icon);
            SetPrivate(slot, "_tooltip", tooltip);
            SetPrivate(slot, "_filledSprite", sprites.Filled);
            SetPrivate(slot, "_emptySprite", sprites.Empty);
            SetPrivate(slot, "_filledHoverSprite", sprites.FilledHover);
            SetPrivate(slot, "_emptyHoverSprite", sprites.EmptyHover);
            return slot;
        }

        private ItemSO MakeItem(string id, string displayName, string description)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            _spawned.Add(item);
            item.ItemId = id;
            item.DisplayName = displayName;
            item.Description = description;
            return item;
        }

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _spawned.Add(tex);
            _spawned.Add(sprite);
            return sprite;
        }

        private static int CountActiveChildren(RectTransform parent)
        {
            int active = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).gameObject.activeSelf) active++;
            return active;
        }

        private static RectTransform AddRectChild(Component parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        private static Image AddImageChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go.AddComponent<Image>();
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }
    }
}
