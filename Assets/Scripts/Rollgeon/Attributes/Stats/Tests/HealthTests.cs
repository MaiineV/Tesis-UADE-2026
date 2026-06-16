using NUnit.Framework;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Attributes.Stats.Tests
{
    /// <summary>
    /// Smoke tests de <see cref="Health"/>: ctor default, ctor initial, duplicate,
    /// round-trip de SetValue, tipo interno.
    /// </summary>
    [TestFixture]
    public class HealthTests
    {
        [Test]
        public void Ctor_Default_IsZero()
        {
            var h = new Health();
            Assert.AreEqual(0, h.Value);
        }

        [Test]
        public void Ctor_Initial_SetsValue()
        {
            var h = new Health(25);
            Assert.AreEqual(25, h.Value);
        }

        [Test]
        public void GetAttributeName_ReturnsHealth()
        {
            var h = new Health();
            Assert.AreEqual("Health", h.GetAttributeName());
        }

        [Test]
        public void Duplicate_ClonesRawValue_NotModifiers()
        {
            var h = new Health(12);
            var clone = h.Duplicate() as Health;

            Assert.IsNotNull(clone);
            Assert.AreNotSame(h, clone);
            Assert.AreEqual(12, clone.Value);
            Assert.AreEqual(0, clone.GetRawModifiers().Count);
        }

        [Test]
        public void GetValueType_IsInt()
        {
            Assert.AreEqual(typeof(int), new Health().GetValueType());
        }

        [Test]
        public void SetValue_RoundTrips()
        {
            var h = new Health(0);
            h.Value = 17;
            Assert.AreEqual(17, h.Value);
            Assert.AreEqual(17, h.GetValue<int>());
        }
    }
}
