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
