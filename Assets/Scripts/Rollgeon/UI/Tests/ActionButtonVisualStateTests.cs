using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica el visual de <see cref="ActionButton"/> en el estado
    /// <see cref="ActionButtonState.Unaffordable"/>: el jugador tiene que ver POR QUÉ no
    /// puede usar la acción, y ese rojo tiene que irse cuando vuelve a poder.
    /// </summary>
    [TestFixture]
    public class ActionButtonVisualStateTests
    {
        private GameObject _go;
        private ActionButton _button;
        private TextMeshProUGUI _costLabel;
        private Outline _outline;
        private Color _authoredCostColor;

        [SetUp]
        public void Setup()
        {
            // Orden importante: el Outline que agrega ActionButton en su Awake necesita
            // un Graphic, así que la Image va antes que el Button.
            _go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var uiButton = _go.AddComponent<Button>();
            uiButton.targetGraphic = _go.GetComponent<Image>();

            var labelGo = new GameObject("Cost", typeof(RectTransform));
            labelGo.transform.SetParent(_go.transform, worldPositionStays: false);
            _costLabel = labelGo.AddComponent<TextMeshProUGUI>();
            _authoredCostColor = Color.white;
            _costLabel.color = _authoredCostColor;

            _button = _go.AddComponent<ActionButton>();
            AssignPrivate(_button, "_button", uiButton);
            AssignPrivate(_button, "_costLabel", _costLabel);

            // El AddComponent ya corrió Awake sin ver el label; re-disparamos para que
            // capture el color de autoría con el wiring completo.
            InvokePrivate(_button, "Awake");

            _outline = _go.GetComponent<Outline>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void test_actionButton_unaffordable_paintsCostAndOutlineRed()
        {
            // Arrange
            var expected = (Color)GetPrivate(_button, "_unaffordableColor");

            // Act
            _button.SetState(ActionButtonState.Unaffordable);

            // Assert
            Assert.AreEqual(expected, _costLabel.color, "el costo tiene que quedar rojo");
            Assert.IsTrue(_outline.enabled, "el outline tiene que encenderse");
            Assert.AreEqual(expected, _outline.effectColor, "el outline tiene que quedar rojo");
        }

        [Test]
        public void test_actionButton_unaffordable_staysNonInteractable()
        {
            // Act
            _button.SetState(ActionButtonState.Unaffordable);

            // Assert — funcionalmente es un Locked: no arranca drag ni responde al hotkey.
            Assert.IsFalse(_button.Button.interactable);
        }

        [Test]
        public void test_actionButton_leavingUnaffordable_restoresAuthoredCostColor()
        {
            // Arrange
            _button.SetState(ActionButtonState.Unaffordable);

            // Act
            _button.SetState(ActionButtonState.Available);

            // Assert — sin esto el número quedaba rojo para siempre.
            Assert.AreEqual(_authoredCostColor, _costLabel.color);
            Assert.IsFalse(_outline.enabled);
        }

        [Test]
        public void test_actionButton_pointerDownWhileUnaffordable_raisesRejected()
        {
            // Arrange
            int rejections = 0;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Unaffordable);

            // Act
            _button.OnPointerDown(null);

            // Assert
            Assert.AreEqual(1, rejections);
        }

        [Test]
        public void test_actionButton_pointerDownWhileAvailable_doesNotRaiseRejected()
        {
            // Arrange
            int rejections = 0;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Available);

            // Act
            _button.OnPointerDown(null);

            // Assert
            Assert.AreEqual(0, rejections);
        }

        // ------------------------------------------------------------------
        // Helpers (patrón de EnergyChipStackViewTests)
        // ------------------------------------------------------------------

        private static void AssignPrivate(object target, string field, object value)
            => Field(target, field).SetValue(target, value);

        private static object GetPrivate(object target, string field)
            => Field(target, field).GetValue(target);

        private static FieldInfo Field(object target, string field)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            return info;
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
