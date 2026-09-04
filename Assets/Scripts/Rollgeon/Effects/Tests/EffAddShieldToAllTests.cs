using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="EffAddShieldToAll"/> (Feature#0085, Coin Shield banda impar): el monto se
    /// computa UNA sola vez y se aplica a jugador + enemigos vivos; entidades sin atributo
    /// Shield se saltean.
    /// </summary>
    [TestFixture]
    public class EffAddShieldToAllTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private sealed class CountingReader : EffectIntReader
        {
            public int Amount;
            public int Calls;
            public override int Read(EffectContext context) { Calls++; return Amount; }
        }

        private FakeEntityQuery _query;
        private AttributesManager _attrs;
        private GridManager _grid;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _player = Guid.NewGuid();
            RegisterWithShield(_player, shield: 0);
            _grid.Register(_player, new GridCoord(0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void RegisterWithShield(Guid guid, int shield)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            attrs.SetAttribute<Shield>(new Shield(shield));
            _attrs.Register(guid, attrs);
        }

        private Guid SpawnEnemyWithShield(GridCoord coord, int shield)
        {
            var guid = Guid.NewGuid();
            RegisterWithShield(guid, shield);
            _grid.Register(guid, coord);
            _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        private Guid SpawnEnemyWithoutShieldAttribute(GridCoord coord)
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
        public void test_applyEffect_readerCalledOnce_evenWithMultipleTargets()
        {
            var e1 = SpawnEnemyWithShield(new GridCoord(1, 0), shield: 0);
            var e2 = SpawnEnemyWithShield(new GridCoord(2, 0), shield: 0);
            var reader = new CountingReader { Amount = 10 };
            var effect = new EffAddShieldToAll { IncludeOwner = true, IncludeEnemies = true };
            effect.EditorSetAmount(reader);

            var result = effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.IsTrue(result);
            Assert.AreEqual(1, reader.Calls, "el monto se computa UNA sola vez para todos los targets.");
            Assert.AreEqual(10, _attrs.GetAttribute<Shield>(_player).Value);
            Assert.AreEqual(10, _attrs.GetAttribute<Shield>(e1).Value);
            Assert.AreEqual(10, _attrs.GetAttribute<Shield>(e2).Value);
        }

        [Test]
        public void test_applyEffect_includeEnemiesFalse_onlyAppliesToOwner()
        {
            var enemy = SpawnEnemyWithShield(new GridCoord(1, 0), shield: 0);
            var reader = new CountingReader { Amount = 5 };
            var effect = new EffAddShieldToAll { IncludeOwner = true, IncludeEnemies = false };
            effect.EditorSetAmount(reader);

            var result = effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.IsTrue(result);
            Assert.AreEqual(5, _attrs.GetAttribute<Shield>(_player).Value);
            Assert.AreEqual(0, _attrs.GetAttribute<Shield>(enemy).Value, "IncludeEnemies false no toca a los enemigos.");
        }

        [Test]
        public void test_applyEffect_entityWithoutShieldAttribute_isSkippedWithoutThrowing()
        {
            var noShield = SpawnEnemyWithoutShieldAttribute(new GridCoord(1, 0));
            var reader = new CountingReader { Amount = 5 };
            var effect = new EffAddShieldToAll { IncludeOwner = true, IncludeEnemies = true };
            effect.EditorSetAmount(reader);

            Assert.DoesNotThrow(() => effect.ApplyEffect(new EffectContext { SourceGuid = _player }));
            Assert.AreEqual(5, _attrs.GetAttribute<Shield>(_player).Value, "el owner sigue recibiendo su escudo.");
        }

        [Test]
        public void test_applyEffect_addsOnTopOfExistingShield_noCapExists()
        {
            RegisterWithShield(_player, shield: 20);
            var reader = new CountingReader { Amount = 7 };
            var effect = new EffAddShieldToAll { IncludeOwner = true, IncludeEnemies = false };
            effect.EditorSetAmount(reader);

            effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.AreEqual(27, _attrs.GetAttribute<Shield>(_player).Value);
        }

        [Test]
        public void test_applyEffect_amountZero_isNoOp()
        {
            var reader = new CountingReader { Amount = 0 };
            var effect = new EffAddShieldToAll { IncludeOwner = true };
            effect.EditorSetAmount(reader);

            var result = effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.IsTrue(result);
            Assert.AreEqual(0, _attrs.GetAttribute<Shield>(_player).Value);
        }

        [Test]
        public void test_applyEffect_nullContext_returnsFalse()
        {
            var effect = new EffAddShieldToAll();
            effect.EditorSetAmount(new CountingReader { Amount = 5 });

            Assert.IsFalse(effect.ApplyEffect(null));
        }
    }
}
