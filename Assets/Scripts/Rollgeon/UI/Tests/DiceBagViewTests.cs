using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD.DiceBag;
using Rollgeon.Upgrades.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Piezas del dice bag drawer nuevo: math responsive de caras, card del dado
    /// (solo sprite + selección por alpha/escala) y fila del acordeón.
    /// </summary>
    /// <remarks>
    /// <c>DiceBagView</c> no se testea directo (depende de
    /// <c>IDiceEnchantmentService</c> con bolsa viva) — se cubren las piezas, igual
    /// que hacía la versión anterior de esta suite.
    /// </remarks>
    [TestFixture]
    public class DiceBagViewTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // DiceBagFaceLayout — responsive
        // ------------------------------------------------------------------

        [Test]
        public void should_use_the_max_cell_when_few_faces_fit_comfortably()
        {
            // Arrange + Act — d6 en una banda ancha: sobra lugar, manda el techo.
            float cell = DiceBagFaceLayout.CellSize(6, bandWidth: 560f, spacing: 4f);

            // Assert
            Assert.AreEqual(DiceBagFaceLayout.MaxCell, cell, 0.001f);
        }

        [Test]
        public void should_shrink_cells_so_twenty_faces_fit_in_one_row()
        {
            // Arrange + Act — d20: 20 celdas + 19 spacings tienen que entrar en la banda.
            float band = 560f, spacing = 4f;
            float cell = DiceBagFaceLayout.CellSize(20, band, spacing);

            // Assert — más chicas que el techo, y el total entra en la banda.
            Assert.Less(cell, DiceBagFaceLayout.MaxCell);
            Assert.LessOrEqual(cell * 20 + spacing * 19, band + 0.001f);
        }

        [Test]
        public void should_clamp_to_the_min_cell_in_a_narrow_band()
        {
            // Arrange + Act — banda absurda de angosta: gana la legibilidad del número.
            float cell = DiceBagFaceLayout.CellSize(20, bandWidth: 200f, spacing: 4f);

            // Assert
            Assert.AreEqual(DiceBagFaceLayout.MinCell, cell, 0.001f);
        }

        // ------------------------------------------------------------------
        // DiceBagDieCardView — solo sprite + selección
        // ------------------------------------------------------------------

        [Test]
        public void should_show_the_die_sprite_and_face_count_when_bound()
        {
            // Arrange
            var card = MakeDieCard(out var icon, out _, out var faceCount);
            var sprite = MakeSprite("d20");

            // Act
            card.Bind(sprite, maxFace: 20, onClick: null);

            // Assert
            Assert.IsTrue(icon.enabled);
            Assert.AreSame(sprite, icon.sprite);
            Assert.AreEqual("20", faceCount.text);
        }

        [Test]
        public void should_mark_selection_with_full_alpha_and_scale()
        {
            // Arrange
            var card = MakeDieCard(out var icon, out _, out _);
            card.Bind(MakeSprite("d6"), maxFace: 6, onClick: null);

            // Act + Assert — seleccionado: pleno y agrandado.
            card.SetSelected(true);
            Assert.AreEqual(1f, icon.color.a, 0.001f);
            Assert.Greater(card.transform.localScale.x, 1f);

            // Act + Assert — deseleccionado: atenuado a escala normal.
            card.SetSelected(false);
            Assert.Less(icon.color.a, 1f);
            Assert.AreEqual(1f, card.transform.localScale.x, 0.001f);
        }

        [Test]
        public void should_invoke_the_click_callback()
        {
            // Arrange
            var card = MakeDieCard(out _, out var button, out _);
            bool clicked = false;
            card.Bind(MakeSprite("d6"), maxFace: 6, () => clicked = true);
            InvokePrivate(card, "Awake");

            // Act
            button.onClick.Invoke();

            // Assert
            Assert.IsTrue(clicked);
        }

        // ------------------------------------------------------------------
        // DiceBagEnchantRowView — header + expand/collapse
        // ------------------------------------------------------------------

        [Test]
        public void should_render_name_and_colored_category_in_the_header()
        {
            // Arrange — id fuera de las tablas: LocalizedContent usa los fallbacks.
            var ench = MakeEnchantment("ench.test.header", "Ancla", EnchantmentCategory.Control);

            // Act
            string header = DiceBagEnchantRowView.BuildHeader(ench);

            // Assert
            string hex = EnchantmentPalette.CategoryHex(EnchantmentCategory.Control);
            StringAssert.StartsWith("Ancla - ", header);
            StringAssert.Contains($"<color=#{hex}>", header);
        }

        [Test]
        public void should_omit_the_category_segment_when_unassigned()
        {
            // Arrange
            var ench = MakeEnchantment("ench.test.none", "Pelado", EnchantmentCategory.None);

            // Act + Assert — sin tipo no hay separador ni color.
            Assert.AreEqual("Pelado", DiceBagEnchantRowView.BuildHeader(ench));
        }

        [Test]
        public void should_start_collapsed_and_toggle_with_set_expanded()
        {
            // Arrange
            var row = MakeEnchantRow(out _, out var panel);
            row.Bind(MakeEnchantment("ench.test.acc", "Filo", EnchantmentCategory.Ataque), onClick: null);

            // Assert — Bind deja la fila cerrada.
            Assert.IsFalse(panel.activeSelf);
            Assert.IsFalse(row.IsExpanded);

            // Act + Assert — abrir y cerrar.
            row.SetExpanded(true);
            Assert.IsTrue(panel.activeSelf);
            Assert.IsTrue(row.IsExpanded);
            row.SetExpanded(false);
            Assert.IsFalse(panel.activeSelf);
        }

        // ------------------------------------------------------------------
        // Palette de categorías
        // ------------------------------------------------------------------

        [Test]
        public void should_map_every_category_to_its_palette_color()
        {
            // Arrange + Act + Assert — regla 9.1: hexas reusados de la paleta.
            Assert.AreEqual("E0763D", EnchantmentPalette.CategoryHex(EnchantmentCategory.Ataque));
            Assert.AreEqual("6E7FD1", EnchantmentPalette.CategoryHex(EnchantmentCategory.Control));
            Assert.AreEqual("A3B3B1", EnchantmentPalette.CategoryHex(EnchantmentCategory.Defensa));
            Assert.AreEqual("D9A44E", EnchantmentPalette.CategoryHex(EnchantmentCategory.Economia));
            Assert.AreEqual("D1365A", EnchantmentPalette.CategoryHex(EnchantmentCategory.Maldicion));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private DiceBagDieCardView MakeDieCard(out Image icon, out Button button,
            out TMPro.TextMeshProUGUI faceCount)
        {
            var go = new GameObject("DieCard", typeof(RectTransform), typeof(CanvasRenderer));
            _spawned.Add(go);

            icon = go.AddComponent<Image>();
            button = go.AddComponent<Button>();

            var faceGo = new GameObject("FaceCount", typeof(RectTransform));
            faceGo.transform.SetParent(go.transform, false);
            faceCount = faceGo.AddComponent<TMPro.TextMeshProUGUI>();

            var card = go.AddComponent<DiceBagDieCardView>();
            SetPrivate(card, "_diceIcon", icon);
            SetPrivate(card, "_faceCountLabel", faceCount);
            SetPrivate(card, "_button", button);
            return card;
        }

        private DiceBagEnchantRowView MakeEnchantRow(out Button headerButton, out GameObject panel)
        {
            var go = new GameObject("EnchantRow", typeof(RectTransform));
            _spawned.Add(go);

            var header = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer));
            header.transform.SetParent(go.transform, false);
            header.AddComponent<Image>();
            headerButton = header.AddComponent<Button>();
            var headerLabel = header.AddComponent<TMPro.TextMeshProUGUI>();

            panel = new GameObject("DescriptionPanel", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            var bodyLabel = panel.AddComponent<TMPro.TextMeshProUGUI>();

            var row = go.AddComponent<DiceBagEnchantRowView>();
            SetPrivate(row, "_headerLabel", headerLabel);
            SetPrivate(row, "_headerButton", headerButton);
            SetPrivate(row, "_descriptionPanel", panel);
            SetPrivate(row, "_descriptionLabel", bodyLabel);
            return row;
        }

        private EnchantmentSO MakeEnchantment(string id, string displayName, EnchantmentCategory category)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _spawned.Add(ench);
            SetPrivate(ench, "_upgradeId", id);
            SetPrivate(ench, "_displayName", displayName);
            ench.EditorSetCategory(category);
            return ench;
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

        private static void SetPrivate(object target, string field, object value)
        {
            var type = target.GetType();
            FieldInfo info = null;
            while (type != null && info == null)
            {
                info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            var info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, null);
        }
    }
}
