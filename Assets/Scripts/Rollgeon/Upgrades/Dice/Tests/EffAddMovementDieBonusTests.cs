using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Effects;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// <see cref="EffAddMovementDieBonus"/> (Torbellino "+2"): escribe al bono de la tirada del
    /// scratch — no a <c>MoveRange</c> —, respeta el stacking redundante del carril y queda
    /// atribuido en el journal para que el dado lo muestre como chip.
    /// </summary>
    [TestFixture]
    public class EffAddMovementDieBonusTests
    {
        private const int Lane = EnchantmentSlotRef.MovementDieSlot;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _svc;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
        }

        private static EffectContext Ctx(EnchantmentScratch scratch, int enchSlot = 0)
        {
            return new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = scratch,
                    Slot = new EnchantmentSlotRef(DiceType.D6, Lane, enchSlot),
                    Channel = Upgrades.ScratchChannel.DiceEnchantment,
                    MovementDieFace = 3,
                },
            };
        }

        private EnchantmentSO MakeMovementEnchantment(string id)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_category", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, EnchantmentCategory.Movimiento);
            return ench;
        }

        private void RegisterServiceWithMovementLane(params EnchantmentSO[] lane)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6 };
            _created.Add(bag);
            _svc = new DiceEnchantmentService(config: null);
            _svc.InitializeFromBag(bag);
            foreach (var e in lane) _svc.Bag.AddEnchantment(Lane, e);
            ServiceLocator.AddService<IDiceEnchantmentService>(_svc, ServiceScope.Global);
        }

        [Test]
        public void Apply_AddsTheAmountToTheScratch_AndSumsBetweenTriggers()
        {
            var scratch = new EnchantmentScratch();
            var eff = new EffAddMovementDieBonus { Amount = 2, OnlyFirstCopy = false };

            Assert.IsTrue(eff.ApplyEffect(Ctx(scratch)));
            Assert.AreEqual(2, scratch.MovementDieBonus);

            Assert.IsTrue(new EffAddMovementDieBonus { Amount = -1, OnlyFirstCopy = false }.ApplyEffect(Ctx(scratch)));
            Assert.AreEqual(1, scratch.MovementDieBonus, "distintas fuentes suman (negativo resta)");
        }

        [Test]
        public void Apply_WithoutScratchContext_FailsWithoutTouchingAnything()
        {
            var eff = new EffAddMovementDieBonus { Amount = 2 };

            Assert.IsFalse(eff.ApplyEffect(new EffectContext { SourceGuid = Guid.NewGuid() }));
            Assert.IsFalse(eff.ApplyEffect(null));
        }

        [Test]
        public void Apply_TwoCopies_OnlyTheFirstOneAdds()
        {
            var ench = MakeMovementEnchantment("ench.torbellino");
            RegisterServiceWithMovementLane(ench, ench);
            var scratch = new EnchantmentScratch();
            var eff = new EffAddMovementDieBonus { Amount = 2, OnlyFirstCopy = true };

            Assert.IsTrue(eff.ApplyEffect(Ctx(scratch, enchSlot: 0)));
            Assert.IsTrue(eff.ApplyEffect(Ctx(scratch, enchSlot: 1)), "la copia extra no corta la cadena");

            Assert.AreEqual(2, scratch.MovementDieBonus, "stacking redundante: +2, no +4");
        }

        [Test]
        public void Apply_OnlyFirstCopyOff_EveryCopyAdds()
        {
            var ench = MakeMovementEnchantment("ench.torbellino");
            RegisterServiceWithMovementLane(ench, ench);
            var scratch = new EnchantmentScratch();
            var eff = new EffAddMovementDieBonus { Amount = 2, OnlyFirstCopy = false };

            eff.ApplyEffect(Ctx(scratch, enchSlot: 0));
            eff.ApplyEffect(Ctx(scratch, enchSlot: 1));

            Assert.AreEqual(4, scratch.MovementDieBonus);
        }

        [Test]
        public void RecordDelta_AttributesTheBonusToTheSource_ForTheDieChip()
        {
            var ench = MakeMovementEnchantment("ench.torbellino");
            var scratch = new EnchantmentScratch();
            var before = ScratchSnapshot.Of(scratch, Lane);

            new EffAddMovementDieBonus { Amount = 2 }.ApplyEffect(Ctx(scratch));
            ScratchSnapshot.RecordDelta(scratch, in before, ScratchSourceKind.Enchantment, ench.name, ench, Lane);

            Assert.AreEqual(1, scratch.Journal.Count);
            var entry = scratch.Journal[0];
            Assert.AreSame(ench, entry.SourceAsset);
            Assert.AreEqual(2, entry.MovementDieBonusDelta);
            Assert.AreEqual(0, entry.BonusDelta, "no toca el bono de combo");
            Assert.AreEqual(Lane, entry.BagSlot);
        }

        [Test]
        public void RecordDelta_NeutralSource_DoesNotJournalTheBonus()
        {
            var scratch = new EnchantmentScratch { MovementDieBonus = 2 };
            var before = ScratchSnapshot.Of(scratch);

            ScratchSnapshot.RecordDelta(scratch, in before, ScratchSourceKind.Enchantment, "ench.otro", null, Lane);

            Assert.IsNull(scratch.Journal, "sin delta no hay entrada");
        }

        [Test]
        public void Reset_ClearsTheBonus()
        {
            var scratch = new EnchantmentScratch { MovementDieBonus = 2 };

            scratch.Reset();

            Assert.AreEqual(0, scratch.MovementDieBonus);
        }
    }
}
