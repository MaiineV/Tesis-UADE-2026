using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using Rollgeon.Heroes;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Resolución del daño base de una fila del contrato (pura, sin TMP):
    /// override explícito de la selección de clase → sheet del héroe actual →
    /// base del catálogo.
    /// </summary>
    [TestFixture]
    public class ComboRowViewTests
    {
        private Combo_Par _par;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.RemoveService<Rollgeon.Player.IPlayerService>();
            _par = ComboTestUtils.CreateCombo<Combo_Par>("combo.pair", 18);
        }

        [TearDown]
        public void Teardown()
        {
            ServiceLocator.RemoveService<Rollgeon.Player.IPlayerService>();
            if (_par != null) Object.DestroyImmediate(_par);
        }

        [Test]
        public void ResolveBaseDamage_WithSheetOverride_ReturnsClassTableValue()
        {
            // Arrange: la clase overridea el daño del Par a 30.
            var sheet = new ContractSheet
            {
                Combos = new List<BaseComboSO> { _par },
                BaseDamageTable = new List<ComboBaseDamageEntry>
                {
                    new ComboBaseDamageEntry { ComboId = "combo.pair", BaseDamage = 30 },
                },
            };

            // Act
            int resolved = ComboRowView.ResolveBaseDamage(_par, sheet);

            // Assert
            Assert.AreEqual(30, resolved,
                "Con override de sheet (selección de clase) debe mostrar el valor de la clase.");
        }

        [Test]
        public void ResolveBaseDamage_OverrideWithoutEntry_FallsBackToComboBase()
        {
            // Arrange: sheet sin entry para el combo → GetBaseDamage cae al base.
            var sheet = new ContractSheet { Combos = new List<BaseComboSO> { _par } };

            // Act
            int resolved = ComboRowView.ResolveBaseDamage(_par, sheet);

            // Assert
            Assert.AreEqual(18, resolved);
        }

        [Test]
        public void ResolveBaseDamage_NoOverride_NoPlayerService_FallsBackToComboBaseDamage()
        {
            // Act
            int resolved = ComboRowView.ResolveBaseDamage(_par, sheetOverride: null);

            // Assert
            Assert.AreEqual(18, resolved,
                "Sin override ni CurrentHero, la fila muestra el daño del catálogo.");
        }

        [Test]
        public void ResolveBaseDamage_NullCombo_ReturnsZero()
        {
            Assert.AreEqual(0, ComboRowView.ResolveBaseDamage(null, sheetOverride: null));
        }
    }
}
