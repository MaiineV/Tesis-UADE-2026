using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class RerollCountViewTests
    {
        private GameObject _go;
        private RerollCountView _view;
        private Button _extraRoll;
        private Guid _playerGuid;
        private bool _savedKeepSelected;

        [SetUp]
        public void Setup()
        {
            // El gate del botón depende del modo persistido en PlayerPrefs: pin al
            // default (invertido) y restore en Teardown.
            _savedKeepSelected = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected;
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = false;

            _playerGuid = Guid.NewGuid();

            _go = new GameObject("RerollCount");
            _view = _go.AddComponent<RerollCountView>();

            var btnGO = new GameObject("ExtraRollBtn");
            btnGO.transform.SetParent(_go.transform, false);
            _extraRoll = btnGO.AddComponent<Button>();

            AssignPrivate(_view, "_extraRollButton", _extraRoll);
        }

        [TearDown]
        public void Teardown()
        {
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = _savedKeepSelected;
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_WithoutService_DisablesExtraRollButton()
        {
            _view.Bind(_playerGuid);
            Assert.IsFalse(_extraRoll.interactable,
                "Sin IRollPoolService, el boton queda disabled.");
        }

        [Test]
        public void Bind_ThenUnbind_IsIdempotent()
        {
            _view.Bind(_playerGuid);
            Assert.DoesNotThrow(() => _view.Unbind());
            Assert.DoesNotThrow(() => _view.Unbind(), "Unbind es idempotente.");
        }

        [Test]
        public void OnDiceRolled_Player_DoesNotThrow()
        {
            _view.Bind(_playerGuid);
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnDiceRolled, _playerGuid));
        }

        [Test]
        public void OnDiceRolled_OtherPlayer_IsIgnored()
        {
            _view.Bind(_playerGuid);
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnDiceRolled, Guid.NewGuid()));
        }

        [Test]
        public void OnRollResolved_Player_DisablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);
            Assert.IsFalse(_extraRoll.interactable,
                "Tras OnRollResolved, el boton queda disabled (accion terminada).");
        }

        [Test]
        public void Unbind_ThenDiceRolled_NoEffect()
        {
            _view.Bind(_playerGuid);
            _view.Unbind();
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnDiceRolled, _playerGuid),
                "Tras Unbind, el evento no debe tener efecto.");
        }

        [Test]
        public void OnExtraRollPressed_UnityEvent_IsExposed()
        {
            _view.Bind(_playerGuid);
            bool invoked = false;
            _view.OnExtraRollPressed.AddListener(() => invoked = true);
            _view.OnExtraRollPressed.Invoke();
            Assert.IsTrue(invoked);
        }

        // -------------------------------------------------------------------
        // Reroll invertido (Balatro): post-primer-roll el botón exige ≥1 dado
        // seleccionado — se re-tiran los seleccionados, sin selección no hay
        // nada que re-tirar.
        // -------------------------------------------------------------------

        /// <summary>Pool de mentira con rolls disponibles.</summary>
        private sealed class AvailableFakePool : Rollgeon.Combat.Rolls.IRollPoolService
        {
            public int CurrentRolls = 5;
            public bool IsCombatActive => true;
            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count) => true;
            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => CurrentRolls;
            public int GetMax(Guid entityId) => 15;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddPerTurnGrantBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) { }
        }

        // Marca el estado post-roll: el gate de seleccion solo aplica despues del
        // primer roll de la accion (antes, el boton es el Roll inicial).
        private void MarkRolled() => EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

        private DiceZoneView MakeZoneWithHolds(bool[] holds)
        {
            var zoneGo = new GameObject("DiceZone");
            zoneGo.transform.SetParent(_go.transform, false);
            var zone = zoneGo.AddComponent<DiceZoneView>();
            AssignPrivate(zone, "_heldStates", holds);
            return zone;
        }

        [Test]
        public void RefreshButtonInteractable_PostRollWithoutSelection_DisablesButton()
        {
            // Arrange — pool con rolls pero ningún dado seleccionado.
            ServiceLocator.Clear();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(new AvailableFakePool());
            try
            {
                MakeZoneWithHolds(new[] { false, false, false });

                // Act — post-roll, RefreshButtonInteractable corre en el handler.
                _view.Bind(_playerGuid);
                MarkRolled();

                // Assert
                Assert.IsFalse(_extraRoll.interactable,
                    "Sin dados seleccionados no hay nada que re-tirar — botón disabled.");
            }
            finally
            {
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RefreshButtonInteractable_PostRollWithSelection_EnablesButton()
        {
            // Arrange — mismo pool, pero con un dado seleccionado para re-tirar.
            ServiceLocator.Clear();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(new AvailableFakePool());
            try
            {
                MakeZoneWithHolds(new[] { true, false, false });

                // Act
                _view.Bind(_playerGuid);
                MarkRolled();

                // Assert
                Assert.IsTrue(_extraRoll.interactable,
                    "Con ≥1 dado seleccionado y rolls en el pool, el botón se habilita.");
            }
            finally
            {
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RefreshButtonInteractable_PostRollEmptyPool_DisablesButton()
        {
            // Arrange — hay dados seleccionados pero el pool está vacío.
            ServiceLocator.Clear();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(
                new AvailableFakePool { CurrentRolls = 0 });
            try
            {
                MakeZoneWithHolds(new[] { true, false, false });

                // Act
                _view.Bind(_playerGuid);
                MarkRolled();

                // Assert
                Assert.IsFalse(_extraRoll.interactable,
                    "Con el pool en 0 no se puede pagar la tirada — botón disabled.");
            }
            finally
            {
                ServiceLocator.Clear();
            }
        }

        // -------------------------------------------------------------------
        // Modo clásico (RerollSelectionPrefs.KeepSelected): vuelan los NO
        // seleccionados — sin holds se re-tira toda la mano (botón habilitado);
        // con todo lockeado no queda nada que re-tirar (deshabilitado).
        // -------------------------------------------------------------------

        [Test]
        public void RefreshButtonInteractable_ClassicModePostRollWithoutSelection_EnablesButton()
        {
            // Arrange — pool con rolls, nada lockeado: vuela toda la mano.
            ServiceLocator.Clear();
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(new AvailableFakePool());
            try
            {
                MakeZoneWithHolds(new[] { false, false, false });

                // Act
                _view.Bind(_playerGuid);
                MarkRolled();

                // Assert
                Assert.IsTrue(_extraRoll.interactable,
                    "En clásico sin selección se re-tira toda la mano — botón habilitado.");
            }
            finally
            {
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RefreshButtonInteractable_ClassicModeAllDiceHeld_DisablesButton()
        {
            // Arrange — todo lockeado: el reroll no movería ningún dado.
            ServiceLocator.Clear();
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(new AvailableFakePool());
            try
            {
                MakeZoneWithHolds(new[] { true, true, true });

                // Act
                _view.Bind(_playerGuid);
                MarkRolled();

                // Assert
                Assert.IsFalse(_extraRoll.interactable,
                    "Con todos los dados lockeados no hay nada que re-tirar — botón disabled.");
            }
            finally
            {
                ServiceLocator.Clear();
            }
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado.");
            field.SetValue(target, value);
        }
    }
}
