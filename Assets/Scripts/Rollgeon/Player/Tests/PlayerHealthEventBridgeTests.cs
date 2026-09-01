using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Heroes;

namespace Rollgeon.Player.Tests
{
    /// <summary>
    /// El bridge re-emite <c>OnPlayerHealthChanged [player, current, max]</c> desde los
    /// payloads tipados de daño/heal — SOLO para el jugador. Es lo que los item hooks
    /// tipo "al cruzar 30% HP" escuchan (el enum reservaba el evento pero nadie lo emitía).
    /// </summary>
    public sealed class PlayerHealthEventBridgeTests
    {
        PlayerHealthEventBridge _bridge;
        AttributesManager _attrMgr;
        Guid _player;
        Guid _enemy;
        int _fired;
        (Guid guid, int current, int max) _last;
        EventManager.EventReceiver _spy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));

            _attrMgr = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrMgr);
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Health>(new Health(37));
            attrs.SetAttribute<MaxHealth>(new MaxHealth(100));
            _attrMgr.Register(_player, attrs);

            _fired = 0;
            _spy = args =>
            {
                _fired++;
                _last = ((Guid)args[0], (int)args[1], (int)args[2]);
            };
            EventManager.Subscribe(EventName.OnPlayerHealthChanged, _spy);

            _bridge = new PlayerHealthEventBridge();
            _bridge.Register();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnPlayerHealthChanged, _spy);
            _bridge?.Dispose();
            _attrMgr?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void DamageOnPlayer_EmitsWithCurrentAndMax()
        {
            // Act
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _enemy,
                TargetGuid = _player,
                FinalDamage = 10,
            });

            // Assert — current lee el stat real (37), max el MaxHealth.ModifiedValue (100)
            Assert.AreEqual(1, _fired);
            Assert.AreEqual(_player, _last.guid);
            Assert.AreEqual(37, _last.current);
            Assert.AreEqual(100, _last.max);
        }

        [Test]
        public void HealOnPlayer_Emits()
        {
            TypedEvent<HealResolvedPayload>.Raise(new HealResolvedPayload
            {
                SourceGuid = _player,
                TargetGuid = _player,
                FinalHeal = 5,
            });

            Assert.AreEqual(1, _fired);
        }

        [Test]
        public void DamageOnEnemy_DoesNotEmit()
        {
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _player,
                TargetGuid = _enemy,
                FinalDamage = 10,
            });

            Assert.AreEqual(0, _fired, "el evento es del JUGADOR — la vida enemiga no pasa al bus");
        }

        [Test]
        public void Dispose_StopsEmitting()
        {
            _bridge.Dispose();

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                TargetGuid = _player,
                FinalDamage = 1,
            });

            Assert.AreEqual(0, _fired);
        }

        sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
