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
    /// Verifica <see cref="ActionButtonsView"/>: los botones se habilitan/deshabilitan
    /// segun <c>OnTurnStarted/Finished</c> y el reroll cae al fallback sin
    /// <c>IRerollBudgetService</c>. Plan §3.10.
    /// </summary>
    [TestFixture]
    public class ActionButtonsViewTests
    {
        private GameObject _go;
        private ActionButtonsView _view;
        private Button _attack;
        private Button _reroll;
        private Button _endTurn;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("ActionButtons");
            _view = _go.AddComponent<ActionButtonsView>();

            _attack = CreateButton("AttackBtn", _go);
            _reroll = CreateButton("RerollBtn", _go);
            _endTurn = CreateButton("EndTurnBtn", _go);

            AssignPrivate(_view, "_attackButton", _attack);
            AssignPrivate(_view, "_energyRerollButton", _reroll);
            AssignPrivate(_view, "_endTurnButton", _endTurn);

            // Forzar Awake via direct call — AddComponent lo invoca al agregar el comp.
            // UnityEngine garantiza que Awake corre al AddComponent en test mode.
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_DisablesAllButtons_NoPlayerTurn()
        {
            _view.Bind(_playerGuid);

            Assert.IsFalse(_attack.interactable, "Attack inicia disabled (sin player turn).");
            Assert.IsFalse(_reroll.interactable, "Reroll inicia disabled.");
            Assert.IsFalse(_endTurn.interactable, "EndTurn inicia disabled.");
        }

        [Test]
        public void OnTurnStarted_Player_EnablesEndTurn()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsTrue(_endTurn.interactable,
                "End Turn se debe habilitar cuando empieza el turno del player.");
        }

        [Test]
        public void OnTurnStarted_Enemy_KeepsEndTurnDisabled()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            Assert.IsFalse(_endTurn.interactable,
                "End Turn debe quedar disabled cuando el turno no es del player.");
        }

        [Test]
        public void OnTurnFinished_Player_DisablesEndTurn()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_endTurn.interactable);

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);
            Assert.IsFalse(_endTurn.interactable);
        }

        [Test]
        public void Reroll_WithoutService_IsDisabled()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsFalse(_reroll.interactable,
                "Sin IRerollBudgetService registrado, reroll queda disabled.");
        }

        [Test]
        public void Attack_WithoutActionDefinition_IsDisabled()
        {
            // No asignamos _attackAction — CanExecuteAttack retorna false por el null check.
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.IsFalse(_attack.interactable,
                "Sin ActionDefinitionSO cableado, attack queda disabled.");
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
