using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Auditoría del asset real de "Torpe" (BUG-030): el driver
    /// <see cref="ForcedRerollCapabilityService"/> depende de que el asset declare
    /// exactamente una <see cref="CapForceRerollOnTurn"/> — si la serialización
    /// Odin pierde la capability (rid huérfano), el encantamiento vuelve a ser
    /// un no-op silencioso.
    /// </summary>
    [TestFixture]
    public class TorpeAssetAuditTests
    {
        private const string AssetPath =
            "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Torpe.asset";

        [Test]
        public void EnchTorpeAsset_DeclaresForceRerollOnTurnTwo()
        {
            // Arrange
            var torpe = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(AssetPath);
            Assert.IsNotNull(torpe, $"{AssetPath} no encontrado — ¿se movió el catálogo?");

            // Act
            var caps = torpe.Capabilities?.OfType<CapForceRerollOnTurn>().ToList();

            // Assert — el contrato exacto que consume el driver.
            Assert.AreEqual("ench.torpe", torpe.UpgradeId);
            Assert.IsNotNull(caps);
            Assert.AreEqual(1, caps.Count, "Torpe debe declarar exactamente una CapForceRerollOnTurn.");
            Assert.AreEqual(2, caps[0].TriggerOnTurn);
            Assert.IsTrue(torpe.Triggers == null || torpe.Triggers.Count == 0,
                "Torpe es puramente declarativo — no debe tener triggers.");
            Assert.IsNull(torpe.FaceFilter, "Torpe no filtra caras.");
        }
    }
}
