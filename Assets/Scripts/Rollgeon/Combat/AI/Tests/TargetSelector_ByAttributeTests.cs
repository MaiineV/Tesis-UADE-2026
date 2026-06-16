using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del selector <see cref="TargetSelector_ByAttribute"/>: filtrado por relacion,
    /// argmin/argmax por stat, skip de dead, tiebreaker por segunda stat, determinismo por
    /// <see cref="Guid.CompareTo"/> y ausencia de servicios.
    /// </summary>
    [TestFixture]
    public class TargetSelector_ByAttributeTests
    {
        private AttributesManager _attrs;
        private RelationshipQueryStub _query;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _query = new RelationshipQueryStub();
            ServiceLocator.AddService<AttributesManager>(_attrs);
            ServiceLocator.AddService<IEntityQueryService>(_query);

            _owner = Guid.NewGuid();
            var ownerAttrs = new ModifiableAttributes();
            ownerAttrs.EnsureInitialized();
            ownerAttrs.SetAttribute<Health>(new Health(50));
            _attrs.Register(_owner, ownerAttrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
        }

        // ----- helpers ---------------------------------------------------

        private Guid Spawn(EntityFilterMask relationToOwner, params (Type stat, int value)[] stats)
        {
            var guid = Guid.NewGuid();
            var ma = new ModifiableAttributes();
            ma.EnsureInitialized();
            foreach (var (statType, val) in stats)
            {
                if (statType == typeof(Health)) ma.SetAttribute<Health>(new Health(val));
                else if (statType == typeof(Attack)) ma.SetAttribute<Attack>(new Attack(val));
                else if (statType == typeof(Speed)) ma.SetAttribute<Speed>(new Speed(val));
                else if (statType == typeof(Energy)) ma.SetAttribute<Energy>(new Energy(val));
                else if (statType == typeof(Shield)) ma.SetAttribute<Shield>(new Shield(val));
                else throw new InvalidOperationException($"Unsupported stat in test: {statType}");
            }
            _attrs.Register(guid, ma);
            _query.SetRelationship(_owner, guid, relationToOwner);
            return guid;
        }

        private AIContext BuildContext()
        {
            return new AIContext { SelfGuid = _owner, Attributes = _attrs };
        }

        // ----- tests -----------------------------------------------------

        [Test]
        public void PickTarget_NoCandidates_ReturnsEmpty()
        {
            // Arrange — solo el owner registrado, ningun candidato cumple Relation.
            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(Guid.Empty, pick);
        }

        [Test]
        public void PickTarget_LowestHpAlly_PicksWoundedAlly()
        {
            // Arrange
            var fullAlly    = Spawn(EntityFilterMask.Allies, (typeof(Health), 20));
            var woundedAlly = Spawn(EntityFilterMask.Allies, (typeof(Health), 5));
            Spawn(EntityFilterMask.Enemies, (typeof(Health), 1)); // distractor (enemy, no debe contarse)

            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(woundedAlly, pick, "Esperado el aliado con menor HP, no el de full life ni el enemy.");
            Assert.AreNotEqual(fullAlly, pick);
        }

        [Test]
        public void PickTarget_HighestAttackEnemy_PicksStrongestEnemy()
        {
            // Arrange
            var weakEnemy   = Spawn(EntityFilterMask.Enemies, (typeof(Health), 10), (typeof(Attack), 2));
            var strongEnemy = Spawn(EntityFilterMask.Enemies, (typeof(Health), 10), (typeof(Attack), 9));
            Spawn(EntityFilterMask.Allies,  (typeof(Health), 10), (typeof(Attack), 99)); // distractor (ally)

            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Enemies,
                Stat = StatType.Attack,
                Mode = ExtremumMode.Highest,
            };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(strongEnemy, pick);
            Assert.AreNotEqual(weakEnemy, pick);
        }

        [Test]
        public void PickTarget_SkipDead_IgnoresZeroHpCandidates()
        {
            // Arrange — el "mas bajo" aparente es el muerto; SkipDead lo descarta.
            var deadAlly   = Spawn(EntityFilterMask.Allies, (typeof(Health), 0));
            var lowestLive = Spawn(EntityFilterMask.Allies, (typeof(Health), 3));

            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
                SkipDead = true,
            };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(lowestLive, pick);
            Assert.AreNotEqual(deadAlly, pick);
        }

        [Test]
        public void PickTarget_TiebreakerBySecondStat_ResolvesTie()
        {
            // Arrange — dos aliados con HP idéntico; desempata por Speed (Lowest).
            var fastWounded = Spawn(EntityFilterMask.Allies, (typeof(Health), 5), (typeof(Speed), 8));
            var slowWounded = Spawn(EntityFilterMask.Allies, (typeof(Health), 5), (typeof(Speed), 2));
            Spawn(EntityFilterMask.Allies, (typeof(Health), 20), (typeof(Speed), 1)); // distractor (HP alto)

            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
                UseTiebreaker = true,
                TiebreakStat = StatType.Speed,
                TiebreakMode = ExtremumMode.Lowest,
            };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(slowWounded, pick);
            Assert.AreNotEqual(fastWounded, pick);
        }

        [Test]
        public void PickTarget_FinalTiebreakerByGuid_IsDeterministic()
        {
            // Arrange — dos aliados con HP idéntico y sin tiebreaker; gana el de menor Guid.
            var a = Spawn(EntityFilterMask.Allies, (typeof(Health), 5));
            var b = Spawn(EntityFilterMask.Allies, (typeof(Health), 5));
            var lower = a.CompareTo(b) < 0 ? a : b;

            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
                UseTiebreaker = false,
            };

            // Act
            var pick1 = selector.PickTarget(BuildContext(), _owner);
            var pick2 = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(lower, pick1);
            Assert.AreEqual(pick1, pick2, "El selector debe ser deterministic across calls.");
        }

        [Test]
        public void PickTarget_OwnerInCandidates_AlwaysExcluded()
        {
            // Arrange — owner mismo aparece registrado y tendria el Health mas bajo.
            _attrs.GetAttribute<Health>(_owner).Value = 1;
            var ally = Spawn(EntityFilterMask.Allies, (typeof(Health), 10));
            // El owner se "auto-relaciona" como Allies en el stub — el selector debe ignorarlo igual.
            _query.SetRelationship(_owner, _owner, EntityFilterMask.Allies);

            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(ally, pick);
            Assert.AreNotEqual(_owner, pick);
        }

        [Test]
        public void PickTarget_RelationNone_ReturnsEmpty()
        {
            Spawn(EntityFilterMask.Allies, (typeof(Health), 5));
            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.None,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };

            var pick = selector.PickTarget(BuildContext(), _owner);

            Assert.AreEqual(Guid.Empty, pick);
        }

        [Test]
        public void PickTarget_ServiceMissing_ReturnsEmpty()
        {
            // Arrange — sin IEntityQueryService registrado, el selector se rinde graciosamente.
            ServiceLocator.RemoveService<IEntityQueryService>();
            Spawn(EntityFilterMask.Allies, (typeof(Health), 5));
            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };

            LogAssert.Expect(LogType.Warning, new Regex(".*IEntityQueryService not registered.*"));

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(Guid.Empty, pick);

            // Cleanup — re-register so TearDown.Clear() is a no-op-friendly state.
            ServiceLocator.AddService<IEntityQueryService>(_query);
        }

        [Test]
        public void PickTarget_NullContext_ReturnsEmpty()
        {
            var selector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };

            LogAssert.Expect(LogType.Warning, new Regex(".*AIContext.Attributes is null.*"));

            var pick = selector.PickTarget(null, _owner);

            Assert.AreEqual(Guid.Empty, pick);
        }

        // ----- in-memory stub --------------------------------------------

        private sealed class RelationshipQueryStub : IEntityQueryService
        {
            private readonly Dictionary<(Guid owner, Guid target), EntityFilterMask> _map
                = new Dictionary<(Guid, Guid), EntityFilterMask>();

            public void SetRelationship(Guid owner, Guid target, EntityFilterMask mask)
            {
                _map[(owner, target)] = mask;
            }

            public EntityFilterMask GetRelationship(Guid owner, Guid target)
            {
                return _map.TryGetValue((owner, target), out var m) ? m : EntityFilterMask.None;
            }

            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Array.Empty<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Array.Empty<Entity>();
        }
    }
}
