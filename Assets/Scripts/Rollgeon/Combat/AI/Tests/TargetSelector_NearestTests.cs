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
using Rollgeon.Grid;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del selector <see cref="TargetSelector_Nearest"/>: elección del más cercano por
    /// distancia Manhattan, filtrado por relación, skip de dead, skip de candidatos sin
    /// posición en grilla, determinismo por <see cref="Guid.CompareTo"/> y ausencia de servicios.
    /// </summary>
    [TestFixture]
    public class TargetSelector_NearestTests
    {
        private AttributesManager _attrs;
        private RelationshipQueryStub _query;
        private GridManager _grid;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _query = new RelationshipQueryStub();
            ServiceLocator.AddService<AttributesManager>(_attrs);
            ServiceLocator.AddService<IEntityQueryService>(_query);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));

            _owner = Guid.NewGuid();
            _grid.Register(_owner, new GridCoord(5, 5));
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

        private Guid Spawn(EntityFilterMask relationToOwner, GridCoord coord, int hp = 10, bool onGrid = true)
        {
            var guid = Guid.NewGuid();
            var ma = new ModifiableAttributes();
            ma.EnsureInitialized();
            ma.SetAttribute<Health>(new Health(hp));
            _attrs.Register(guid, ma);
            if (onGrid) _grid.Register(guid, coord);
            _query.SetRelationship(_owner, guid, relationToOwner);
            return guid;
        }

        private AIContext BuildContext() =>
            new AIContext { SelfGuid = _owner, Attributes = _attrs, Grid = _grid };

        // ----- tests -----------------------------------------------------

        [Test]
        public void PickTarget_PicksClosestByManhattan()
        {
            // Arrange — owner en (5,5); near a dist 2, far a dist 5.
            var far = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 10));
            var near = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 7));
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(near, pick);
            Assert.AreNotEqual(far, pick);
        }

        [Test]
        public void PickTarget_FiltersByRelation_IgnoresNonMatching()
        {
            // Arrange — aliado más cerca que el enemigo; Relation=Enemies debe ignorar al aliado.
            var closerAlly = Spawn(EntityFilterMask.Allies, new GridCoord(5, 6));   // dist 1
            var enemy = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 8));         // dist 3
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(enemy, pick);
            Assert.AreNotEqual(closerAlly, pick);
        }

        [Test]
        public void PickTarget_SkipDead_IgnoresZeroHpEvenIfClosest()
        {
            // Arrange — el muerto es el más cercano; SkipDead lo descarta.
            var deadNear = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 6), hp: 0); // dist 1
            var liveFar = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 8), hp: 5);  // dist 3
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies, SkipDead = true };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(liveFar, pick);
            Assert.AreNotEqual(deadNear, pick);
        }

        [Test]
        public void PickTarget_CandidateWithoutGridPosition_IsSkipped()
        {
            // Arrange — el más cercano "lógico" no tiene posición en grilla; debe ignorarse.
            var offGrid = Spawn(EntityFilterMask.Enemies, default, onGrid: false);
            var onGrid = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 9)); // dist 4
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(onGrid, pick);
            Assert.AreNotEqual(offGrid, pick);
        }

        [Test]
        public void PickTarget_TieDistance_ResolvesByLowerGuid_Deterministic()
        {
            // Arrange — dos enemigos equidistantes (dist 2); gana el de menor Guid.
            var a = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 7)); // dist 2
            var b = Spawn(EntityFilterMask.Enemies, new GridCoord(3, 5)); // dist 2
            var lower = a.CompareTo(b) < 0 ? a : b;
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            // Act
            var pick1 = selector.PickTarget(BuildContext(), _owner);
            var pick2 = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(lower, pick1);
            Assert.AreEqual(pick1, pick2, "El selector debe ser determinístico across calls.");
        }

        [Test]
        public void PickTarget_OwnerInCandidates_AlwaysExcluded()
        {
            // Arrange — el owner se auto-relaciona como Enemies y estaría a dist 0; debe ignorarse.
            _query.SetRelationship(_owner, _owner, EntityFilterMask.Enemies);
            var enemy = Spawn(EntityFilterMask.Enemies, new GridCoord(5, 8));
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(enemy, pick);
            Assert.AreNotEqual(_owner, pick);
        }

        [Test]
        public void PickTarget_RelationNone_ReturnsEmpty()
        {
            Spawn(EntityFilterMask.Enemies, new GridCoord(5, 6));
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.None };

            var pick = selector.PickTarget(BuildContext(), _owner);

            Assert.AreEqual(Guid.Empty, pick);
        }

        [Test]
        public void PickTarget_OwnerNotOnGrid_ReturnsEmpty()
        {
            // Arrange — owner sin posición registrada en la grilla.
            _grid.Unregister(_owner);
            Spawn(EntityFilterMask.Enemies, new GridCoord(5, 6));
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            // Act
            var pick = selector.PickTarget(BuildContext(), _owner);

            // Assert
            Assert.AreEqual(Guid.Empty, pick);
        }

        [Test]
        public void PickTarget_GridNull_ReturnsEmpty()
        {
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };
            var ctx = new AIContext { SelfGuid = _owner, Attributes = _attrs, Grid = null };

            LogAssert.Expect(LogType.Warning, new Regex(".*AIContext.Grid is null.*"));

            var pick = selector.PickTarget(ctx, _owner);

            Assert.AreEqual(Guid.Empty, pick);
        }

        [Test]
        public void PickTarget_AttributesNull_ReturnsEmpty()
        {
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            LogAssert.Expect(LogType.Warning, new Regex(".*AIContext.Attributes is null.*"));

            var pick = selector.PickTarget(null, _owner);

            Assert.AreEqual(Guid.Empty, pick);
        }

        [Test]
        public void PickTarget_QueryServiceMissing_ReturnsEmpty()
        {
            ServiceLocator.RemoveService<IEntityQueryService>();
            Spawn(EntityFilterMask.Enemies, new GridCoord(5, 6));
            var selector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies };

            LogAssert.Expect(LogType.Warning, new Regex(".*IEntityQueryService not registered.*"));

            var pick = selector.PickTarget(BuildContext(), _owner);

            Assert.AreEqual(Guid.Empty, pick);

            // Cleanup — re-register para que TearDown.Clear() sea consistente.
            ServiceLocator.AddService<IEntityQueryService>(_query);
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
