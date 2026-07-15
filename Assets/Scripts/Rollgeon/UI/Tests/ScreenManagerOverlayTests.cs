using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="ScreenManager"/> overlay semantics.
    /// <para>
    /// Regresión del bug "pausa durante selección de target rompe el combate":
    /// un overlay (ej. PauseMenuOverlay) debe apilarse SIN desactivar el screen de
    /// atrás (el CombatHUDView), para no disparar un ciclo OnDisable/OnEnable que
    /// resetee bindings contra gameplay en curso. Los push destructivos
    /// (<see cref="ScreenManager.Push{TScreen}"/>) siguen ocultando el screen de atrás.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ScreenManagerOverlayTests
    {
        private ScreenManager _manager;
        private GameObject _goA;
        private GameObject _goB;
        private TestScreenA _a;
        private TestScreenB _b;

        // Dos subclases distintas: el registro del ScreenManager indexa por tipo, así que
        // dos screens deben ser tipos diferentes para coexistir en el stack.
        private class TestScreenA : BaseScreen
        {
            public int EnableCount;
            public int DisableCount;
            public int GainFocusCount;
            public int LoseFocusCount;

            private void OnEnable() => EnableCount++;
            private void OnDisable() => DisableCount++;
            protected override void OnGainFocus() => GainFocusCount++;
            protected override void OnLoseFocus() => LoseFocusCount++;
        }

        private class TestScreenB : BaseScreen { }

        [SetUp]
        public void SetUp()
        {
            _manager = new ScreenManager();

            // Los screens nacen inactivos (como los deja el ScreenHost tras registrarlos),
            // así que desactivamos ANTES de AddComponent para no disparar un OnEnable inicial.
            _goA = new GameObject("ScreenA");
            _goA.SetActive(false);
            _a = _goA.AddComponent<TestScreenA>();

            _goB = new GameObject("ScreenB");
            _goB.SetActive(false);
            _b = _goB.AddComponent<TestScreenB>();

            _manager.RegisterScreen(_a);
            _manager.RegisterScreen(_b);
        }

        [TearDown]
        public void TearDown()
        {
            if (_goA != null) Object.DestroyImmediate(_goA);
            if (_goB != null) Object.DestroyImmediate(_goB);
        }

        [Test]
        public void PushOverlay_KeepsScreenBehindActive()
        {
            // Arrange: A es el top activo (HUD en combate).
            _manager.Push<TestScreenA>();
            Assert.IsTrue(_goA.activeSelf, "Precondición: A debe quedar activo tras Push.");
            _a.DisableCount = 0; // reset del OnDisable/OnEnable espurio del setup.

            // Act: se apila un overlay encima (ej. Pause).
            _manager.PushOverlay<TestScreenB>();

            // Assert: el screen de atrás sigue vivo y solo perdió foco.
            Assert.IsTrue(_goA.activeSelf,
                "Un overlay NO debe desactivar el screen de atrás.");
            Assert.AreEqual(0, _a.DisableCount,
                "El screen de atrás no debe sufrir OnDisable al apilar un overlay.");
            Assert.AreEqual(1, _a.LoseFocusCount,
                "El screen de atrás debe recibir OnLoseFocus una vez.");
            Assert.IsTrue(_goB.activeSelf, "El overlay debe quedar activo/visible.");
        }

        [Test]
        public void PopOverlay_RestoresFocusWithoutReactivatingBehind()
        {
            // Arrange: A activo, overlay B apilado encima sin ocultar A.
            _manager.Push<TestScreenA>();
            _manager.PushOverlay<TestScreenB>();
            _a.EnableCount = 0;
            _a.GainFocusCount = 0;

            // Act: se cierra el overlay.
            _manager.PopOverlay();

            // Assert: A vuelve a ser top, sigue activo, recupera foco, y NO se re-activó
            // (sin OnEnable espurio) porque nunca se había ocultado.
            Assert.IsTrue(_goA.activeSelf, "A debe seguir activo tras cerrar el overlay.");
            Assert.AreSame(_a, _manager.Current, "A debe volver a ser el top del stack.");
            Assert.AreEqual(1, _a.GainFocusCount, "A debe recuperar el foco exactamente una vez.");
            Assert.AreEqual(0, _a.EnableCount,
                "A no debe re-activarse (sin ciclo OnEnable) al cerrar un overlay no-destructivo.");
            Assert.IsFalse(_goB.activeSelf, "El overlay debe quedar oculto tras PopOverlay.");
        }

        [Test]
        public void Push_Destructive_DeactivatesScreenBehind()
        {
            // Arrange: A es el top activo.
            _manager.Push<TestScreenA>();
            Assert.IsTrue(_goA.activeSelf, "Precondición: A activo.");

            // Act: push destructivo (full-screen) encima.
            _manager.Push<TestScreenB>();

            // Assert: el push destructivo SÍ oculta el screen de atrás (comportamiento legacy).
            Assert.IsFalse(_goA.activeSelf,
                "Un Push destructivo debe desactivar el screen de atrás.");
            Assert.AreEqual(1, _a.LoseFocusCount, "A debe recibir OnLoseFocus.");
        }

        [Test]
        public void PopCurrent_AfterDestructivePush_ReactivatesScreenBehind()
        {
            // Arrange: A oculto por un push destructivo de B.
            _manager.Push<TestScreenA>();
            _manager.Push<TestScreenB>();
            Assert.IsFalse(_goA.activeSelf, "Precondición: A oculto por B.");

            // Act: se cierra B.
            _manager.PopCurrent();

            // Assert: A se re-activa y recupera el foco.
            Assert.IsTrue(_goA.activeSelf, "A debe re-activarse al cerrar el push destructivo.");
            Assert.AreSame(_a, _manager.Current, "A debe volver a ser el top.");
        }
    }
}
