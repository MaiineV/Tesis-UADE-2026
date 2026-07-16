using System;
using NUnit.Framework;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Units puros de los concretos genéricos <see cref="AddGoldOnRoomEntered"/> y
    /// <see cref="AddShieldOnTurnStart"/> — sin service ni ServiceLocator, solo
    /// trigger + context + scratch.
    /// </summary>
    [TestFixture]
    public class GenericConcreteTriggerTests
    {
        private static ComboPassiveContext MakeContext(string roomId = null)
        {
            return new ComboPassiveContext
            {
                Effect = new EffectContext(),
                Scratch = new EnchantmentScratch(),
                RoomId = roomId,
                RoomInstanceId = Guid.NewGuid(),
            };
        }

        // ---- AddGoldOnRoomEntered -------------------------------------

        [Test]
        public void AddGoldOnRoomEntered_NoFilter_AddsToScratchBonusGold()
        {
            var trigger = new AddGoldOnRoomEntered { Amount = new ReadConstantInt { Value = 3 } };
            var ctx = MakeContext(roomId: "room.shop");

            trigger.OnRoomEntered(ctx);

            Assert.AreEqual(3, ctx.Scratch.BonusGold);
        }

        [Test]
        public void AddGoldOnRoomEntered_MatchingRoomFilter_Adds()
        {
            var trigger = new AddGoldOnRoomEntered
            {
                Amount = new ReadConstantInt { Value = 2 },
                RoomIdFilter = "room.shop",
            };
            var ctx = MakeContext(roomId: "room.shop");

            trigger.OnRoomEntered(ctx);

            Assert.AreEqual(2, ctx.Scratch.BonusGold);
        }

        [Test]
        public void AddGoldOnRoomEntered_NonMatchingRoomFilter_NoOp()
        {
            var trigger = new AddGoldOnRoomEntered
            {
                Amount = new ReadConstantInt { Value = 2 },
                RoomIdFilter = "room.shop",
            };
            var ctx = MakeContext(roomId: "room.combat");

            trigger.OnRoomEntered(ctx);

            Assert.AreEqual(0, ctx.Scratch.BonusGold);
        }

        [Test]
        public void AddGoldOnRoomEntered_NullScratch_NoThrow()
        {
            var trigger = new AddGoldOnRoomEntered { Amount = new ReadConstantInt { Value = 2 } };
            var ctx = new ComboPassiveContext { Effect = new EffectContext(), Scratch = null };

            Assert.DoesNotThrow(() => trigger.OnRoomEntered(ctx));
        }

        [Test]
        public void AddGoldOnRoomEntered_NullAmount_AddsZero()
        {
            var trigger = new AddGoldOnRoomEntered { Amount = null };
            var ctx = MakeContext(roomId: "room.shop");

            trigger.OnRoomEntered(ctx);

            Assert.AreEqual(0, ctx.Scratch.BonusGold);
        }

        // ---- AddShieldOnTurnStart -------------------------------------

        [Test]
        public void AddShieldOnTurnStart_AddsToScratchBonusShield()
        {
            var trigger = new AddShieldOnTurnStart { Amount = new ReadConstantInt { Value = 1 } };
            var ctx = MakeContext();

            trigger.OnTurnStarted(ctx);

            Assert.AreEqual(1, ctx.Scratch.BonusShield);
        }

        [Test]
        public void AddShieldOnTurnStart_NullScratch_NoThrow()
        {
            var trigger = new AddShieldOnTurnStart { Amount = new ReadConstantInt { Value = 1 } };
            var ctx = new ComboPassiveContext { Effect = new EffectContext(), Scratch = null };

            Assert.DoesNotThrow(() => trigger.OnTurnStarted(ctx));
        }
    }
}
