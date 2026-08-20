using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Traits;

namespace Rollgeon.Entities.Tests
{
    /// <summary>
    /// Tests de <see cref="UnitTraitService"/>: default seguro para guids desconocidos,
    /// registro/overwrite/unregister, y limpieza por OnRunEnd.
    /// </summary>
    [TestFixture]
    public class UnitTraitServiceTests
    {
        private UnitTraitService _svc;
        private Guid _entity;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _entity = Guid.NewGuid();
            _svc = new UnitTraitService();
            _svc.Register();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void Get_UnknownGuid_ReturnsDefaultGroundTraits()
        {
            var traits = _svc.Get(Guid.NewGuid());

            Assert.IsFalse(traits.IsFlying);
            Assert.IsFalse(traits.IsBoss);
            Assert.AreEqual(AIPersonality.Normal, traits.Personality);
            Assert.IsFalse(traits.KamikazeIgnoresSurvival);
        }

        [Test]
        public void TryGet_UnknownGuid_ReturnsFalseWithDefault()
        {
            bool found = _svc.TryGet(Guid.NewGuid(), out var traits);

            Assert.IsFalse(found);
            Assert.IsFalse(traits.IsFlying);
        }

        [Test]
        public void Register_ThenGet_ReturnsRegisteredTraits()
        {
            _svc.Register(_entity, new UnitTraits(isFlying: true, isBoss: true,
                AIPersonality.Kamikaze, kamikazeIgnoresSurvival: true));

            var traits = _svc.Get(_entity);

            Assert.IsTrue(traits.IsFlying);
            Assert.IsTrue(traits.IsBoss);
            Assert.AreEqual(AIPersonality.Kamikaze, traits.Personality);
            Assert.IsTrue(traits.KamikazeIgnoresSurvival);
        }

        [Test]
        public void Register_SameGuidTwice_OverwritesTraits()
        {
            _svc.Register(_entity, new UnitTraits(isFlying: true, isBoss: false));

            _svc.Register(_entity, new UnitTraits(isFlying: false, isBoss: true, AIPersonality.Aggressive));

            var traits = _svc.Get(_entity);
            Assert.IsFalse(traits.IsFlying);
            Assert.IsTrue(traits.IsBoss);
            Assert.AreEqual(AIPersonality.Aggressive, traits.Personality);
        }

        [Test]
        public void Unregister_KnownGuid_FallsBackToDefault()
        {
            _svc.Register(_entity, new UnitTraits(isFlying: true, isBoss: false));

            _svc.Unregister(_entity);

            Assert.IsFalse(_svc.TryGet(_entity, out _));
        }

        [Test]
        public void Register_EmptyGuid_IsIgnored()
        {
            _svc.Register(Guid.Empty, new UnitTraits(isFlying: true, isBoss: true));

            Assert.IsFalse(_svc.TryGet(Guid.Empty, out _));
        }

        [Test]
        public void OnRunEnd_ClearsAllTraits()
        {
            _svc.Register(_entity, new UnitTraits(isFlying: true, isBoss: false));

            EventManager.Trigger(EventName.OnRunEnd);

            Assert.IsFalse(_svc.TryGet(_entity, out _),
                "OnRunEnd limpia el registro entero — la próxima run arranca sin traits huérfanos.");
        }

        [Test]
        public void OnCombatEnd_DoesNotClearTraits()
        {
            _svc.Register(_entity, new UnitTraits(isFlying: true, isBoss: false));

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsTrue(_svc.TryGet(_entity, out _),
                "Los traits sobreviven al combate: el player se registra una sola vez por run.");
        }
    }
}
