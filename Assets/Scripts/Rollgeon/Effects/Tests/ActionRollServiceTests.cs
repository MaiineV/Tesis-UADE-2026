using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Rolls;
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
        private FakeRollsForActionRoll _energy;
        private ActionRollService _service;
        private DiceBagSO _bag;
        private Guid _player;
        private bool _savedKeepSelected;

        [SetUp]
        public void SetUp()
        {
            // El mapeo selección→keep depende del modo persistido en PlayerPrefs:
            // pin al default (invertido) y restore en TearDown.
            _savedKeepSelected = RerollSelectionPrefs.KeepSelected;
            RerollSelectionPrefs.KeepSelected = false;

            _roller = new FakeRollerForActionRoll();
            _energy = new FakeRollsForActionRoll();
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
            RerollSelectionPrefs.KeepSelected = _savedKeepSelected;
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
            Assert.AreEqual(1, _energy.SpendCalls); // base roll cobrado en Confirm
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
            Assert.AreEqual(1, _energy.SpendCalls); // base roll ya cobrado

            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 }; // sum 30 post-reroll
            // Reroll invertido: se re-tiran los dados SELECCIONADOS — para re-tirar
            // la mano entera hay que seleccionarla entera.
            _service.SetHolds(new[] { true, true, true, true, true });
            _service.RequestReroll();
            // Despues del reroll, el flow vuelve a AwaitingRerollDecision (el user
            // ve los nuevos dados y decide). NO resuelve directo.
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(2, _energy.SpendCalls); // base + reroll
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
            Assert.AreEqual(1, _energy.SpendCalls);
        }

        [Test]
        public void MultipleRerolls_ChargeEachOne_StayInAwaitingDecisionUntilConfirm()
        {
            // Spec: el jugador puede rerollear N veces, gastando 1 roll por reroll,
            // mientras tenga rolls en el pool. No hay límite artificial (single-shot).
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            Assert.AreEqual(1, _energy.SpendCalls); // base roll

            // Tres rerolls consecutivos. Reroll invertido: cada reroll consume la
            // selección, así que hay que re-seleccionar la mano antes de cada uno.
            _roller.NextRoll = new[] { 2, 2, 2, 2, 2 };
            _service.SetHolds(new[] { true, true, true, true, true });
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(2, _service.RollIndex);
            Assert.AreEqual(2, _energy.SpendCalls); // base + 1 reroll

            _roller.NextRoll = new[] { 3, 3, 3, 3, 3 };
            _service.SetHolds(new[] { true, true, true, true, true });
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(3, _service.RollIndex);
            Assert.AreEqual(3, _energy.SpendCalls); // + 1 reroll mas

            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.SetHolds(new[] { true, true, true, true, true });
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(4, _service.RollIndex);
            Assert.AreEqual(4, _energy.SpendCalls); // + 1 reroll mas

            _service.SetHolds(new[] { true, true, true, true, true });
            _service.Confirm();

            Assert.IsTrue(captured.PassedThreshold);
            Assert.AreEqual(30, captured.FinalSum);
            Assert.AreEqual(4, captured.RollsUsed); // inicial + 3 rerolls
        }

        [Test]
        public void CanAffordReroll_FollowsPoolAndPhase()
        {
            _energy.CurrentRolls = 2; // base 1 + 1 alcanza para UN solo reroll
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };

            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            // Reroll invertido: CanAffordReroll además exige ≥1 dado seleccionado.
            _service.SetHolds(new[] { true, true, true, true, true });

            // Tras pagar base, queda 1 — alcanza para el primer reroll.
            Assert.IsTrue(_service.CanAffordReroll);

            _roller.NextRoll = new[] { 2, 2, 2, 2, 2 };
            _service.RequestReroll();

            // Pool a 0: el panel debería deshabilitar el botón.
            _service.SetHolds(new[] { true, true, true, true, true });
            Assert.IsFalse(_service.CanAffordReroll);

            _service.Confirm();

            Assert.AreEqual(2, captured.RollsUsed);
        }

        [Test]
        public void Reroll_BlockedByEmptyPool_ResolvesWhenUserConfirms()
        {
            _energy.CurrentRolls = 1; // alcanza solo para el base, no para el reroll
            _roller.NextRoll = new[] { 1, 1, 1, 1, 1 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            // Aunque no haya rolls para reroll, igual entra a AwaitingRerollDecision
            // (panel muestra Reroll deshabilitado vía CanAffordReroll, solo Confirm).
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.IsFalse(_service.CanAffordReroll);

            // Si user igual intenta RequestReroll (button no debio responder pero
            // defendamos), TrySpendRolls falla → resuelve. Reroll invertido: hace falta
            // selección para pasar el guard de "nada que re-tirar" y llegar al cobro.
            _service.SetHolds(new[] { true, true, true, true, true });
            _service.RequestReroll();
            Assert.AreEqual(ActionRollPhase.Resolved, _service.Phase);
            Assert.IsFalse(captured.PassedThreshold);
            Assert.AreEqual(1, captured.RollsUsed);
        }

        [Test]
        public void EmptyPoolForBase_CancelsBeforeRolling()
        {
            _energy.CurrentRolls = 0; // el roll base no se puede cobrar
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
        // BUG-014 (reroll invertido): sin ningún dado seleccionado el reroll no
        // movería ningún dado — no debe consumir rolls ni avanzar el RollIndex,
        // y CanAffordReroll debe reportar false aunque haya rolls de sobra.
        // -------------------------------------------------------------------------

        [Test]
        public void CanAffordReroll_WhenNoDiceSelected_ReturnsFalse()
        {
            // Arrange — post-roll, sin ninguna selección (holds vacíos).
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);

            // Act + Assert — sin selección no hay nada que re-tirar.
            Assert.IsFalse(_service.CanAffordReroll,
                "Sin dados seleccionados el reroll no tendría efecto — botón debe quedar deshabilitado.");

            // Con ≥1 dado seleccionado (y rolls de sobra) el botón se habilita.
            _service.SetHolds(new[] { true, false, false, false, false });
            Assert.IsTrue(_service.CanAffordReroll,
                "Con al menos un dado seleccionado y rolls, el reroll debe habilitarse.");
        }

        [Test]
        public void RequestReroll_WhenNoDiceSelected_DoesNotConsumeRolls()
        {
            // Arrange — post-roll, holds vacíos (nada seleccionado para re-tirar).
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            int spendCallsAfterBase = _energy.SpendCalls;
            int energyAfterBase = _energy.CurrentRolls;
            int rollIndexBefore = _service.RollIndex;

            // Act
            _service.RequestReroll();

            // Assert
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase,
                "El reroll bloqueado no debe cambiar la fase.");
            Assert.AreEqual(spendCallsAfterBase, _energy.SpendCalls,
                "No se debe haber cobrado rolls en el reroll bloqueado.");
            Assert.AreEqual(energyAfterBase, _energy.CurrentRolls);
            Assert.AreEqual(rollIndexBefore, _service.RollIndex,
                "RollIndex no debe avanzar — no hubo tirada.");
        }

        [Test]
        public void RequestReroll_RerollsSelectedDice_AndKeepsUnselected()
        {
            // Arrange — selecciono los dados 0 y 2; el resto debe conservar su cara.
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            _service.SetHolds(new[] { true, false, true, false, false });

            // Act
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.RequestReroll();

            // Assert — el roller recibió keep = complemento de la selección.
            CollectionAssert.AreEqual(new[] { false, true, false, true, true }, _roller.LastKeep,
                "keep[] debe conservar los NO seleccionados y re-tirar los seleccionados.");
        }

        [Test]
        public void RequestReroll_ClearsSelectionAfterCharge()
        {
            // Arrange — el descarte consume la selección (Balatro): la tirada nueva
            // arranca sin holds y el user re-selecciona para el combo.
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            _service.SetHolds(new[] { true, true, false, false, false });

            // Act
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.RequestReroll();

            // Assert
            for (int i = 0; i < _service.CurrentHolds.Count; i++)
                Assert.IsFalse(_service.CurrentHolds[i],
                    $"El hold {i} debe quedar limpio después del reroll.");
        }

        // -------------------------------------------------------------------------
        // Modo clásico (RerollSelectionPrefs.KeepSelected): los dados seleccionados
        // se QUEDAN; vuelan los no seleccionados. Los holds persisten entre rerolls.
        // -------------------------------------------------------------------------

        [Test]
        public void RequestReroll_ClassicMode_KeepsSelectedDice_AndRerollsUnselected()
        {
            // Arrange — selecciono los dados 0 y 2: son los que deben conservarse.
            RerollSelectionPrefs.KeepSelected = true;
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            _service.SetHolds(new[] { true, false, true, false, false });

            // Act
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.RequestReroll();

            // Assert — el roller recibió keep = la selección tal cual.
            CollectionAssert.AreEqual(new[] { true, false, true, false, false }, _roller.LastKeep,
                "keep[] debe conservar los seleccionados y re-tirar el resto.");
        }

        [Test]
        public void RequestReroll_ClassicMode_NothingSelected_RerollsAllAndChargesRoll()
        {
            // Arrange — sin holds: en clásico nada está lockeado, vuela toda la mano.
            RerollSelectionPrefs.KeepSelected = true;
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            int spendCallsAfterBase = _energy.SpendCalls;
            int rollIndexBefore = _service.RollIndex;

            // Act
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.RequestReroll();

            // Assert — la tirada corrió y el reroll se cobró.
            CollectionAssert.AreEqual(
                new[] { false, false, false, false, false }, _roller.LastKeep);
            Assert.AreEqual(spendCallsAfterBase + 1, _energy.SpendCalls,
                "El reroll de toda la mano debe cobrarse normalmente.");
            Assert.AreEqual(rollIndexBefore + 1, _service.RollIndex);
        }

        [Test]
        public void RequestReroll_ClassicMode_AllDiceSelected_DoesNotConsumeRolls()
        {
            // Arrange — todo lockeado: el reroll no movería ningún dado.
            RerollSelectionPrefs.KeepSelected = true;
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            _service.SetHolds(new[] { true, true, true, true, true });
            int spendCallsAfterBase = _energy.SpendCalls;
            int rollIndexBefore = _service.RollIndex;

            // Act
            _service.RequestReroll();

            // Assert — guard defensivo: ni fase, ni cobro, ni tirada.
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(spendCallsAfterBase, _energy.SpendCalls);
            Assert.AreEqual(rollIndexBefore, _service.RollIndex);
        }

        [Test]
        public void RequestReroll_ClassicMode_HoldsPersistAfterReroll()
        {
            // Arrange — en clásico los dados lockeados siguen lockeados tras el
            // reroll: siguen siendo el pick de combo.
            RerollSelectionPrefs.KeepSelected = true;
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            _service.SetHolds(new[] { true, true, false, false, false });

            // Act
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };
            _service.RequestReroll();

            // Assert
            CollectionAssert.AreEqual(
                new[] { true, true, false, false, false }, _service.CurrentHolds,
                "Los holds deben persistir tras el reroll en modo clásico.");
        }

        [Test]
        public void CanAffordReroll_ClassicMode_FollowsUnselectedDice()
        {
            // Arrange — post-roll con rolls de sobra.
            RerollSelectionPrefs.KeepSelected = true;
            _energy.CurrentRolls = 99;
            _roller.NextRoll = new[] { 1, 2, 3, 4, 5 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);

            // Act + Assert — sin holds todo vuela: habilitado.
            Assert.IsTrue(_service.CanAffordReroll,
                "En clásico sin selección se re-tira toda la mano — botón habilitado.");

            // Con todo lockeado no queda nada que re-tirar: deshabilitado.
            _service.SetHolds(new[] { true, true, true, true, true });
            Assert.IsFalse(_service.CanAffordReroll,
                "Con todos los dados lockeados el reroll no tendría efecto.");
        }

        // -------------------------------------------------------------------------
        // Outcome enriquecido (Spec Heal N×M): el outcome transporta la detección
        // REAL del combo + snapshot del subset holdeado, para que los effects que
        // usan la fórmula compartida (heal) resuelvan tabla + Σcaras.
        // -------------------------------------------------------------------------

        [Test]
        public void Outcome_WithCombo_CarriesRealDetection_AndEffectiveTotalInvariant()
        {
            // Arrange — Generala con base 100; se holdea la mano entera.
            var catalog = MakeCatalogWithGenerala(baseDamage: 100);
            var service = new ActionRollService(_roller, _energy, catalog);
            try
            {
                _roller.NextRoll = new[] { 4, 4, 4, 4, 4 };
                ActionRollOutcome captured = default;
                service.StartFlow(SpecForceDoorNoConfirm(threshold: 30), _player, _bag, o => captured = o);
                service.SetHolds(new[] { true, true, true, true, true });

                // Act
                service.Confirm();

                // Assert — detección real (no el sintético sin id/índices).
                Assert.IsTrue(captured.Combo.HasValue, "El outcome debe traer la detección real.");
                var combo = captured.Combo.Value;
                Assert.AreEqual("combo.generala", combo.ComboId);
                Assert.AreEqual(5, combo.ContributingIndices.Count);
                Assert.AreEqual(captured.EffectiveTotal, combo.EffectiveTotal,
                    "Invariante: el EffectiveTotal del combo real debe coincidir con el del outcome.");
            }
            finally
            {
                service.Dispose();
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Outcome_PartialHolds_HeldSnapshotMapsSubsetToBagSlots()
        {
            // Arrange — se holdean solo los slots 0 y 2 (caras 3 y 5), sin combo posible.
            _roller.NextRoll = new[] { 3, 4, 5, 1, 2 };
            ActionRollOutcome captured = default;
            _service.StartFlow(SpecForceDoorNoConfirm(), _player, _bag, o => captured = o);
            _service.SetHolds(new[] { true, false, true, false, false });

            // Act
            _service.Confirm();

            // Assert — snapshot alineado subset→slot.
            CollectionAssert.AreEqual(new[] { 3, 5 }, captured.HeldDice,
                "HeldDice debe contener solo las caras holdeadas, en orden de slot.");
            CollectionAssert.AreEqual(new[] { 0, 2 }, captured.HeldDiceOriginalIndices,
                "Los índices originales deben mapear cada cara held a su slot de bag.");
            Assert.IsFalse(captured.Combo.HasValue, "Sin match del contrato, Combo debe ser null.");
            Assert.AreEqual(8, captured.EffectiveTotal, "Sin combo, effective = suma de held.");
        }

        [Test]
        public void Outcome_NoCombo_HeldSnapshotStillPopulated_ForHighestDieFallback()
        {
            // Arrange — el fallback del heal (dado más alto) necesita el snapshot
            // aunque no haya combo.
            var catalog = MakeCatalogWithGenerala(baseDamage: 100);
            var service = new ActionRollService(_roller, _energy, catalog);
            try
            {
                _roller.NextRoll = new[] { 3, 4, 5, 1, 2 };
                ActionRollOutcome captured = default;
                service.StartFlow(SpecForceDoorNoConfirm(threshold: 30), _player, _bag, o => captured = o);
                service.SetHolds(new[] { true, true, true, true, true });

                // Act
                service.DeclineReroll();

                // Assert
                Assert.IsFalse(captured.Combo.HasValue);
                CollectionAssert.AreEqual(new[] { 3, 4, 5, 1, 2 }, captured.HeldDice);
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, captured.HeldDiceOriginalIndices);
            }
            finally
            {
                service.Dispose();
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
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
            CostsRolls = true,
            Threshold = 10,
            RequireConfirm = true,
            ActionLabel = "Forzar Puerta",
            AllowReroll = true,
            AlwaysSucceeds = false,
        };

        private static ActionRollSpec SpecForceDoorNoConfirm(int threshold = 10) => new ActionRollSpec
        {
            CostsRolls = true,
            Threshold = threshold,
            RequireConfirm = false,
            ActionLabel = "Forzar Puerta",
            AllowReroll = true,
            AlwaysSucceeds = false,
        };

        private static ActionRollSpec SpecHeal() => new ActionRollSpec
        {
            CostsRolls = true,
            Threshold = 15,
            RequireConfirm = false,
            ActionLabel = "Curarse",
            AllowReroll = true,
            AlwaysSucceeds = true,
        };

        // -------------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------------

        private sealed class FakeRollerForActionRoll : IDiceRoller
        {
            public int[] NextRoll = new[] { 1, 1, 1, 1, 1 };

            /// <summary>Último keep[] recibido en <see cref="Reroll"/> — null si nunca se rerolleó.</summary>
            public bool[] LastKeep;

            public int[] RollAll(DiceBagSO bag)
            {
                var copy = new int[NextRoll.Length];
                Array.Copy(NextRoll, copy, NextRoll.Length);
                return copy;
            }

            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
            {
                LastKeep = keep != null ? (bool[])keep.Clone() : null;
                return RollAll(bag);
            }
        }

        private sealed class FakeRollsForActionRoll : IRollPoolService
        {
            public int CurrentRolls = 99;
            public int SpendCalls;

            public bool IsCombatActive => true;
            public bool TrySpendRolls(Guid id, int count)
            {
                if (count > CurrentRolls) return false;
                CurrentRolls -= count;
                SpendCalls += count;
                return true;
            }

            public int Drain(Guid id, int amount)
            {
                int drained = Math.Min(amount, CurrentRolls);
                CurrentRolls -= drained;
                return drained;
            }

            public void AddRolls(Guid id, int amount) => CurrentRolls += amount;
            public int GetCurrent(Guid id) => CurrentRolls;
            public int GetMax(Guid id) => 99;
            public int GetRollsPerTurn(Guid id) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void InitializeForEntity(Guid id) { }
            public void RestoreCurrent(Guid id, int value) => CurrentRolls = value;
        }
    }
}
