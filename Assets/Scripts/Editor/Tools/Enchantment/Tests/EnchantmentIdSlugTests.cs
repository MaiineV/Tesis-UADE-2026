using NUnit.Framework;

namespace Rollgeon.Editor.Tools.Enchantment.Tests
{
    public class EnchantmentIdSlugTests
    {
        [Test]
        public void FromDisplayName_TwoWords_PrefixesChannelAndSnakeCases()
        {
            // Arrange + Act
            var id = EnchantmentIdSlug.FromDisplayName("Piedra Sangrienta");

            // Assert
            Assert.AreEqual("ench.piedra_sangrienta", id);
        }

        [Test]
        public void FromDisplayName_AccentsAndEnie_StripToPlainAscii()
        {
            // Arrange + Act
            var id = EnchantmentIdSlug.FromDisplayName("Año del Ñandú Mágico");

            // Assert
            Assert.AreEqual("ench.ano_del_nandu_magico", id);
        }

        [Test]
        public void FromDisplayName_PunctuationRuns_CollapseToOneSeparator()
        {
            // Arrange + Act
            var id = EnchantmentIdSlug.FromDisplayName("Caras... Centrales!");

            // Assert
            Assert.AreEqual("ench.caras_centrales", id);
        }

        [Test]
        public void FromDisplayName_Digits_AreKept()
        {
            // Arrange + Act
            var id = EnchantmentIdSlug.FromDisplayName("Múltiplo de 3");

            // Assert
            Assert.AreEqual("ench.multiplo_de_3", id);
        }

        [Test]
        public void FromDisplayName_LeadingAndTrailingSeparators_AreTrimmed()
        {
            // Arrange + Act
            var id = EnchantmentIdSlug.FromDisplayName("  ¡Volátil!  ");

            // Assert
            Assert.AreEqual("ench.volatil", id);
        }

        [Test]
        public void FromDisplayName_NullOrWhitespace_ReturnsEmpty()
        {
            // Arrange + Act + Assert
            Assert.AreEqual(string.Empty, EnchantmentIdSlug.FromDisplayName(null));
            Assert.AreEqual(string.Empty, EnchantmentIdSlug.FromDisplayName("   "));
        }

        [Test]
        public void FromDisplayName_OnlySymbols_ReturnsEmpty()
        {
            // Arrange + Act
            var id = EnchantmentIdSlug.FromDisplayName("!!! --- ...");

            // Assert — sin prefijo suelto: un display name que no deriva nada es inválido entero.
            Assert.AreEqual(string.Empty, id);
        }
    }
}
