using NUnit.Framework;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Attributes.Stats.Tests
{
    [TestFixture]
    public class EnergyTests
    {
        [Test]
        public void Ctor_Default_IsZero()
        {
            var e = new Energy();
            Assert.AreEqual(0, e.Value);
        }

        [Test]
        public void Ctor_Initial_SetsValue()
        {
            var e = new Energy(2);
            Assert.AreEqual(2, e.Value);
        }

        [Test]
        public void GetAttributeName_ReturnsEnergy()
        {
            var e = new Energy();
            Assert.AreEqual("Energy", e.GetAttributeName());
        }

        [Test]
        public void Duplicate_ClonesRawValue_NotModifiers()
        {
            var e = new Energy(3);
            // BaseAttribute.Duplicate no clona modificadores (TECHNICAL.md §2.2).
            // Aqui validamos el contrato de Duplicate sobre el stat concreto.
            var clone = e.Duplicate() as Energy;

            Assert.IsNotNull(clone);
            Assert.AreNotSame(e, clone);
            Assert.AreEqual(3, clone.Value);
            Assert.AreEqual(0, clone.GetRawModifiers().Count);
        }

        [Test]
        public void GetModifiedValue_NoMods_ReturnsRawValue()
        {
            var e = new Energy(4);
            // Sin mods Intrinsic, ModifiedValue == Value.
            Assert.AreEqual(4, e.GetModifiedValue<int>());
        }

        [Test]
        public void SetValue_RoundTrips()
        {
            var e = new Energy(0);
            e.Value = 7;
            Assert.AreEqual(7, e.Value);
            Assert.AreEqual(7, e.GetValue<int>());
        }

        [Test]
        public void GetValueType_IsInt()
        {
            var e = new Energy();
            Assert.AreEqual(typeof(int), e.GetValueType());
        }
    }
}
