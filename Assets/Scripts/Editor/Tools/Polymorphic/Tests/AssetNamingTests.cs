using NUnit.Framework;
using Rollgeon.Editor.Tools.Polymorphic;

namespace Rollgeon.Editor.Tools.Polymorphic.Tests
{
    /// <summary>
    /// The rename button derives a file name from an authored id, so the rule has to reproduce the
    /// naming already on disk — otherwise it would offer to rename 33 correctly-named assets.
    /// </summary>
    public sealed class AssetNamingTests
    {
        [Test]
        public void PascalCaseId_SplitsOnDots()
        {
            Assert.AreEqual("PotionHealing", AssetNaming.PascalCaseId("potion.healing"));
        }

        [Test]
        public void PascalCaseId_SplitsOnUnderscoresAndKeepsDigits()
        {
            // ench.multiplo_de_3 (prefix stripped by the host) -> Ench_MultiploDe3, the name on disk.
            Assert.AreEqual("MultiploDe3", AssetNaming.PascalCaseId("multiplo_de_3"));
        }

        [Test]
        public void PascalCaseId_SplitsOnHyphensAndSpaces()
        {
            Assert.AreEqual("GoldOnRoll", AssetNaming.PascalCaseId("gold-on roll"));
        }

        [Test]
        public void PascalCaseId_LeavesAnAlreadyPascalIdAlone()
        {
            Assert.AreEqual("NewItemTest", AssetNaming.PascalCaseId("NewItemTest"));
        }

        [Test]
        public void PascalCaseId_CollapsesRepeatedSeparators()
        {
            Assert.AreEqual("AB", AssetNaming.PascalCaseId("a..__b"));
        }

        [Test]
        public void PascalCaseId_EmptyOrNull_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, AssetNaming.PascalCaseId(null));
            Assert.AreEqual(string.Empty, AssetNaming.PascalCaseId(string.Empty));
        }

        [Test]
        public void PascalCaseId_SeparatorsOnly_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, AssetNaming.PascalCaseId("..._"));
        }
    }
}
