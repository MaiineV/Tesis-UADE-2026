using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.ComboLog;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ComboLogService"/> (Sistemas prerequisito Bosses §3):
    /// Record / LastCombo / Last(n) ventana configurable / marcador "sin combo" / Clear.
    /// </summary>
    [TestFixture]
    public class ComboLogServiceTests
    {
        private ComboLogService _svc;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _svc = new ComboLogService();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            ServiceLocator.Clear();
        }

        [Test]
        public void Record_KeepsMostRecentCombo_AsLastCombo()
        {
            _svc.Record("combo.par");
            _svc.Record("combo.trio");

            Assert.AreEqual("combo.trio", _svc.LastCombo);
        }

        [Test]
        public void Last_ReturnsRequestedWindow_MostRecentFirst()
        {
            _svc.Record("combo.par");
            _svc.Record("combo.trio");
            _svc.Record("combo.escalera");

            var window = _svc.Last(2);

            Assert.AreEqual(2, window.Count);
            Assert.AreEqual("combo.escalera", window[0]);
            Assert.AreEqual("combo.trio", window[1]);
        }

        [Test]
        public void Last_WhenWindowExceedsHistory_ReturnsOnlyWhatExists()
        {
            _svc.Record("combo.par");

            var window = _svc.Last(2);

            Assert.AreEqual(1, window.Count);
            Assert.AreEqual("combo.par", window[0]);
        }

        [Test]
        public void Record_NullOrEmpty_LogsNoComboMarker()
        {
            _svc.Record(null);

            Assert.AreEqual(_svc.NoComboMarker, _svc.LastCombo);
        }

        [Test]
        public void Last_WithZeroOrNegativeWindow_ReturnsEmpty()
        {
            _svc.Record("combo.par");

            Assert.AreEqual(0, _svc.Last(0).Count);
            Assert.AreEqual(0, _svc.Last(-3).Count);
        }

        [Test]
        public void Clear_EmptiesHistory()
        {
            _svc.Record("combo.par");

            _svc.Clear();

            Assert.IsNull(_svc.LastCombo);
            Assert.AreEqual(0, _svc.Last(5).Count);
        }
    }
}
