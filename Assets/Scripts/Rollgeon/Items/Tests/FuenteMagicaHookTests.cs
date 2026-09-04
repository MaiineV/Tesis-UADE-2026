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
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Regresión de los playtests 2026-09-04 sobre "Fuente Mágica" (el dado más alto del combo
    /// suma su cara al multiplicador, NO al daño base): primero el dado seguía sumado en N
    /// además de ir a M y el item disparaba en Número Mayor (<c>combo.higher_number</c>);
    /// después el número cerraba pero el desglose mostraba el dado entrando a N y un "−X"
    /// del item — el jugador leía que contaba en los dos. Replica la autoría del asset: hook
    /// ComboPlayed + filtro Exclude + <see cref="EffMoveDieToMultiplier"/>, y verifica que la
    /// fórmula saque la cara de Σcaras y la ponga en M con el dado atribuido al item.
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
            hook.Effect.Effects.Add(new EffMoveDieToMultiplier { Pick = ContributingDiePick.Highest });
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
        public void test_fuente_magica_trio_marks_highest_die_to_move_to_multiplier()
        {
            // Arrange
            _service.AddItem(NewFuenteMagica());

            // Act — trío 4-4-6: el 6 (slot 2, mapeo identidad) es el dado más alto del combo.
            var scratch = PlayCombo("combo.trio", new[] { 4, 4, 6 }, new[] { 0, 1, 2 });

            // Assert — solo se marca el slot: la cara la mueve la fórmula, no el efecto.
            Assert.IsNotNull(scratch);
            CollectionAssert.AreEqual(new[] { 2 }, scratch.DiceMovedToMultiplier);
            Assert.AreEqual(0, scratch.BonusComboDamage);
            Assert.AreEqual(0f, scratch.ComboMultiplierBonus, 0.0001f);
        }

        [Test]
        public void test_fuente_magica_journals_the_moved_die_with_the_item_asset()
        {
            // Arrange
            var item = NewFuenteMagica();
            _service.AddItem(item);

            // Act
            var scratch = PlayCombo("combo.trio", new[] { 4, 4, 6 }, new[] { 0, 1, 2 });

            // Assert — la entrada neutra del journal lleva el ItemSO: el desglose le pone el
            // icono al dado que vuela a M.
            Assert.IsNotNull(scratch.Journal);
            Assert.AreEqual(1, scratch.Journal.Count);
            var entry = scratch.Journal[0];
            Assert.AreEqual(ScratchSourceKind.Item, entry.Kind);
            Assert.AreEqual("fuente.magica", entry.SourceId);
            Assert.AreSame(item, entry.SourceAsset);
            Assert.AreEqual(2, entry.MovedDieBagSlot);
            Assert.AreEqual(0, entry.BonusDelta);
            Assert.AreEqual(0f, entry.MultiplierBonusDelta, 0.0001f);
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
            Assert.IsNull(scratch.DiceMovedToMultiplier);
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

            // Assert — N = 10 + (4+4) = 18 (el 6 nunca entra a N); M = 1 + 6 = 7; 18 × 7 = 126.
            Assert.AreEqual(8, breakdown.FacesSum);
            Assert.AreEqual(6, breakdown.MovedFacesSum);
            CollectionAssert.AreEqual(new[] { 2 }, breakdown.DiceMovedToMultiplier);
            Assert.AreEqual(0, breakdown.AdditiveBonus);
            Assert.AreEqual(18f, breakdown.N, 0.0001f);
            Assert.AreEqual(6f, breakdown.ScratchMultiplierBonus, 0.0001f);
            Assert.AreEqual(7f, breakdown.M, 0.0001f);
            Assert.AreEqual(126, total);
        }

        [Test]
        public void test_fuente_magica_moves_the_real_bag_slot_when_holds_are_a_subset()
        {
            // Arrange — bolsa 1-4-2-4-6, holdeados los slots 1, 3 y 4 (4-4-6): el 6 es el slot 4.
            _service.AddItem(NewFuenteMagica());

            // Act
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                ActionKind = RollActionKind.Attack,
                DiceResult = new[] { 1, 4, 2, 4, 6 },
                KeptDice = new[] { 4, 4, 6 },
                KeptDiceOriginalIndices = new[] { 1, 3, 4 },
                ComboResult = ComboDetectionResult.Match("combo.trio", 10, 3, new[] { 0, 1, 2 }),
            });
            var scratch = _play.CurrentPlayScratch;
            _play.EndPlay();

            // Assert
            CollectionAssert.AreEqual(new[] { 4 }, scratch.DiceMovedToMultiplier);
        }

        [Test]
        public void test_fuente_magica_does_not_fire_for_heal_combos()
        {
            // Arrange — ActionKindFilter = Attack: Curarse con trío no mueve nada.
            _service.AddItem(NewFuenteMagica());

            // Act
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                ActionKind = RollActionKind.Heal,
                DiceResult = new[] { 4, 4, 6 },
                ComboResult = ComboDetectionResult.Match("combo.trio", 10, 3, new[] { 0, 1, 2 }),
            });
            var scratch = _play.CurrentPlayScratch;
            _play.EndPlay();

            // Assert
            Assert.IsNull(scratch.DiceMovedToMultiplier);
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
