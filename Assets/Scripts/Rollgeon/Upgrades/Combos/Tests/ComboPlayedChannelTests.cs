using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Tests del hook <see cref="IOnComboPlayedPassiveTrigger"/> del
    /// <see cref="ComboPassiveService"/>: scope por TargetComboId, escritura al play
    /// scratch (no a LastComboScratch), apply único por el dueño de la ventana, y el
    /// bridge <c>ExecuteEffectsOnEvent(ComboPlayed)</c> + <c>EffAddComboBonus</c>.
    /// </summary>
    [TestFixture]
    public class ComboPlayedChannelTests
    {
        private ComboPassiveService _svc;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            SaveSystem.ResetForTests();

            _svc = new ComboPassiveService();
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

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void TriggerRunStart()
        {
            SaveSystem.Clear();
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "test-ruleset");
        }

        private ComboPassiveSO MakePassive(string id, string targetComboId, params IComboPassiveTrigger[] triggers)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            passive.name = id;
            _created.Add(passive);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, id);
            typeof(ComboPassiveSO).GetField("_targetComboId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, targetComboId);
            typeof(ComboPassiveSO).GetField("_extraTriggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, new List<IComboPassiveTrigger>(triggers));
            return passive;
        }

        private static void AddToRunState(ComboPassiveSO passive)
        {
            ServiceLocator.GetService<RunComboPassivesState>().Add(passive);
        }

        private static ComboPlayedPayload BuildPayload(Guid source, string comboId)
        {
            return new ComboPlayedPayload
            {
                SourceGuid = source,
                ComboId = comboId,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
                DiceResult = new[] { 2, 2, 5 },
            };
        }

        private static EffectContext BuildPlayContext(Guid source, string comboId)
        {
            return new EffectContext
            {
                SourceGuid = source,
                DiceResult = new[] { 2, 2, 5 },
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
            };
        }

        // ---- Stubs ----------------------------------------------------

        private sealed class RecordingPlayedTrigger : IOnComboPlayedPassiveTrigger
        {
            public readonly List<ComboPassiveContext> Calls = new List<ComboPassiveContext>();
            public void OnComboPlayed(ComboPassiveContext ctx) => Calls.Add(ctx);
        }

        private sealed class WriteScratchOnPlayedTrigger : IOnComboPlayedPassiveTrigger
        {
            public int BonusDamage;
            public int Gold;

            public void OnComboPlayed(ComboPassiveContext ctx)
            {
                if (BonusDamage != 0) ctx.Scratch.BonusComboDamage += BonusDamage;
                if (Gold != 0) ctx.Scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, Gold);
            }
        }

        private sealed class CountingEconomyStub : IEconomyService
        {
            public int CurrentGold { get; private set; }
            public int AddCalls { get; private set; }

            public void Add(int amount)
            {
                if (amount <= 0) return;
                CurrentGold += amount;
                AddCalls++;
            }

            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }

            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }

        // ================================================================
        // Scope por TargetComboId
        // ================================================================

        [Test]
        public void ComboPlayed_DispatchesToMatchingAndGenericPassives_NotOthers()
        {
            TriggerRunStart();
            var parTrigger = new RecordingPlayedTrigger();
            var trioTrigger = new RecordingPlayedTrigger();
            var genericTrigger = new RecordingPlayedTrigger();
            AddToRunState(MakePassive("p-par", "combo.par", parTrigger));
            AddToRunState(MakePassive("p-trio", "combo.trio", trioTrigger));
            AddToRunState(MakePassive("p-generic", null, genericTrigger));

            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload(Guid.NewGuid(), "combo.par"));

            Assert.AreEqual(1, parTrigger.Calls.Count, "La pasiva del combo jugado debe dispatchar.");
            Assert.AreEqual(0, trioTrigger.Calls.Count, "Una pasiva de OTRO combo no debe dispatchar.");
            Assert.AreEqual(1, genericTrigger.Calls.Count, "TargetComboId vacío = cualquier combo.");
            Assert.AreEqual("combo.par", parTrigger.Calls[0].ComboId);
        }

        [Test]
        public void ComboPlayed_DoesNotTouchLastComboScratch()
        {
            TriggerRunStart();
            var trigger = new WriteScratchOnPlayedTrigger { BonusDamage = 4 };
            AddToRunState(MakePassive("p1", "combo.par", trigger));

            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload(Guid.NewGuid(), "combo.par"));

            Assert.IsNull(_svc.LastComboScratch,
                "El canal at-played no debe tocar LastComboScratch (contrato del preview).");
        }

        // ================================================================
        // Scratch: local (sin play service) vs play scratch (con service)
        // ================================================================

        [Test]
        public void ComboPlayed_WithoutPlayService_AppliesLocalScratchOnce()
        {
            TriggerRunStart();
            var economy = new CountingEconomyStub();
            ServiceLocator.AddService<IEconomyService>(economy);
            var trigger = new WriteScratchOnPlayedTrigger { Gold = 5 };
            AddToRunState(MakePassive("p1", "combo.par", trigger));

            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload(Guid.NewGuid(), "combo.par"));

            Assert.AreEqual(5, economy.CurrentGold);
            Assert.AreEqual(1, economy.AddCalls, "Sin play service, el handler aplica su scratch local una vez.");
        }

        [Test]
        public void ComboPlayed_WithPlayService_WritesToPlayScratch_AndOwnerAppliesOnce()
        {
            TriggerRunStart();
            var economy = new CountingEconomyStub();
            ServiceLocator.AddService<IEconomyService>(economy);
            var play = new ComboPlayService();
            play.Register();
            var trigger = new WriteScratchOnPlayedTrigger { BonusDamage = 4, Gold = 5 };
            AddToRunState(MakePassive("p1", "combo.par", trigger));

            // BeginPlay emite el payload sincrónico; el handler escribe al play scratch
            // y el DUEÑO (ComboPlayService) aplica los recursos una única vez.
            play.BeginPlay(BuildPlayContext(Guid.NewGuid(), "combo.par"));

            Assert.AreEqual(4, play.CurrentPlayScratch.BonusComboDamage,
                "El bono debe quedar en el play scratch, listo para PlayerComboDamage.Resolve.");
            Assert.AreEqual(5, economy.CurrentGold);
            Assert.AreEqual(1, economy.AddCalls, "Un solo apply: el del dueño de la ventana.");
            Assert.IsNull(_svc.LastComboScratch);

            play.EndPlay();
            play.Dispose();
        }

        // ================================================================
        // Bridge data-driven: ExecuteEffectsOnEvent(ComboPlayed) + EffAddComboBonus
        // ================================================================

        [Test]
        public void ExecuteEffectsOnEvent_ComboPlayed_EffAddComboBonus_LandsInPlayScratch()
        {
            TriggerRunStart();
            var play = new ComboPlayService();
            play.Register();
            var bridge = new Triggers.Concretes.ExecuteEffectsOnEvent
            {
                Event = Triggers.Concretes.ComboPassiveHookEvent.ComboPlayed,
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect>
                        {
                            new EffAddComboBonus { Amount = new ReadConstantInt { Value = 7 } },
                        },
                    },
                },
            };
            AddToRunState(MakePassive("p1", "combo.par", bridge));

            play.BeginPlay(BuildPlayContext(Guid.NewGuid(), "combo.par"));

            Assert.AreEqual(7, play.CurrentPlayScratch.BonusComboDamage,
                "El EffAddComboBonus autorado en la pasiva debe escribir al play scratch vía ScratchTriggerContext.");

            play.EndPlay();
            play.Dispose();
        }
    }
}
