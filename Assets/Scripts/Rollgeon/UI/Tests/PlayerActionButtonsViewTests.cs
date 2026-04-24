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

            _confirm = CreateButton("ConfirmBtn", _go);

            AssignPrivate(_view, "_confirmButton", _confirm);

            var awake = typeof(PlayerActionButtonsView).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(_view, null);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_DisablesAllButtons_Initially()
        {
            _view.Bind(_playerGuid);

            Assert.IsFalse(_confirm.interactable, "Confirm inicia disabled.");
        }

        [Test]
        public void OnTurnStarted_Player_EnablesBehaviors()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsFalse(_confirm.interactable, "Confirm disabled en WaitingForAction.");
        }

        [Test]
        public void OnTurnStarted_OtherEntity_KeepsAllDisabled()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            Assert.IsFalse(_confirm.interactable);
        }

        [Test]
        public void OnDiceRolled_Player_EnablesConfirm()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            Assert.IsTrue(_confirm.interactable, "Confirm enabled en Rolled.");
        }

        [Test]
        public void OnTurnFinished_Player_DisablesAll()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            Assert.IsFalse(_confirm.interactable, "Confirm disabled tras TurnFinished.");
        }

        [Test]
        public void Unbind_RemovesSubscriptions()
        {
            _view.Bind(_playerGuid);
            _view.Unbind();

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsFalse(_confirm.interactable,
                "Tras Unbind, OnTurnStarted no debe tener efecto.");
        }

        [Test]
        public void DoubleBindIsIdempotent()
        {
            _view.Bind(_playerGuid);
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsTrue(_confirm.interactable, "Tras doble Bind, un solo handler activo.");

            _view.Unbind();
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_confirm.interactable,
                "Tras Unbind del doble Bind, no quedan handlers colgados.");
        }

        [Test]
        public void OnDisable_Unbinds()
        {
            _view.Bind(_playerGuid);
            var onDisable = typeof(PlayerActionButtonsView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, "OnDisable method not found on PlayerActionButtonsView.");
            onDisable.Invoke(_view, null);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_confirm.interactable,
                "OnDisable desuscribe; el evento no tiene efecto.");
        }

        [Test]
        public void ConfirmButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnConfirmPressed.AddListener(() => fired = true);
            _confirm.onClick.Invoke();
            Assert.IsTrue(fired, "OnConfirmPressed debe dispararse al clickear Confirm.");
        }

        [Test]
        public void OnRollResolved_Player_ReturnsToWaitingForAction()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsTrue(_confirm.interactable);

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            Assert.IsFalse(_confirm.interactable, "Confirm disabled en WaitingForAction.");
        }

        [Test]
        public void BehaviorSelected_FiresDelegate()
        {
            int selectedIndex = -1;
            _view.OnBehaviorSelected = (idx) => selectedIndex = idx;

            var movement = CreateButton("MovementBtn", _go);
            AssignPrivate(_view, "_movementButton", movement);

            var awake = typeof(PlayerActionButtonsView).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(_view, null);

            movement.onClick.Invoke();
            Assert.AreEqual(0, selectedIndex, "Movement click debe invocar OnBehaviorSelected(0).");
        }

        private static Button CreateButton(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<Button>();
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
