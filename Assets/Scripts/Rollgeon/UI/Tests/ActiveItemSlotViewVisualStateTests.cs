using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// BUG-074: la ficha de ítem activo (poción/arco) tenía que pintar el mismo
    /// outline rojo de "no disponible" que <see cref="ActionButton"/> en
    /// Unaffordable — antes Inactive/Depleted solo togglaban overlays, sin
    /// señal roja. Espeja el patrón de <see cref="ActionButtonVisualStateTests"/>.
    /// </summary>
    [TestFixture]
    public class ActiveItemSlotViewVisualStateTests
    {
        private GameObject _go;
        private ActiveItemSlotView _slot;
        private Image _icon;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Slot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _icon = _go.GetComponent<Image>();

            _slot = _go.AddComponent<ActiveItemSlotView>();
            AssignPrivate(_slot, "_icon", _icon);
            // Display-only: aisla el test del auto-wiring de Button/click, que no es
            // lo que estamos probando acá.
            AssignPrivate(_slot, "_displayOnly", true);

            InvokePrivate(_slot, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void test_activeItemSlotView_inactive_paintsIconOutlineRedAtFullAlpha()
        {
            // Act
            _slot.SetState(ActiveItemState.Inactive);

            // Assert
            var outline = _icon.GetComponent<Outline>();
            Assert.IsNotNull(outline, "SetState debe auto-agregar el Outline si falta");
            Assert.IsTrue(outline.enabled, "el outline tiene que encenderse");
            Assert.AreEqual(UnavailableTint.TintColor, outline.effectColor);
            Assert.AreEqual(1f, _icon.color.a, 0.001f,
                "el icono no se atenúa — atenuarlo apaga el outline (mismo gotcha de ActionButton)");
        }

        [Test]
        public void test_activeItemSlotView_depleted_paintsIconOutlineRed()
        {
            // Act
            _slot.SetState(ActiveItemState.Depleted);

            // Assert
            var outline = _icon.GetComponent<Outline>();
            Assert.IsTrue(outline.enabled);
            Assert.AreEqual(UnavailableTint.TintColor, outline.effectColor);
        }

        [Test]
        public void test_activeItemSlotView_active_hasNoOutline()
        {
            // Act
            _slot.SetState(ActiveItemState.Active);

            // Assert — no se auto-agrega Outline si nunca hizo falta.
            var outline = _icon.GetComponent<Outline>();
            Assert.IsTrue(outline == null || !outline.enabled);
        }

        [Test]
        public void test_activeItemSlotView_leavingInactive_removesOutline()
        {
            // Arrange
            _slot.SetState(ActiveItemState.Inactive);

            // Act
            _slot.SetState(ActiveItemState.Active);

            // Assert
            var outline = _icon.GetComponent<Outline>();
            Assert.IsNotNull(outline);
            Assert.IsFalse(outline.enabled);
        }

        [Test]
        public void test_activeItemSlotView_leavingDepleted_removesOutline()
        {
            // Arrange
            _slot.SetState(ActiveItemState.Depleted);

            // Act
            _slot.SetState(ActiveItemState.Active);

            // Assert
            Assert.IsFalse(_icon.GetComponent<Outline>().enabled);
        }

        // ------------------------------------------------------------------
        // Helpers (patrón de ActionButtonVisualStateTests)
        // ------------------------------------------------------------------

        private static void AssignPrivate(object target, string field, object value)
            => Field(target, field).SetValue(target, value);

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
