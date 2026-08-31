using NUnit.Framework;

namespace Rollgeon.Editor.Tools.Item.Tests
{
    /// <summary>
    /// Item ids are frozen at creation and used as save keys (spec §3), so the slug derivation has to
    /// be predictable: accents, punctuation and casing must not sneak a second id into the catalog for
    /// what is really the same word.
    /// </summary>
    public sealed class ItemIdSlugTests
    {
        [Test]
        public void FromDisplayName_SimpleTwoWordName_LowercasesAndDotSeparates()
        {
            Assert.AreEqual("banquete.real", ItemIdSlug.FromDisplayName("Banquete Real"));
        }

        [Test]
        public void FromDisplayName_StripsAccentsAndTilde()
        {
            Assert.AreEqual("bendicion.del.corazon", ItemIdSlug.FromDisplayName("Bendición del Corazón"));
            Assert.AreEqual("pinata.magica", ItemIdSlug.FromDisplayName("Piñata Mágica"));
        }

        [Test]
        public void FromDisplayName_PunctuationCollapsesToASingleDot()
        {
            Assert.AreEqual("botas.del.viento", ItemIdSlug.FromDisplayName("Botas -- del  Viento!!"));
        }

        [Test]
        public void FromDisplayName_KeepsDigits()
        {
            Assert.AreEqual("corona.tier.3", ItemIdSlug.FromDisplayName("Corona Tier 3"));
        }

        [Test]
        public void FromDisplayName_TrimsLeadingAndTrailingSeparators()
        {
            Assert.AreEqual("egoista", ItemIdSlug.FromDisplayName("  ¡Egoísta!  "));
        }

        [Test]
        public void FromDisplayName_NullOrWhitespace_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ItemIdSlug.FromDisplayName(null));
            Assert.AreEqual(string.Empty, ItemIdSlug.FromDisplayName(string.Empty));
            Assert.AreEqual(string.Empty, ItemIdSlug.FromDisplayName("   "));
        }

        [Test]
        public void FromDisplayName_OnlySymbols_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ItemIdSlug.FromDisplayName("!!! --- ???"));
        }
    }
}
