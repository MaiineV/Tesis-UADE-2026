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
            def.FreeRollCount = 3; // 1 roll + 2 rerolls gratis.
            def.AllowsEnergyReroll = true;
            def.Effect = new EffectData();
            _createdDefs.Add(def);
            return def;
        }

        /// <summary>
        /// Flow canonical (§5.1): start → 2 free rerolls → 1 paid reroll → blocked.
        /// Verifica que:
        /// <list type="bullet">
        ///   <item>Energia se debita <b>exactamente 1 vez</b> (solo en el paid).</item>
        ///   <item><c>OnRerollStarted</c> dispara 3 veces (una por reroll concedido).</item>
        ///   <item>El 4to intento falla sin mutar energia.</item>
        /// </list>
        /// </summary>
        [Test]
        public void CanonicalFlow_TwoFreeRerolls_OnePaid_ThenBlocked()
        {
            var action = MakeAttack();
            int initialEnergy = _energy.Current[_player];

            var events = new List<RerollStartedPayload>();
            _svc.OnRerollStarted += p => events.Add(p);

            // 1) Combat controller abre el budget.
            _svc.StartBudget(action);
            Assert.AreEqual(2, _svc.Current.FreeRollsRemaining);

            // 2) Primer reroll — gratis.
            var q1 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q1.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // 3) Segundo reroll — gratis.
            var q2 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q2.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // 4) Tercer reroll — paid.
            var q3 = _svc.QueryExtraRoll(_player);
            Assert.IsTrue(q3.CostsEnergy);
            Assert.IsFalse(q3.IsFreeRoll);
            Assert.IsTrue(_svc.TryExtraRoll(_player));

            // Gasto exacto: 1 llamada a SpendEnergy con cost=1.
            Assert.AreEqual(1, _energy.SpendCallCount);
            Assert.AreEqual(initialEnergy - 1, _energy.Current[_player]);

            // Tres eventos — el primero gratis, el segundo gratis, el tercero pago.
            Assert.AreEqual(3, events.Count);
            Assert.IsTrue(events[0].IsFree);
            Assert.IsTrue(events[1].IsFree);
            Assert.IsFalse(events[2].IsFree);
            // Snapshot post-consumo del tercer evento: free=0, paid=1.
            Assert.AreEqual(0, events[2].FreeRollsRemaining);
            Assert.AreEqual(1, events[2].PaidRollsUsed);

            // 5) Agotamos energia hasta 0; 4to intento debe fallar por no-energy.
            _energy.Current[_player] = 0;
            var q4 = _svc.QueryExtraRoll(_player);
            Assert.IsFalse(q4.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonNoEnergy, q4.BlockedReason);
            Assert.IsFalse(_svc.TryExtraRoll(_player));

            // 6) EndBudget limpia estado.
            _svc.EndBudget();
            Assert.IsNull(_svc.Current);
        }

        /// <summary>
        /// Secondary action (heal / force-door, <c>FreeRollCount=1</c>): el 1er reroll
        /// ya es paid. Con <c>AllowsEnergyReroll=false</c> el reroll esta bloqueado.
        /// </summary>
        [Test]
        public void SecondaryAction_OneShotNoEnergyReroll_BlockedImmediately()
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

            var q = _svc.QueryExtraRoll(_player);
            Assert.IsFalse(q.IsAvailable);
            Assert.AreEqual(RerollBudgetService.BlockedReasonActionForbidsEnergyReroll, q.BlockedReason);
            Assert.IsFalse(_svc.TryExtraRoll(_player));
            Assert.AreEqual(0, _energy.SpendCallCount);
        }
    }
}
