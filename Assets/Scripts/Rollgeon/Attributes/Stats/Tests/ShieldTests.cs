using NUnit.Framework;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Attributes.Stats.Tests
{
    [TestFixture]
    public class ShieldTests
    {
        [Test]
        public void Ctor_Default_IsZero()
        {
            var s = new Shield();
            Assert.AreEqual(0, s.Value);
        }

        [Test]
        public void Ctor_Initial_SetsValue()
        {
            var s = new Shield(15);
            Assert.AreEqual(15, s.Value);
        }

        [Test]
        public void GetAttributeName_ReturnsShield()
        {
            var s = new Shield();
            Assert.AreEqual("Shield", s.GetAttributeName());
        }

        [Test]
        public void Duplicate_ClonesRawValue_NotModifiers()
        {
            var s = new Shield(8);
            var clone = s.Duplicate() as Shield;

            Assert.IsNotNull(clone);
            Assert.AreNotSame(s, clone);
            Assert.AreEqual(8, clone.Value);
            Assert.AreEqual(0, clone.GetRawModifiers().Count);
        }

        [Test]
        public void GetValueType_IsInt()
        {
            Assert.AreEqual(typeof(int), new Shield().GetValueType());
        }

        [Test]
        public void SetValue_RoundTrips()
        {
            var s = new Shield(0);
            s.Value = 42;
            Assert.AreEqual(42, s.Value);
            Assert.AreEqual(42, s.GetValue<int>());
        }
    }
}
