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
            Assert.AreEqual(15, _attrs.GetAttribute<Health>(_player).ModifiedValue);
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
