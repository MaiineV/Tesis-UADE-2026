using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD.CharacterFrame;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Lógica pura del marco de personaje y el snap del controller. Las animaciones no se
    /// testean acá (necesitan play mode): con el gate <c>!Application.isPlaying</c> todo
    /// snapea, así que se valida estado final, no el recorrido.
    /// </summary>
    [TestFixture]
    public class CharacterFrameTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // CharacterFrameLogic (pura)
        // ------------------------------------------------------------------

        [Test]
        public void should_resolve_pressed_over_hover()
        {
            // Arrange + Act + Assert — tabla de verdad completa.
            Assert.AreEqual(CharacterFrameVisual.Normal, CharacterFrameLogic.Resolve(false, false));
            Assert.AreEqual(CharacterFrameVisual.Hover, CharacterFrameLogic.Resolve(true, false));
            Assert.AreEqual(CharacterFrameVisual.Pressed, CharacterFrameLogic.Resolve(false, true));
            Assert.AreEqual(CharacterFrameVisual.Pressed, CharacterFrameLogic.Resolve(true, true),
                "pinned gana aunque haya hover");
        }

        [Test]
        public void should_reveal_with_hover_or_pin()
        {
            Assert.IsFalse(CharacterFrameLogic.ShouldReveal(false, false));
            Assert.IsTrue(CharacterFrameLogic.ShouldReveal(true, false));
            Assert.IsTrue(CharacterFrameLogic.ShouldReveal(false, true));
            Assert.IsTrue(CharacterFrameLogic.ShouldReveal(true, true));
        }

        [Test]
        public void should_clamp_the_stagger_window()
        {
            // Arrange — ventana [0.3, 1.0] como la del contrato.
            // Act + Assert
            Assert.AreEqual(0f, CharacterFrameLogic.Window(0f, 0.3f, 1f), 0.001f);
            Assert.AreEqual(0f, CharacterFrameLogic.Window(0.3f, 0.3f, 1f), 0.001f);
            Assert.AreEqual(0.5f, CharacterFrameLogic.Window(0.65f, 0.3f, 1f), 0.001f);
            Assert.AreEqual(1f, CharacterFrameLogic.Window(1f, 0.3f, 1f), 0.001f);
            Assert.AreEqual(1f, CharacterFrameLogic.Window(1.2f, 0.3f, 1f), 0.001f);
        }

        [Test]
        public void should_spin_one_full_turn_across_the_progress()
        {
            Assert.AreEqual(0f, CharacterFrameLogic.SpinDegrees(0f), 0.001f);
            Assert.AreEqual(-180f, CharacterFrameLogic.SpinDegrees(0.5f), 0.001f);
            Assert.AreEqual(-360f, CharacterFrameLogic.SpinDegrees(1f), 0.001f);
        }

        // ------------------------------------------------------------------
        // Controller (snap en EditMode)
        // ------------------------------------------------------------------

        [Test]
        public void should_start_closed_after_awake()
        {
            // Arrange — el installer autora el prefab abierto; Awake snapea a cerrado.
            var c = MakeController(out var parts);

            // Act
            InvokePrivate(c, "Awake");

            // Assert
            Assert.AreEqual(0f, parts.IconGroup.alpha, 0.001f);
            Assert.AreEqual(parts.Hidden, parts.IconRect.anchoredPosition);
            Assert.IsFalse(parts.IconGroup.blocksRaycasts);
            Assert.AreEqual(parts.Collapsed, parts.StatusRow.anchoredPosition);
            Assert.AreSame(parts.Normal, parts.RingImage.sprite);
        }

        [Test]
        public void should_reveal_and_press_when_pinned()
        {
            // Arrange
            var c = MakeController(out var parts);
            InvokePrivate(c, "Awake");

            // Act
            c.TogglePin();

            // Assert — sin play mode el snap es inmediato.
            Assert.IsTrue(c.IsPinned);
            Assert.AreEqual(1f, parts.IconGroup.alpha, 0.001f);
            Assert.AreEqual(parts.Shown, parts.IconRect.anchoredPosition);
            Assert.IsTrue(parts.IconGroup.blocksRaycasts);
            Assert.AreEqual(parts.Expanded, parts.StatusRow.anchoredPosition);
            Assert.AreSame(parts.Pressed, parts.RingImage.sprite);
        }

        [Test]
        public void should_show_hover_sprite_and_close_on_exit()
        {
            // Arrange
            var c = MakeController(out var parts);
            InvokePrivate(c, "Awake");

            // Act + Assert — enter abre con sprite Hover…
            c.OnPointerEnter(null);
            Assert.AreSame(parts.Hover, parts.RingImage.sprite);
            Assert.AreEqual(1f, parts.IconGroup.alpha, 0.001f);

            // …y exit sin pin cierra (en EditMode el grace se saltea y snapea).
            c.OnPointerExit(null);
            Assert.AreSame(parts.Normal, parts.RingImage.sprite);
            Assert.AreEqual(0f, parts.IconGroup.alpha, 0.001f);
        }

        [Test]
        public void should_keep_the_pin_but_drop_the_hover_on_disable()
        {
            // Arrange — uGUI no dispara PointerExit sobre un GO desactivado; el pin es
            // estado del jugador y sobrevive.
            var c = MakeController(out var parts);
            InvokePrivate(c, "Awake");
            c.TogglePin();
            c.OnPointerEnter(null);

            // Act
            InvokePrivate(c, "OnDisable");

            // Assert
            Assert.IsTrue(c.IsPinned);
            Assert.AreEqual(1f, parts.IconGroup.alpha, 0.001f);
            Assert.AreSame(parts.Pressed, parts.RingImage.sprite);
        }

        [Test]
        public void should_close_on_disable_when_only_hovered()
        {
            // Arrange
            var c = MakeController(out var parts);
            InvokePrivate(c, "Awake");
            c.OnPointerEnter(null);

            // Act
            InvokePrivate(c, "OnDisable");

            // Assert
            Assert.AreEqual(0f, parts.IconGroup.alpha, 0.001f);
            Assert.AreSame(parts.Normal, parts.RingImage.sprite);
        }

        [Test]
        public void should_unpin_and_close_on_second_toggle()
        {
            // Arrange
            var c = MakeController(out var parts);
            InvokePrivate(c, "Awake");
            c.TogglePin();

            // Act
            c.TogglePin();

            // Assert
            Assert.IsFalse(c.IsPinned);
            Assert.AreEqual(0f, parts.IconGroup.alpha, 0.001f);
            Assert.AreSame(parts.Normal, parts.RingImage.sprite);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private sealed class Parts
        {
            public Image RingImage;
            public RectTransform IconRect;
            public CanvasGroup IconGroup;
            public RectTransform StatusRow;
            public Sprite Normal, Hover, Pressed;
            public Vector2 Hidden = new Vector2(76f, -22f);
            public Vector2 Shown = new Vector2(140f, -22f);
            public Vector2 Collapsed = new Vector2(168f, -50f);
            public Vector2 Expanded = new Vector2(358f, -50f);
        }

        private CharacterFrameController MakeController(out Parts parts)
        {
            var go = new GameObject("TopLeftCluster", typeof(RectTransform));
            _spawned.Add(go);

            var ring = AddRectChild(go, "FrameRing");
            ring.gameObject.AddComponent<CanvasRenderer>();
            var ringImage = ring.gameObject.AddComponent<Image>();

            var icon = AddRectChild(go, "DiceBagIcon");
            var group = icon.gameObject.AddComponent<CanvasGroup>();

            var statusRow = AddRectChild(go, "StatusRow");

            parts = new Parts
            {
                RingImage = ringImage,
                IconRect = icon,
                IconGroup = group,
                StatusRow = statusRow,
                Normal = MakeSprite("normal"),
                Hover = MakeSprite("hover"),
                Pressed = MakeSprite("pressed"),
            };

            var c = go.AddComponent<CharacterFrameController>();
            SetPrivate(c, "_ring", ring);
            SetPrivate(c, "_ringImage", ringImage);
            SetPrivate(c, "_normalSprite", parts.Normal);
            SetPrivate(c, "_hoverSprite", parts.Hover);
            SetPrivate(c, "_pressedSprite", parts.Pressed);
            SetPrivate(c, "_icons", new[]
            {
                new CharacterFrameController.RevealElement
                {
                    Rect = icon,
                    Group = group,
                    HiddenPos = parts.Hidden,
                    ShownPos = parts.Shown,
                    WindowStart = 0f,
                    WindowEnd = 0.7f,
                },
            });
            SetPrivate(c, "_statusRow", statusRow);
            SetPrivate(c, "_statusRowCollapsedPos", parts.Collapsed);
            SetPrivate(c, "_statusRowExpandedPos", parts.Expanded);
            return c;
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

        private static RectTransform AddRectChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return (RectTransform)go.transform;
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
    }
}
