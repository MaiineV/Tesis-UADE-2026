using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Effects.Selection;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="EffBloodRedistribute"/> (Feature#0085, Blood Transfusion banda 1-3):
    /// único elegible → Sangrado; 2+ → reparto equitativo capeado por máximo, resto de a
    /// uno nearest-first.
    /// </summary>
    [TestFixture]
    public class EffBloodRedistributeTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private sealed class FakeEnemyAIRegistry : IEnemyAIRegistry
        {
            public readonly Dictionary<Guid, int> MaxHp = new Dictionary<Guid, int>();
            public void Register(Guid enemyId, AIDecisionNode root, int maxHp) => MaxHp[enemyId] = maxHp;
            public void Unregister(Guid enemyId) => MaxHp.Remove(enemyId);
            public bool TryGet(Guid enemyId, out AIDecisionNode root, out int maxHp)
            {
                root = null;
                return MaxHp.TryGetValue(enemyId, out maxHp);
            }
            public bool Has(Guid enemyId) => MaxHp.ContainsKey(enemyId);
        }

        private sealed class FakeBleedService : IBleedService
        {
            public readonly List<(Guid Entity, Guid Source, int Stacks)> Calls = new();
            public void AddStack(Guid entity, Guid source, int stacks = 1) => Calls.Add((entity, source, stacks));
            public bool IsBleeding(Guid entity) => false;
            public int GetStacks(Guid entity) => 0;
            public int GetMaxRemainingTurns(Guid entity) => 0;
            public void Clear(Guid entity) { }
            public void ClearAll() { }
        }

        private FakeEntityQuery _query;
        private FakeEnemyAIRegistry _aiRegistry;
        private FakeBleedService _bleed;
        private AttributesManager _attrs;
        private GridManager _grid;
        private UnitTraitService _traits;
        private DamagePipeline _damage;
        private HealPipeline _heal;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _aiRegistry = new FakeEnemyAIRegistry();
            ServiceLocator.AddService<IEnemyAIRegistry>(_aiRegistry, ServiceScope.Global);

            _bleed = new FakeBleedService();
            ServiceLocator.AddService<IBleedService>(_bleed, ServiceScope.Global);

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _traits = new UnitTraitService();
            _traits.Register();

            _damage = new DamagePipeline(_attrs);
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _heal = new HealPipeline(_attrs, MaxHpResolver.Resolve);
            ServiceLocator.AddService<IHealPipeline>(_heal, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            _traits?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Guid SpawnEnemy(GridCoord coord, int hp, int maxHp)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _attrs.Register(guid, attrs);
            _grid.Register(guid, coord);
            _query.Enemies.Add(new Entity { Guid = guid });
            _aiRegistry.MaxHp[guid] = maxHp;
            return guid;
        }

        private EffectContext MakeContext() => new EffectContext { SourceGuid = _player };

        [Test]
        public void test_applyEffect_singleEligibleEnemy_appliesOneBleedStack_insteadOfRedistributing()
        {
            var enemy = SpawnEnemy(new GridCoord(1, 0), hp: 50, maxHp: 100);
            var effect = new EffBloodRedistribute();

            var result = effect.ApplyEffect(MakeContext());

            Assert.IsTrue(result);
            Assert.AreEqual(1, _bleed.Calls.Count);
            Assert.AreEqual(enemy, _bleed.Calls[0].Entity);
            Assert.AreEqual(_player, _bleed.Calls[0].Source);
            Assert.AreEqual(1, _bleed.Calls[0].Stacks);
            Assert.AreEqual(50, _attrs.GetAttribute<Health>(enemy).Value, "único elegible no redistribuye su HP.");
        }

        [Test]
        public void test_applyEffect_twoEnemies_equalisesHpAroundAverage()
        {
            var high = SpawnEnemy(new GridCoord(1, 0), hp: 80, maxHp: 100);
            var low = SpawnEnemy(new GridCoord(2, 0), hp: 20, maxHp: 100);
            var effect = new EffBloodRedistribute();

            var result = effect.ApplyEffect(MakeContext());

            Assert.IsTrue(result);
            Assert.AreEqual(50, _attrs.GetAttribute<Health>(high).Value);
            Assert.AreEqual(50, _attrs.GetAttribute<Health>(low).Value);
            Assert.AreEqual(0, _bleed.Calls.Count, "con 2+ elegibles no se aplica Sangrado.");
        }

        [Test]
        public void test_applyEffect_capsAtMax_surplusGoesToTheOneStillBelowMax()
        {
            // total = 100, count = 2 → basePerHead 50. B ya está en su máximo (20) — todo
            // el sobrante (30) tiene que volver a A, que sigue debajo del suyo (100).
            var a = SpawnEnemy(new GridCoord(1, 0), hp: 90, maxHp: 100);
            var b = SpawnEnemy(new GridCoord(2, 0), hp: 10, maxHp: 20);
            var effect = new EffBloodRedistribute();

            var result = effect.ApplyEffect(MakeContext());

            Assert.IsTrue(result);
            Assert.AreEqual(80, _attrs.GetAttribute<Health>(a).Value);
            Assert.AreEqual(20, _attrs.GetAttribute<Health>(b).Value);
        }

        [Test]
        public void test_applyEffect_remainderByModulo_goesToNearestEnemyFirst()
        {
            // Los enemigos con HP 0 no son elegibles (CombatantQuery los filtra), asi que
            // todos arrancan vivos: total = 11, count = 3 → basePerHead 3 (spent 9), resto 2
            // → +1 al más cercano (A) y +1 al siguiente (B); C queda en 3.
            var a = SpawnEnemy(new GridCoord(1, 0), hp: 9, maxHp: 100); // distancia 1
            var b = SpawnEnemy(new GridCoord(2, 0), hp: 1, maxHp: 100); // distancia 2
            var c = SpawnEnemy(new GridCoord(3, 0), hp: 1, maxHp: 100); // distancia 3
            var effect = new EffBloodRedistribute();

            var result = effect.ApplyEffect(MakeContext());

            Assert.IsTrue(result);
            Assert.AreEqual(4, _attrs.GetAttribute<Health>(a).Value, "el más cercano se lleva el resto primero.");
            Assert.AreEqual(4, _attrs.GetAttribute<Health>(b).Value);
            Assert.AreEqual(3, _attrs.GetAttribute<Health>(c).Value);
        }

        [Test]
        public void test_applyEffect_noEligibleEnemies_returnsTrueAndDoesNothing()
        {
            var effect = new EffBloodRedistribute();

            var result = effect.ApplyEffect(MakeContext());

            Assert.IsTrue(result, "nunca corta la cadena — el roll ya se pagó.");
            Assert.AreEqual(0, _bleed.Calls.Count);
        }

        [Test]
        public void test_applyEffect_nullContext_returnsFalse()
        {
            var effect = new EffBloodRedistribute();

            Assert.IsFalse(effect.ApplyEffect(null));
        }
    }
}
