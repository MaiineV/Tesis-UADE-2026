using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Toggle carrusel ↔ minimapa del panel derecho de combate. En EditMode los
    /// cambios aplican por snap (sin tweens) — asserts deterministas. Sin
    /// IGameplayHotkeyService registrado, Bind sigue funcionando (la hotkey queda
    /// sin engancharse, Toggle() se puede invocar directo).
    /// </summary>
    [TestFixture]
    public class CombatRightPanelSwitcherTests
    {
        private GameObject _go;
        private CombatRightPanelSwitcher _switcher;
        private RectTransform _carousel;
        private CanvasGroup _carouselGroup;
        private RectTransform _minimap;
        private CanvasGroup _minimapGroup;

        private static readonly Vector2 Home = new Vector2(-50f, -28f);

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _go = new GameObject("Switcher", typeof(RectTransform));
            _go.SetActive(false);

            _carousel = MakePanel("Carousel", out _carouselGroup);
            _minimap = MakePanel("Minimap", out _minimapGroup);

            _switcher = _go.AddComponent<CombatRightPanelSwitcher>();
            AssignPrivate(_switcher, "_carouselPanel", _carousel);
            AssignPrivate(_switcher, "_carouselGroup", _carouselGroup);
            AssignPrivate(_switcher, "_minimapPanel", _minimap);
            AssignPrivate(_switcher, "_minimapGroup", _minimapGroup);
        }

        [TearDown]
        public void TearDown()
        {
            _switcher.Unbind();
            if (_go != null) Object.DestroyImmediate(_go);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Bind_DefaultsToCarousel()
        {
            // Act
            _switcher.Bind();

            // Assert
            Assert.IsFalse(_switcher.ShowingMinimap);
            Assert.AreEqual(1f, _carouselGroup.alpha);
            Assert.AreEqual(0f, _minimapGroup.alpha);
            Assert.IsFalse(_minimapGroup.blocksRaycasts);
        }

        [Test]
        public void Toggle_SwapsToMinimap_WithSnapInEditMode()
        {
            // Arrange
            _switcher.Bind();

            // Act
            _switcher.Toggle();

            // Assert — alphas invertidos y posiciones en home (snap, sin tween).
            Assert.IsTrue(_switcher.ShowingMinimap);
            Assert.AreEqual(0f, _carouselGroup.alpha);
            Assert.AreEqual(1f, _minimapGroup.alpha);
            Assert.AreEqual(Home, _carousel.anchoredPosition);
            Assert.AreEqual(Home, _minimap.anchoredPosition);
        }

        [Test]
        public void Toggle_Twice_ReturnsToCarousel()
        {
            // Arrange
            _switcher.Bind();

            // Act
            _switcher.Toggle();
            _switcher.Toggle();

            // Assert
            Assert.IsFalse(_switcher.ShowingMinimap);
            Assert.AreEqual(1f, _carouselGroup.alpha);
            Assert.AreEqual(0f, _minimapGroup.alpha);
        }

        [Test]
        public void Rebind_AfterToggle_ResetsToCarousel()
        {
            // Arrange — quedó en minimapa; re-entrar a combate re-bindea.
            _switcher.Bind();
            _switcher.Toggle();
            Assert.IsTrue(_switcher.ShowingMinimap);

            // Act
            _switcher.Unbind();
            _switcher.Bind();

            // Assert — arranca en el default (carrusel), sin heredar el toggle viejo.
            Assert.IsFalse(_switcher.ShowingMinimap);
            Assert.AreEqual(1f, _carouselGroup.alpha);
            Assert.AreEqual(Home, _carousel.anchoredPosition);
            Assert.AreEqual(Home, _minimap.anchoredPosition);
        }

        // ----- Helpers ---------------------------------------------------------

        private RectTransform MakePanel(string name, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_go.transform, false);
            rect.anchoredPosition = Home;
            group = go.GetComponent<CanvasGroup>();
            return rect;
        }

        private static void AssignPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Reflection layout cambió: '{field}' no encontrado.");
            f.SetValue(target, value);
        }
    }
}
