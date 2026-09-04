using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Regresión del playtest 2026-09-04 sobre "Fuente Mágica" (el dado más alto del combo
    /// suma su cara al multiplicador, NO al daño base): el dado seguía sumado en N (Σ caras)
    /// además de ir a M, y el item disparaba en Número Mayor (<c>combo.higher_number</c>),
    /// donde ese dado ES el combo entero. Replica la autoría del asset: hook ComboPlayed +
    /// filtro Exclude + <see cref="EffAddComboMultiplier"/> y <see cref="EffAddComboBonus"/>
    /// con <c>Subtract</c>, ambos leyendo <see cref="ReadHighestContributingDie"/>.
    /// </summary>
    [TestFixture]
    public class FuenteMagicaHookTests
    {
        private InventoryService _service;
        private ComboPlayService _play;
        private Guid _playerGuid;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(_playerGuid));
            _service = new InventoryService(null, 4);
            _play = new ComboPlayService();
            _play.Register();
        }

        [TearDown]
        public void TearDown()
        {
            _play?.Dispose();
            _play = null;
            _service?.Dispose();
            _service = null;

            foreach (var o in _created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _created.Clear();

            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private ItemSO NewFuenteMagica()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "fuente.magica";
            item.DisplayName = "Fuente Mágica";
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = new ComboFilter
                {
                    Mode = ComboFilterMode.ExcludeComboIds,
                    ComboIds = new List<string> { "combo.higher_number" },
                },
                ActionKindFilter = RollActionKind.Attack,
            };
            hook.Effect.Effects.Add(new EffAddComboMultiplier
            {
                AmountReader = new ReadHighestContributingDie(),
                ReaderScale = 1f,
            });
            hook.Effect.Effects.Add(new EffAddComboBonus
            {
                Amount = new ReadHighestContributingDie(),
                Subtract = true,
            });
            item.PassiveHooks.Add(hook);

            _created.Add(item);
            return item;
        }

        private EnchantmentScratch PlayCombo(string comboId, int[] dice, int[] contributingIndices)
        {
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                ActionKind = RollActionKind.Attack,
                DiceResult = dice,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10,
                    countUsed: contributingIndices.Length, contributingIndices: contributingIndices),
            });
            var scratch = _play.CurrentPlayScratch;
            _play.EndPlay();
            return scratch;
        }

        // ================================================================
        // Tests
        // ================================================================

        [Test]
        public void test_fuente_magica_trio_moves_highest_die_from_base_to_multiplier()
        {
            // Arrange
            _service.AddItem(NewFuenteMagica());

            // Act — trío 4-4-6: el 6 es el dado más alto del combo.
            var scratch = PlayCombo("combo.trio", new[] { 4, 4, 6 }, new[] { 0, 1, 2 });

            // Assert — +6 a M y −6 a N (la cara ya está en Σcaras, se descuenta una vez).
            Assert.IsNotNull(scratch);
            Assert.AreEqual(6f, scratch.ComboMultiplierBonus, 0.0001f);
            Assert.AreEqual(-6, scratch.BonusComboDamage);
        }

        [Test]
        public void test_fuente_magica_higher_number_does_not_trigger()
        {
            // Arrange
            _service.AddItem(NewFuenteMagica());

            // Act — Número Mayor: el combo es solo el dado más alto.
            var scratch = PlayCombo("combo.higher_number", new[] { 2, 3, 6 }, new[] { 2 });

            // Assert — el filtro Exclude corta el hook: scratch intacto.
            Assert.IsNotNull(scratch);
            Assert.AreEqual(0f, scratch.ComboMultiplierBonus, 0.0001f);
            Assert.AreEqual(0, scratch.BonusComboDamage);
        }

        [Test]
        public void test_fuente_magica_formula_counts_highest_die_once_and_only_in_m()
        {
            // Arrange — sin AttributesManager: dmg_base_PJ y bonos_PJ valen 0.
            _service.AddItem(NewFuenteMagica());
            PlayCombo("combo.trio", new[] { 4, 4, 6 }, new[] { 0, 1, 2 });
            var contributing = new[]
            {
                new ContributingDie(0, 4, DiceType.D6),
                new ContributingDie(1, 4, DiceType.D6),
                new ContributingDie(2, 6, DiceType.D6),
            };

            // Act — la fórmula lee LastPlayScratch (persiste tras EndPlay).
            int total = PlayerComboDamage.Resolve(_playerGuid, comboBaseDamage: 10, contributing,
                abilityMultiplier: 1f, PlayerComboFormulaKind.Damage, out var breakdown);

            // Assert — N = 10 + (4+4+6) − 6 = 18; M = 1 + 6 = 7; 18 × 7 = 126.
            Assert.AreEqual(14, breakdown.FacesSum);
            Assert.AreEqual(-6, breakdown.AdditiveBonus);
            Assert.AreEqual(18f, breakdown.N, 0.0001f);
            Assert.AreEqual(7f, breakdown.M, 0.0001f);
            Assert.AreEqual(126, total);
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public StubPlayerService(Guid guid) { PlayerGuid = guid; }
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }
    }
}
