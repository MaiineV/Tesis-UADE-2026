using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Hundimiento de botones del HUD (<see cref="HudButtonSink"/>): mientras el
    /// Button está no-interactable la ficha baja hasta el borde inferior de la
    /// pantalla, y al re-habilitarse vuelve a su anchoredPosition de origen. En
    /// EditMode el paso es instantáneo (<c>!Application.isPlaying</c>), así que un
    /// solo LateUpdate resuelve cada tramo.
    /// </summary>
    [TestFixture]
    public class HudButtonSinkTests
    {
        private GameObject _go;
        private RectTransform _rect;
        private Button _button;
        private HudButtonSink _sink;

        [SetUp]
        public void SetUp()
        {
            // Sin canvas: world == unidades locales, alcanza para medir el centro.
            _go = new GameObject("SinkButton", typeof(RectTransform));
            _rect = (RectTransform)_go.transform;
            _rect.sizeDelta = new Vector2(100f, 100f);
            _rect.anchoredPosition = new Vector2(0f, 300f);

            _button = _go.AddComponent<Button>();
            _sink = HudButtonSink.Attach(_button);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void test_hud_button_sink_lowers_the_center_to_the_screen_edge_when_not_interactable()
        {
            // Arrange
            _button.interactable = false;

            // Act
            InvokeLateUpdate();

            // Assert — centro en y=0: media ficha visible sobre el borde inferior.
            Assert.AreEqual(0f, _rect.position.y, 0.001f,
                "Con el botón no usable la ficha debe quedar a media asta en el borde.");
        }

        [Test]
        public void test_hud_button_sink_stays_at_the_edge_while_still_not_interactable()
        {
            // Arrange
            _button.interactable = false;
            InvokeLateUpdate();

            // Act — un frame más no debe seguir empujando bajo el borde.
            InvokeLateUpdate();

            // Assert
            Assert.AreEqual(0f, _rect.position.y, 0.001f,
                "El hundimiento converge al borde y no acumula más allá.");
        }

        [Test]
        public void test_hud_button_sink_restores_the_home_position_when_interactable_again()
        {
            // Arrange — hundir primero para que haya algo que restaurar.
            _button.interactable = false;
            InvokeLateUpdate();
            Assume.That(_rect.anchoredPosition.y, Is.Not.EqualTo(300f),
                "Precondición: la ficha se hundió.");

            // Act
            _button.interactable = true;
            InvokeLateUpdate();

            // Assert
            Assert.AreEqual(new Vector2(0f, 300f), _rect.anchoredPosition,
                "Re-habilitado, el botón vuelve a su anchoredPosition de origen.");
        }

        [Test]
        public void test_hud_button_sink_attach_is_idempotent()
        {
            // Arrange + Act
            var again = HudButtonSink.Attach(_button);

            // Assert
            Assert.AreSame(_sink, again,
                "Attach sobre un botón que ya tiene sink debe reusar el componente.");
        }

        // ---------------- helpers ----------------

        private void InvokeLateUpdate()
        {
            var method = typeof(HudButtonSink).GetMethod("LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Método 'LateUpdate' no encontrado en HudButtonSink.");
            method.Invoke(_sink, null);
        }
    }
}
