using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class PlayerActionButtonsViewTests
    {
        private GameObject _go;
        private PlayerActionButtonsView _view;
        private Button _confirm;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("PlayerActionButtons");
            _view = _go.AddComponent<PlayerActionButtonsView>();

            _confirm = CreateRawButton("ConfirmBtn", _go);

            AssignPrivate(_view, "_confirmButton", _confirm);
            AssignPrivate(_view, "_buttons", new ActionButton[4]);

            InvokeAwake(_view);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void should_disable_confirm_when_bind_initially()
        {
            // Arrange + Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.IsFalse(_confirm.interactable, "Confirm inicia disabled.");
        }

        [Test]
        public void should_keep_confirm_disabled_when_player_turn_started_without_roll()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Assert
            Assert.IsFalse(_confirm.interactable, "Confirm disabled en WaitingForAction.");
        }

        [Test]
        public void should_keep_confirm_disabled_when_other_entity_turn_starts()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            // Assert
            Assert.IsFalse(_confirm.interactable);
        }

        [Test]
        public void should_enable_confirm_when_player_dice_rolled()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.IsTrue(_confirm.interactable, "Confirm enabled tras OnDiceRolled.");
        }

        [Test]
        public void should_disable_confirm_when_player_turn_finished()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.IsFalse(_confirm.interactable, "Confirm disabled tras TurnFinished.");
        }

        [Test]
        public void should_remove_subscriptions_when_unbind()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            _view.Unbind();
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.IsFalse(_confirm.interactable,
                "Tras Unbind, los eventos no deben tener efecto.");
        }

        [Test]
        public void should_subscribe_once_when_double_bind()
        {
            // Arrange + Act
            _view.Bind(_playerGuid);
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.IsTrue(_confirm.interactable, "Tras doble Bind, un solo handler activo.");

            _view.Unbind();
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_confirm.interactable,
                "Tras Unbind del doble Bind, no quedan handlers colgados.");
        }

        [Test]
        public void should_unbind_when_disabled()
        {
            // Arrange
            _view.Bind(_playerGuid);
            var onDisable = typeof(PlayerActionButtonsView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, "OnDisable method not found on PlayerActionButtonsView.");

            // Act
            onDisable.Invoke(_view, null);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.IsFalse(_confirm.interactable,
                "OnDisable desuscribe; el evento no tiene efecto.");
        }

        [Test]
        public void should_invoke_confirm_event_when_button_clicked()
        {
            // Arrange
            bool fired = false;
            _view.OnConfirmPressed.AddListener(() => fired = true);

            // Act
            _confirm.onClick.Invoke();

            // Assert
            Assert.IsTrue(fired, "OnConfirmPressed debe dispararse al clickear Confirm.");
        }

        [Test]
        public void should_disable_confirm_when_roll_resolved_outside_chain()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsTrue(_confirm.interactable);

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.IsFalse(_confirm.interactable, "Confirm disabled en WaitingForAction tras OnRollResolved.");
        }

        [Test]
        public void should_keep_confirm_enabled_when_roll_resolved_during_chain()
        {
            // Regresion para bug del chain: OnRollResolved entre fases NO debe
            // resetear el estado del confirm (el chain todavia corre).
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.IsTrue(_confirm.interactable,
                "Durante un chain, OnRollResolved entre fases no debe deshabilitar el confirm.");
        }

        [Test]
        public void should_disable_confirm_when_chain_completed()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsTrue(_confirm.interactable);

            // Act
            EventManager.Trigger(EventName.OnChainCompleted, _playerGuid, 2, 2, false);

            // Assert
            Assert.IsFalse(_confirm.interactable,
                "Al completarse el chain, el confirm vuelve a disabled (WaitingForAction).");
        }

        [Test]
        public void should_invoke_behavior_selected_delegate_when_action_button_clicked()
        {
            // Arrange
            int selectedIndex = -1;
            _view.OnBehaviorSelected = (idx) => selectedIndex = idx;

            var actionButton = CreateActionButton("MovementBtn", _go, HeroBehaviorSlot.Movement);
            var array = new ActionButton[4];
            array[0] = actionButton;
            AssignPrivate(_view, "_buttons", array);

            InvokeAwake(_view);

            // Act
            actionButton.Button.onClick.Invoke();

            // Assert
            Assert.AreEqual(0, selectedIndex, "Movement click debe invocar OnBehaviorSelected(0).");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static Button CreateRawButton(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<Button>();
        }

        private static ActionButton CreateActionButton(string name, GameObject parent, HeroBehaviorSlot slot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var button = go.AddComponent<Button>();
            var actionButton = go.AddComponent<ActionButton>();

            AssignPrivate(actionButton, "_button", button);
            AssignPrivate(actionButton, "_slot", slot);

            InvokeAwake(actionButton);
            return actionButton;
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokeAwake(object target)
        {
            var awake = target.GetType().GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(target, null);
        }
    }
}
