using NUnit.Framework;
using Rollgeon.UI;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Smoke test de registro y lifecycle de <see cref="ScreenManager"/>. Plan §3.3.
    /// EditMode puro — crea GameObjects en memoria, sin escenas ni assets.
    /// </summary>
    [TestFixture]
    public class ScreenManagerTests
    {
        private class FakeScreenA : BaseScreen { }
        private class FakeScreenB : BaseScreen { }

        private GameObject _goA;
        private GameObject _goB;
        private FakeScreenA _a;
        private FakeScreenB _b;
        private ScreenManager _mgr;

        [SetUp]
        public void Setup()
        {
            _goA = new GameObject("FakeScreenA");
            _a = _goA.AddComponent<FakeScreenA>();
            _goA.SetActive(false);

            _goB = new GameObject("FakeScreenB");
            _b = _goB.AddComponent<FakeScreenB>();
            _goB.SetActive(false);

            _mgr = new ScreenManager();
            _mgr.RegisterScreen(_a);
            _mgr.RegisterScreen(_b);
        }

        [TearDown]
        public void Teardown()
        {
            if (_goA != null) Object.DestroyImmediate(_goA);
            if (_goB != null) Object.DestroyImmediate(_goB);
        }

        [Test]
        public void Current_IsNull_WhenStackIsEmpty()
        {
            Assert.IsNull(_mgr.Current);
        }

        [Test]
        public void Push_Generic_ActivatesScreenAndSetsCurrent()
        {
            _mgr.Push<FakeScreenA>();

            Assert.AreSame(_a, _mgr.Current);
            Assert.IsTrue(_goA.activeSelf);
        }

        [Test]
        public void PushByStringId_FindsScreenByTypeName()
        {
            _mgr.PushByStringId(nameof(FakeScreenA));

            Assert.AreSame(_a, _mgr.Current);
        }

        [Test]
        public void PushByStringId_WithUnknownId_LogsWarningAndDoesNotThrow()
        {
            // Arrange — interceptar el warning esperado.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*ClassSelectionScreen.*"));

            // Act — no debe lanzar.
            Assert.DoesNotThrow(() => _mgr.PushByStringId("ClassSelectionScreen"));

            // Assert — estado inalterado.
            Assert.IsNull(_mgr.Current);
        }

        [Test]
        public void Push_Then_Push_HidesPreviousAndShowsNew()
        {
            _mgr.Push<FakeScreenA>();
            _mgr.Push<FakeScreenB>();

            Assert.AreSame(_b, _mgr.Current);
            Assert.IsFalse(_goA.activeSelf, "FakeScreenA debe quedar inactiva al ser cubierta.");
            Assert.IsTrue(_goB.activeSelf);
        }

        [Test]
        public void PopCurrent_ReactivatesPrevious()
        {
            _mgr.Push<FakeScreenA>();
            _mgr.Push<FakeScreenB>();

            _mgr.PopCurrent();

            Assert.AreSame(_a, _mgr.Current);
            Assert.IsTrue(_goA.activeSelf);
            Assert.IsFalse(_goB.activeSelf);
        }

        [Test]
        public void PopCurrent_OnEmptyStack_LogsWarningAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*stack vacio.*"));

            Assert.DoesNotThrow(() => _mgr.PopCurrent());
        }

        [Test]
        public void UnregisterScreen_RemovesFromBothIndices()
        {
            _mgr.UnregisterScreen(_a);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*FakeScreenA.*"));
            _mgr.Push<FakeScreenA>();

            Assert.IsNull(_mgr.Current);
        }
    }
}
