using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Contrato visual de los chips de exploración (<see cref="ChipButtonVisual"/>):
    /// mismo look que los de combate — base en reposo, highlight al hover,
    /// highlight atenuado cuando el botón no es interactable.
    /// </summary>
    [TestFixture]
    public class ChipButtonVisualTests
    {
        private readonly List<Object> _spawned = new();

        private GameObject _go;
        private Button _uiButton;
        private Image _image;
        private ChipButtonVisual _visual;
        private Sprite _baseSprite;
        private Sprite _highlight;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _spawned.Add(_go);
            _image = _go.GetComponent<Image>();
            _uiButton = _go.AddComponent<Button>();
            _uiButton.targetGraphic = _image;

            var tex = new Texture2D(4, 4);
            _baseSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _highlight = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _spawned.Add(tex);
            _spawned.Add(_baseSprite);
            _spawned.Add(_highlight);
            _image.sprite = _baseSprite;

            _visual = _go.AddComponent<ChipButtonVisual>();
            AssignPrivate(_visual, "_highlightSprite", _highlight);
            // El AddComponent ya corrió Awake sin el sprite wireado — re-disparar.
            InvokePrivate(_visual, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [Test]
        public void test_chipVisual_hover_swapsToHighlightAndBack()
        {
            // Arrange
            Assert.AreSame(_baseSprite, _image.sprite, "sanity: en reposo usa el base");

            // Act + Assert
            _visual.OnPointerEnter(null);
            Assert.AreSame(_highlight, _image.sprite);
            _visual.OnPointerExit(null);
            Assert.AreSame(_baseSprite, _image.sprite);
        }

        [Test]
        public void test_chipVisual_disabledButton_showsDimmedHighlight()
        {
            // Arrange
            float expectedAlpha = (float)GetPrivate(_visual, "_disabledAlpha");

            // Act — el view solo togglea interactable; el visual lo ve en LateUpdate.
            _uiButton.interactable = false;
            InvokePrivate(_visual, "LateUpdate");

            // Assert
            Assert.AreSame(_highlight, _image.sprite);
            Assert.AreEqual(expectedAlpha, _image.color.a, 0.001f);
        }

        [Test]
        public void test_chipVisual_reenabledButton_restoresBaseLook()
        {
            // Arrange
            _uiButton.interactable = false;
            InvokePrivate(_visual, "LateUpdate");

            // Act
            _uiButton.interactable = true;
            InvokePrivate(_visual, "LateUpdate");

            // Assert
            Assert.AreSame(_baseSprite, _image.sprite);
            Assert.AreEqual(1f, _image.color.a, 0.001f);
        }

        // ------------------------------------------------------------------
        // Helpers (patrón de ActionButtonVisualStateTests)
        // ------------------------------------------------------------------

        private static void AssignPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private static object GetPrivate(object target, string field)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            return info.GetValue(target);
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
