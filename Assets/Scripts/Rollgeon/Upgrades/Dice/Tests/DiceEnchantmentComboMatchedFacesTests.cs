using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.Readers;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Fix#0081: el preview de combo (<c>ComboMatched</c>) dispara en cada toggle de hold,
    /// ANTES del <c>OnRollResolved</c> de esa tirada. El service leía las caras de
    /// <c>_lastFinalRoll</c> — la tirada de la acción ANTERIOR — y Oxidado / Volátil mutaban
    /// una cara vieja (o ninguna en la primera tirada del combate). Las caras vigentes ahora
    /// viajan en <see cref="ComboMatchedPayload.DiceResult"/>.
    /// </summary>
    [TestFixture]
    public class DiceEnchantmentComboMatchedFacesTests
    {
        private DiceEnchantmentService _svc;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
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
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
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

        /// <summary>
        /// Misma composición que Ench_Oxidado / Ench_Volatil: ComboMatched + AnyCombo +
        /// RequireCarrierParticipates + EffMutateCarrierFace(ReadCarrierRollDelta(op)).
        /// </summary>
        private EnchantmentSO MakeFaceMutator(string id, CarrierRollDeltaOp op)
        {
            var group = new EffectData();
            group.Effects.Add(new EffMutateCarrierFace { Delta = new ReadCarrierRollDelta { Op = op } });

            var bridge = new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                Filter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                RequireCarrierParticipates = true,
            };
            bridge.Effects.Add(group);

            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>());
            typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentTrigger> { bridge });
            return ench;
        }

        /// <summary>Bag de 2 D6 con el mutador en el slot 0 (el carrier).</summary>
        private void SetUpCarrierAt0(CarrierRollDeltaOp op)
        {
            _svc.InitializeFromBag(MakeBag(DiceType.D6, DiceType.D6));
            Assert.IsTrue(_svc.Apply(0, MakeFaceMutator("e-face-" + op, op)).Success);
        }

        /// <summary>Simula la tirada de la acción ANTERIOR ya ejecutada (lo que cachea el service).</summary>
        private static void ResolvePreviousRoll(params int[] faces)
        {
            EventManager.Trigger(EventName.OnRollResolved, Guid.NewGuid(), (IReadOnlyList<int>)faces,
                RollActionKind.Attack);
        }

        /// <summary>Preview de la tirada EN CURSO: ambos slots forman el combo.</summary>
        private static ComboMatchedPayload PreviewOf(int[] currentFaces, string comboId = "combo.par",
            bool includeFaces = true)
        {
            var contributing = new List<ContributingDie>();
            for (int slot = 0; slot < currentFaces.Length; slot++)
                contributing.Add(new ContributingDie(slot, currentFaces[slot], DiceType.D6));

            return new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = comboId,
                BaseDamage = 10,
                ContributingDice = contributing,
                DiceResult = includeFaces ? currentFaces : null,
            };
        }

        // ================================================================
        // Tests
        // ================================================================

        [Test]
        public void ComboMatched_Oxidado_ExcludesCurrentFace_NotThePreviousRollsFace()
        {
            // Arrange — la acción anterior tiró 3; la tirada en curso tiene un 6.
            SetUpCarrierAt0(CarrierRollDeltaOp.Exclude);
            ResolvePreviousRoll(3, 2);

            // Act
            TypedEvent<ComboMatchedPayload>.Raise(PreviewOf(new[] { 6, 6 }));

            // Assert — "no suma" es −6 (la cara vigente), no −3 (la cara que jugó antes).
            Assert.IsNotNull(_svc.LastComboScratch);
            Assert.AreEqual(-6, _svc.LastComboScratch.GetFaceDelta(0));
        }

        [Test]
        public void ComboMatched_FirstRollOfCombat_MutatesFace_WithoutAnyResolvedRoll()
        {
            // Arrange — primera tirada: el service nunca vio un OnRollResolved.
            SetUpCarrierAt0(CarrierRollDeltaOp.Exclude);

            // Act
            TypedEvent<ComboMatchedPayload>.Raise(PreviewOf(new[] { 6, 6 }));

            // Assert — antes del fix el delta era 0 (DiceResult null ⇒ el reader devolvía 0).
            Assert.AreEqual(-6, _svc.LastComboScratch.GetFaceDelta(0));
        }

        [Test]
        public void ComboMatched_Volatil_HalvesCurrentFace_EvenIfPreviousRollWasMax()
        {
            // Arrange — la acción anterior sacó 6 (doble); ahora el dado muestra 3 (mitad).
            SetUpCarrierAt0(CarrierRollDeltaOp.DoubleMaxHalveRest);
            ResolvePreviousRoll(6, 6);

            // Act
            TypedEvent<ComboMatchedPayload>.Raise(PreviewOf(new[] { 3, 3 }));

            // Assert — 3 → ceil(3/2) = 2 ⇒ delta −1. Con la cara vieja hubiera sido +6.
            Assert.AreEqual(-1, _svc.LastComboScratch.GetFaceDelta(0));
        }

        [Test]
        public void ComboMatched_Volatil_DoublesCurrentMaxFace_EvenIfPreviousRollWasLow()
        {
            // Arrange
            SetUpCarrierAt0(CarrierRollDeltaOp.DoubleMaxHalveRest);
            ResolvePreviousRoll(1, 1);

            // Act
            TypedEvent<ComboMatchedPayload>.Raise(PreviewOf(new[] { 6, 6 }));

            // Assert — 6 en un D6 vale doble ⇒ delta +6.
            Assert.AreEqual(6, _svc.LastComboScratch.GetFaceDelta(0));
        }

        [Test]
        public void ComboMatched_PayloadWithoutFaces_FallsBackToLastResolvedRoll()
        {
            // Arrange — emisor legacy (sin DiceResult): se conserva el comportamiento viejo.
            SetUpCarrierAt0(CarrierRollDeltaOp.Exclude);
            ResolvePreviousRoll(3, 3);

            // Act
            TypedEvent<ComboMatchedPayload>.Raise(PreviewOf(new[] { 3, 3 }, includeFaces: false));

            // Assert
            Assert.AreEqual(-3, _svc.LastComboScratch.GetFaceDelta(0));
        }

        [Test]
        public void ComboMatched_HigherNumber_WithAnyComboFilter_LeavesFaceUntouched()
        {
            // Arrange — decisión GD 2026-09-04: Oxidado / Volátil solo mutan en combos REALES;
            // en Número Alto el dado vale su cara.
            SetUpCarrierAt0(CarrierRollDeltaOp.Exclude);

            // Act
            TypedEvent<ComboMatchedPayload>.Raise(PreviewOf(new[] { 6, 6 }, comboId: ComboId.HigherNumber));

            // Assert
            Assert.IsNotNull(_svc.LastComboScratch);
            Assert.AreEqual(0, _svc.LastComboScratch.GetFaceDelta(0));
        }
    }
}
