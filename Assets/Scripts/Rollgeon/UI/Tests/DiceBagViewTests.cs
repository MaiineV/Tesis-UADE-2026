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
    /// Cards y cupos del panel de la bolsa de dados.
    /// </summary>
    /// <remarks>
    /// El panel entero (<c>DiceBagView</c>) depende de <c>IDiceEnchantmentService</c> con
    /// una bolsa viva, así que acá se cubren las piezas: el marco de selección de la card y
    /// el círculo del cupo, que es donde vive la lógica de dibujo.
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

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _spawned.Add(tex);
            _spawned.Add(sprite);
            return sprite;
        }

        // ------------------------------------------------------------------
        // Card de dado
        // ------------------------------------------------------------------

        private DiceBagDieCardView MakeCard(out Image frame, out TMPro.TextMeshProUGUI slots,
            out Sprite idle, out Sprite selected)
        {
            var go = new GameObject("DieCard", typeof(RectTransform));
            _spawned.Add(go);

            frame = AddImageChild(go, "Frame");
            var icon = AddImageChild(go, "Icon");
            var faceCount = AddLabelChild(go, "FaceCount");
            slots = AddLabelChild(go, "Slots");
            var button = frame.gameObject.AddComponent<Button>();

            idle = MakeSprite("idle");
            selected = MakeSprite("selected");

            var card = go.AddComponent<DiceBagDieCardView>();
            SetPrivate(card, "_frame", frame);
            SetPrivate(card, "_diceIcon", icon);
            SetPrivate(card, "_faceCountLabel", faceCount);
            SetPrivate(card, "_slotsLabel", slots);
            SetPrivate(card, "_button", button);
            SetPrivate(card, "_idleFrame", idle);
            SetPrivate(card, "_selectedFrame", selected);
            InvokePrivate(card, "Awake");
            return card;
        }

        [Test]
        public void should_show_the_enchant_count_with_the_localized_suffix()
        {
            // Arrange
            var card = MakeCard(out _, out var slots, out _, out _);

            // Act
            card.Bind(MakeSprite("d6"), maxFace: 6, enchantCount: 2, "encantamientos", onClick: null);

            // Assert
            Assert.AreEqual("2 encantamientos", slots.text);
        }

        [Test]
        public void should_hide_the_counter_for_a_die_with_no_enchantments()
        {
            // Arrange — hay dados sin encantar; "0 encantamientos" sería ruido.
            var card = MakeCard(out _, out var slots, out _, out _);

            // Act
            card.Bind(MakeSprite("d4"), maxFace: 4, enchantCount: 0, "encantamientos", onClick: null);

            // Assert
            Assert.AreEqual(string.Empty, slots.text);
        }

        [Test]
        public void should_swap_the_frame_when_selected()
        {
            // Arrange
            var card = MakeCard(out var frame, out _, out var idle, out var selected);
            card.Bind(MakeSprite("d6"), 6, 0, "encantamientos", null);

            // Act + Assert
            card.SetSelected(true);
            Assert.AreSame(selected, frame.sprite);

            card.SetSelected(false);
            Assert.AreSame(idle, frame.sprite, "el marco tiene que volver, no quedarse elegido");
        }

        [Test]
        public void should_invoke_the_click_callback()
        {
            // Arrange
            var card = MakeCard(out var frame, out _, out _, out _);
            int clicks = 0;
            card.Bind(MakeSprite("d6"), 6, 0, "encantamientos", () => clicks++);

            // Act
            frame.gameObject.GetComponent<Button>().onClick.Invoke();

            // Assert
            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void should_show_the_holo_material_only_on_an_enchanted_die()
        {
            // Arrange — mismo contrato que DiceSlotView: material puesto ⇔ dado encantado.
            var card = MakeCard(out _, out _, out _, out _);
            var holo = new Material(Shader.Find("UI/Default"));
            _spawned.Add(holo);
            SetPrivate(card, "_enchantMaterial", holo);
            var icon = (Image)GetPrivate(card, "_diceIcon");
            var enchantment = ScriptableObject.CreateInstance<EnchantmentSO>();
            _spawned.Add(enchantment);

            // Act + Assert
            card.SetEnchantVisual(enchantment);
            Assert.AreSame(holo, icon.material);

            card.SetEnchantVisual(null);
            Assert.AreNotSame(holo, icon.material, "sin encantamiento vuelve al material default");
        }

        [Test]
        public void should_show_the_cursed_material_only_on_a_cursed_die()
        {
            // Arrange — mismo contrato que DiceSlotView: CapCursed ⇒ material maldito,
            // encantamiento bueno ⇒ holo.
            var card = MakeCard(out _, out _, out _, out _);
            var holo = new Material(Shader.Find("UI/Default"));
            var cursed = new Material(Shader.Find("UI/Default"));
            _spawned.Add(holo);
            _spawned.Add(cursed);
            SetPrivate(card, "_enchantMaterial", holo);
            SetPrivate(card, "_cursedMaterial", cursed);
            var icon = (Image)GetPrivate(card, "_diceIcon");

            var goodEnchantment = ScriptableObject.CreateInstance<EnchantmentSO>();
            var cursedEnchantment = ScriptableObject.CreateInstance<EnchantmentSO>();
            _spawned.Add(goodEnchantment);
            _spawned.Add(cursedEnchantment);
            SetPrivate(cursedEnchantment, "_capabilities",
                new List<IEnchantmentCapability> { new CapCursed() });

            // Act + Assert
            card.SetEnchantVisual(cursedEnchantment);
            Assert.AreSame(cursed, icon.material);

            card.SetEnchantVisual(goodEnchantment);
            Assert.AreSame(holo, icon.material, "un encantamiento bueno mantiene el holo");

            card.SetEnchantVisual(null);
            Assert.AreNotSame(cursed, icon.material, "sin encantamiento vuelve al material default");
            Assert.AreNotSame(holo, icon.material, "sin encantamiento vuelve al material default");
        }

        // ------------------------------------------------------------------
        // Cupo
        // ------------------------------------------------------------------

        private DiceBagSlotView MakeSlot(out Image circle, out Image rune,
            out Sprite empty, out Sprite filled)
        {
            var go = new GameObject("Slot", typeof(RectTransform));
            _spawned.Add(go);

            circle = AddImageChild(go, "Circle");
            rune = AddImageChild(go, "Rune");
            rune.sprite = MakeSprite("rune");
            var button = circle.gameObject.AddComponent<Button>();

            empty = MakeSprite("empty");
            filled = MakeSprite("filled");

            var slot = go.AddComponent<DiceBagSlotView>();
            SetPrivate(slot, "_circle", circle);
            SetPrivate(slot, "_rune", rune);
            SetPrivate(slot, "_button", button);
            SetPrivate(slot, "_emptyCircle", empty);
            SetPrivate(slot, "_filledCircle", filled);
            InvokePrivate(slot, "Awake");
            return slot;
        }

        [Test]
        public void should_show_the_rune_only_on_a_filled_slot()
        {
            // Arrange
            var slot = MakeSlot(out var circle, out var rune, out var empty, out var filled);

            // Act + Assert
            slot.Bind(filled: true, onClick: null);
            Assert.IsTrue(rune.enabled);
            Assert.AreSame(filled, circle.sprite);

            slot.Bind(filled: false, onClick: null);
            Assert.IsFalse(rune.enabled);
            Assert.AreSame(empty, circle.sprite, "el círculo también cambia, no solo la runa");
        }

        [Test]
        public void should_dim_the_slots_that_are_not_selected()
        {
            // Arrange — el detalle de abajo habla del cupo elegido, así que tiene que
            // distinguirse de los demás.
            var slot = MakeSlot(out var circle, out _, out _, out _);
            slot.Bind(filled: true, onClick: null);

            // Act
            slot.SetSelected(false);
            float dimmed = circle.color.a;
            slot.SetSelected(true);

            // Assert
            Assert.Less(dimmed, 1f);
            Assert.AreEqual(1f, circle.color.a, 0.001f);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Image AddImageChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go.AddComponent<Image>();
        }

        private static TMPro.TextMeshProUGUI AddLabelChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go.AddComponent<TMPro.TextMeshProUGUI>();
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
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

        private static object GetPrivate(object target, string field)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            return info.GetValue(target);
        }
    }
}
