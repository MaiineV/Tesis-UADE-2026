using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Rolls;
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

        private sealed class GateFakeRolls : IRollPoolService
        {
            public bool IsCombatActive => true;
            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count) => true;
            public int Drain(Guid entityId, int amount) => amount;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => 99;
            public int GetMax(Guid entityId) => 99;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) { }
        }

        private GateFakeRoller _roller;
        private ActionRollService _service;
        private DiceThrowService _throwService;
        private DiceThrowSettingsSO _settings;
        private DiceBagSO _bag;
        private Guid _player;
        private int _diceRolledEvents;
        private bool _savedKeepSelected;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            // Los asserts asumen el modo invertido default; la pref persiste en
            // PlayerPrefs del editor, así que se fuerza y restaura.
            _savedKeepSelected = RerollSelectionPrefs.KeepSelected;
            RerollSelectionPrefs.KeepSelected = false;

            _roller = new GateFakeRoller();
            // El throw service resuelve IDiceRoller del locator (como en producción,
            // donde ambos apuntan al EnchantedDiceRoller registrado).
            ServiceLocator.AddService<IDiceRoller>(_roller, ServiceScope.Global);

            _settings = ScriptableObject.CreateInstance<DiceThrowSettingsSO>();
            _settings.DefaultMode = DiceThrowMode.TwoD;
            _throwService = new DiceThrowService(_settings);
            _throwService.AttachPresenter(this);
            ServiceLocator.AddService<IDiceThrowService>(_throwService, ServiceScope.Global);

            _service = new ActionRollService(_roller, new GateFakeRolls());

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
            RerollSelectionPrefs.KeepSelected = _savedKeepSelected;
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
            CostsRolls = true,
            Threshold = 15,
            RequireConfirm = false,
            ActionLabel = "Curarse",
            AllowReroll = true,
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
        public void RequestReroll_ManualMode_ReentersRolling_AndRerollsSelectedDice()
        {
            _service.StartFlow(SpecHealNoConfirm(), _player, _bag, _ => { });
            PumpThrow();

            // Arrange — reroll invertido (Balatro): seleccionar el dado 0 significa
            // RE-TIRARLO; los no seleccionados conservan su cara.
            _service.SetHolds(new[] { true, false, false, false, false });
            _roller.NextRoll = new[] { 6, 6, 6, 6, 6 };

            // Act
            _service.RequestReroll();

            // Assert: de vuelta en Rolling esperando el throw (solo modos manuales).
            Assert.AreEqual(ActionRollPhase.Rolling, _service.Phase);
            Assert.IsTrue(_throwService.ThrownMask[0], "el seleccionado es el que vuela");
            Assert.IsFalse(_throwService.ThrownMask[1], "los no seleccionados no participan del throw");

            PumpThrow();

            Assert.AreEqual(ActionRollPhase.AwaitingRerollDecision, _service.Phase);
            CollectionAssert.AreEqual(new[] { 6, 4, 4, 4, 4 }, _service.CurrentRoll,
                "el seleccionado tomó la cara nueva, el resto conserva la suya");
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
