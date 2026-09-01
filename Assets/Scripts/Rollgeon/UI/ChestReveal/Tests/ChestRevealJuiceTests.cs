using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.UI.ChestReveal.Tests
{
    /// <summary>
    /// Regresión de BUG-071 (deriva del panel del cofre): cuando la view mata el
    /// shake desde afuera (<c>Tween.StopAll</c> sobre el panel) el tween muere sin
    /// restore; el cleanup del juice debe devolver el rest capturado igual, o la
    /// posición derivada se re-captura como reposo y el corrimiento se acumula
    /// cofre a cofre.
    /// </summary>
    [TestFixture]
    public class ChestRevealJuiceTests
    {
        private GameObject _go;
        private ChestRevealJuice _juice;
        private RectTransform _shakeTarget;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("juice", typeof(RectTransform));
            _juice = _go.AddComponent<ChestRevealJuice>();
            var targetGo = new GameObject("shakeTarget", typeof(RectTransform));
            targetGo.transform.SetParent(_go.transform, false);
            _shakeTarget = (RectTransform)targetGo.transform;
            SetPrivate("_shakeTarget", _shakeTarget);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private void SetPrivate(string field, object value)
        {
            var info = typeof(ChestRevealJuice).GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"Field '{field}' not found.");
            info.SetValue(_juice, value);
        }

        [Test]
        public void should_restore_shake_rest_position_when_shake_tween_was_stopped_externally()
        {
            // Arrange — rest válido capturado por un ShakePanel previo, pero el tween
            // ya no está vivo (la view lo mató con StopAll) y el panel quedó corrido.
            var rest = new Vector2(10f, -77f);
            SetPrivate("_shakeRestAnchored", rest);
            SetPrivate("_shakeRestValid", true);
            _shakeTarget.anchoredPosition = rest + new Vector2(6.5f, -4.2f);

            // Act — el camino de skip/watchdog.
            _juice.OnForceFinalState();

            // Assert — sin el fix, el guard isAlive salteaba el restore.
            Assert.AreEqual(rest, _shakeTarget.anchoredPosition);
        }

        [Test]
        public void should_keep_position_when_no_shake_rest_was_captured()
        {
            // Arrange — sin rest válido no hay nada que restaurar: la pose actual manda.
            var current = new Vector2(3f, 4f);
            _shakeTarget.anchoredPosition = current;

            // Act
            _juice.OnForceFinalState();

            // Assert
            Assert.AreEqual(current, _shakeTarget.anchoredPosition);
        }
    }
}
