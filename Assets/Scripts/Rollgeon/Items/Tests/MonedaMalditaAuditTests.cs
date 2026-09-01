using NUnit.Framework;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Upgrades.Dice;
using UnityEditor;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Auditoría de datos de Moneda Maldita: las dos mitades del efecto (descuento
    /// del altar + peso de malditos), el ícono, y el wiring del servicio de pesos en
    /// ServiceBootstrap — sin ese asset en ExtraServices el multiplicador degrada en
    /// silencio a "sin efecto".
    /// </summary>
    [TestFixture]
    public class MonedaMalditaAuditTests
    {
        private const string ItemPath = "Assets/Rollgeon/Items/Item_MonedaMaldita.asset";
        private const string BootstrapPath = "Assets/Rollgeon/ServiceBootstrap.asset";

        [Test]
        public void MonedaMaldita_HasIconAndBothMultipliers()
        {
            // Arrange + Act
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(ItemPath);

            // Assert
            Assert.IsNotNull(item, "No se encontró " + ItemPath);
            Assert.IsNotNull(item.Icon, "Moneda Maldita sin ícono (QA lo reportó — al menos placeholder)");
            Assert.AreEqual(0.5f, item.EnchantmentCostMultiplier, 0.0001f,
                "El descuento del altar (mitad existente del item) no debe perderse");
            Assert.AreEqual(3f, item.CursedEnchantmentWeightMultiplier, 0.0001f,
                "La mitad 'caos': malditos aparecen 3x más seguido");
        }

        [Test]
        public void ServiceBootstrap_IncludesEnchantmentWeightModifierBootstrap()
        {
            // Arrange + Act
            var bootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(BootstrapPath);

            // Assert
            Assert.IsNotNull(bootstrap, "No se encontró " + BootstrapPath);
            bool wired = false;
            foreach (var svc in bootstrap.ExtraServices)
                if (svc is EnchantmentWeightModifierServiceBootstrap) wired = true;
            Assert.IsTrue(wired,
                "EnchantmentWeightModifierServiceBootstrap falta en ExtraServices — " +
                "sin él, el peso de malditos de Moneda Maldita no hace nada");
        }
    }
}
