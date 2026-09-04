using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Effects.Concretes;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="EffPersistShield"/> (Feature#0084, Coin Shield banda par): delega en
    /// <see cref="IShieldPersistenceService.PersistThroughNextReset"/>; sin servicio, no-op.
    /// </summary>
    [TestFixture]
    public class EffPersistShieldTests
    {
        private sealed class FakeShieldPersistenceService : IShieldPersistenceService
        {
            public readonly List<Guid> PersistedCalls = new List<Guid>();
            private readonly HashSet<Guid> _persisted = new HashSet<Guid>();

            public void PersistThroughNextReset(Guid entity)
            {
                PersistedCalls.Add(entity);
                _persisted.Add(entity);
            }

            public bool TryConsume(Guid entity) => _persisted.Remove(entity);
            public bool IsPersisted(Guid entity) => _persisted.Contains(entity);
            public void ClearAll() => _persisted.Clear();
        }

        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void test_applyEffect_withService_persistsOwnerThroughNextReset()
        {
            var service = new FakeShieldPersistenceService();
            ServiceLocator.AddService<IShieldPersistenceService>(service, ServiceScope.Global);
            var effect = new EffPersistShield();

            var result = effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.IsTrue(result);
            CollectionAssert.AreEqual(new[] { _player }, service.PersistedCalls);
            Assert.IsTrue(service.IsPersisted(_player));
        }

        [Test]
        public void test_applyEffect_withoutService_returnsTrueNoOp()
        {
            var effect = new EffPersistShield();

            var result = effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.IsTrue(result, "nunca corta la cadena — el roll ya se pagó.");
        }

        [Test]
        public void test_applyEffect_nullContext_returnsFalse()
        {
            var effect = new EffPersistShield();

            Assert.IsFalse(effect.ApplyEffect(null));
        }
    }
}
