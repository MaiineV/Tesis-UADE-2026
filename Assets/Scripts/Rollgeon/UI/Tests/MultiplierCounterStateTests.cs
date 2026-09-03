using NUnit.Framework;
using Rollgeon.UI.HUD.Breakdown;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="MultiplierCounterState"/>: el contador M compone
    /// <c>Core × (1 + Σadd)</c>, así que cae en el mismo valor sin importar el orden en que
    /// lleguen pasos aditivos y multiplicativos.
    /// </summary>
    [TestFixture]
    public class MultiplierCounterStateTests
    {
        [Test]
        public void AddThenMultiply_MatchesFormula()
        {
            var m = MultiplierCounterState.At(0.75f);

            m.AddBonus(2f);
            m.Multiply(1.5f);

            // 0.75 × (1 + 2) × 1.5
            Assert.AreEqual(3.375f, m.Value, 0.0001f);
        }

        [Test]
        public void MultiplyThenAdd_SameResult()
        {
            var m = MultiplierCounterState.At(0.75f);

            m.Multiply(1.5f);
            m.AddBonus(2f);

            Assert.AreEqual(3.375f, m.Value, 0.0001f);
        }

        [Test]
        public void At_ResetsAddSum()
        {
            var m = MultiplierCounterState.At(1f);
            m.AddBonus(2f);

            m = MultiplierCounterState.At(1f);

            Assert.AreEqual(1f, m.Value, 0.0001f);
            Assert.AreEqual(0f, m.AddSum, 0.0001f);
        }

        [Test]
        public void AddBonus_AccumulatesAdditively()
        {
            var m = MultiplierCounterState.At(1f);

            m.AddBonus(2f);
            m.AddBonus(3f);

            Assert.AreEqual(6f, m.Value, 0.0001f, "1 × (1 + 2 + 3)");
        }
    }
}
