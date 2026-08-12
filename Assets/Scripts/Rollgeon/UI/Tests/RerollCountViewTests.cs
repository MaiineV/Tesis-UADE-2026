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
                "Sin IRerollBudgetService, el boton queda disabled.");
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
                "Tras OnRollResolved, el boton queda disabled (budget terminado).");
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

        /// <summary>Budget de mentira con reroll disponible; Current=null ⇒ el view
        /// no lo trata como "primer roll pendiente" (post-roll a efectos del gate).</summary>
        private sealed class AvailableFakeBudget : Rollgeon.Dice.IRerollBudgetService
        {
            public Rollgeon.Dice.RerollBudget Current => null;
#pragma warning disable CS0067
            public event Action<Rollgeon.Dice.RerollStartedPayload> OnRerollStarted;
            public event Action<Rollgeon.Dice.RerollBudget> OnBudgetStarted;
#pragma warning restore CS0067
            public void StartBudget(Rollgeon.Combat.Actions.ActionDefinitionSO action) { }
            public void EndBudget() { }
            public Rollgeon.Dice.RerollQueryResult QueryExtraRoll(Guid playerGuid)
                => Rollgeon.Dice.RerollQueryResult.Free();
            public bool TryExtraRoll(Guid playerGuid) => false;
        }

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
            // Arrange — budget con reroll disponible pero ningún dado seleccionado.
            ServiceLocator.Clear();
            ServiceLocator.AddService<Rollgeon.Dice.IRerollBudgetService>(new AvailableFakeBudget());
            try
            {
                MakeZoneWithHolds(new[] { false, false, false });

                // Act — Bind corre RefreshButtonInteractable.
                _view.Bind(_playerGuid);

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
            // Arrange — mismo budget, pero con un dado seleccionado para re-tirar.
            ServiceLocator.Clear();
            ServiceLocator.AddService<Rollgeon.Dice.IRerollBudgetService>(new AvailableFakeBudget());
            try
            {
                MakeZoneWithHolds(new[] { true, false, false });

                // Act
                _view.Bind(_playerGuid);

                // Assert
                Assert.IsTrue(_extraRoll.interactable,
                    "Con ≥1 dado seleccionado y reroll disponible, el botón se habilita.");
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
            // Arrange — budget disponible, nada lockeado: vuela toda la mano.
            ServiceLocator.Clear();
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            ServiceLocator.AddService<Rollgeon.Dice.IRerollBudgetService>(new AvailableFakeBudget());
            try
            {
                MakeZoneWithHolds(new[] { false, false, false });

                // Act — Bind corre RefreshButtonInteractable.
                _view.Bind(_playerGuid);

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
            ServiceLocator.AddService<Rollgeon.Dice.IRerollBudgetService>(new AvailableFakeBudget());
            try
            {
                MakeZoneWithHolds(new[] { true, true, true });

                // Act
                _view.Bind(_playerGuid);

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
