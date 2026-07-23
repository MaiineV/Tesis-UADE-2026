using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Combat.Handoff.Tests
{
    /// <summary>
    /// Regresión: los héroes cuyo ataque usa chain deben registrar su combo en
    /// <see cref="IComboLogService"/>. El path chain (<c>ExecuteChainPhase</c>) detectaba el
    /// combo pero nunca lo registraba — el Record del path primario no corre para chains
    /// porque <c>DoConfirm</c> hace early-return con el chain activo. Sin este registro el
    /// historial queda vacío y los consumidores del log (forbid-combo del jefe de piso 2 y
    /// el snapshot de resume) no ven el combo del turno.
    /// </summary>
    [TestFixture]
    public class ChainComboRecordTests
    {
        private ComboLogService _comboLog;
        private Combo_Par _par;
        private ContractSheet _sheet;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _comboLog = new ComboLogService();
            _comboLog.Register();

            _par = ScriptableObject.CreateInstance<Combo_Par>();
            SetField(_par, "_comboId", "combo.par");
            SetField(_par, "_baseDamage", 10);
            _sheet = new ContractSheet { Combos = new List<BaseComboSO> { _par } };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_par);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void DetectChainCombo_PhaseZero_RecordsMatchedCombo()
        {
            // Act
            var result = CombatHandoffService.DetectChainCombo(
                _sheet, new[] { 3, 3 }, null, chainPhaseIndex: 0);

            // Assert
            Assert.IsTrue(result.HasValue && result.Value.IsMatch, "Par debe matchear [3,3].");
            Assert.AreEqual("combo.par", _comboLog.LastCombo,
                "La fase 0 del chain debe registrar el combo del ataque — sin esto el " +
                "historial queda vacío para el forbid-combo del jefe 2 y el resume.");
        }

        [Test]
        public void DetectChainCombo_PhaseZero_NoMatch_RecordsNoComboMarker()
        {
            // Act
            var result = CombatHandoffService.DetectChainCombo(
                _sheet, new[] { 1, 2 }, null, chainPhaseIndex: 0);

            // Assert
            Assert.IsFalse(result.HasValue && result.Value.IsMatch,
                "[1,2] no debe matchear Par.");
            Assert.AreEqual(_comboLog.NoComboMarker, _comboLog.LastCombo,
                "Ataque sin combo también registra (marcador), igual que el path primario.");
        }

        [Test]
        public void DetectChainCombo_LaterPhases_DoNotRecord()
        {
            // Arrange: la fase 0 ya registró el combo del ataque.
            CombatHandoffService.DetectChainCombo(_sheet, new[] { 3, 3 }, null, chainPhaseIndex: 0);

            // Act: continuación del chain con tirada nueva.
            CombatHandoffService.DetectChainCombo(_sheet, new[] { 4, 4 }, null, chainPhaseIndex: 1);

            // Assert
            Assert.AreEqual(1, _comboLog.Last(5).Count,
                "Las fases > 0 son continuaciones — no redefinen 'el combo del turno'.");
            Assert.AreEqual("combo.par", _comboLog.LastCombo);
        }

        // Mismo helper que ComboTestUtils (assembly de tests de Combos, no referenciado acá).
        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
