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
        public void GetKnownComboIds_InEditMode_ContainsCanonicalIds()
        {
            // Audit-style: depende de los BaseComboSO assets del proyecto, igual que
            // EnchantmentAssetAuditTests. Si esto falla, el dropdown sale vacío.
            // Literales a propósito: las constantes de ComboId.cs están desfasadas de
            // los ids reales de los assets (PUL-015) — acá pineamos la verdad del asset.
            var ids = BaseComboSO.GetKnownComboIds().ToList();

            Assert.Contains("combo.pair", ids);
            Assert.Contains("combo.generala", ids);
        }
    }
}
