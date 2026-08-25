using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Damage;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests de <see cref="ExecuteEffectsOnDiceEvent"/> — el puente data-driven del
    /// canal dados: gate por evento, ComboFilter, participación del carrier, contexto
    /// fresco por grupo, y ruteo del scratch del dispatch.
    /// </summary>
    [TestFixture]
    public class ExecuteEffectsOnDiceEventTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();

            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static EnchantmentTriggerContext BuildCtx(
            string comboId = null,
            int[] faces = null,
            int carrierIndex = 0,
            ComboDetectionResult? combo = null,
            int[] keptMap = null)
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = Guid.NewGuid(),
                    DiceResult = faces ?? new[] { 2, 2, 5 },
                    KeptDiceOriginalIndices = keptMap,
                    ComboResult = combo,
                },
                Scratch = new EnchantmentScratch(),
                Slot = new EnchantmentSlotRef(DiceType.D6, carrierIndex, 0),
                ComboId = comboId,
            };
        }

        private static ExecuteEffectsOnDiceEvent BuildBridge(
            EnchantmentHookEvent evt, params EffectData[] groups)
        {
            return new ExecuteEffectsOnDiceEvent
            {
                Event = evt,
                Effects = new List<EffectData>(groups),
            };
        }

        private static EffectData Group(params IEffect[] effects)
        {
            var data = new EffectData();
            foreach (var eff in effects) data.Effects.Add(eff);
            return data;
        }

        private sealed class CountingEffect : BaseEffect
        {
            public int Applies;
            public bool ReturnValue = true;
            protected override bool ShowSelection => false;

            public override bool ApplyEffect(EffectContext context)
            {
                Applies++;
                return ReturnValue;
            }
        }

        // ================================================================
        // Gate por evento + filtro
        // ================================================================

        [Test]
        public void RunIf_OnlyConfiguredEventFires()
        {
            // Arrange
            var effect = new CountingEffect();
            var bridge = BuildBridge(EnchantmentHookEvent.DiceRolled, Group(effect));

            // Act
            bridge.OnDiceRolled(BuildCtx());
            bridge.OnRollResolved(BuildCtx());
            bridge.OnTurnFinished(BuildCtx());

            // Assert
            Assert.AreEqual(1, effect.Applies);
        }

        [Test]
        public void ComboMatched_RespectsComboFilter()
        {
            // Arrange
            var effect = new CountingEffect();
            var bridge = BuildBridge(EnchantmentHookEvent.ComboMatched, Group(effect));
            bridge.Filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { "combo.par" },
            };

            // Act
            bridge.OnComboMatched(BuildCtx("combo.trio"));
            bridge.OnComboMatched(BuildCtx("combo.par"));

            // Assert
            Assert.AreEqual(1, effect.Applies);
        }

        // ================================================================
        // Scratch del dispatch + PCs con Effect
        // ================================================================

        [Test]
        public void ComboScratchWriter_WritesToDispatchScratch()
        {
            // Arrange
            var bridge = BuildBridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 7 } }));
            var ctx = BuildCtx("combo.par");

            // Act
            bridge.OnComboMatched(ctx);

            // Assert
            Assert.AreEqual(7, ctx.Scratch.BonusComboDamage);
        }

        [Test]
        public void Groups_RunWithFreshContext_NoCrossShortCircuit()
        {
            // Arrange — el false del grupo 1 no debe frenar al grupo 2.
            var failing = new CountingEffect { ReturnValue = false };
            var second = new CountingEffect();
            var bridge = BuildBridge(EnchantmentHookEvent.RollResolved, Group(failing), Group(second));

            // Act
            bridge.OnRollResolved(BuildCtx());

            // Assert
            Assert.AreEqual(1, failing.Applies);
            Assert.AreEqual(1, second.Applies);
        }

        [Test]
        public void PreConditionContext_CarriesEffectContext_ForRollAwarePcs()
        {
            // Arrange — PcNoComboThisRoll gatea el "oro de consuelo" de Ench_GoldOnRoll.
            var effect = new CountingEffect();
            var group = Group(effect);
            group.PreConditions.Add(new PcNoComboThisRoll());
            var bridge = BuildBridge(EnchantmentHookEvent.RollResolved, group);

            // Act — con combo matcheado NO dispara; sin combo sí.
            bridge.OnRollResolved(BuildCtx(combo: ComboDetectionResult.Match(10, 2)));
            bridge.OnRollResolved(BuildCtx());

            // Assert
            Assert.AreEqual(1, effect.Applies);
        }

        // ================================================================
        // RequireCarrierParticipates
        // ================================================================

        [Test]
        public void RequireCarrierParticipates_FiresOnlyWhenCarrierContributes()
        {
            // Arrange — kept [j0→bag0, j1→bag2]; el combo usa los kept 0 y 1.
            var effect = new CountingEffect();
            var bridge = BuildBridge(EnchantmentHookEvent.ComboMatched, Group(effect));
            bridge.RequireCarrierParticipates = true;
            var combo = ComboDetectionResult.Match("combo.par", baseDamage: 10, countUsed: 2,
                contributingIndices: new[] { 0, 1 });

            // Act — carrier en bag 0 participa; carrier en bag 1 no.
            bridge.OnComboMatched(BuildCtx("combo.par", carrierIndex: 0, combo: combo, keptMap: new[] { 0, 2 }));
            bridge.OnComboMatched(BuildCtx("combo.par", carrierIndex: 1, combo: combo, keptMap: new[] { 0, 2 }));

            // Assert
            Assert.AreEqual(1, effect.Applies);
        }

        [Test]
        public void RequireCarrierParticipates_MissingContributionData_DoesNotFire()
        {
            // Arrange — resultados sin índices (overload legacy) no permiten verificar.
            var effect = new CountingEffect();
            var bridge = BuildBridge(EnchantmentHookEvent.ComboMatched, Group(effect));
            bridge.RequireCarrierParticipates = true;

            // Act
            bridge.OnComboMatched(BuildCtx("combo.par", combo: ComboDetectionResult.Match(10, 2)));

            // Assert
            Assert.AreEqual(0, effect.Applies);
        }

        // ================================================================
        // Integración vía DiceEnchantmentService (preview channel)
        // ================================================================

        [Test]
        public void ServiceDispatch_ComboMatched_BridgeFeedsLastComboScratch()
        {
            // Arrange
            var svc = new DiceEnchantmentService(config: null);
            svc.SubscribeEventsForTests();
            try
            {
                var bag = ScriptableObject.CreateInstance<DiceBagSO>();
                bag.Dice = new List<DiceType> { DiceType.D6 };
                _created.Add(bag);
                svc.InitializeFromBag(bag);

                var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
                _created.Add(ench);
                typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, "e-bridge");
                typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, new List<DiceType>());
                typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, new List<IEnchantmentTrigger>
                    {
                        BuildBridge(EnchantmentHookEvent.ComboMatched,
                            Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 4 } })),
                    });
                Assert.IsTrue(svc.Apply(0, ench).Success);

                // Act — el preview matchea un combo.
                TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
                {
                    SourceGuid = Guid.NewGuid(),
                    ComboId = "combo.par",
                    BaseDamage = 10,
                });

                // Assert — el bono del bridge queda en el canal at-match (LastComboScratch).
                Assert.IsNotNull(svc.LastComboScratch);
                Assert.AreEqual(4, svc.LastComboScratch.BonusComboDamage);
            }
            finally
            {
                svc.UnsubscribeEventsForTests();
            }
        }

        // ================================================================
        // Regresión — DiceEnchantmentService.OnComboMatchedHandler + carrier-scope
        // ================================================================

        /// <summary>
        /// Arma un service con un bag de 2 dados y un encantamiento con
        /// RequireCarrierParticipates en el bag slot 0 — helper común a los 3 tests
        /// de esta sección (solo cambia el gate y el ContributingDice del payload).
        /// </summary>
        private DiceEnchantmentService BuildServiceWithCarrierAt0(bool requireCarrierParticipates)
        {
            var svc = new DiceEnchantmentService(config: null);
            svc.SubscribeEventsForTests();

            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D6 };
            _created.Add(bag);
            svc.InitializeFromBag(bag);

            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, "e-carrier-scope");
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>());

            var bridge = BuildBridge(EnchantmentHookEvent.ComboMatched,
                Group(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 4 } }));
            bridge.RequireCarrierParticipates = requireCarrierParticipates;
            typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentTrigger> { bridge });

            // El encantamiento vive en el bag slot 0 — ese es el "carrier".
            Assert.IsTrue(svc.Apply(0, ench).Success);
            return svc;
        }

        [Test]
        public void ServiceDispatch_ComboMatched_RequireTrue_CarrierOutsideCombo_DoesNotWriteScratch()
        {
            var svc = BuildServiceWithCarrierAt0(requireCarrierParticipates: true);
            try
            {
                // El combo real solo lo forma el bag slot 1 — el carrier (slot 0) queda afuera.
                var contributingDice = new List<ContributingDie> { new ContributingDie(1, 5, DiceType.D6) };

                TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
                {
                    SourceGuid = Guid.NewGuid(),
                    ComboId = "combo.par",
                    BaseDamage = 10,
                    ContributingDice = contributingDice,
                });

                Assert.IsNotNull(svc.LastComboScratch);
                Assert.AreEqual(0, svc.LastComboScratch.BonusComboDamage,
                    "El carrier no participó del combo — RequireCarrierParticipates debe bloquear el bono.");
            }
            finally
            {
                svc.UnsubscribeEventsForTests();
            }
        }

        [Test]
        public void ServiceDispatch_ComboMatched_RequireTrue_CarrierInsideCombo_WritesScratch()
        {
            var svc = BuildServiceWithCarrierAt0(requireCarrierParticipates: true);
            try
            {
                // El combo lo forman ambos slots, carrier incluido.
                var contributingDice = new List<ContributingDie>
                {
                    new ContributingDie(0, 3, DiceType.D6),
                    new ContributingDie(1, 3, DiceType.D6),
                };

                TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
                {
                    SourceGuid = Guid.NewGuid(),
                    ComboId = "combo.par",
                    BaseDamage = 10,
                    ContributingDice = contributingDice,
                });

                Assert.IsNotNull(svc.LastComboScratch);
                Assert.AreEqual(4, svc.LastComboScratch.BonusComboDamage,
                    "El carrier participó del combo — el bono debe escribirse.");
            }
            finally
            {
                svc.UnsubscribeEventsForTests();
            }
        }

        [Test]
        public void ServiceDispatch_ComboMatched_RequireFalse_FiresRegardlessOfParticipation()
        {
            // Regresión: encantamientos SIN el flag (mayoría del catálogo) deben seguir
            // disparando siempre en ComboMatched, participe o no el carrier — el fix no
            // les cambia el comportamiento.
            var svc = BuildServiceWithCarrierAt0(requireCarrierParticipates: false);
            try
            {
                var contributingDice = new List<ContributingDie> { new ContributingDie(1, 5, DiceType.D6) };

                TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
                {
                    SourceGuid = Guid.NewGuid(),
                    ComboId = "combo.par",
                    BaseDamage = 10,
                    ContributingDice = contributingDice,
                });

                Assert.IsNotNull(svc.LastComboScratch);
                Assert.AreEqual(4, svc.LastComboScratch.BonusComboDamage);
            }
            finally
            {
                svc.UnsubscribeEventsForTests();
            }
        }
    }
}
