using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Dice;
using Rollgeon.Dice.Throw;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Integración ActionRollService × DiceThrowService (CNF-008): con el gate en modo
    /// manual el flujo queda en <see cref="ActionRollPhase.Rolling"/> hasta que el
    /// jugador arroja los dados; el reveal dispara la continuación y OnDiceRolled.
    /// </summary>
    [TestFixture]
    public class ActionRollThrowGateTests
    {
        private sealed class GateFakeRoller : IDiceRoller
        {
            public int[] NextRoll = { 4, 4, 4, 4, 4 };

            public int[] RollAll(DiceBagSO bag) => (int[])NextRoll.Clone();

            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
            {
                var result = (int[])NextRoll.Clone();
                if (previousResult == null || keep == null) return result;
                for (int i = 0; i < result.Length && i < previousResult.Length; i++)
                    if (i < keep.Length && keep[i]) result[i] = previousResult[i];
                return result;
            }
        }

        private sealed class GateFakeEnergy : IEnergyService
        {
            public void InitializeForEntity(Guid entityId) { }
            public bool SpendEnergy(Guid entityId, int cost) => true;
            public void RegenerateAtTurnEnd(Guid entityId) { }
            public int GetCurrent(Guid entityId) => 99;
            public int GetMax(Guid entityId) => 99;
        }

        private GateFakeRoller _roller;
        private ActionRollService _service;
        private DiceThrowService _throwService;
        private DiceThrowSettingsSO _settings;
        private DiceBagSO _bag;
        private Guid _player;
        private int _diceRolledEvents;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _roller = new GateFakeRoller();
            // El throw service resuelve IDiceRoller del locator (como en producción,
            // donde ambos apuntan al EnchantedDiceRoller registrado).
            ServiceLocator.AddService<IDiceRoller>(_roller, ServiceScope.Global);

            _settings = ScriptableObject.CreateInstance<DiceThrowSettingsSO>();
            _settings.DefaultMode = DiceThrowMode.TwoD;
            _throwService = new DiceThrowService(_settings);
            _throwService.AttachPresenter(this);
            ServiceLocator.AddService<IDiceThrowService>(_throwService, ServiceScope.Global);

            _service = new ActionRollService(_roller, new GateFakeEnergy());

            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.Dice = new List<DiceType>(5)
            {
                DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6,
            };
            _player = Guid.NewGuid();

            _diceRolledEvents = 0;
            EventManager.Subscribe(EventName.OnDiceRolled, CountDiceRolled);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnDiceRolled, CountDiceRolled);
            _service.Dispose();
            if (_bag != null) Object.DestroyImmediate(_bag);
            if (_settings != null) Object.DestroyImmediate(_settings);
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void CountDiceRolled(params object[] args) => _diceRolledEvents++;

        private void PumpThrow()
        {
            var mask = _throwService.ThrownMask;
            for (int i = 0; i < mask.Count; i++)
                if (mask[i]) _throwService.TryGrab(i);
            foreach (var i in _throwService.ReleaseGrabbed())
                _throwService.NotifyDieSettled(i);
            _throwService.CompleteReveal();
        }

        private static ActionRollSpec SpecHealNoConfirm() => new ActionRollSpec
        {
            EnergyCost = 1,
            Threshold = 15,
            RequireConfirm = false,
            ActionLabel = "Curarse",
            AllowReroll = true,
            RerollEnergyCost = 1,
            AlwaysSucceeds = true,
        };

        [Test]
        public void StartFlow_ManualMode_StaysInRolling_UntilPlayerThrows()
        {
            // Arrange + Act: el initial roll ya no es automático.
            _service.StartFlow(SpecHealNoConfirm(), _player, _bag, _ => { });

            // Assert: fase Rolling (panel sin botones), sin reveal todavía.
            Assert.AreEqual(ActionRollPhase.Rolling, _service.Phase);
            Assert.AreEqual(0, _diceRolledEvents, "reveal diferido hasta el throw");
            Assert.IsTrue(_throwService.IsBusy);

            // Act: el jugador arroja y los dados asientan.
            PumpThrow();

            // Assert: continuación corrió — decisión de reroll con la tirada visible.
            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            Assert.AreEqual(1, _diceRolledEvents);
            CollectionAssert.AreEqual(new[] { 4, 4, 4, 4, 4 }, _service.CurrentRoll);
        }

        [Test]
        public void RequestReroll_ManualMode_ReentersRolling_AndKeepsHeldDice()
        {
            _service.StartFlow(SpecHealNoConfirm(), _player, _bag, _ => { });
            PumpThrow();

            // Arrange: holdear el dado 0; el resto se re-tira con caras nuevas.
            _service.SetHolds(new[] { true, false, false, false, false });
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };

            // Act
            _service.RequestReroll();

            // Assert: de vuelta en Rolling esperando el throw (solo modos manuales).
            Assert.AreEqual(ActionRollPhase.Rolling, _service.Phase);
            Assert.IsFalse(_throwService.ThrownMask[0], "el held no participa del throw");

            PumpThrow();

            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            CollectionAssert.AreEqual(new[] { 4, 6, 6, 6, 6 }, _service.CurrentRoll,
                "held conserva su cara, el resto tomó la nueva tirada");
            Assert.AreEqual(2, _diceRolledEvents);
        }

        [Test]
        public void Cancel_MidThrow_AbortsSessionWithoutReveal()
        {
            _service.StartFlow(SpecHealNoConfirm(), _player, _bag, _ => { });
            Assert.IsTrue(_throwService.IsBusy);

            // Act: cancelar el flujo con los dados todavía sin arrojar.
            _service.Cancel();

            // Assert: throw abortado, sin OnDiceRolled fantasma.
            Assert.IsFalse(_throwService.IsBusy);
            Assert.AreEqual(0, _diceRolledEvents);
            Assert.AreEqual(ActionRollPhase.Cancelled, _service.Phase);
        }
    }
}
