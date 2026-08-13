using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests del hook <see cref="IOnComboPlayedTrigger"/> en el
    /// <see cref="DiceEnchantmentService"/>: dispatch con contexto autosuficiente del
    /// payload, escritura al play scratch y no-interferencia con LastComboScratch.
    /// </summary>
    [TestFixture]
    public class DiceEnchantmentComboPlayedTests
    {
        private DiceEnchantmentService _svc;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();

            _svc = new DiceEnchantmentService(config: null);
            _svc.SubscribeEventsForTests();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.UnsubscribeEventsForTests();
            _svc = null;

            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();

            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private DiceBagSO MakeBag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>(dice);
            bag.name = "TestBag";
            _created.Add(bag);
            return bag;
        }

        private EnchantmentSO MakeEnchantment(string id, params IEnchantmentTrigger[] triggers)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);

            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>());
            typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentTrigger>(triggers));
            return ench;
        }

        private static ComboPlayedPayload BuildPayload(string comboId)
        {
            return new ComboPlayedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = comboId,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
                DiceResult = new[] { 2, 2, 5 },
                KeptDice = new[] { 2, 2 },
                KeptDiceOriginalIndices = new[] { 0, 1 },
            };
        }

        private sealed class RecordingPlayedTrigger : IOnComboPlayedTrigger
        {
            public readonly List<(string ComboId, EnchantmentSlotRef Slot, EffectContext Effect)> Calls
                = new List<(string, EnchantmentSlotRef, EffectContext)>();

            public void OnComboPlayed(EnchantmentTriggerContext ctx)
                => Calls.Add((ctx.ComboId, ctx.Slot, ctx.Effect));
        }

        private sealed class BonusOnPlayedTrigger : IOnComboPlayedTrigger
        {
            public int Bonus;
            public void OnComboPlayed(EnchantmentTriggerContext ctx) => ctx.Scratch.BonusComboDamage += Bonus;
        }

        // ================================================================
        // Tests
        // ================================================================

        [Test]
        public void ComboPlayed_DispatchesToEnchantmentTriggers_WithPayloadContext()
        {
            // Arrange
            _svc.InitializeFromBag(MakeBag(DiceType.D6));
            var trigger = new RecordingPlayedTrigger();
            Assert.IsTrue(_svc.Apply(0, 0, MakeEnchantment("e1", trigger)).Success);

            // Act
            var payload = BuildPayload("combo.par");
            TypedEvent<ComboPlayedPayload>.Raise(payload);

            // Assert — contexto autosuficiente del payload (no del _lastFinalRoll).
            Assert.AreEqual(1, trigger.Calls.Count);
            Assert.AreEqual("combo.par", trigger.Calls[0].ComboId);
            Assert.AreEqual(0, trigger.Calls[0].Slot.BagSlotIndex);
            CollectionAssert.AreEqual(new[] { 2, 2, 5 }, trigger.Calls[0].Effect.DiceResult);
            CollectionAssert.AreEqual(new[] { 0, 1 }, trigger.Calls[0].Effect.KeptDiceOriginalIndices);
        }

        [Test]
        public void ComboPlayed_EmptyComboId_DoesNotDispatch()
        {
            // Arrange
            _svc.InitializeFromBag(MakeBag(DiceType.D6));
            var trigger = new RecordingPlayedTrigger();
            Assert.IsTrue(_svc.Apply(0, 0, MakeEnchantment("e1", trigger)).Success);

            // Act
            TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload { ComboId = "" });

            // Assert
            Assert.AreEqual(0, trigger.Calls.Count);
        }

        [Test]
        public void ComboPlayed_WithPlayService_WritesToPlayScratch_NotLastComboScratch()
        {
            // Arrange
            _svc.InitializeFromBag(MakeBag(DiceType.D6));
            Assert.IsTrue(_svc.Apply(0, 0, MakeEnchantment("e1", new BonusOnPlayedTrigger { Bonus = 3 })).Success);
            var play = new ComboPlayService();
            play.Register();

            // Act — la ventana emite; el service de dados escribe al play scratch.
            play.BeginPlay(new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                DiceResult = new[] { 2, 2, 5 },
                ComboResult = ComboDetectionResult.Match("combo.par", baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
            });

            // Assert
            Assert.AreEqual(3, play.CurrentPlayScratch.BonusComboDamage);
            Assert.IsNull(_svc.LastComboScratch,
                "El canal at-played no debe tocar LastComboScratch (contrato del preview).");

            play.EndPlay();
            play.Dispose();
        }
    }
}
