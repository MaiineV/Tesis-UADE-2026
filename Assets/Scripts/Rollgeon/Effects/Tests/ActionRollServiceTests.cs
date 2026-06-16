using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class ActionRollServiceTests
    {
        private FakeRollerForActionRoll _roller;
        private FakeEnergyForActionRoll _energy;
        private ActionRollService _service;
        private DiceBagSO _bag;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            _roller = new FakeRollerForActionRoll();
            _energy = new FakeEnergyForActionRoll();
            _service = new ActionRollService(_roller, _energy);

            // El guard de StartFlow chequea que Dice no sea null/empty — el roller fake
            // ignora el contenido y devuelve la secuencia preprogramada igual.
            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.Dice = new List<DiceType>(5) { default, default, default, default, default };

            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            if (_bag != null) UnityEngine.Object.DestroyImmediate(_bag);
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void RequireConfirm_StopsInAwaitingConfirm_UntilConfirm()
        {
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoor(), _player, _bag, o => captured = o);

            Assert.AreEqual(ActionRollPhase.AwaitingConfirm, _service.Phase);
            Assert.AreEqual(0, _energy.SpendCalls); // todavia no cobro

            _roller.NextRoll = new[] { 3, 3, 2, 1, 1 }; // sum = 10, threshold 10
            _service.Confirm();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);

            // Nuevo modelo: el user holdea dados para que cuenten en el combo +
            // sum. Sin holds el effective = 0. Holdeamos todos para sumar 10.
            _service.SetHolds(new[] { true, true, true, true, true });
            _service.Confirm();

            Assert.IsFalse(captured.Cancelled);
            Assert.IsTrue(captured.PassedThreshold);
            Assert.AreEqual(2, _energy.SpendCalls); // base cost cobrado en Confirm
        }

        [Test]
        public void Cancel_FromAwaitingConfirm_ReturnsCancelledOutcome()
        {
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoor(), _player, _bag, o => captured = o);

            _service.Cancel();

            Assert.IsTrue(captured.Cancelled);
            Assert.AreEqual(0, _energy.SpendCalls);
            Assert.AreEqual(ActionRollPhase.Cancelled, _service.Phase);
        }

        [Test]
        public void NoConfirm_FirstRollGoesToAwaitingRerollDecision()
        {
            _roller.NextRoll = new[] { 4, 4, 4, 4, 4 }; // sum 20

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecHeal(), _player, _bag, o => captured = o);

            // Nuevo flow: SIEMPRE espera decision del user (holdear / rerollear /
            // confirmar). NO hay auto-resolve en el initial roll, aunque pase el
            // threshold — eso seria contraproducente para Heal donde el user puede
            // querer rerollear buscando bonus extra.
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(1, _energy.SpendCalls); // base cost ya cobrado

            _service.SetHolds(new[] { true, true, true, true, true });
            _service.Confirm(); // user clickea Confirm → resuelve

            Assert.IsTrue(captured.PassedThreshold);
            Assert.AreEqual(20, captured.FinalSum);
            Assert.AreEqual(1, captured.RollsUsed);
        }

        [Test]
        public void BelowThreshold_OffersReroll_AndChargesOnAccept()
        {
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 }; // sum 5 (< 10)

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(2, _energy.SpendCalls); // base cost ya cobrado

            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 }; // sum 30 post-reroll
            _service.RequestReroll();
            // Despues del reroll, el flow vuelve a AwaitingRerollDecision (el user
            // ve los nuevos dados y decide). NO resuelve directo.
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(3, _energy.SpendCalls); // base + reroll
            Assert.AreEqual(2, _service.RollIndex);

            _service.SetHolds(new[] { true, true, true, true, true });
            _service.Confirm();

            Assert.IsTrue(captured.PassedThreshold);
            Assert.AreEqual(30, captured.FinalSum);
            Assert.AreEqual(2, captured.RollsUsed);
        }

        [Test]
        public void DeclineReroll_ResolvesWithFirstRoll_NoExtraCharge()
        {
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            _service.DeclineReroll();

            Assert.IsFalse(captured.PassedThreshold);
            Assert.AreEqual(5, captured.FinalSum);
            Assert.AreEqual(1, captured.RollsUsed);
            Assert.AreEqual(2, _energy.SpendCalls);
        }

        [Test]
        public void MultipleRerolls_ChargeEachOne_StayInAwaitingDecisionUntilConfirm()
        {
            // Spec: el jugador puede rerollear N veces, gastando 1 energía por reroll,
            // mientras tenga energía suficiente. No hay límite artificial (single-shot).
            _energy.CurrentEnergy = 99;
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            Assert.AreEqual(2, _energy.SpendCalls); // base cost

            // Tres rerolls consecutivos:
            _roller.NextRoll = new[] { 2, 2, 2, 2, 2 };
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(2, _service.RollIndex);
            Assert.AreEqual(3, _energy.SpendCalls); // base + 1 reroll

            _roller.NextRoll = new[] { 3, 3, 3, 3, 3 };
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(3, _service.RollIndex);
            Assert.AreEqual(4, _energy.SpendCalls); // + 1 reroll mas

            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(4, _service.RollIndex);
            Assert.AreEqual(5, _energy.SpendCalls); // + 1 reroll mas

            _service.SetHolds(new[] { true, true, true, true, true });
            _service.Confirm();

            Assert.IsTrue(captured.PassedThreshold);
            Assert.AreEqual(30, captured.FinalSum);
            Assert.AreEqual(4, captured.RollsUsed); // inicial + 3 rerolls
        }

        [Test]
        public void CanAffordReroll_FollowsEnergyAndPhase()
        {
            _energy.CurrentEnergy = 3; // base 2 + 1 alcanza para UN solo reroll
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            // Tras pagar base, queda 1 — alcanza para el primer reroll.
            Assert.IsTrue(_service.CanAffordReroll);

            _roller.NextRoll = new[] { 2, 2, 2, 2, 2 };
            _service.RequestReroll();

            // Energía a 0: el panel debería deshabilitar el botón.
            Assert.IsFalse(_service.CanAffordReroll);

            _service.SetHolds(new[] { true, true, true, true, true });
            _service.Confirm();

            Assert.AreEqual(2, captured.RollsUsed);
        }

        [Test]
        public void Reroll_BlockedByEnergy_ResolvesWhenUserConfirms()
        {
            _energy.CurrentEnergy = 2; // alcanza solo para el base, no para el reroll
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            // Aunque no haya energia para reroll, igual entra a AwaitingRerollDecision
            // (panel muestra Reroll deshabilitado vía CanAffordReroll, solo Confirm).
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.IsFalse(_service.CanAffordReroll);

            // Si user igual intenta RequestReroll (button no debio responder pero
            // defendamos), SpendEnergy falla → resuelve.
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.Resolved, _service.Phase);
            Assert.IsFalse(captured.PassedThreshold);
            Assert.AreEqual(1, captured.RollsUsed);
        }

        [Test]
        public void InsufficientEnergyForBase_CancelsBeforeRolling()
        {
            _energy.CurrentEnergy = 1; // base cost es 2
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            Assert.IsTrue(captured.Cancelled);
            Assert.AreEqual(ActionRollPhase.Cancelled, _service.Phase);
        }

        [Test]
        public void WithComboCatalog_GeneralaRoll_EffectiveTotalUsesComboBaseDamage()
        {
            // Generala BaseDamage = 100. Roll [4,4,4,4,4] sum=20. Threshold 30.
            // Sin combo: 20 < 30 → fallaria. Con combo (formula B): 100 ≥ 30 → pasa.
            var catalog = MakeCatalogWithGenerala(baseDamage: 100);
            var service = new ActionRollService(_roller, _energy, catalog);
            try
            {
                _roller.NextRoll = new[] { 4, 4, 4, 4, 4 };
                ActionRollOutcome captured = default;
                service.StartFlow(SpecForceDoorNoConfirm(threshold: 30), _player, _bag, o => captured = o);

                // Nuevo flow: post-roll va a AwaitingRerollDecision; el user confirma
                // para resolver (no hay auto-resolve aunque pase threshold).
                Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, service.Phase);
                service.SetHolds(new[] { true, true, true, true, true });
                service.Confirm();

                Assert.IsTrue(captured.HasCombo, "Generala debio detectarse.");
                Assert.AreEqual(100, captured.EffectiveTotal,
                    "EffectiveTotal debe ser combo.BaseDamage cuando hay combo (formula B).");
                Assert.AreEqual(20, captured.FinalSum, "FinalSum sigue siendo la suma cruda de pips.");
                Assert.IsTrue(captured.PassedThreshold);
                Assert.AreEqual("combo.generala", captured.ComboId);
            }
            finally
            {
                service.Dispose();
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void WithComboCatalog_NoMatchingCombo_FallsBackToRawSum()
        {
            // Roll [3,4,5,1,2] no es Generala. Catalog solo tiene Generala. Sin match → suma cruda.
            var catalog = MakeCatalogWithGenerala(baseDamage: 100);
            var service = new ActionRollService(_roller, _energy, catalog);
            try
            {
                _roller.NextRoll = new[] { 3, 4, 5, 1, 2 }; // sum 15
                ActionRollOutcome captured = default;
                service.StartFlow(SpecForceDoorNoConfirm(threshold: 30), _player, _bag, o => captured = o);

                // Sum 15 < threshold 30 + AllowReroll=true → entra en AwaitingRerollDecision.
                // Holdeamos todos para que el sum cuente como effective.
                Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, service.Phase);
                service.SetHolds(new[] { true, true, true, true, true });
                service.DeclineReroll();

                Assert.IsFalse(captured.HasCombo);
                Assert.AreEqual(15, captured.EffectiveTotal,
                    "Sin combo, EffectiveTotal cae a la suma cruda de los held dice.");
                Assert.IsFalse(captured.PassedThreshold);
            }
            finally
            {
                service.Dispose();
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        // -------------------------------------------------------------------------
        // BUG-014: si el user holdeó todos los dados, el reroll no movería ningún
        // dado — no debe consumir energía ni avanzar el RollIndex, y CanAffordReroll
        // debe reportar false aunque haya energía suficiente.
        // -------------------------------------------------------------------------

        [Test]
        public void CanAffordReroll_WhenAllDiceHeld_ReturnsFalse()
        {
            // Arrange
            _energy.CurrentEnergy = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);

            // Act
            _service.SetHolds(new[] { true, true, true, true, true });

            // Assert
            Assert.IsFalse(_service.CanAffordReroll,
                "Con todos los dados holdeados, el reroll no tendría efecto — botón debe quedar deshabilitado.");
        }

        [Test]
        public void RequestReroll_WhenAllDiceHeld_DoesNotConsumeEnergy()
        {
            // Arrange
            _energy.CurrentEnergy = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            int spendCallsAfterBase = _energy.SpendCalls;
            int energyAfterBase = _energy.CurrentEnergy;
            int rollIndexBefore = _service.RollIndex;
            _service.SetHolds(new[] { true, true, true, true, true });

            // Act
            _service.RequestReroll();

            // Assert
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase,
                "El reroll bloqueado no debe cambiar la fase.");
            Assert.AreEqual(spendCallsAfterBase, _energy.SpendCalls,
                "No se debe haber cobrado energía en el reroll bloqueado.");
            Assert.AreEqual(energyAfterBase, _energy.CurrentEnergy);
            Assert.AreEqual(rollIndexBefore, _service.RollIndex,
                "RollIndex no debe avanzar — no hubo tirada.");
        }

        // ----- helpers para tests con combo ---------------------------------

        private static ComboCatalogSO MakeCatalogWithGenerala(int baseDamage)
        {
            var generala = ScriptableObject.CreateInstance<Combo_Generala>();
            // _comboId, _displayName, _baseDamage son protected — uso reflection.
            SetField(generala, "_comboId", "combo.generala");
            SetField(generala, "_displayName", "Generala");
            SetField(generala, "_baseDamage", baseDamage);
            SetField(generala, "_valueMultipliers", new float[6]);
            SetField(generala, "_generalMultiplier", 1f);

            var catalog = ScriptableObject.CreateInstance<ComboCatalogSO>();
            // BaseCatalogSO expone Entries pero el setter es probable que sea privado;
            // uso reflection sobre el campo serializado interno.
            SetField(catalog, "_entries", new System.Collections.Generic.List<BaseComboSO> { generala });
            return catalog;
        }

        private static void SetField(object instance, string name, object value)
        {
            var t = instance.GetType();
            while (t != null)
            {
                var f = t.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) { f.SetValue(instance, value); return; }
                t = t.BaseType;
            }
            Assert.Fail($"Field '{name}' not found on {instance.GetType().Name}.");
        }

        // -------------------------------------------------------------------------
        // Specs
        // -------------------------------------------------------------------------

        private static ActionRollSpec SpecForceDoor() => new ActionRollSpec
        {
            EnergyCost = 2,
            Threshold = 10,
            RequireConfirm = true,
            ActionLabel = "Forzar Puerta",
            AllowReroll = true,
            RerollEnergyCost = 1,
            AlwaysSucceeds = false,
        };

        private static ActionRollSpec SpecForceDoorNoConfirm(int threshold = 10) => new ActionRollSpec
        {
            EnergyCost = 2,
            Threshold = threshold,
            RequireConfirm = false,
            ActionLabel = "Forzar Puerta",
            AllowReroll = true,
            RerollEnergyCost = 1,
            AlwaysSucceeds = false,
        };

        private static ActionRollSpec SpecHeal() => new ActionRollSpec
        {
            EnergyCost = 1,
            Threshold = 15,
            RequireConfirm = false,
            ActionLabel = "Curarse",
            AllowReroll = true,
            RerollEnergyCost = 1,
            AlwaysSucceeds = true,
        };

        // -------------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------------

        private sealed class FakeRollerForActionRoll : IDiceRoller
        {
            public int[] NextRoll = new[] { 1, 1, 1, 1, 1 };

            public int[] RollAll(DiceBagSO bag)
            {
                var copy = new int[NextRoll.Length];
                Array.Copy(NextRoll, copy, NextRoll.Length);
                return copy;
            }

            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
            {
                return RollAll(bag);
            }
        }

        private sealed class FakeEnergyForActionRoll : IEnergyService
        {
            public int CurrentEnergy = 99;
            public int SpendCalls;

            public bool SpendEnergy(Guid id, int cost)
            {
                if (cost > CurrentEnergy) return false;
                CurrentEnergy -= cost;
                SpendCalls += cost;
                return true;
            }

            public int GetCurrent(Guid id) => CurrentEnergy;
            public int GetMax(Guid id) => 99;
            public void InitializeForEntity(Guid id) { }
            public void RegenerateAtTurnEnd(Guid id) { }
        }
    }
}
