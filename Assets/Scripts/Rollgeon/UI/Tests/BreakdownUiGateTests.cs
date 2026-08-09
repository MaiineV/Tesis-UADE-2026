using NUnit.Framework;
using Rollgeon.Feedback;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="BreakdownUiGate"/>: semántica de ref-count (a diferencia del
    /// bool de DiceOutroGate) y notificación solo en transiciones.
    /// </summary>
    [TestFixture]
    public class BreakdownUiGateTests
    {
        [SetUp]
        public void SetUp() => Drain();

        [TearDown]
        public void TearDown() => Drain();

        private static void Drain()
        {
            // El gate es estático: drenar cualquier pendiente que otro test haya dejado.
            for (int i = 0; i < 8 && BreakdownUiGate.Pending; i++) BreakdownUiGate.End();
        }

        [Test]
        public void Begin_SetsPendingAndRaisesChangedOnce()
        {
            // Arrange
            int raised = 0;
            System.Action handler = () => raised++;
            BreakdownUiGate.Changed += handler;

            // Act
            BreakdownUiGate.Begin();

            // Assert
            Assert.IsTrue(BreakdownUiGate.Pending);
            Assert.AreEqual(1, raised);
            BreakdownUiGate.Changed -= handler;
            BreakdownUiGate.End();
        }

        [Test]
        public void RefCount_NestedBegins_OnlyLastEndReleases()
        {
            int raised = 0;
            System.Action handler = () => raised++;

            BreakdownUiGate.Begin();
            BreakdownUiGate.Changed += handler;
            BreakdownUiGate.Begin();  // anidado: sin transición
            Assert.AreEqual(0, raised);

            BreakdownUiGate.End();    // sigue pendiente
            Assert.IsTrue(BreakdownUiGate.Pending);
            Assert.AreEqual(0, raised);

            BreakdownUiGate.End();    // libera: transición
            Assert.IsFalse(BreakdownUiGate.Pending);
            Assert.AreEqual(1, raised);

            BreakdownUiGate.Changed -= handler;
        }

        [Test]
        public void End_WithoutBegin_IsInert()
        {
            int raised = 0;
            System.Action handler = () => raised++;
            BreakdownUiGate.Changed += handler;

            BreakdownUiGate.End();

            Assert.IsFalse(BreakdownUiGate.Pending);
            Assert.AreEqual(0, raised);
            BreakdownUiGate.Changed -= handler;
        }
    }
}
