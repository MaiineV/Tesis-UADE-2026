using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcAllyBelowMaxExistsTests
    {
        private AttributesManager _manager;
        private EnemyAIRegistry _aiRegistry;
        private Guid _ownerId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _manager = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_manager);

            _aiRegistry = new EnemyAIRegistry();
            ServiceLocator.AddService<IEnemyAIRegistry>(_aiRegistry);

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

        // Sin IEntityQueryService la PC cae al scan de AttributesManager.
        private Guid RegisterAlly(int hp, int maxHp)
        {
            var id = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _manager.Register(id, attrs);
            _aiRegistry.Register(id, root: null, maxHp: maxHp);
            return id;
        }

        [Test]
        public void Evaluate_OnlyOwner_ReturnsFalse()
        {
            Assert.IsFalse(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_WoundedAlly_ReturnsTrue()
        {
            RegisterAlly(hp: 5, maxHp: 10);
            Assert.IsTrue(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_AllyAtFullHp_ReturnsFalse()
        {
            RegisterAlly(hp: 10, maxHp: 10);
            Assert.IsFalse(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_DeadAlly_ReturnsFalse()
        {
            RegisterAlly(hp: 0, maxHp: 10);
            Assert.IsFalse(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_WoundedAllyWithoutMaxHp_ReturnsFalse()
        {
            // Aliado herido pero sin entry de max HP → conservador: no lo contamos.
            var id = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(5));
            _manager.Register(id, attrs);
            // Deliberadamente NO registramos maxHp en el aiRegistry.

            Assert.IsFalse(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_MixedAllies_ReturnsTrue_WhenAnyWounded()
        {
            RegisterAlly(hp: 10, maxHp: 10); // full
            RegisterAlly(hp: 0, maxHp: 10);  // dead
            RegisterAlly(hp: 3, maxHp: 10);  // wounded
            Assert.IsTrue(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoAttributesManager_ReturnsFalse()
        {
            ServiceLocator.Clear();
            Assert.IsFalse(new PcAllyBelowMaxExists().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_EmptyOwner_ReturnsFalse()
        {
            Assert.IsFalse(new PcAllyBelowMaxExists().Evaluate(new PreConditionContext()));
        }
    }
}
