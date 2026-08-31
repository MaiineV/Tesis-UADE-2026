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
    /// <summary>
    /// El view solo orquesta los 4 chips de behavior — el botón Confirm lo absorbió
    /// el botón contextual de turno (ver <c>EndTurnButtonViewTests</c>, que hereda
    /// aquella cobertura de gating).
    /// </summary>
    [TestFixture]
    public class PlayerActionButtonsViewTests
    {
        private GameObject _go;
        private PlayerActionButtonsView _view;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("PlayerActionButtons");
            _view = _go.AddComponent<PlayerActionButtonsView>();

            AssignPrivate(_view, "_buttons", new ActionButton[4]);

            InvokeAwake(_view);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        // El estado interno _isPlayerTurn es el observable de lifecycle que quedó
        // tras mudarse el Confirm: refleja si los handlers del bus siguen vivos.
        private bool IsPlayerTurn()
        {
            var field = typeof(PlayerActionButtonsView).GetField("_isPlayerTurn",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Campo '_isPlayerTurn' no encontrado.");
            return (bool)field.GetValue(_view);
        }

        [Test]
        public void should_track_player_turn_when_turn_started()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Assert
            Assert.IsTrue(IsPlayerTurn(), "OnTurnStarted del player debe marcar el turno.");
        }

        [Test]
        public void should_ignore_turn_started_of_other_entity()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            // Assert
            Assert.IsFalse(IsPlayerTurn(), "El turno de otra entidad no es el del player.");
        }

        [Test]
        public void should_remove_subscriptions_when_unbind()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            _view.Unbind();
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Assert
            Assert.IsFalse(IsPlayerTurn(), "Tras Unbind, los eventos no deben tener efecto.");
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

            // Assert
            Assert.IsFalse(IsPlayerTurn(), "OnDisable desuscribe; el evento no tiene efecto.");
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

        [Test]
        public void should_not_invoke_behavior_selected_when_click_activation_disabled()
        {
            // Arrange — CNF-002 v2: los chips de combate son drag-only; el
            // ActionDragController apaga el click via SetClickActivation(false).
            int selectedIndex = -1;
            _view.OnBehaviorSelected = (idx) => selectedIndex = idx;

            var actionButton = CreateActionButton("MovementBtn", _go, HeroBehaviorSlot.Movement);
            actionButton.SetClickActivation(false);
            var array = new ActionButton[4];
            array[0] = actionButton;
            AssignPrivate(_view, "_buttons", array);

            InvokeAwake(_view);

            // Act
            actionButton.Button.onClick.Invoke();

            // Assert
            Assert.AreEqual(-1, selectedIndex,
                "Con la activación por click deshabilitada, el click no debe invocar OnBehaviorSelected.");
        }

        [Test]
        public void should_invoke_behavior_selected_via_delegate_when_click_disabled()
        {
            // Arrange — el seam OnClicked (usado por el drag dispatcher) NO pasa por el
            // gate de click: debe seguir activando el behavior aunque el click esté off.
            int selectedIndex = -1;
            _view.OnBehaviorSelected = (idx) => selectedIndex = idx;

            var actionButton = CreateActionButton("MovementBtn", _go, HeroBehaviorSlot.Movement);
            actionButton.SetClickActivation(false);
            var array = new ActionButton[4];
            array[0] = actionButton;
            AssignPrivate(_view, "_buttons", array);

            InvokeAwake(_view);

            // Act
            actionButton.OnClicked?.Invoke();

            // Assert
            Assert.AreEqual(0, selectedIndex,
                "OnClicked directo (drag dispatcher) debe activar el behavior aun con click off.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

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

        // ------------------------------------------------------------------
        // ShouldFlagUnaffordable — regla pura del flag ortogonal (BUG-074)
        // ------------------------------------------------------------------

        [Test]
        public void test_shouldFlagUnaffordable_playerTurnIdleNoRolls_flags()
        {
            Assert.IsTrue(PlayerActionButtonsView.ShouldFlagUnaffordable(
                isPlayerTurn: true, inChain: false, rolled: false,
                modalRollActive: false, hasRolls: false));
        }

        [Test]
        public void test_shouldFlagUnaffordable_withRolls_doesNotFlag()
        {
            Assert.IsFalse(PlayerActionButtonsView.ShouldFlagUnaffordable(
                isPlayerTurn: true, inChain: false, rolled: false,
                modalRollActive: false, hasRolls: true));
        }

        [Test]
        public void test_shouldFlagUnaffordable_enemyTurn_doesNotFlag()
        {
            Assert.IsFalse(PlayerActionButtonsView.ShouldFlagUnaffordable(
                isPlayerTurn: false, inChain: false, rolled: false,
                modalRollActive: false, hasRolls: false));
        }

        [Test]
        public void test_shouldFlagUnaffordable_actionInFlight_doesNotFlag()
        {
            // El pool baja transitoriamente durante la propia acción — pintar rojo
            // ahí es ruido (parpadeo durante la animación).
            Assert.IsFalse(PlayerActionButtonsView.ShouldFlagUnaffordable(
                isPlayerTurn: true, inChain: true, rolled: false,
                modalRollActive: false, hasRolls: false));
            Assert.IsFalse(PlayerActionButtonsView.ShouldFlagUnaffordable(
                isPlayerTurn: true, inChain: false, rolled: true,
                modalRollActive: false, hasRolls: false));
        }

        [Test]
        public void test_shouldFlagUnaffordable_modalRollOpen_doesNotFlag()
        {
            Assert.IsFalse(PlayerActionButtonsView.ShouldFlagUnaffordable(
                isPlayerTurn: true, inChain: false, rolled: false,
                modalRollActive: true, hasRolls: false));
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
