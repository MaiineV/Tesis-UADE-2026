using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests del overload <c>Resolve(..., out DamageBreakdown)</c>: cada término reportado
    /// coincide con lo que la fórmula computó, y el overload simple devuelve lo mismo.
    /// </summary>
    [TestFixture]
    public class DamageBreakdownTests
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

        private void RegisterPlayerAttackWithFlatBonus(int baseValue, int flatBonus)
        {
            var a = new ModifiableAttributes();
            a.SetAttribute<Attack>(new Attack(baseValue));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);
            var mod = new Modifier<int>(
                amount: flatBonus, op: ModifierOperation.Add, duration: 0,
                carrierId: _player, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            _attrs.AddModifier<Attack, int>(_player, mod);
        }

        private void RegisterScratches()
        {
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, ComboDamageMultiplier = 2f }
            }, ServiceScope.Global);
            ServiceLocator.AddService<IComboPlayService>(new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 3, ComboDamageMultiplier = 1.5f }
            }, ServiceScope.Global);
        }

        [Test]
        public void Resolve_OutBreakdown_ReportsEveryTermOfTheFormula()
        {
            // Arrange
            RegisterPlayerAttackWithFlatBonus(baseValue: 5, flatBonus: 3);
            RegisterScratches();
            var dice = new[]
            {
                new ContributingDie(0, 4, DiceType.D6),
                new ContributingDie(2, 18, DiceType.D20),
            };

            // Act
            int result = PlayerComboDamage.Resolve(_player, 10, dice, 0.75f,
                PlayerComboFormulaKind.Damage, out var b);

            // Assert — N = 10 + 5 + 3 + 22 + 7 = 47; M = (2 × 1.5) × 0.75 = 2.25 → 105.75 → 106
            Assert.AreEqual(PlayerComboFormulaKind.Damage, b.Kind);
            Assert.AreEqual(10, b.ComboBase);
            Assert.AreEqual(5, b.AttackBase);
            Assert.AreEqual(3, b.AttackBonus);
            Assert.AreEqual(22, b.FacesSum);
            Assert.AreEqual(7, b.AdditiveBonus);
            Assert.AreEqual(47, b.N);
            Assert.AreEqual(3f, b.ScratchMultiplier, 0.0001f);
            Assert.AreEqual(0.75f, b.AbilityMultiplier, 0.0001f);
            Assert.AreEqual(2.25f, b.M, 0.0001f);
            Assert.IsFalse(b.Blocked);
            Assert.AreEqual(106, b.Final);
            Assert.AreEqual(result, b.Final);
            Assert.AreEqual(PlayerComboDamage.RoundNxM(b.N, b.M), b.Final);
            Assert.AreSame(dice, b.Dice, "Los dados contribuyentes son pass-through, no copia.");
        }

        [Test]
        public void Resolve_Blocked_PopulatesTermsButFinalZero()
        {
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { BonusComboDamage = 4, BlockComboDamage = true }
            }, ServiceScope.Global);

            int result = PlayerComboDamage.Resolve(_player, 10, null, 1f,
                PlayerComboFormulaKind.Damage, out var b);

            Assert.AreEqual(0, result);
            Assert.IsTrue(b.Blocked);
            Assert.AreEqual(0, b.Final);
            // Los términos siguen desglosados — la UI puede mostrar QUÉ se bloqueó.
            Assert.AreEqual(10, b.ComboBase);
            Assert.AreEqual(4, b.AdditiveBonus);
            Assert.AreEqual(14, b.N);
        }

        [Test]
        public void Resolve_SimpleOverload_MatchesOutOverload()
        {
            RegisterPlayerAttackWithFlatBonus(5, 3);
            RegisterScratches();
            var dice = new[] { new ContributingDie(0, 6, DiceType.D6) };

            int simple = PlayerComboDamage.Resolve(_player, 10, dice, 0.75f);
            int detailed = PlayerComboDamage.Resolve(_player, 10, dice, 0.75f,
                PlayerComboFormulaKind.Damage, out _);

            Assert.AreEqual(simple, detailed);
        }

        [Test]
        public void Resolve_IsPure_SameBreakdownOnRepeatedCalls()
        {
            // La captura de atribución vive en el dispatch de los hooks, NUNCA en Resolve:
            // resolver dos veces debe dar exactamente el mismo desglose sin side-effects.
            RegisterPlayerAttackWithFlatBonus(5, 3);
            RegisterScratches();
            var dice = new[] { new ContributingDie(1, 12, DiceType.D20) };

            PlayerComboDamage.Resolve(_player, 10, dice, 1f, PlayerComboFormulaKind.Damage, out var first);
            PlayerComboDamage.Resolve(_player, 10, dice, 1f, PlayerComboFormulaKind.Damage, out var second);

            Assert.AreEqual(first.N, second.N);
            Assert.AreEqual(first.M, second.M, 0.0001f);
            Assert.AreEqual(first.Final, second.Final);
            Assert.AreEqual(first.AdditiveBonus, second.AdditiveBonus);
        }

        [Test]
        public void Resolve_OutBreakdown_ReportsScratchMultiplierBonus()
        {
            // Arrange — dos canales suman +2 y +1 al bono de M; ability 0.75
            ServiceLocator.AddService<IComboPassiveService>(new FakeComboPassiveService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 2f, ComboDamageMultiplier = 3f }
            }, ServiceScope.Global);
            ServiceLocator.AddService<IComboPlayService>(new FakeComboPlayService
            {
                Scratch = new EnchantmentScratch { ComboMultiplierBonus = 1f }
            }, ServiceScope.Global);

            // Act
            PlayerComboDamage.Resolve(_player, 10, null, 0.75f, PlayerComboFormulaKind.Damage, out var b);

            // Assert — M = (1 + 3) × 3 × 0.75 = 9
            Assert.AreEqual(3f, b.ScratchMultiplierBonus, 0.0001f);
            Assert.AreEqual(3f, b.ScratchMultiplier, 0.0001f);
            Assert.AreEqual(9f, b.M, 0.0001f);
            Assert.AreEqual(90, b.Final);
        }

        [Test]
        public void Resolve_Sources_CarriesMultiplierBonusDelta()
        {
            var playScratch = new EnchantmentScratch { ComboMultiplierBonus = 2f };
            playScratch.RecordContribution(new ScratchContribution(
                ScratchSourceKind.Item, "piedra.angular", null, -1, 0, 1f, false, multiplierBonusDelta: 2f));
            ServiceLocator.AddService<IComboPlayService>(
                new FakeComboPlayService { Scratch = playScratch }, ServiceScope.Global);

            PlayerComboDamage.Resolve(_player, 10, null, 1f, PlayerComboFormulaKind.Damage, out var b);

            Assert.AreEqual(1, b.Sources.Count);
            Assert.AreEqual(2f, b.Sources[0].MultiplierBonusDelta, 0.0001f);
            Assert.AreEqual(30, b.Final);
        }

        [Test]
        public void Resolve_Sources_AggregatesJournals_PassivesThenPlay()
        {
            // Arrange — cada canal trae su journal ya capturado por el dispatch
            var passiveScratch = new EnchantmentScratch { BonusComboDamage = 4 };
            passiveScratch.RecordContribution(new ScratchContribution(
                ScratchSourceKind.ComboPassive, "pasiva.trio", null, -1, 4, 1f, false));
            var playScratch = new EnchantmentScratch { BonusComboDamage = 3 };
            playScratch.RecordContribution(new ScratchContribution(
                ScratchSourceKind.Item, "item.ritual", null, -1, 3, 1f, false));
            ServiceLocator.AddService<IComboPassiveService>(
                new FakeComboPassiveService { Scratch = passiveScratch }, ServiceScope.Global);
            ServiceLocator.AddService<IComboPlayService>(
                new FakeComboPlayService { Scratch = playScratch }, ServiceScope.Global);

            // Act
            PlayerComboDamage.Resolve(_player, 10, null, 1f, PlayerComboFormulaKind.Damage, out var b);

            // Assert — orden de agregación de la fórmula: pasivas → (enchants) → play
            Assert.AreEqual(2, b.Sources.Count);
            Assert.AreEqual("pasiva.trio", b.Sources[0].SourceId);
            Assert.AreEqual("item.ritual", b.Sources[1].SourceId);

            // Resolve es lectura pura: repetir NO duplica la atribución.
            PlayerComboDamage.Resolve(_player, 10, null, 1f, PlayerComboFormulaKind.Damage, out var again);
            Assert.AreEqual(2, again.Sources.Count);
        }

        [Test]
        public void Resolve_Sources_NullWhenNoJournalEntries()
        {
            RegisterScratches(); // scratches con valores pero SIN journal capturado

            PlayerComboDamage.Resolve(_player, 10, null, 1f, PlayerComboFormulaKind.Damage, out var b);

            Assert.IsNull(b.Sources);
        }

        [Test]
        public void ShieldResolve_BaseZeroGate_ReturnsEmptyShieldBreakdown()
        {
            RegisterPlayerAttackWithFlatBonus(5, 3);

            int result = PlayerComboShield.Resolve(_player, 0,
                new[] { new ContributingDie(0, 20, DiceType.D20) }, 1f, out var b);

            Assert.AreEqual(0, result);
            Assert.AreEqual(PlayerComboFormulaKind.Shield, b.Kind);
            Assert.AreEqual(0, b.Final);
            Assert.AreEqual(0, b.N, "El gate corta ANTES de la fórmula: sin términos.");
        }

        [Test]
        public void ShieldResolve_OutOverload_SharesDamageFormula()
        {
            RegisterPlayerAttackWithFlatBonus(5, 2);
            var dice = new[] { new ContributingDie(0, 4, DiceType.D6) };

            PlayerComboShield.Resolve(_player, 7, dice, 0.75f, out var shield);
            PlayerComboDamage.Resolve(_player, 7, dice, 0.75f, PlayerComboFormulaKind.Damage, out var damage);

            Assert.AreEqual(PlayerComboFormulaKind.Shield, shield.Kind);
            Assert.AreEqual(damage.N, shield.N);
            Assert.AreEqual(damage.M, shield.M, 0.0001f);
            Assert.AreEqual(damage.Final, shield.Final);
        }

        // Fakes mínimos (mismo criterio que PlayerComboDamageTests).
        private sealed class FakeComboPassiveService : IComboPassiveService
        {
            public EnchantmentScratch Scratch;
            public bool IsReady => true;
            public IReadOnlyList<ComboPassiveSO> GetPassivesFor(string comboId) => Array.Empty<ComboPassiveSO>();
            public void Apply(ComboPassiveSO passive) { }
            public int GetBonusDamage(string comboId) => 0;
            public EnchantmentScratch LastComboScratch => Scratch;
        }

        private sealed class FakeComboPlayService : IComboPlayService
        {
            public EnchantmentScratch Scratch;
            public EnchantmentScratch CurrentPlayScratch => Scratch;
            public EnchantmentScratch LastPlayScratch => Scratch;
            public bool IsPlayWindowOpen => Scratch != null;
            public string CurrentComboId => null;
            public void BeginPlay(EffectContext effCtx) { }
            public void EndPlay() { }
        }
    }
}
