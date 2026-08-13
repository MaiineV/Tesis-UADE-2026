using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Click derecho sobre un slot de dado = sacarlo del combo. El izquierdo lo sigue
    /// manejando el <see cref="Button"/> (toggle), así que acá sólo se verifica que el
    /// derecho dispare <c>OnUnholdRequested</c> y que respete los mismos gates.
    /// </summary>
    [TestFixture]
    public class DiceSlotRightClickTests
    {
        private GameObject _go;
        private DiceSlotView _slot;
        private Button _button;
        private int _unholdRequests;
        private int _toggles;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("DieSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _button = _go.AddComponent<Button>();
            _button.targetGraphic = _go.GetComponent<Image>();

            _slot = _go.AddComponent<DiceSlotView>();
            AssignPrivate(_slot, "_button", _button);
            InvokePrivate(_slot, "Awake");

            _unholdRequests = 0;
            _toggles = 0;
            _slot.OnUnholdRequested.AddListener(() => _unholdRequests++);
            _slot.OnToggled.AddListener(() => _toggles++);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private static PointerEventData Click(PointerEventData.InputButton button)
            => new PointerEventData(EventSystem.current) { button = button };

        [Test]
        public void should_request_unhold_when_right_clicked()
        {
            // Arrange + Act
            _slot.OnPointerClick(Click(PointerEventData.InputButton.Right));

            // Assert
            Assert.AreEqual(1, _unholdRequests);
            Assert.AreEqual(0, _toggles, "el derecho no debe pasar por el toggle");
        }

        [Test]
        public void should_ignore_left_click()
        {
            // Arrange + Act — el izquierdo es del Button, no de este handler.
            _slot.OnPointerClick(Click(PointerEventData.InputButton.Left));

            // Assert
            Assert.AreEqual(0, _unholdRequests);
        }

        [Test]
        public void should_ignore_middle_click()
        {
            // Arrange + Act
            _slot.OnPointerClick(Click(PointerEventData.InputButton.Middle));

            // Assert
            Assert.AreEqual(0, _unholdRequests);
        }

        [Test]
        public void should_ignore_right_click_when_die_is_blocked()
        {
            // Arrange — Boss 1 §2: un dado bloqueado no se toca.
            _slot.SetBlocked(true);

            // Act
            _slot.OnPointerClick(Click(PointerEventData.InputButton.Right));

            // Assert
            Assert.AreEqual(0, _unholdRequests);
        }

        [Test]
        public void should_ignore_right_click_while_hold_is_not_interactable()
        {
            // Arrange — durante el spin SetHoldInteractable(false) apaga el Button, y
            // uGUI filtra los handlers por isActiveAndEnabled, nunca por interactable:
            // sin este gate el derecho pasaba con los dados todavía girando.
            _slot.SetHoldInteractable(false);

            // Act
            _slot.OnPointerClick(Click(PointerEventData.InputButton.Right));

            // Assert
            Assert.AreEqual(0, _unholdRequests);
        }

        [Test]
        public void should_ignore_null_event_data()
        {
            // Arrange + Act — invocaciones programáticas defensivas.
            _slot.OnPointerClick(null);

            // Assert
            Assert.AreEqual(0, _unholdRequests);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void AssignPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            var info = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, null);
        }
    }
}
