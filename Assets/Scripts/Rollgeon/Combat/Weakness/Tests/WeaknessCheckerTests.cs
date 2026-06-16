using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combos;
using UnityEngine;

namespace Rollgeon.Combat.Weakness.Tests
{
    /// <summary>
    /// Cobertura §9.1 DoD del plan: GetMultiplier returns DefaultMultiplier for matching combo,
    /// 1.0 para non-matching, override toma precedencia sobre default, y OnWeaknessHit fires.
    /// </summary>
    [TestFixture]
    public class WeaknessCheckerTests
    {
        private RulesetSO _ruleset;
        private WeaknessRegistry _registry;
        private WeaknessChecker _sut;

        // Probe del EventManager para verificar OnWeaknessHit.
        private int _eventFireCount;
        private Guid _lastAttackerArg;
        private Guid _lastTargetArg;
        private EventManager.EventReceiver _probe;

        [SetUp]
        public void SetUp()
        {
            _ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            // Set default multiplier = 1.5f via reflection — the field is private.
            var field = typeof(RulesetSO).GetField("_weakness",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(_ruleset, new WeaknessConfig { DefaultMultiplier = 1.5f });

            _registry = new WeaknessRegistry();
            _sut = new WeaknessChecker(_registry, _ruleset);

            _eventFireCount = 0;
            _lastAttackerArg = Guid.Empty;
            _lastTargetArg = Guid.Empty;
            _probe = (args) =>
            {
                _eventFireCount++;
                if (args != null && args.Length >= 2)
                {
                    _lastAttackerArg = (Guid)args[0];
                    _lastTargetArg = (Guid)args[1];
                }
            };
            EventManager.Subscribe(EventName.OnWeaknessHit, _probe);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnWeaknessHit, _probe);
            UnityEngine.Object.DestroyImmediate(_ruleset);
        }

        // ---- Default multiplier path ------------------------------------

        [Test]
        public void GetMultiplier_Returns_Default_For_Matching_Combo()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.FullHouse, 0f); // 0 = use default

            float mult = _sut.GetMultiplier(attacker, target, ComboId.FullHouse);

            Assert.AreEqual(1.5f, mult, 0.0001f, "Should use RulesetSO.Weakness.DefaultMultiplier = 1.5.");
        }

        [Test]
        public void GetMultiplier_Returns_One_For_NonMatching_Combo()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.FullHouse, 0f);

            float mult = _sut.GetMultiplier(attacker, target, ComboId.Par);

            Assert.AreEqual(1.0f, mult, 0.0001f);
            Assert.AreEqual(0, _eventFireCount, "OnWeaknessHit should NOT fire for non-matching combo.");
        }

        [Test]
        public void GetMultiplier_Returns_One_For_Unknown_Target()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            // No weakness registered.

            float mult = _sut.GetMultiplier(attacker, target, ComboId.FullHouse);

            Assert.AreEqual(1.0f, mult, 0.0001f);
            Assert.AreEqual(0, _eventFireCount);
        }

        [Test]
        public void GetMultiplier_Returns_One_For_Empty_Target_Guid()
        {
            float mult = _sut.GetMultiplier(Guid.NewGuid(), Guid.Empty, ComboId.Par);
            Assert.AreEqual(1.0f, mult, 0.0001f);
        }

        [Test]
        public void GetMultiplier_Returns_One_For_Null_Combo()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.Par, 0f);

            Assert.AreEqual(1.0f, _sut.GetMultiplier(attacker, target, null), 0.0001f);
            Assert.AreEqual(1.0f, _sut.GetMultiplier(attacker, target, string.Empty), 0.0001f);
        }

        // ---- Override takes precedence ----------------------------------

        [Test]
        public void Override_Takes_Precedence_Over_Default()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.Poker, 3.0f);

            float mult = _sut.GetMultiplier(attacker, target, ComboId.Poker);

            Assert.AreEqual(3.0f, mult, 0.0001f,
                "Override = 3.0 should take precedence over default 1.5.");
        }

        // ---- Event firing -----------------------------------------------

        [Test]
        public void OnWeaknessHit_Fires_When_Multiplier_Gt_One()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.Generala, 0f);

            _sut.GetMultiplier(attacker, target, ComboId.Generala);

            Assert.AreEqual(1, _eventFireCount, "OnWeaknessHit should fire exactly once.");
            Assert.AreEqual(attacker, _lastAttackerArg, "args[0] must be attacker Guid.");
            Assert.AreEqual(target, _lastTargetArg, "args[1] must be target Guid.");
        }

        [Test]
        public void OnWeaknessHit_Does_Not_Fire_When_Multiplier_Is_One()
        {
            var attacker = Guid.NewGuid();
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.Par, 0f);

            _sut.GetMultiplier(attacker, target, ComboId.FullHouse); // non-matching

            Assert.AreEqual(0, _eventFireCount);
        }

        // ---- Constructor guards -----------------------------------------

        [Test]
        public void Ctor_Throws_On_Null_Registry()
        {
            Assert.Throws<ArgumentNullException>(() => new WeaknessChecker(null, _ruleset));
        }

        [Test]
        public void Ctor_Throws_On_Null_Ruleset()
        {
            Assert.Throws<ArgumentNullException>(() => new WeaknessChecker(_registry, null));
        }

        // ---- Registry semantics -----------------------------------------

        [Test]
        public void Registry_Unregister_Makes_Target_Unknown()
        {
            var target = Guid.NewGuid();
            _registry.SetWeakness(target, ComboId.Par, 0f);
            Assert.IsTrue(_registry.TryGet(target, out _));

            _registry.Unregister(target);
            Assert.IsFalse(_registry.TryGet(target, out _));
        }

        [Test]
        public void Registry_Rejects_Empty_Guid()
        {
            _registry.SetWeakness(Guid.Empty, ComboId.Par, 0f);
            Assert.IsFalse(_registry.TryGet(Guid.Empty, out _));
        }
    }
}
