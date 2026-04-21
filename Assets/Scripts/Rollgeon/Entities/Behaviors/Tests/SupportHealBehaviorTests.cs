using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Stubs;
using Rollgeon.Entities.Behaviors;

namespace Rollgeon.Entities.Behaviors.Tests
{
    /// <summary>
    /// Tests de comportamiento del <see cref="SupportHealBehavior"/>. Covers:
    /// idle con 0 aliados, idle con aliados a full HP, pick por menor HP absoluto,
    /// monto de heal derivado de HealStrength.
    /// </summary>
    [TestFixture]
    public class SupportHealBehaviorTests
    {
        private AttributesManager _attrs;
        private FakeEntityQueryService _query;
        private Guid _supportGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _query = new FakeEntityQueryService();
            ServiceLocator.AddService<AttributesManager>(_attrs);
            ServiceLocator.AddService<IEntityQueryService>(_query);

            _supportGuid = Guid.NewGuid();
            var supportAttrs = new ModifiableAttributes();
            supportAttrs.EnsureInitialized();
            supportAttrs.SetAttribute<Health>(new Health(20));
            supportAttrs.SetAttribute<HealStrength>(new HealStrength(5));
            _attrs.Register(_supportGuid, supportAttrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
        }

        // ----- helpers ---------------------------------------------------

        private Guid SpawnAlly(int currentHp, int maxHp)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(currentHp));
            _attrs.Register(guid, attrs);

            if (!_query.Allies.TryGetValue(_supportGuid, out var list))
            {
                list = new List<Entity>();
                _query.Allies[_supportGuid] = list;
            }
            list.Add(new Entity { Guid = guid });
            return guid;
        }

        private static BehaviorContext CtxFor(Guid sourceGuid)
        {
            return new TestBehaviorContext { SourceEntity = new Entity { Guid = sourceGuid } };
        }

        private sealed class TestBehaviorContext : BehaviorContext { }

        // ----- tests -----------------------------------------------------

        [Test]
        public void Execute_NoAllies_DoesNothing()
        {
            // No hay aliados en el fake query → no-op.
            var behavior = new SupportHealBehavior { BaseHealAmount = 6 };

            Assert.DoesNotThrow(() => behavior.Execute(CtxFor(_supportGuid)));
            // Sanity: el bag de StoredValues quedo vacio.
            Assert.IsFalse(behavior.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingHeal, out _));
        }

        [Test]
        public void Execute_OneWoundedAlly_HealsThatAlly()
        {
            var allyGuid = SpawnAlly(currentHp: 5, maxHp: 20);

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };
            behavior.Execute(CtxFor(_supportGuid));

            // HP esperada: 5 + (6 base + 5 heal strength) = 16, clamp a 20 no corresponde.
            int hp = _attrs.GetAttributeValue<Health, int>(allyGuid);
            Assert.AreEqual(16, hp);
        }

        [Test]
        public void Execute_AllFullHP_DoesNothing()
        {
            var allyGuid = SpawnAlly(currentHp: 20, maxHp: 20);
            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };

            behavior.Execute(CtxFor(_supportGuid));

            // HP sin cambios → 20.
            int hp = _attrs.GetAttributeValue<Health, int>(allyGuid);
            Assert.AreEqual(20, hp);
        }

        [Test]
        public void Execute_ManyAllies_HealsLowestHP()
        {
            var allyHighHp = SpawnAlly(currentHp: 15, maxHp: 20);
            var allyLowHp = SpawnAlly(currentHp: 3, maxHp: 20);
            var allyMidHp = SpawnAlly(currentHp: 10, maxHp: 20);

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };
            behavior.Execute(CtxFor(_supportGuid));

            // Low gets healed; others untouched.
            Assert.AreEqual(3 + 11, _attrs.GetAttributeValue<Health, int>(allyLowHp)); // 14
            Assert.AreEqual(15, _attrs.GetAttributeValue<Health, int>(allyHighHp));
            Assert.AreEqual(10, _attrs.GetAttributeValue<Health, int>(allyMidHp));
        }

        [Test]
        public void Execute_DeadAllies_AreSkipped()
        {
            var dead = SpawnAlly(currentHp: 0, maxHp: 20);
            // 5 + 11 = 16 — por debajo del cap (20) para aislar "dead skipped"
            // del clamp del MaxHpResolver.
            var wounded = SpawnAlly(currentHp: 5, maxHp: 20);

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };
            behavior.Execute(CtxFor(_supportGuid));

            // Dead permanece en 0 — el Support no resucita.
            Assert.AreEqual(0, _attrs.GetAttributeValue<Health, int>(dead));
            Assert.AreEqual(5 + 11, _attrs.GetAttributeValue<Health, int>(wounded));
        }

        [Test]
        public void Execute_HealClampsToMaxHP()
        {
            var ally = SpawnAlly(currentHp: 17, maxHp: 20);

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };
            behavior.Execute(CtxFor(_supportGuid));

            // 17 + (6+5)=28 → clamp a 20.
            Assert.AreEqual(20, _attrs.GetAttributeValue<Health, int>(ally));
        }

        [Test]
        public void Execute_RegistersFloatingHealStoredValue()
        {
            var ally = SpawnAlly(currentHp: 5, maxHp: 20);

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };
            behavior.Execute(CtxFor(_supportGuid));

            Assert.IsTrue(behavior.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingHeal, out var values));
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(11f, values[0].Value);
            Assert.AreEqual(ally, values[0].TargetEntityGuid);
        }

        [Test]
        public void PickLowestHpWoundedAlly_TieBreaker_UsesGuidCompareTo()
        {
            // Two allies at same HP — expect lowest Guid wins.
            var aGuid = new Guid("00000000-0000-0000-0000-000000000001");
            var bGuid = new Guid("00000000-0000-0000-0000-000000000002");

            foreach (var g in new[] { aGuid, bGuid })
            {
                var attrs = new ModifiableAttributes();
                attrs.EnsureInitialized();
                attrs.SetAttribute<Health>(new Health(5));
                _attrs.Register(g, attrs);
            }
            _query.Allies[_supportGuid] = new List<Entity>
            {
                // Intentional order: b first, a second — tiebreak must pick a.
                new Entity { Guid = bGuid },
                new Entity { Guid = aGuid },
            };

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 20,
            };
            var picked = behavior.PickLowestHpWoundedAlly(_supportGuid);
            Assert.IsNotNull(picked);
            Assert.AreEqual(aGuid, picked.Guid);
        }

        [Test]
        public void Execute_HealAmount_UsesHealStrengthModifiedValue()
        {
            var ally = SpawnAlly(currentHp: 5, maxHp: 100);

            // Support con HealStrength = 5 por defecto en SetUp; cambiar a 10.
            _attrs.SetAttributeValue<HealStrength, int>(_supportGuid, 10);

            var behavior = new SupportHealBehavior
            {
                BaseHealAmount = 6,
                MaxHpResolver = _ => 100,
            };
            behavior.Execute(CtxFor(_supportGuid));

            // HP esperada: 5 + (6 + 10) = 21.
            Assert.AreEqual(21, _attrs.GetAttributeValue<Health, int>(ally));
        }
    }
}
