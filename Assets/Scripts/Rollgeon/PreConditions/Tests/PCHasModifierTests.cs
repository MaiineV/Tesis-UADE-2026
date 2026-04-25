using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PCHasModifierTests
    {
        private AttributesManager _manager;
        private Guid _ownerId;
        private TestEnergy _energy;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            OperationResolver.ClearCache();

            _manager = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_manager);

            _ownerId = Guid.NewGuid();
            _energy = new TestEnergy(0);
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestEnergy>(_energy);
            _manager.Register(_ownerId, attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Modifier<int> AddMod(Guid sourceId, ModifierDirection dir = ModifierDirection.Intrinsic)
        {
            var mod = new Modifier<int>(
                amount: 1,
                op: ModifierOperation.Add,
                duration: 0,
                carrierId: _ownerId,
                sourceId: sourceId,
                dir: dir,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnStarted);
            _energy.AddModifier<int>(mod);
            return mod;
        }

        private static PreConditionContext Ctx(Guid owner) => new PreConditionContext { OwnerGuid = owner };

        [Test]
        public void Evaluate_AnyModifierPresent_ReturnsTrue()
        {
            AddMod(Guid.Empty);
            var pc = new PCHasModifier { AttributeType = typeof(TestEnergy), MinCount = 1 };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoModifiers_ReturnsFalse()
        {
            var pc = new PCHasModifier { AttributeType = typeof(TestEnergy), MinCount = 1 };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_FilterBySourceId_OnlyMatchingCounts()
        {
            var sourceA = Guid.NewGuid();
            var sourceB = Guid.NewGuid();
            AddMod(sourceA);
            AddMod(sourceA);
            AddMod(sourceB);

            var matchA = new PCHasModifier
            {
                AttributeType = typeof(TestEnergy),
                SourceIdString = sourceA.ToString(),
                MinCount = 2,
            };
            Assert.IsTrue(matchA.Evaluate(Ctx(_ownerId)),
                "Deben matchear los 2 mods de sourceA.");

            var matchAStrict = new PCHasModifier
            {
                AttributeType = typeof(TestEnergy),
                SourceIdString = sourceA.ToString(),
                MinCount = 3,
            };
            Assert.IsFalse(matchAStrict.Evaluate(Ctx(_ownerId)),
                "Sólo hay 2 de sourceA — MinCount=3 falla.");

            var matchUnknown = new PCHasModifier
            {
                AttributeType = typeof(TestEnergy),
                SourceIdString = Guid.NewGuid().ToString(),
                MinCount = 1,
            };
            Assert.IsFalse(matchUnknown.Evaluate(Ctx(_ownerId)),
                "SourceId desconocido no debe matchear ningún mod.");
        }

        [Test]
        public void Evaluate_FilterByDirection_RespectsDirection()
        {
            AddMod(Guid.Empty, ModifierDirection.Intrinsic);
            AddMod(Guid.Empty, ModifierDirection.Outgoing);

            var intrinsic = new PCHasModifier
            {
                AttributeType = typeof(TestEnergy),
                FilterByDirection = true,
                Direction = ModifierDirection.Intrinsic,
                MinCount = 1,
            };
            Assert.IsTrue(intrinsic.Evaluate(Ctx(_ownerId)));

            var incoming = new PCHasModifier
            {
                AttributeType = typeof(TestEnergy),
                FilterByDirection = true,
                Direction = ModifierDirection.Incoming,
                MinCount = 1,
            };
            Assert.IsFalse(incoming.Evaluate(Ctx(_ownerId)),
                "No hay mods Incoming registrados.");
        }

        [Test]
        public void Evaluate_NullAttributeType_ReturnsFalse()
        {
            AddMod(Guid.Empty);
            var pc = new PCHasModifier { AttributeType = null };
            Assert.IsFalse(pc.Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OwnerNotRegistered_ReturnsFalse()
        {
            AddMod(Guid.Empty);
            var pc = new PCHasModifier { AttributeType = typeof(TestEnergy) };
            Assert.IsFalse(pc.Evaluate(Ctx(Guid.NewGuid())));
        }

        [Test]
        public void Evaluate_InvalidSourceIdString_FallsBackToAnySource()
        {
            // String inválido se trata como Guid.Empty (sin filtro).
            AddMod(Guid.NewGuid());
            var pc = new PCHasModifier
            {
                AttributeType = typeof(TestEnergy),
                SourceIdString = "not-a-guid",
                MinCount = 1,
            };
            Assert.IsTrue(pc.Evaluate(Ctx(_ownerId)));
        }
    }
}
