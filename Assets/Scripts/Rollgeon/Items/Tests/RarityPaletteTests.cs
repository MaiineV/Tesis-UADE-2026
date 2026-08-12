using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Los hexas por tier son un contrato de diseño (GDD del Cofre §21) — se
    /// asserta el valor exacto para que un retoque accidental no pase silencioso.
    /// </summary>
    [TestFixture]
    public class RarityPaletteTests
    {
        [TestCase(ItemRarity.Common, 0xB0, 0x89, 0x68)]
        [TestCase(ItemRarity.Uncommon, 0xB3, 0x3A, 0x1F)]
        [TestCase(ItemRarity.Rare, 0x5C, 0x4A, 0x7A)]
        [TestCase(ItemRarity.Legendary, 0xD9, 0xA4, 0x4E)]
        public void BodyColor_ShouldMatchApprovedHex(ItemRarity rarity, int r, int g, int b)
        {
            // Act
            Color32 color = RarityPalette.BodyColor(rarity);

            // Assert
            Assert.AreEqual((byte)r, color.r);
            Assert.AreEqual((byte)g, color.g);
            Assert.AreEqual((byte)b, color.b);
            Assert.AreEqual((byte)0xFF, color.a);
        }

        [Test]
        public void Fittings_ShouldMatchApprovedHex()
        {
            // Act
            Color32 color = RarityPalette.Fittings;

            // Assert — #5F737A, común a todos los tiers.
            Assert.AreEqual((byte)0x5F, color.r);
            Assert.AreEqual((byte)0x73, color.g);
            Assert.AreEqual((byte)0x7A, color.b);
        }
    }
}
