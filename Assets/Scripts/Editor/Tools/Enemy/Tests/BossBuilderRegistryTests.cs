using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class BossBuilderRegistryTests
    {
        [TestCase(CajeroAssetBuilder.EnemyAssetPath,   CajeroAssetBuilder.MenuPath)]
        [TestCase(CajeroAssetBuilder.CritterAssetPath, CajeroAssetBuilder.MenuPath)]
        [TestCase(CroupierAssetBuilder.BossAssetPath,  CroupierAssetBuilder.MenuPath)]
        [TestCase(GeneralaAssetBuilder.BossAssetPath,  GeneralaAssetBuilder.MenuPath)]
        [TestCase(GeneralaAssetBuilder.DiceAssetPath,  GeneralaAssetBuilder.MenuPath)]
        [TestCase(TahurAssetBuilder.AssetPath,         TahurAssetBuilder.MenuPath)]
        [TestCase(BandidaAssetBuilder.BossAssetPath,   BandidaAssetBuilder.MenuPath)]
        [TestCase(BandidaAssetBuilder.ReelAssetPath,   BandidaAssetBuilder.MenuPath)]
        [TestCase(AnotadorAssetBuilder.EnemyAssetPath, AnotadorAssetBuilder.MenuPath)]
        public void TryGetBuilderForPath_EachBuilderAsset_ReturnsItsMenuPath(string assetPath, string expectedMenu)
        {
            Assert.IsTrue(BossBuilderRegistry.TryGetBuilderForPath(assetPath, out var menu));
            Assert.AreEqual(expectedMenu, menu);
        }

        [Test]
        public void TryGetBuilderForPath_UnknownPath_False()
        {
            Assert.IsFalse(BossBuilderRegistry.TryGetBuilderForPath("Assets/Rollgeon/Enemies/ED_Healer.asset", out _));
            Assert.IsFalse(BossBuilderRegistry.TryGetBuilderForPath(null, out _));
        }

        [Test]
        public void Registry_HasNineAssets_AllUnderBossesMenu()
        {
            Assert.AreEqual(9, BossBuilderRegistry.ByAssetPath.Count);
            foreach (var kv in BossBuilderRegistry.ByAssetPath)
                StringAssert.StartsWith("Tools/Rollgeon/Bosses/Build ", kv.Value, kv.Key);
        }

        [Test]
        public void BannerText_MentionsTheMenuPath()
        {
            StringAssert.Contains(TahurAssetBuilder.MenuPath, BossBuilderRegistry.BannerText(TahurAssetBuilder.MenuPath));
        }
    }
}
