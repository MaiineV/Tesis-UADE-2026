using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Balance;
using Rollgeon.Combat.EnergyLib;
using UnityEngine;

namespace Rollgeon.Combat.EnergyLib.Tests
{
    [TestFixture]
    public class EnergyServiceTests
    {
        private AttributesManager _attrs;
        private RulesetSO _ruleset;
        private EnergyService _service;
        private List<object[]> _energyChangedArgs;
        private List<object[]> _playerEnergyChangedArgs;
        private EventManager.EventReceiver _energyRec;
        private EventManager.EventReceiver _playerEnergyRec;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrs = new AttributesManager();

            _ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            // defaults del SO: 4/2/2.

            _service = new EnergyService();
            _service.ConfigureForTests(_ruleset, _attrs);

            _energyChangedArgs = new List<object[]>();
            _playerEnergyChangedArgs = new List<object[]>();
            _energyRec = args => _energyChangedArgs.Add(args);
            _playerEnergyRec = args => _playerEnergyChangedArgs.Add(args);
            EventManager.Subscribe(EventName.OnEnergyChanged, _energyRec);
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, _playerEnergyRec);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _attrs?.Dispose();
            if (_ruleset != null)
            {
                UnityEngine.Object.DestroyImmediate(_ruleset);
                _ruleset = null;
            }
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private Guid RegisterEntityWithEmptyAttrs()
        {
            var id = Guid.NewGuid();
            _attrs.Register(id, new ModifiableAttributes());
            return id;
        }

        // --- InitializeForEntity -----------------------------------------

        [Test]
        public void Initialize_SetsStart_2_of_4_AndFiresEvent()
        {
            var id = RegisterEntityWithEmptyAttrs();

            _service.InitializeForEntity(id);

            Assert.AreEqual(2, _service.GetCurrent(id));
            Assert.AreEqual(4, _service.GetMax(id));
            Assert.AreEqual(1, _energyChangedArgs.Count);
            var last = _energyChangedArgs[0];
            Assert.AreEqual(id, (Guid)last[0]);
            Assert.AreEqual(2, (int)last[1]);
            Assert.AreEqual(4, (int)last[2]);
        }

        [Test]
        public void Initialize_EmitsPlayerEnergyChanged_ForPlayer()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);

            Assert.AreEqual(1, _playerEnergyChangedArgs.Count);
            Assert.AreEqual(id, (Guid)_playerEnergyChangedArgs[0][0]);
        }

        [Test]
        public void Initialize_StartClampsToMax()
        {
            // Sube AtRunStart por encima de max — Validate lo clampea al Validate,
            // pero aqui lo forzamos con un valor invalido pre-Validate para probar
            // el clamp defensivo del servicio.
            _ruleset.Energy.EnergyMax = 3;
            _ruleset.Energy.EnergyAtRunStart = 99;

            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);

            Assert.AreEqual(3, _service.GetCurrent(id));
        }

        // --- SpendEnergy --------------------------------------------------

        [Test]
        public void SpendEnergy_Sufficient_RemovesAndFires()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);
            _energyChangedArgs.Clear();

            bool ok = _service.SpendEnergy(id, 1);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, _service.GetCurrent(id));
            Assert.AreEqual(1, _energyChangedArgs.Count);
            Assert.AreEqual(1, (int)_energyChangedArgs[0][1]);
            Assert.AreEqual(4, (int)_energyChangedArgs[0][2]);
        }

        [Test]
        public void SpendEnergy_Insufficient_ReturnsFalseAndDoesNotMutate()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);
            _energyChangedArgs.Clear();

            bool ok = _service.SpendEnergy(id, 99);

            Assert.IsFalse(ok);
            Assert.AreEqual(2, _service.GetCurrent(id)); // intacto.
            Assert.AreEqual(0, _energyChangedArgs.Count);
        }

        [Test]
        public void SpendEnergy_NegativeCost_ReturnsFalse()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);

            bool ok = _service.SpendEnergy(id, -3);
            Assert.IsFalse(ok);
            Assert.AreEqual(2, _service.GetCurrent(id));
        }

        [Test]
        public void SpendEnergy_ExactAmount_LeavesZero()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);

            bool ok = _service.SpendEnergy(id, 2);
            Assert.IsTrue(ok);
            Assert.AreEqual(0, _service.GetCurrent(id));
        }

        // --- RegenerateAtTurnEnd -----------------------------------------

        [Test]
        public void Regenerate_ClampsToMax()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);
            // current == 2, regen 2, max 4 → 4.
            _energyChangedArgs.Clear();

            _service.RegenerateAtTurnEnd(id);

            Assert.AreEqual(4, _service.GetCurrent(id));
            Assert.AreEqual(1, _energyChangedArgs.Count);
            Assert.AreEqual(4, (int)_energyChangedArgs[0][1]);
        }

        [Test]
        public void Regenerate_AlreadyAtMax_IsNoOp()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);
            _service.RegenerateAtTurnEnd(id); // → 4
            _energyChangedArgs.Clear();

            _service.RegenerateAtTurnEnd(id); // no cambia: 4+2=6 → cap 4.

            Assert.AreEqual(4, _service.GetCurrent(id));
            Assert.AreEqual(0, _energyChangedArgs.Count);
        }

        // --- OnTurnFinished gating ---------------------------------------

        [Test]
        public void OnTurnFinished_ForPlayer_TriggersRegen()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);
            _service.SpendEnergy(id, 2); // 2 → 0
            _energyChangedArgs.Clear();

            EventManager.Trigger(EventName.OnTurnFinished, id);

            Assert.AreEqual(2, _service.GetCurrent(id));
            Assert.AreEqual(1, _energyChangedArgs.Count);
        }

        [Test]
        public void OnTurnFinished_ForOtherEntity_DoesNotRegenPlayer()
        {
            var playerId = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(playerId);
            _service.SpendEnergy(playerId, 2); // 2 → 0
            _energyChangedArgs.Clear();

            // Otro Guid (enemigo) termina turno → no regenera al player.
            var enemyId = Guid.NewGuid();
            EventManager.Trigger(EventName.OnTurnFinished, enemyId);

            Assert.AreEqual(0, _service.GetCurrent(playerId));
            Assert.AreEqual(0, _energyChangedArgs.Count);
        }

        [Test]
        public void OnTurnFinished_BeforeInitialize_DoesNothing()
        {
            var id = RegisterEntityWithEmptyAttrs();
            // nunca llamamos InitializeForEntity.

            EventManager.Trigger(EventName.OnTurnFinished, id);

            Assert.AreEqual(0, _energyChangedArgs.Count);
        }

        // --- OnRunStart --------------------------------------------------

        [Test]
        public void OnRunStart_ResetsCachedPlayerId()
        {
            var id = RegisterEntityWithEmptyAttrs();
            _service.InitializeForEntity(id);
            _service.SpendEnergy(id, 2);

            // Nueva run arranca: el cache del playerId se limpia, y un
            // OnTurnFinished con ese mismo Guid NO debe regenerar hasta que
            // alguien vuelva a llamar InitializeForEntity.
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "ruleset.default");
            _energyChangedArgs.Clear();

            EventManager.Trigger(EventName.OnTurnFinished, id);

            Assert.AreEqual(0, _energyChangedArgs.Count);
        }
    }
}
