using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.Readers;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// <see cref="EffAddTemporaryModifier"/>: bono aditivo sobre Attack / MoveRange que muere
    /// solo en el siguiente <c>OnTurnFinished</c> (Carga, Torbellino).
    /// </summary>
    [TestFixture]
    public class EffAddTemporaryModifierTests
    {
        private AttributesManager _attrs;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _player = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Attack>(new Attack(10));
            attrs.SetAttribute<MoveRange>(new MoveRange(0));
            _attrs.Register(_player, attrs);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Run);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private EffectContext Ctx(int tiles = 0, int tilesThisTurn = 0)
        {
            return new EffectContext
            {
                SourceGuid = _player,
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(DiceType.D6, EnchantmentSlotRef.MovementDieSlot, 0),
                    Channel = Upgrades.ScratchChannel.DiceEnchantment,
                    TilesTraversed = tiles,
                    TilesTraversedThisTurn = tilesThisTurn,
                },
            };
        }

        [Test]
        public void Carga_AddsAttackPerTileUntilTheTurnEnds()
        {
            var eff = new EffAddTemporaryModifier
            {
                Stat = TemporaryModifierStat.Attack,
                Reader = new ReadTilesTraversed { Multiplier = 1 },
                DurationTurns = 1,
            };

            Assert.IsTrue(eff.ApplyEffect(Ctx(tiles: 3, tilesThisTurn: 3)));
            Assert.AreEqual(13, _attrs.GetAttributeModifiedValue<Attack, int>(_player));

            Assert.IsTrue(eff.ApplyEffect(Ctx(tiles: 2, tilesThisTurn: 5)));
            Assert.AreEqual(15, _attrs.GetAttributeModifiedValue<Attack, int>(_player), "Dos movimientos suman.");

            EventManager.Trigger(EventName.OnTurnFinished, _player);
            Assert.AreEqual(10, _attrs.GetAttributeModifiedValue<Attack, int>(_player), "Muere al fin del turno.");
            Assert.AreEqual(10, _attrs.GetAttributeValue<Attack, int>(_player), "El raw nunca cambió.");
        }

        [Test]
        public void Torbellino_AddsTwoMoveRangeForTheAction()
        {
            var eff = new EffAddTemporaryModifier
            {
                Stat = TemporaryModifierStat.MoveRange,
                Amount = 2,
                DurationTurns = 1,
                OnlyFirstCopy = true,
            };

            Assert.IsTrue(eff.ApplyEffect(Ctx()));
            Assert.AreEqual(2, _attrs.GetAttributeModifiedValue<MoveRange, int>(_player));

            EventManager.Trigger(EventName.OnTurnFinished, _player);
            Assert.AreEqual(0, _attrs.GetAttributeModifiedValue<MoveRange, int>(_player));
        }

        [Test]
        public void ZeroAmount_IsANoOp()
        {
            var eff = new EffAddTemporaryModifier { Stat = TemporaryModifierStat.Attack, Amount = 0 };

            Assert.IsTrue(eff.ApplyEffect(Ctx()));
            Assert.AreEqual(10, _attrs.GetAttributeModifiedValue<Attack, int>(_player));
        }
    }
}
