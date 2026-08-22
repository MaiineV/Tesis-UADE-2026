using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Meta.Conditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Readers;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Pinea el contrato del dropdown transversal de combo ids. La expresión
    /// <c>"@Rollgeon.Combos.BaseComboSO.GetKnownComboIds()"</c> es un string que Odin
    /// resuelve recién al dibujar: un rename del método o del tipo compila igual y
    /// rompe TODOS los dropdowns en silencio (queda un error box que nadie mira).
    /// </summary>
    [TestFixture]
    public class ComboIdDropdownContractTests
    {
        private const string Resolver = "@Rollgeon.Combos.BaseComboSO.GetKnownComboIds()";

        [TestCase(typeof(ComboFilter), nameof(ComboFilter.ComboIds))]
        [TestCase(typeof(ReadComboCounter), nameof(ReadComboCounter.ComboId))]
        [TestCase(typeof(PCComboAvailable), nameof(PCComboAvailable.ComboId))]
        [TestCase(typeof(ComboNeverExecutedCondition), nameof(ComboNeverExecutedCondition.ComboId))]
        [TestCase(typeof(ComboExecutedTimesCondition), nameof(ComboExecutedTimesCondition.ComboId))]
        [TestCase(typeof(ComboBaseDamageEntry), nameof(ComboBaseDamageEntry.ComboId))]
        [TestCase(typeof(ComboShieldBaseEntry), nameof(ComboShieldBaseEntry.ComboId))]
        [TestCase(typeof(ComboHealBaseEntry), nameof(ComboHealBaseEntry.ComboId))]
        public void AuthorableComboIdFields_CarryTheSharedDropdown(Type host, string fieldName)
        {
            var dropdown = host.GetField(fieldName)?.GetCustomAttribute<ValueDropdownAttribute>();

            Assert.IsNotNull(dropdown, $"{host.Name}.{fieldName} perdió su [ValueDropdown] — vuelve a ser string tipeable");
            Assert.AreEqual(Resolver, dropdown.ValuesGetter, $"{host.Name}.{fieldName} no usa el resolver transversal");
        }

        [Test]
        public void ResolverExpression_StillPointsToTheRealProvider()
        {
            // Un refactor-rename actualiza este nameof pero NO los strings de los
            // atributos: si divergen, este es el único test que lo delata.
            Assert.AreEqual(
                $"@{typeof(BaseComboSO).FullName}.{nameof(BaseComboSO.GetKnownComboIds)}()",
                Resolver);
        }

        [Test]
        public void ComboIdConstants_And_ProjectAssets_StayInParity()
        {
            // Audit-style: depende de los BaseComboSO assets del proyecto, igual que
            // EnchantmentAssetAuditTests. Cierra PUL-015: las constantes habían quedado
            // desfasadas de los assets (combo.par vs combo.pair) sin que nada lo delate.
            // Paridad en ambas direcciones: una constante stale nunca matchea en runtime,
            // y un asset sin constante es invisible para el código.
            var constants = typeof(ComboId)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();
            var assetIds = BaseComboSO.GetKnownComboIds().ToList();

            CollectionAssert.AreEquivalent(assetIds, constants,
                "ComboId.cs y los BaseComboSO assets divergieron — renombrar un id se hace en ambos lados");
        }
    }
}
