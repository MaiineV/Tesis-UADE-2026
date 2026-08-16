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
    public class EndTurnButtonViewTests
    {
        private GameObject _go;
        private EndTurnButtonView _view;
        private Button _button;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("EndTurnButton");
            _view = _go.AddComponent<EndTurnButtonView>();

            _button = CreateButton("EndTurnBtn", _go);
            AssignPrivate(_view, "_endTurnButton", _button);

            var awake = typeof(EndTurnButtonView).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(_view, null);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            // El view se suscribe al toggle de holds (re-gateo del Confirm) — sin el
            // Clear, un test que no desbindea dejaría el handler colgado para otros
            // fixtures que disparen el payload.
            TypedEvent<ComboMatchedPayload>.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_DisablesButton_Initially()
        {
            _view.Bind(_playerGuid);
            Assert.IsFalse(_button.interactable, "EndTurn inicia disabled.");
        }

        [Test]
        public void OnTurnStarted_Player_EnablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable, "EndTurn enabled en turno del player.");
        }

        [Test]
        public void OnTurnStarted_OtherEntity_KeepsDisabled()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());
            Assert.IsFalse(_button.interactable, "EndTurn ignora otros guids.");
        }

        [Test]
        public void OnTurnFinished_Player_DisablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable);

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);
            Assert.IsFalse(_button.interactable, "EndTurn disabled al terminar turno.");
        }

        [Test]
        public void OnDiceRolled_Player_DisablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable);

            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_button.interactable, "EndTurn disabled durante behavior.");
        }

        [Test]
        public void OnRollResolved_Player_EnablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_button.interactable);

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);
            Assert.IsTrue(_button.interactable, "EndTurn re-enabled post confirm.");
        }

        [Test]
        public void Unbind_RemovesSubscriptions()
        {
            _view.Bind(_playerGuid);
            _view.Unbind();

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_button.interactable, "Tras Unbind, eventos no tienen efecto.");
        }

        [Test]
        public void DoubleBindIsIdempotent()
        {
            _view.Bind(_playerGuid);
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable, "Tras doble Bind, un solo handler activo.");

            _view.Unbind();
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_button.interactable,
                "Tras Unbind del doble Bind, no quedan handlers colgados.");
        }

        [Test]
        public void OnDisable_Unbinds()
        {
            _view.Bind(_playerGuid);
            var onDisable = typeof(EndTurnButtonView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, "OnDisable method not found.");
            onDisable.Invoke(_view, null);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_button.interactable, "OnDisable desuscribe.");
        }

        [Test]
        public void EndTurnButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnEndTurnPressed.AddListener(() => fired = true);
            _button.onClick.Invoke();
            Assert.IsTrue(fired, "OnEndTurnPressed debe dispararse al clickear EndTurn.");
        }

        // ==================================================================
        // Botón contextual — modos EndTurn / Confirm / Pass
        // (hereda la cobertura de gating que tenía el Confirm de
        // PlayerActionButtonsView)
        // ==================================================================

        [Test]
        public void ChainStarted_EntersConfirmMode_DisabledWithoutRoll()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Con un chain en curso el botón está en modo Confirm.");
            Assert.IsFalse(_button.interactable,
                "Sin tirada revelada no hay nada que confirmar.");
        }

        [Test]
        public void DiceRolledWithHeldDie_EnablesConfirm()
        {
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode);
            Assert.IsTrue(_button.interactable,
                "Con dados rolleados y al menos un hold, Confirm se habilita.");
        }

        [Test]
        public void RolledWithoutHolds_KeepsConfirmDisabled()
        {
            // Arrange — sin holds no hay combo posible; el Confirm engañaría al jugador.
            WireDiceZoneWithHolds(new[] { false, false });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode);
            Assert.IsFalse(_button.interactable,
                "Confirm disabled tras OnDiceRolled si no hay dados holdeados.");
        }

        [Test]
        public void ClickInConfirmMode_FiresConfirm_NotEndTurn_AndLocksButton()
        {
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            bool confirmFired = false, endTurnFired = false;
            _view.OnConfirmPressed.AddListener(() => confirmFired = true);
            _view.OnEndTurnPressed.AddListener(() => endTurnFired = true);

            // Act
            _button.onClick.Invoke();

            // Assert
            Assert.IsTrue(confirmFired, "En modo Confirm el click dispara OnConfirmPressed.");
            Assert.IsFalse(endTurnFired, "En modo Confirm el click NO debe pasar turno.");
            Assert.IsFalse(_button.interactable,
                "BUG-018: en chain el click apaga el botón hasta el próximo refresh con estado fresco.");
        }

        [Test]
        public void RollResolvedDuringChain_KeepsConfirmMode()
        {
            // Regresión heredada del chain: OnRollResolved entre fases NO cierra la
            // acción — el modo Confirm se mantiene hasta OnChainCompleted.
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Durante un chain, OnRollResolved entre fases no vuelve a End Turn.");
            Assert.IsTrue(_button.interactable,
                "El confirm sigue habilitado con el latch del chain activo.");
        }

        [Test]
        public void RollResolvedOutsideChain_ReturnsToEndTurnMode()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode, "Precondición: modo Confirm.");

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.EndTurn, _view.CurrentMode,
                "Fuera de chain, resolver la tirada devuelve el botón a End Turn.");
            Assert.IsTrue(_button.interactable, "En turno propio End Turn queda habilitado.");
        }

        [Test]
        public void ChainCompleted_ReturnsToEndTurnMode()
        {
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnChainCompleted, _playerGuid, 2, 2, false);

            // Assert
            Assert.AreEqual(TurnButtonMode.EndTurn, _view.CurrentMode,
                "Al completarse el chain el botón vuelve a End Turn.");
            Assert.IsTrue(_button.interactable);
        }

        [Test]
        public void PaidRollPending_EntersPassMode_AndClickFiresPass()
        {
            // Arrange — fase de chain con entrada paga (prompt 'X Roll -1⚡' visible).
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            bool passFired = false, endTurnFired = false, confirmFired = false;
            _view.OnPassPressed.AddListener(() => passFired = true);
            _view.OnEndTurnPressed.AddListener(() => endTurnFired = true);
            _view.OnConfirmPressed.AddListener(() => confirmFired = true);

            // Act
            _view.SetChainPaidRollPending(true);

            // Assert
            Assert.AreEqual(TurnButtonMode.Pass, _view.CurrentMode,
                "Con un roll pago pendiente y sin tirada, el botón ofrece Pass.");
            Assert.IsTrue(_button.interactable, "Pass es la salida sin costo — habilitado.");

            _button.onClick.Invoke();
            Assert.IsTrue(passFired, "En modo Pass el click dispara OnPassPressed.");
            Assert.IsFalse(endTurnFired, "Pass NO debe cerrar el turno.");
            Assert.IsFalse(confirmFired, "Pass NO debe confirmar.");
        }

        [Test]
        public void PaidRollCleared_ReturnsToConfirmMode()
        {
            // Arrange — el jugador pagó y tiró: el prompt se esconde y llega el reveal.
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            _view.SetChainPaidRollPending(true);
            Assert.AreEqual(TurnButtonMode.Pass, _view.CurrentMode, "Precondición: modo Pass.");

            // Act
            _view.SetChainPaidRollPending(false);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Pagado el roll, el botón vuelve a Confirm para la tirada nueva.");
            Assert.IsTrue(_button.interactable);
        }

        private static Button CreateButton(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<Button>();
        }

        /// <summary>
        /// DiceZoneView con holds simulados, cableado ANTES del Bind (así el view no
        /// intenta el auto-resolve por escena). Sin Bind del zone: solo hace falta
        /// que GetHeldStates devuelva los holds.
        /// </summary>
        private void WireDiceZoneWithHolds(bool[] holds)
        {
            var go = new GameObject("DiceZone");
            go.transform.SetParent(_go.transform, false);
            var zone = go.AddComponent<DiceZoneView>();
            AssignPrivate(zone, "_heldStates", holds);
            AssignPrivate(_view, "_diceZone", zone);
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
