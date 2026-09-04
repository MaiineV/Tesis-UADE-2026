using System;
using Rollgeon.Effects.Selection;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Chests;
using Rollgeon.Combat.Rooms;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>
    /// <see cref="PcEligibleEnemyExists"/> (Feature#0084, Blood Transfusion): gate sobre
    /// <c>CombatantQuery.LiveEnemiesOf</c> + <c>IsEligibleForBlood</c>. Mismo fixture que
    /// <c>CombatantQueryTests</c>.
    /// </summary>
    [TestFixture]
    public class PcEligibleEnemyExistsTests
    {
        private sealed class FakeEntityQuery : Rollgeon.Entities.IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private FakeEntityQuery _query;
        private AttributesManager _attrs;
        private GridManager _grid;
        private UnitTraitService _traits;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<Rollgeon.Entities.IEntityQueryService>(_query, ServiceScope.Global);

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(8, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _traits = new UnitTraitService();
            _traits.Register();

            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            _traits?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Guid RegisterAlive(GridCoord coord, bool bloodless = false)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            _attrs.Register(guid, attrs);
            _grid.Register(guid, coord);
            _query.Enemies.Add(new Entity { Guid = guid });
            if (bloodless) _traits.Register(guid, new UnitTraits(isFlying: false, isBoss: false, bloodless: true));
            return guid;
        }

        [Test]
        public void test_evaluate_ownerGuidEmpty_returnsFalse()
        {
            var pc = new PcEligibleEnemyExists();

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = Guid.Empty });

            Assert.IsFalse(result);
        }

        [Test]
        public void test_evaluate_noEnemies_returnsFalse()
        {
            var pc = new PcEligibleEnemyExists();

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _player });

            Assert.IsFalse(result);
        }

        [Test]
        public void test_evaluate_eligibleEnemyExists_returnsTrue()
        {
            RegisterAlive(new GridCoord(1, 0));
            var pc = new PcEligibleEnemyExists { ExcludeBloodless = true };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _player });

            Assert.IsTrue(result);
        }

        [Test]
        public void test_evaluate_onlyBloodlessEnemy_excludeBloodlessTrue_returnsFalse()
        {
            RegisterAlive(new GridCoord(1, 0), bloodless: true);
            var pc = new PcEligibleEnemyExists { ExcludeBloodless = true };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _player });

            Assert.IsFalse(result);
        }

        [Test]
        public void test_evaluate_onlyBloodlessEnemy_excludeBloodlessFalse_returnsTrue()
        {
            RegisterAlive(new GridCoord(1, 0), bloodless: true);
            var pc = new PcEligibleEnemyExists { ExcludeBloodless = false };

            var result = pc.Evaluate(new PreConditionContext { OwnerGuid = _player });

            Assert.IsTrue(result);
        }
    }
}
