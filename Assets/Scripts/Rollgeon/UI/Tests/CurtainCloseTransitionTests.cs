using NUnit.Framework;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests de <see cref="CurtainCloseTransition"/>. Fuera de Play Mode el
    /// componente no tweenea: aplica el estado final (hojas activas en su posición
    /// cerrada autorada) y el callback es sincrónico — eso es lo que se cubre acá.
    /// El deslizamiento real (PrimeTween) queda para playtest.
    /// </summary>
    [TestFixture]
    public class CurtainCloseTransitionTests
    {
        private GameObject _hostGO;
        private CurtainCloseTransition _transition;

        [SetUp]
        public void SetUp()
        {
            _hostGO = new GameObject("CurtainHost");
            _hostGO.SetActive(false);
            _transition = _hostGO.AddComponent<CurtainCloseTransition>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGO != null) Object.DestroyImmediate(_hostGO);
        }

        [Test]
        public void Play_WithoutCurtainsWired_InvokesCallbackImmediately()
        {
            // Arrange — sin hojas cableadas (estado por defecto del componente).
            int callbackCalls = 0;

            // Act
            _transition.Play(() => callbackCalls++);

            // Assert
            Assert.AreEqual(1, callbackCalls,
                "Sin hojas no hay nada que animar: el callback debe dispararse igual, una vez.");
            Assert.IsFalse(_transition.IsPlaying,
                "Terminado el Play, IsPlaying debe volver a false.");
        }

        [Test]
        public void Play_OutsidePlayMode_SnapsCurtainsToAuthoredClosedPosition()
        {
            // Arrange — hojas autoradas en posición cerrada, inactivas (setup real).
            var left = CreateCurtain("CourtainLeft", closedX: -462f);
            var right = CreateCurtain("CourtainRight", closedX: 463f);
            AssignPrivate("_curtainLeft", left);
            AssignPrivate("_curtainRight", right);
            int callbackCalls = 0;

            // Act
            _transition.Play(() => callbackCalls++);

            // Assert — snap instantáneo: activas, en la posición cerrada, callback sincrónico.
            Assert.IsTrue(left.gameObject.activeSelf, "La hoja izquierda debe activarse al cerrar.");
            Assert.IsTrue(right.gameObject.activeSelf, "La hoja derecha debe activarse al cerrar.");
            Assert.AreEqual(-462f, left.anchoredPosition.x,
                "Fuera de Play Mode la hoja izquierda debe quedar en su X cerrada autorada.");
            Assert.AreEqual(463f, right.anchoredPosition.x,
                "Fuera de Play Mode la hoja derecha debe quedar en su X cerrada autorada.");
            Assert.AreEqual(1, callbackCalls, "El callback debe dispararse exactamente una vez.");
        }

        [Test]
        public void Play_CalledAgainAfterFinishing_InvokesCallbackAgain()
        {
            // Arrange — un primer cierre ya completado.
            var left = CreateCurtain("CourtainLeft", closedX: -462f);
            AssignPrivate("_curtainLeft", left);
            int callbackCalls = 0;
            _transition.Play(() => callbackCalls++);

            // Act — segundo Play (p. ej. la screen se re-pusheó sin recargar escena).
            _transition.Play(() => callbackCalls++);

            // Assert — la posición cerrada capturada se conserva y el callback vuelve a salir.
            Assert.AreEqual(2, callbackCalls,
                "Un Play posterior al primero (ya terminado) debe volver a cerrar y avisar.");
            Assert.AreEqual(-462f, left.anchoredPosition.x,
                "La X cerrada capturada en el primer Play debe reutilizarse, no re-leerse corrida.");
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private RectTransform CreateCurtain(string name, float closedX)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_hostGO.transform, false);
            rect.sizeDelta = new Vector2(960f, 1080f);
            rect.anchoredPosition = new Vector2(closedX, 0f);
            go.SetActive(false);
            return rect;
        }

        private void AssignPrivate(string fieldName, object value)
        {
            var field = typeof(CurtainCloseTransition).GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in CurtainCloseTransition.");
            field.SetValue(_transition, value);
        }
    }
}
