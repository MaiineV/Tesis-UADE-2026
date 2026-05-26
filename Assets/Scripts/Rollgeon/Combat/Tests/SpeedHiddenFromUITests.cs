using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Asserts estáticos que protegen el contrato "Speed es oculto en UI"
    /// (TECHNICAL.md §4.2, §12.7). Si alguien borra el atributo por accidente,
    /// este test rompe antes de que llegue al HUD.
    /// </summary>
    [TestFixture]
    public class SpeedHiddenFromUITests
    {
        [Test]
        public void Speed_IsMarkedWithHiddenFromUIAttribute()
        {
            var attrs = (HiddenFromUIAttribute[])typeof(Speed)
                .GetCustomAttributes(typeof(HiddenFromUIAttribute), inherit: true);

            Assert.AreEqual(1, attrs.Length,
                "Speed debe declararse con [HiddenFromUI] — el HUD lo usa para skippear el stat al renderizar.");
        }
    }
}
