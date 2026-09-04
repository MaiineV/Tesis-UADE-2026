using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Chests;
using Rollgeon.Combat.Rooms;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="CombatantQuery"/>: <c>LiveEnemiesOf</c> excluye cofres, objetos de
    /// sala rastreados, HP≤0 y entidades fuera de grilla — misma clasificación que
    /// <c>ClassSkillPushResolver.Classify</c>. Los helpers Bloodless/Immovable/StunImmune
    /// leen <see cref="IUnitTraitService"/>.
    /// </summary>
    [TestFixture]
    public class CombatantQueryTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private sealed class FakeChestRegistry : IChestRegistry
        {
            public readonly HashSet<Guid> Chests = new HashSet<Guid>();
            public bool IsChest(Guid guid) => Chests.Contains(guid);
            public bool TryGetActiveChest(out Guid chestGuid) { chestGuid = Guid.Empty; return false; }
        }

        private sealed class FakeRoomObjectCleanup : IRoomObjectCleanupService
        {
            public readonly List<Guid> TrackedList = new List<Guid>();
            public void Track(Guid guid) { if (!TrackedList.Contains(guid)) TrackedList.Add(guid); }
            public void Forget(Guid guid) => TrackedList.Remove(guid);
            public IReadOnlyList<Guid> Tracked => TrackedList;
            public void TearDownAll() => TrackedList.Clear();
        }

        private FakeEntityQuery _query;
        private FakeChestRegistry _chests;
        private FakeRoomObjectCleanup _roomObjects;
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
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _chests = new FakeChestRegistry();
            ServiceLocator.AddService<IChestRegistry>(_chests, ServiceScope.Global);

            _roomObjects = new FakeRoomObjectCleanup();
            ServiceLocator.AddService<IRoomObjectCleanupService>(_roomObjects, ServiceScope.Global);

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

        private Guid RegisterAlive(GridCoord coord)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            _attrs.Register(guid, attrs);
            _grid.Register(guid, coord);
            _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        [Test]
        public void LiveEnemiesOf_IncludesRegisteredAliveOnGrid()
        {
            var e = RegisterAlive(new GridCoord(1, 0));

            var result = CombatantQuery.LiveEnemiesOf(_player);

            CollectionAssert.Contains(result, e);
        }

        [Test]
        public void LiveEnemiesOf_ExcludesChests()
        {
            var chest = RegisterAlive(new GridCoord(1, 0));
            _chests.Chests.Add(chest);

            var result = CombatantQuery.LiveEnemiesOf(_player);

            CollectionAssert.DoesNotContain(result, chest);
        }

        [Test]
        public void LiveEnemiesOf_ExcludesTrackedRoomObjects()
        {
            var prop = RegisterAlive(new GridCoord(1, 0));
            _roomObjects.Track(prop);

            var result = CombatantQuery.LiveEnemiesOf(_player);

            CollectionAssert.DoesNotContain(result, prop);
        }

        [Test]
        public void LiveEnemiesOf_ExcludesDeadEntities()
        {
            var dead = RegisterAlive(new GridCoord(1, 0));
            _attrs.SetAttributeValue<Health, int>(dead, 0);

            var result = CombatantQuery.LiveEnemiesOf(_player);

            CollectionAssert.DoesNotContain(result, dead);
        }

        [Test]
        public void LiveEnemiesOf_ExcludesOffGridEntities()
        {
            var offGrid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            _attrs.Register(offGrid, attrs);
            _query.Enemies.Add(new Entity { Guid = offGrid }); // nunca se registra en _grid

            var result = CombatantQuery.LiveEnemiesOf(_player);

            CollectionAssert.DoesNotContain(result, offGrid);
        }

        [Test]
        public void IsEligibleForBlood_FalseWhenBloodless()
        {
            var e = Guid.NewGuid();
            _traits.Register(e, new UnitTraits(isFlying: false, isBoss: false, bloodless: true));

            Assert.IsFalse(CombatantQuery.IsEligibleForBlood(e));
        }

        [Test]
        public void IsEligibleForBlood_TrueByDefault()
        {
            var e = Guid.NewGuid();
            Assert.IsTrue(CombatantQuery.IsEligibleForBlood(e), "sin traits registrados = perfil seguro (no Bloodless).");
        }

        [Test]
        public void IsMovable_FalseWhenImmovable()
        {
            var e = Guid.NewGuid();
            _traits.Register(e, new UnitTraits(isFlying: false, isBoss: false, immovable: true));

            Assert.IsFalse(CombatantQuery.IsMovable(e));
        }

        [Test]
        public void IsMovable_FalseWhenBoss()
        {
            var e = Guid.NewGuid();
            _traits.Register(e, new UnitTraits(isFlying: false, isBoss: true));

            Assert.IsFalse(CombatantQuery.IsMovable(e));
        }

        [Test]
        public void IsMovable_FalseWhenMultiCellFootprint()
        {
            var e = Guid.NewGuid();
            Assert.IsTrue(_grid.TryRegister(e, new GridCoord(0, 0), new Vector2Int(2, 2)));

            Assert.IsFalse(CombatantQuery.IsMovable(e));
        }

        [Test]
        public void IsMovable_TrueForDefaultUnitFootprint()
        {
            var e = Guid.NewGuid();
            _grid.Register(e, new GridCoord(0, 0));

            Assert.IsTrue(CombatantQuery.IsMovable(e));
        }

        [Test]
        public void IsStunnable_FalseWhenStunImmune()
        {
            var e = Guid.NewGuid();
            _traits.Register(e, new UnitTraits(isFlying: false, isBoss: false, stunImmune: true));

            Assert.IsFalse(CombatantQuery.IsStunnable(e));
        }

        [Test]
        public void TryGetCoord_ReturnsRegisteredPosition()
        {
            var e = Guid.NewGuid();
            _grid.Register(e, new GridCoord(3, 1));

            Assert.IsTrue(CombatantQuery.TryGetCoord(e, out var coord));
            Assert.AreEqual(new GridCoord(3, 1), coord);
        }
    }
}
