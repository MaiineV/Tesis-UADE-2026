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

        [SetUp]
        public void Setup()
        {
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

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado.");
            field.SetValue(target, value);
        }
    }
}
