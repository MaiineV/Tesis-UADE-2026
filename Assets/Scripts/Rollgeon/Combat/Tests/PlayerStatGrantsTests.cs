using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades;
using Rollgeon.Upgrades.Character;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests del canal único <see cref="PlayerStatGrants"/>: aplica boosts permanentes de stat al
    /// jugador (Attack = daño base del PJ) vía Modifier Add/Run/Intrinsic. Compartido por rewards
    /// de personaje y pasivas/ítems de tienda.
    /// </summary>
    [TestFixture]
    public class PlayerStatGrantsTests
    {
        private AttributesManager _attrs;
        private FakePlayerService _ps;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _player = Guid.NewGuid();

            _attrs = new AttributesManager();
            var a = new ModifiableAttributes();
            a.SetAttribute<Attack>(new Attack(0));
            a.SetAttribute<Health>(new Health(10));
            // BUG-022: los grants de Health rutean al stat de máximo.
            a.SetAttribute<MaxHealth>(new MaxHealth(10));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _ps = new FakePlayerService { PlayerGuid = _player };
            ServiceLocator.AddService<IPlayerService>(_ps, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            _attrs?.Dispose();
        }

        [Test]
        public void Apply_Attack_AddsToModifiedValue()
        {
            bool ok = PlayerStatGrants.Apply(CharacterRewardTargetStat.Attack, 3);

            Assert.IsTrue(ok);
            Assert.AreEqual(3, _attrs.GetAttribute<Attack>(_player).ModifiedValue);
        }

        [Test]
        public void Apply_AmountZero_IsNoop()
        {
            Assert.IsFalse(PlayerStatGrants.Apply(CharacterRewardTargetStat.Attack, 0));
            Assert.AreEqual(0, _attrs.GetAttribute<Attack>(_player).ModifiedValue);
        }

        [Test]
        public void Apply_StatNotRegisteredOnPlayer_ReturnsFalse()
        {
            // El player de este test no tiene Speed registrado → AddModifier falla.
            Assert.IsFalse(PlayerStatGrants.Apply(CharacterRewardTargetStat.Speed, 5));
        }

        [Test]
        public void Apply_List_AppliesEachGrant()
        {
            var grants = new List<StatGrant>
            {
                new StatGrant { Stat = CharacterRewardTargetStat.Attack, Amount = 2 },
                new StatGrant { Stat = CharacterRewardTargetStat.Health, Amount = 5 },
            };

            int applied = PlayerStatGrants.Apply(grants);

            Assert.AreEqual(2, applied);
            Assert.AreEqual(2, _attrs.GetAttribute<Attack>(_player).ModifiedValue);
            // BUG-022: el grant de Health sube el MÁXIMO, no el stack de Health (que la
            // IA lee como HP actual vía ModifiedValue).
            Assert.AreEqual(15, _attrs.GetAttribute<MaxHealth>(_player).ModifiedValue);
        }

        // ================================================================
        // Regression BUG-022 — "ningún ítem de recompensa de jefe funciona"
        // ================================================================

        [Test]
        public void Apply_Health_RoutesToMaxHealth_AndHealsSameAmount()
        {
            // Arrange — HP 10/10.

            // Act
            bool ok = PlayerStatGrants.Apply(CharacterRewardTargetStat.Health, 5);

            // Assert — max 15 y el heal-on-gain llevó el HP actual a 15; el stack de
            // Health quedó sin modifiers (ModifiedValue == Value).
            Assert.IsTrue(ok);
            Assert.AreEqual(15, PlayerMaxHp.Resolve(_player));
            Assert.AreEqual(15, _attrs.GetAttribute<Health>(_player).Value);
            Assert.AreEqual(_attrs.GetAttribute<Health>(_player).Value,
                            _attrs.GetAttribute<Health>(_player).ModifiedValue);
        }

        [Test]
        public void Apply_Health_HealOnGain_ClampsToNewMax()
        {
            // Arrange — HP actual 8 de 10: +5 de max cura 5 y clampea en 13 (< nuevo max 15).
            _attrs.SetAttributeValue<Health, int>(_player, 8);

            // Act
            PlayerStatGrants.Apply(CharacterRewardTargetStat.Health, 5);

            // Assert
            Assert.AreEqual(15, PlayerMaxHp.Resolve(_player));
            Assert.AreEqual(13, _attrs.GetAttribute<Health>(_player).Value);
        }

        [Test]
        public void Apply_RollRegen_RaisesPerTurnGrant()
        {
            // Feature#0050: el reward ex "Energía +1" suma +1 al grant por turno
            // del Pool de Rolls (via IRollPoolService, no como modifier de atributo).
            var pool = new Rollgeon.Combat.Rolls.RollPoolService();
            var ruleset = UnityEngine.ScriptableObject.CreateInstance<Rollgeon.Balance.RulesetSO>();
            try
            {
                pool.ConfigureForTests(ruleset);
                ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(pool);

                bool ok = PlayerStatGrants.Apply(CharacterRewardTargetStat.RollRegen, 1);

                Assert.IsTrue(ok);
                Assert.AreEqual(6, pool.GetRollsPerTurn(_player),
                    "El grant por turno debe pasar de 5 (ruleset) a 6.");
            }
            finally
            {
                pool.Dispose();
                UnityEngine.Object.DestroyImmediate(ruleset);
            }
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
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
