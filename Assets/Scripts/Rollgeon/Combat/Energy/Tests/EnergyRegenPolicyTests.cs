using NUnit.Framework;
using Rollgeon.Combat.EnergyLib;

namespace Rollgeon.Combat.EnergyLib.Tests
{
    [TestFixture]
    public class EnergyRegenPolicyTests
    {
        // Tabla exhaustiva GDD #100, defaults (max=4, regen=2).
        [TestCase(0, 4, 2, ExpectedResult = 2)]
        [TestCase(2, 4, 2, ExpectedResult = 4)] // 2+2 = 4 (cap exacto).
        [TestCase(3, 4, 2, ExpectedResult = 4)] // 3+2 = 5 → cap 4.
        [TestCase(4, 4, 2, ExpectedResult = 4)] // 4+2 = 6 → cap 4.
        [TestCase(1, 4, 2, ExpectedResult = 3)]
        // Otros presets (designer tweak future).
        [TestCase(2, 6, 3, ExpectedResult = 5)]
        [TestCase(5, 6, 3, ExpectedResult = 6)] // cap.
        public int ComputeNewCurrent_Table(int current, int max, int regen)
        {
            return EnergyRegenPolicy.ComputeNewCurrent(current, max, regen);
        }

        [Test]
        public void NegativeCurrent_Clamps()
        {
            // Defensivo: si alguien pasa un current negativo, lo tratamos como 0.
            Assert.AreEqual(2, EnergyRegenPolicy.ComputeNewCurrent(-5, 4, 2));
        }

        [Test]
        public void NegativeRegen_TreatedAsZero()
        {
            Assert.AreEqual(2, EnergyRegenPolicy.ComputeNewCurrent(2, 4, -1));
        }

        [Test]
        public void NegativeMax_TreatedAsZero()
        {
            Assert.AreEqual(0, EnergyRegenPolicy.ComputeNewCurrent(2, -1, 2));
        }

        [Test]
        public void ZeroMax_ForcesZero()
        {
            Assert.AreEqual(0, EnergyRegenPolicy.ComputeNewCurrent(0, 0, 2));
        }
    }
}
