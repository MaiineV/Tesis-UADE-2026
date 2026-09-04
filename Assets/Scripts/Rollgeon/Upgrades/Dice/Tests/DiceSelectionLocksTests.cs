using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// <see cref="DiceSelectionLocks"/> + <see cref="CapSelectionRequirement"/> (Fix#0053):
    /// Sediento sin 2 de oro / Vampiro con 5 de vida o menos quedan con candado — no se
    /// seleccionan ni entran al combo.
    /// </summary>
    [TestFixture]
    public class DiceSelectionLocksTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private AttributesManager _attrs;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _player = Guid.NewGuid();
            var a = new ModifiableAttributes();
            a.SetAttribute<Health>(new Health(20));
            _attrs.Register(_player, a);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            _attrs?.Dispose();
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private RuntimeDiceBag BagWith(params IEnchantmentCapability[] caps)
        {
            var bagSo = ScriptableObject.CreateInstance<DiceBagSO>();
            bagSo.Dice = new List<DiceType> { DiceType.D6, DiceType.D6 };
            _created.Add(bagSo);

            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.EditorSetUpgradeId("ench.test_lock");
            ench.EditorSetCapabilities(new List<IEnchantmentCapability>(caps));
            _created.Add(ench);

            var svc = new DiceEnchantmentService(config: null);
            svc.InitializeFromBag(bagSo);
            Assert.IsTrue(svc.Apply(0, ench).Success);
            return svc.Bag;
        }

        private static CapSelectionRequirement RequireHealthAbove(int value, string label = "6+ vida")
            => new CapSelectionRequirement
            {
                LockLabel = label,
                Conditions = new List<BasePreCondition>
                {
                    new PcOwnerStatCompare
                    {
                        Stat = StatType.Health,
                        Comparison = IntComparison.Greater,
                        Value = value,
                        UseModified = true,
                    },
                },
            };

        [Test]
        public void SlotWithUnmetRequirement_IsLocked_WithItsLabel()
        {
            _attrs.SetAttributeValue<Health, int>(_player, 5);
            var bag = BagWith(RequireHealthAbove(5));

            bool locked = DiceSelectionLocks.IsSlotLocked(bag, 0, _player, out var label);

            Assert.IsTrue(locked);
            Assert.AreEqual("6+ vida", label, "sin tabla de loc cae al fallback del asset");
        }

        [Test]
        public void SlotWithMetRequirement_IsFree()
        {
            _attrs.SetAttributeValue<Health, int>(_player, 6);
            var bag = BagWith(RequireHealthAbove(5));

            Assert.IsFalse(DiceSelectionLocks.IsSlotLocked(bag, 0, _player, out var label));
            Assert.IsNull(label);
        }

        [Test]
        public void OtherSlots_AreNotAffected()
        {
            _attrs.SetAttributeValue<Health, int>(_player, 1);
            var bag = BagWith(RequireHealthAbove(5));

            Assert.IsFalse(DiceSelectionLocks.IsSlotLocked(bag, 1, _player, out _));
        }

        [Test]
        public void CapabilityWithoutConditions_NeverLocks()
        {
            var bag = BagWith(new CapSelectionRequirement { LockLabel = "x" });

            Assert.IsFalse(DiceSelectionLocks.IsSlotLocked(bag, 0, _player, out _));
        }

        [Test]
        public void NullBagOrOutOfRange_IsFree()
        {
            Assert.IsFalse(DiceSelectionLocks.IsSlotLocked(null, 0, _player, out _));
            var bag = BagWith(RequireHealthAbove(50));
            Assert.IsFalse(DiceSelectionLocks.IsSlotLocked(bag, 7, _player, out _));
            Assert.IsFalse(DiceSelectionLocks.IsSlotLocked(bag, -1, _player, out _));
        }

        [Test]
        public void WithoutEnchantmentService_PlayerSlotIsFree()
        {
            Assert.IsFalse(DiceSelectionLocks.IsPlayerSlotLocked(0, out _));
        }
    }
}
