using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="PlayerActionButtonsView"/>: los botones se habilitan/deshabilitan
    /// segun la fase behavior-first (Idle, WaitingForAction, Rolled).
    /// </summary>
    [TestFixture]
    public class PlayerActionButtonsViewTests
    {
        private GameObject _go;
        private PlayerActionButtonsView _view;
        private Button _rollDice;
        private Button _reroll;
        private Button _confirmAttack;
        private Button _endTurn;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("PlayerActionButtons");
            _view = _go.AddComponent<PlayerActionButtonsView>();

            _rollDice = CreateButton("RollDiceBtn", _go);
            _reroll = CreateButton("RerollBtn", _go);
            _confirmAttack = CreateButton("ConfirmBtn", _go);
            _endTurn = CreateButton("EndTurnBtn", _go);

            AssignPrivate(_view, "_rollDiceButton", _rollDice);
            AssignPrivate(_view, "_rerollButton", _reroll);
            AssignPrivate(_view, "_confirmAttackButton", _confirmAttack);
            AssignPrivate(_view, "_endTurnButton", _endTurn);

            // Awake ran before fields were assigned; re-invoke to wire onClick listeners
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

            Assert.IsFalse(_rollDice.interactable, "RollDice inicia disabled (Idle).");
            Assert.IsFalse(_reroll.interactable, "Reroll inicia disabled.");
            Assert.IsFalse(_confirmAttack.interactable, "Confirm inicia disabled.");
            Assert.IsFalse(_endTurn.interactable, "EndTurn inicia disabled.");
        }

        [Test]
        public void OnTurnStarted_Player_EnablesBehaviorsAndEndTurn()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsTrue(_rollDice.interactable, "Legacy RollDice enabled en WaitingForAction.");
            Assert.IsFalse(_reroll.interactable, "Reroll disabled en WaitingForAction.");
            Assert.IsFalse(_confirmAttack.interactable, "Confirm disabled en WaitingForAction.");
            Assert.IsTrue(_endTurn.interactable, "EndTurn enabled en WaitingForAction.");
        }

        [Test]
        public void OnTurnStarted_OtherEntity_KeepsAllDisabled()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            Assert.IsFalse(_rollDice.interactable);
            Assert.IsFalse(_reroll.interactable);
            Assert.IsFalse(_confirmAttack.interactable);
            Assert.IsFalse(_endTurn.interactable);
        }

        [Test]
        public void OnDiceRolled_Player_DisablesBehaviorsEnablesConfirm()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            Assert.IsFalse(_rollDice.interactable, "Legacy RollDice disabled en Rolled.");
            Assert.IsTrue(_confirmAttack.interactable, "Confirm enabled en Rolled.");
            Assert.IsFalse(_endTurn.interactable, "EndTurn disabled en Rolled (behavior in progress).");
        }

        [Test]
        public void OnDiceRolled_Player_RerollDisabledWithoutService()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            Assert.IsFalse(_reroll.interactable,
                "Sin IRerollBudgetService registrado, reroll queda disabled.");
        }

        [Test]
        public void OnTurnFinished_Player_DisablesAll()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_rollDice.interactable);

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            Assert.IsFalse(_rollDice.interactable, "RollDice disabled tras TurnFinished.");
            Assert.IsFalse(_reroll.interactable, "Reroll disabled tras TurnFinished.");
            Assert.IsFalse(_confirmAttack.interactable, "Confirm disabled tras TurnFinished.");
            Assert.IsFalse(_endTurn.interactable, "EndTurn disabled tras TurnFinished.");
        }

        [Test]
        public void Unbind_RemovesSubscriptions()
        {
            _view.Bind(_playerGuid);
            _view.Unbind();

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsFalse(_rollDice.interactable,
                "Tras Unbind, OnTurnStarted no debe tener efecto.");
            Assert.IsFalse(_endTurn.interactable);
        }

        [Test]
        public void DoubleBindIsIdempotent()
        {
            _view.Bind(_playerGuid);
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_rollDice.interactable, "Tras doble Bind, un solo handler activo.");

            _view.Unbind();
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_rollDice.interactable,
                "Tras Unbind del doble Bind, no quedan handlers colgados.");
        }

        [Test]
        public void OnDisable_Unbinds()
        {
            _view.Bind(_playerGuid);
            // SendMessage pega contra la assertion interna de Unity
            // (ShouldRunBehaviour) en EditMode — invocamos OnDisable directo via
            // reflection para saltar esa check.
            var onDisable = typeof(PlayerActionButtonsView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, "OnDisable method not found on PlayerActionButtonsView.");
            onDisable.Invoke(_view, null);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_rollDice.interactable,
                "OnDisable desuscribe; el evento no tiene efecto.");
        }

        [Test]
        public void RollDiceButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnRollDicePressed.AddListener(() => fired = true);
            _rollDice.onClick.Invoke();
            Assert.IsTrue(fired, "OnRollDicePressed debe dispararse al clickear RollDice.");
        }

        [Test]
        public void RerollButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnRerollPressed.AddListener(() => fired = true);
            _reroll.onClick.Invoke();
            Assert.IsTrue(fired, "OnRerollPressed debe dispararse al clickear Reroll.");
        }

        [Test]
        public void ConfirmButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnConfirmAttackPressed.AddListener(() => fired = true);
            _confirmAttack.onClick.Invoke();
            Assert.IsTrue(fired, "OnConfirmAttackPressed debe dispararse al clickear Confirm.");
        }

        [Test]
        public void EndTurnButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnEndTurnPressed.AddListener(() => fired = true);
            _endTurn.onClick.Invoke();
            Assert.IsTrue(fired, "OnEndTurnPressed debe dispararse al clickear EndTurn.");
        }

        [Test]
        public void OnRollResolved_Player_ReturnsToWaitingForAction()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsTrue(_confirmAttack.interactable);

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            Assert.IsTrue(_rollDice.interactable, "Legacy RollDice enabled en WaitingForAction.");
            Assert.IsFalse(_reroll.interactable, "Reroll disabled en WaitingForAction.");
            Assert.IsFalse(_confirmAttack.interactable, "Confirm disabled en WaitingForAction.");
            Assert.IsTrue(_endTurn.interactable, "EndTurn enabled en WaitingForAction.");
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
