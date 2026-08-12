using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Items;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Inventory;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Fórmulas de layout del drawer de inventario, el resize del rebuild y el bind de la
    /// celda (base/ícono/tooltip).
    /// </summary>
    /// <remarks>
    /// El rebuild se prueba sin <c>IInventoryService</c> registrado (scope Run): el drawer
    /// tiene que mostrar la fila vacía del mockup en vez de romper.
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
        // Fórmulas puras
        // ------------------------------------------------------------------

        [Test]
        public void should_always_show_a_full_first_row_even_when_empty()
        {
            // Arrange + Act + Assert — el mockup muestra 6 celdas vacías, no cero.
            Assert.AreEqual(6, InventoryDrawerView.VisibleCells(0));
            Assert.AreEqual(1, InventoryDrawerView.Rows(0));
        }

        [Test]
        public void should_wrap_to_a_new_row_after_six_items()
        {
            // Arrange + Act + Assert
            Assert.AreEqual(6, InventoryDrawerView.VisibleCells(6));
            Assert.AreEqual(12, InventoryDrawerView.VisibleCells(7));
            Assert.AreEqual(3, InventoryDrawerView.Rows(13));
        }

        [Test]
        public void should_grow_box_and_panel_height_per_extra_row()
        {
            // Arrange — números del plan: caja 88 la primera fila, +72 por fila extra.
            // Act + Assert
            Assert.AreEqual(88f, InventoryDrawerView.BoxHeight(1), 0.001f);
            Assert.AreEqual(160f, InventoryDrawerView.BoxHeight(2), 0.001f);
            Assert.AreEqual(196f, InventoryDrawerView.PanelHeight(1), 0.001f);
            Assert.AreEqual(268f, InventoryDrawerView.PanelHeight(2), 0.001f);
        }

        // ------------------------------------------------------------------
        // Rebuild sin service
        // ------------------------------------------------------------------

        [Test]
        public void should_show_six_empty_cells_when_the_service_is_missing()
        {
            // Arrange
            var view = MakeDrawer(out var panel, out var itemsBox, out var grid);

            // Act
            view.Rebuild();

            // Assert
            Assert.AreEqual(6, CountActiveChildren(grid));
            Assert.AreEqual(InventoryDrawerView.PanelHeight(1), panel.sizeDelta.y, 0.001f);
            Assert.AreEqual(InventoryDrawerView.BoxHeight(1), itemsBox.sizeDelta.y, 0.001f);
        }

        // ------------------------------------------------------------------
        // Celda
        // ------------------------------------------------------------------

        [Test]
        public void should_show_nothing_inside_an_empty_cell()
        {
            // Arrange — celda vacía = cuadradito pelado: ni base, ni ícono, ni tooltip.
            var slot = MakeSlot(out _, out var baseImage, out var icon, out var tooltip);

            // Act
            slot.Bind(null);

            // Assert
            Assert.IsFalse(baseImage.enabled);
            Assert.IsFalse(icon.enabled);
            Assert.IsNotNull(tooltip.TextProvider, "vacío explícito, no null — null cae en AutoResolve");
            Assert.AreEqual(string.Empty, tooltip.TextProvider());
        }

        [Test]
        public void should_show_the_item_icon_and_hide_the_base_when_bound()
        {
            // Arrange
            var item = MakeItem("moneda.suerte.par", "Moneda de la suerte", "Cada Par da +2 oro.");
            item.Icon = MakeSprite("base");
            var slot = MakeSlot(out _, out var baseImage, out var icon, out _);

            // Act
            slot.Bind(item);

            // Assert
            Assert.IsTrue(icon.enabled);
            Assert.AreSame(item.Icon, icon.sprite);
            Assert.IsFalse(baseImage.enabled, "la base es el fallback, no un fondo permanente");
        }

        [Test]
        public void should_keep_the_base_when_the_item_has_no_icon()
        {
            // Arrange
            var item = MakeItem("coraza.reforzada", "Coraza Reforzada", "+2 vida máxima");
            var slot = MakeSlot(out _, out var baseImage, out var icon, out _);

            // Act
            slot.Bind(item);

            // Assert
            Assert.IsTrue(baseImage.enabled);
            Assert.IsFalse(icon.enabled);
        }

        [Test]
        public void should_provide_name_and_description_through_the_tooltip()
        {
            // Arrange — id que no existe en las tablas: LocalizedContent devuelve el
            // fallback (los campos del asset) sin importar si la localización cargó.
            var item = MakeItem("test.tooltip.item", "Moneda de la suerte", "Cada Par da +2 oro.");
            var slot = MakeSlot(out _, out _, out _, out var tooltip);

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
            var slot = MakeSlot(out _, out _, out _, out var tooltip);

            // Act
            slot.Bind(item);

            // Assert
            Assert.AreEqual("<b>test.tooltip.nameless</b>", tooltip.TextProvider());
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private InventoryDrawerView MakeDrawer(out RectTransform panel, out RectTransform itemsBox,
            out RectTransform grid)
        {
            var go = new GameObject("InventoryDrawer", typeof(RectTransform));
            _spawned.Add(go);

            panel = AddRectChild(go.transform, "Panel");
            itemsBox = AddRectChild(panel, "ItemsBox");
            grid = AddRectChild(itemsBox, "Grid");

            var slotPrefab = MakeSlot(out _, out _, out _, out _);

            go.AddComponent<SlidingDrawer>();
            var view = go.AddComponent<InventoryDrawerView>();
            SetPrivate(view, "_panel", panel);
            SetPrivate(view, "_itemsBox", itemsBox);
            SetPrivate(view, "_grid", grid);
            SetPrivate(view, "_slotPrefab", slotPrefab);
            return view;
        }

        private InventoryItemSlotView MakeSlot(out Image cell, out Image baseImage,
            out Image icon, out UITooltipTrigger tooltip)
        {
            var go = new GameObject("Slot", typeof(RectTransform));
            _spawned.Add(go);

            cell = go.AddComponent<Image>();
            baseImage = AddImageChild(go, "Base");
            icon = AddImageChild(go, "ItemIcon");
            tooltip = go.AddComponent<UITooltipTrigger>();

            var slot = go.AddComponent<InventoryItemSlotView>();
            SetPrivate(slot, "_cellBg", cell);
            SetPrivate(slot, "_base", baseImage);
            SetPrivate(slot, "_icon", icon);
            SetPrivate(slot, "_tooltip", tooltip);
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

        private static RectTransform AddRectChild(GameObject parent, string name)
            => AddRectChild(parent.transform, name);

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
