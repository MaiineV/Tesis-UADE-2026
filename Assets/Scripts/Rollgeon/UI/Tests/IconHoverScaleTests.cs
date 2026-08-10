using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// La escala de reposo del hover.
    /// </summary>
    /// <remarks>
    /// El crecimiento en sí no se puede testear en EditMode: el componente sale temprano
    /// si <c>!Application.isPlaying</c> (como todo el juice del repo) y el tween necesita
    /// play mode. Lo que sí se cubre acá es de dónde sale el reposo y que se restaure —
    /// que es donde estaba el riesgo real de dejar un ícono agrandado para siempre.
    /// </remarks>
    [TestFixture]
    public class IconHoverScaleTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private IconHoverScale MakeIcon(float restScale)
        {
            _go = new GameObject("Icon", typeof(RectTransform));
            _go.transform.localScale = Vector3.one * restScale;
            var hover = _go.AddComponent<IconHoverScale>();
            InvokePrivate(hover, "Awake");
            return hover;
        }

        [Test]
        public void should_capture_the_authored_rest_scale_not_one()
        {
            // Arrange — mochila y bolsa están autoradas a 0.8; si el reposo se asumiera 1,
            // el primer hover les cambiaría el tamaño de base.
            var hover = MakeIcon(0.8f);
            _go.transform.localScale = Vector3.one * 1.5f;

            // Act — el restore usa la escala capturada en Awake.
            InvokePrivate(hover, "OnDisable");

            // Assert
            Assert.AreEqual(0.8f, _go.transform.localScale.x, 0.001f);
        }

        [Test]
        public void should_restore_the_rest_scale_when_disabled_mid_hover()
        {
            // Arrange — si el HUD se apaga con el mouse encima, el pointer exit nunca llega.
            var hover = MakeIcon(1f);
            _go.transform.localScale = Vector3.one * 1.12f;

            // Act
            InvokePrivate(hover, "OnDisable");

            // Assert
            Assert.AreEqual(1f, _go.transform.localScale.x, 0.001f);
        }

        [Test]
        public void should_not_drift_the_rest_scale_across_repeated_captures()
        {
            // Arrange — capturar de nuevo a mitad de un tween correría el reposo hover
            // tras hover hasta dejar el ícono enorme.
            var hover = MakeIcon(1f);
            _go.transform.localScale = Vector3.one * 1.12f;

            // Act — un pointer enter en EditMode no anima, pero sí pasa por CaptureBaseScale.
            hover.OnPointerEnter(null);
            InvokePrivate(hover, "OnDisable");

            // Assert
            Assert.AreEqual(1f, _go.transform.localScale.x, 0.001f);
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
