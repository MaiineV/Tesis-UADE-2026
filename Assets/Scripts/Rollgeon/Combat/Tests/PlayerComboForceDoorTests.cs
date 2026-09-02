using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="PlayerComboForceDoor.Resolve"/>: el check de Forzar Puerta
    /// es la MISMA aritmética v3 que el daño (N = base + ATQ + Σcaras + bonos; M), solo
    /// que rotulada como <see cref="PlayerComboFormulaKind.ForceDoor"/>. El bonus de
    /// items (ForceDoorRollBonus) entra a N y queda journaleado para la animación.
    /// </summary>
    [TestFixture]
    public class PlayerComboForceDoorTests
    {
        private AttributesManager _attrs;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            _attrs?.Dispose();
        }

        private static ContributingDie[] DiceOf(params int[] faces)
        {
            var result = new ContributingDie[faces.Length];
            for (int i = 0; i < faces.Length; i++)
                result[i] = new ContributingDie(i, faces[i], DiceType.D6);
            return result;
        }

        [Test]
        public void Resolve_ComboBaseAndDice_ReturnsRoundedNxM_AndKindForceDoor()
        {
            // Arrange — sin servicios: N = 22 + 4+4+4 = 34; M = 1.
            var dice = DiceOf(4, 4, 4);

            // Act
            int total = PlayerComboForceDoor.Resolve(_player, 22, dice, 1f, out var breakdown);

            // Assert
            Assert.AreEqual(34, total);
            Assert.AreEqual(PlayerComboFormulaKind.ForceDoor, breakdown.Kind);
        }

        [Test]
        public void Resolve_WithAttackRegistered_AddsAttackToN()
        {
            // Arrange — Attack 5 del PJ entra a N igual que en el daño.
            var a = new ModifiableAttributes();
            a.SetAttribute<Attack>(new Attack(5));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            // Act — N = 22 + 5 + 12 = 39.
            int total = PlayerComboForceDoor.Resolve(_player, 22, DiceOf(4, 4, 4));

            // Assert
            Assert.AreEqual(39, total);
        }

        [Test]
        public void Resolve_AbilityMultiplier_ScalesWholeN()
        {
            // Arrange + Act — N = 34; M = 1.5 → 51 (AwayFromZero).
            int total = PlayerComboForceDoor.Resolve(_player, 22, DiceOf(4, 4, 4), 1.5f);

            // Assert
            Assert.AreEqual(51, total);
        }

        [Test]
        public void Resolve_ForceDoorRollBonus_EntersN_AndIsJournaledAsItemSource()
        {
            // Arrange — stat +5 del item (Pico de Minero) registrado en el jugador.
            var a = new ModifiableAttributes();
            a.SetAttribute<ForceDoorRollBonus>(new ForceDoorRollBonus(5));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            // Act — N = 22 + 12 + 5 = 39; M = 1.
            int total = PlayerComboForceDoor.Resolve(_player, 22, DiceOf(4, 4, 4), 1f, out var bd);

            // Assert — el bonus vive en N (AdditiveBonus) y tiene entrada en el journal
            // para que el breakdown lo haga volar como modificador global.
            Assert.AreEqual(39, total);
            Assert.AreEqual(5, bd.AdditiveBonus);
            Assert.IsNotNull(bd.Sources);
            Assert.AreEqual(1, bd.Sources.Count);
            Assert.AreEqual(Rollgeon.Upgrades.Dice.ScratchSourceKind.Item, bd.Sources[0].Kind);
            Assert.AreEqual(5, bd.Sources[0].BonusDelta);
            Assert.AreEqual(-1, bd.Sources[0].BagSlot);
        }

        [Test]
        public void Resolve_ForceDoorRollBonus_IsScaledByMultiplier()
        {
            // Arrange — (22 + 12 + 5) × 2 = 78: el item ya no es flat post-M.
            var a = new ModifiableAttributes();
            a.SetAttribute<ForceDoorRollBonus>(new ForceDoorRollBonus(5));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            // Act
            int total = PlayerComboForceDoor.Resolve(_player, 22, DiceOf(4, 4, 4), 2f);

            // Assert
            Assert.AreEqual(78, total);
        }

        [Test]
        public void Resolve_DamageKind_IgnoresForceDoorRollBonus()
        {
            // Arrange — el stat solo aplica al check de puerta, nunca al daño.
            var a = new ModifiableAttributes();
            a.SetAttribute<ForceDoorRollBonus>(new ForceDoorRollBonus(5));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            // Act
            int total = PlayerComboDamage.Resolve(_player, 22, DiceOf(4, 4, 4));

            // Assert
            Assert.AreEqual(34, total);
        }
    }
}
