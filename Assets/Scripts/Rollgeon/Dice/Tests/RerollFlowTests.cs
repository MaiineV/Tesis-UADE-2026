using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Effects;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// Integration-style tests (EditMode — el servicio es pura C#, no requiere
    /// PlayMode) que simulan el canonical flow del plan §5.1.
    /// </summary>
    /// <remarks>
    /// El plan §3.3 referencia <c>Assets/Tests/PlayMode/...</c>; como el servicio es
    /// lifecycle-agnostico (no usa <c>MonoBehaviour</c>, <c>Coroutine</c> ni frame
    /// boundaries), los tests integrationes corren in-process bajo EditMode — misma
    /// cobertura, setup mas simple. Ver plan §10 (riesgos) — ningun riesgo requiere
    /// PlayMode especifico para esta feature.
    /// </remarks>
    [TestFixture]
    public class RerollFlowTests
    {
        private RerollBudgetService _svc;
        private FakeEnergyService _energy;
        private List<ActionDefinitionSO> _createdDefs;
        private Guid _player;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _energy = new FakeEnergyService();
            _player = Guid.NewGuid();
            _energy.Current[_player] = 4;

            _svc = new RerollBudgetService();
            _svc.ConfigureForTests(_energy);

            _createdDefs = new List<ActionDefinitionSO>();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            foreach (var def in _createdDefs)
            {
                if (def != null) UnityEngine.Object.DestroyImmediate(def);
            }
            _createdDefs = null;

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private ActionDefinitionSO MakeAttack()
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = "attack.basic";
            def.Type = ActionType.Attack;
            def.EnergyCost = 0;
            def.FreeRollCount = 3; // 3 rolls gratis (primer roll + 2 rerolls).
            def.AllowsEnergyReroll = true;
            def.Effect = new EffectData();
            _createdDefs.Add(def);
            return def;
        }

        /// <summary>
        /// Flow canonical: start → 3 free rolls (primer roll + 2 rerolls) → 1 paid
        /// reroll → blocked. Verifica que:
        /// <list type="bullet">
        ///   <item>Energia se debita <b>exactamente 1 vez</b> (solo en el paid).</item>
        ///   <item><c>OnRerollStarted</c> dispara 4 veces (una por roll concedido).</item>
        ///   <item>El 5to intento falla sin mutar energia.</item>
        /// </list>
        /// </summary>
        [Test]
        public void CanonicalFlow_ThreeFreeRolls_OnePaid_ThenBlocked()
        {
            var action = MakeAttack();
            int initialEnergy = _energy.Current[_player];

            var events = new List<RerollStartedPayload>();
            _svc.OnRerollStarted += p => events.Add(p);

            // 1) Combat controller abre el budget.
            _svc.StartBudget(action);
            Assert.AreEqual(3, _svc.Current.FreeRollsRemaining);

            // 2) Primer roll — gratis.
            var q1 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q1.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // 3) Primer reroll — gratis.
            var q2 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q2.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // 4) Segundo reroll — gratis.
            var q3 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q3.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // 5) Tercer reroll — paid.
            var q4 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q4.CostsEnergy);
            Assert.IsFalse(q4.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // Gasto exacto: 1 llamada a SpendEnergy con cost=1.
            Assert.AreEqual(1, _energy.SpendCallCount);
            Assert.AreEqual(initialEnergy - 1, _energy.Current[_player]);

            // Cuatro eventos — tres gratis, el cuarto pago.
            Assert.AreEqual(4, events.Count);
            Assert.IsTrue(events[0].IsFree);
            Assert.IsTrue(events[1].IsFree);
            Assert.IsTrue(events[2].IsFree);
            Assert.IsFalse(events[3].IsFree);
            // Snapshot post-consumo del cuarto evento: free=0, paid=1.
            Assert.AreEqual(0, events[3].FreeRollsRemaining);
            Assert.AreEqual(1, events[3].PaidRollsUsed);

            // 6) Agotamos energia hasta 0; 5to intento debe fallar por no-energy.
            _energy.Current[_player] = 0;
            var q5 = _svc.QueryExtraRoll(_player);
            Assert.IsFalse(q5.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonNoEnergy, q5.BlockedReason);
            Assert.IsFalse(_svc.TryExtraRoll(_player));

            // 7) EndBudget limpia estado.
            _svc.EndBudget();
            Assert.IsNull(_svc.Current);
        }

        /// <summary>
        /// Secondary action (heal / force-door, <c>FreeRollCount=1</c> y
        /// <c>AllowsEnergyReroll=false</c>): hay UN free roll (el primero, gatillado
        /// por el boton Roll), despues no se permite reroll alguno.
        /// </summary>
        [Test]
        public void SecondaryAction_OneShotNoEnergyReroll_AfterFirstRoll_BlocksRerolls()
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = "skill.cutscene";
            def.Type = ActionType.SkillCheck;
            def.EnergyCost = 0;
            def.FreeRollCount = 1;
            def.AllowsEnergyReroll = false;
            def.Effect = new EffectData();
            _createdDefs.Add(def);

            _svc.StartBudget(def);

            // 1) Primer roll — gratis.
            var q1 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q1.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // 2) Cualquier intento posterior — bloqueado (action prohibe paid).
            var q2 = _svc.QueryExtraRoll(_player);
            Assert.IsFalse(q2.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonActionForbidsEnergyReroll, q2.BlockedReason);
            Assert.IsFalse(_svc.TryExtraRoll(_player));
            Assert.AreEqual(0, _energy.SpendCallCount);
        }
    }
}
