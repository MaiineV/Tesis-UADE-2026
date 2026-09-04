using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dice;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="EffBloodDrain"/> (Feature#0084, Blood Transfusion bandas mixta/positiva):
    /// <c>dmg = max(1, floor(A × cara / 10))</c>, <c>A</c> = cara máxima del dado más grande
    /// de la bolsa; la curación es un % del daño REAL (HP perdido, sin contar escudo).
    /// </summary>
    [TestFixture]
    public class EffBloodDrainTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid, DiceBagSO bag) { PlayerGuid = guid; DiceBag = bag; }
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag { get; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }

        private FakeEntityQuery _query;
        private AttributesManager _attrs;
        private GridManager _grid;
        private DamagePipeline _damage;
        private HealPipeline _heal;
        private Guid _player;
        private Guid _enemy;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();

            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D20 }; // dado más grande = D20, A = 20
            _created.Add(bag);
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player, bag), ServiceScope.Global);

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _damage = new DamagePipeline(_attrs);
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _heal = new HealPipeline(_attrs, _ => int.MaxValue);
            ServiceLocator.AddService<IHealPipeline>(_heal, ServiceScope.Global);

            RegisterAttrs(_player, hp: 50, shield: 0);
            _grid.Register(_player, new GridCoord(0, 0));

            RegisterAttrs(_enemy, hp: 100, shield: 0);
            _grid.Register(_enemy, new GridCoord(1, 0));
            _query.Enemies.Add(new Entity { Guid = _enemy });
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            foreach (var asset in _created) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void RegisterAttrs(Guid guid, int hp, int shield)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Shield>(new Shield(shield));
            _attrs.Register(guid, attrs);
        }

        private EffectContext MakeContext(int face)
        {
            return new EffectContext
            {
                SourceGuid = _player,
                TriggerContext = new ActiveItemRollTriggerContext { Face = face },
            };
        }

        [Test]
        public void test_applyEffect_face4_dealsFormulaDamage_andHealsFiftyPercent()
        {
            // A = 20 (D20 más grande), cara 4 → dmg = floor(20*4/10) = 8. Heal 50% → 4.
            var effect = new EffBloodDrain();
            effect.EditorSetHealPct(0.5f);

            var result = effect.ApplyEffect(MakeContext(face: 4));

            Assert.IsTrue(result);
            Assert.AreEqual(92, _attrs.GetAttribute<Health>(_enemy).Value, "100 - 8 de daño.");
            Assert.AreEqual(54, _attrs.GetAttribute<Health>(_player).Value, "50 + 4 de cura (50% de 8).");
        }

        [Test]
        public void test_applyEffect_face10_dealsFormulaDamage_andHealsFullAmount()
        {
            // A = 20, cara 10 → dmg = floor(20*10/10) = 20. Heal 100% → 20.
            var effect = new EffBloodDrain();
            effect.EditorSetHealPct(1.0f);

            var result = effect.ApplyEffect(MakeContext(face: 10));

            Assert.IsTrue(result);
            Assert.AreEqual(80, _attrs.GetAttribute<Health>(_enemy).Value, "100 - 20 de daño.");
            Assert.AreEqual(70, _attrs.GetAttribute<Health>(_player).Value, "50 + 20 de cura (100% de 20).");
        }

        [Test]
        public void test_applyEffect_healOnlyCountsRealHpLost_notShieldAbsorbed()
        {
            // dmg = 8 (cara 4). Shield 5 absorbe 5 → daño REAL (HP perdido) = 3.
            // Heal 50% de 3 = floor(1.5) = 1, NO 50% de 8.
            RegisterAttrs(_enemy, hp: 100, shield: 5);
            _grid.Register(_enemy, new GridCoord(1, 0));

            var effect = new EffBloodDrain();
            effect.EditorSetHealPct(0.5f);

            var result = effect.ApplyEffect(MakeContext(face: 4));

            Assert.IsTrue(result);
            Assert.AreEqual(97, _attrs.GetAttribute<Health>(_enemy).Value, "8 de daño - 5 de escudo = 3 de HP real.");
            Assert.AreEqual(51, _attrs.GetAttribute<Health>(_player).Value, "cura = floor(0.5 * 3) = 1, no floor(0.5 * 8).");
        }

        [Test]
        public void test_applyEffect_noEligibleTarget_returnsTrueNoOp()
        {
            _query.Enemies.Clear();
            var effect = new EffBloodDrain();

            var result = effect.ApplyEffect(MakeContext(face: 10));

            Assert.IsTrue(result, "nunca corta la cadena — el roll ya se pagó.");
            Assert.AreEqual(50, _attrs.GetAttribute<Health>(_player).Value, "sin target no hay cura.");
        }

        [Test]
        public void test_applyEffect_missingTriggerContext_returnsTrueNoOp()
        {
            var effect = new EffBloodDrain();

            var result = effect.ApplyEffect(new EffectContext { SourceGuid = _player });

            Assert.IsTrue(result);
            Assert.AreEqual(100, _attrs.GetAttribute<Health>(_enemy).Value);
        }

        [Test]
        public void test_applyEffect_nullContext_returnsFalse()
        {
            var effect = new EffBloodDrain();

            Assert.IsFalse(effect.ApplyEffect(null));
        }
    }
}
