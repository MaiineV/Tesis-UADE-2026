using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcAllyAliveExistsTests
    {
        private AttributesManager _manager;
        private Guid _ownerId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _manager = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_manager);
            _ownerId = Guid.NewGuid();

            var oAttrs = new ModifiableAttributes();
            oAttrs.EnsureInitialized();
            oAttrs.SetAttribute<Health>(new Health(20));
            _manager.Register(_ownerId, oAttrs);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static PreConditionContext Ctx(Guid owner) =>
            new PreConditionContext { OwnerGuid = owner };

        private void RegisterEntity(Guid id, int hp)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _manager.Register(id, attrs);
        }

        [Test]
        public void Evaluate_OnlyOwner_ReturnsFalse()
        {
            // Sin IEntityQueryService el fallback escanea AttributesManager;
            // sólo el owner está registrado → no hay aliado.
            Assert.IsFalse(new PcAllyAliveExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OneAliveEntity_ReturnsTrue()
        {
            RegisterEntity(Guid.NewGuid(), 10);
            Assert.IsTrue(new PcAllyAliveExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_AllAlliesDead_ReturnsFalse()
        {
            RegisterEntity(Guid.NewGuid(), 0);
            RegisterEntity(Guid.NewGuid(), 0);
            Assert.IsFalse(new PcAllyAliveExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoAttributesManager_ReturnsFalse()
        {
            ServiceLocator.Clear();
            Assert.IsFalse(new PcAllyAliveExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_EmptyOwner_ReturnsFalse()
        {
            Assert.IsFalse(new PcAllyAliveExists().Evaluate(new PreConditionContext()));
        }
    }
}
