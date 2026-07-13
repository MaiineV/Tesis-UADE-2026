using NUnit.Framework;
using Rollgeon.UI.HUD.DiceAnim;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Latch estático que difiere el teardown de la zona de dados durante el outro.
    /// Estado global ⇒ cada test lo deja limpio en teardown.
    /// </summary>
    [TestFixture]
    public class DiceOutroGateTests
    {
        [TearDown]
        public void TearDown()
        {
            DiceOutroGate.End();
        }

        [Test]
        public void Begin_SetsOutroPendingAndRaisesChanged()
        {
            int raised = 0;
            System.Action handler = () => raised++;
            DiceOutroGate.Changed += handler;

            DiceOutroGate.Begin();

            Assert.IsTrue(DiceOutroGate.OutroPending);
            Assert.AreEqual(1, raised);
            DiceOutroGate.Changed -= handler;
        }

        [Test]
        public void End_ClearsOutroPendingAndRaisesChanged()
        {
            DiceOutroGate.Begin();
            int raised = 0;
            System.Action handler = () => raised++;
            DiceOutroGate.Changed += handler;

            DiceOutroGate.End();

            Assert.IsFalse(DiceOutroGate.OutroPending);
            Assert.AreEqual(1, raised);
            DiceOutroGate.Changed -= handler;
        }

        [Test]
        public void Begin_Twice_IsIdempotent()
        {
            DiceOutroGate.Begin();
            int raised = 0;
            System.Action handler = () => raised++;
            DiceOutroGate.Changed += handler;

            DiceOutroGate.Begin();

            Assert.IsTrue(DiceOutroGate.OutroPending);
            Assert.AreEqual(0, raised, "Un Begin redundante no debe re-disparar Changed.");
            DiceOutroGate.Changed -= handler;
        }

        [Test]
        public void End_WithoutBegin_DoesNothing()
        {
            int raised = 0;
            System.Action handler = () => raised++;
            DiceOutroGate.Changed += handler;

            DiceOutroGate.End();

            Assert.IsFalse(DiceOutroGate.OutroPending);
            Assert.AreEqual(0, raised);
            DiceOutroGate.Changed -= handler;
        }
    }
}
