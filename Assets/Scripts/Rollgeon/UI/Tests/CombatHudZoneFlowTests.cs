using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// CNF-007 — el flow de zonas apaga los chips al arrancar el flujo de dados y los
    /// restaura al resolverse la acción. Duraciones en 0 → aplica sin tween (EditMode).
    /// </summary>
    public class CombatHudZoneFlowTests
    {
        private GameObject _go;
        private CanvasGroup _chipsGroup;
        private CombatHudZoneFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ZoneFlowRoot");
            _chipsGroup = _go.AddComponent<CanvasGroup>();
            _flow = _go.AddComponent<CombatHudZoneFlow>();

            AssignPrivate(_flow, "_chipsGroup", _chipsGroup);
            AssignPrivate(_flow, "_fadeSeconds", 0f);
            AssignPrivate(_flow, "_chipMoveSeconds", 0f);

            // EditMode: OnEnable no corre en AddComponent — se invoca a mano DESPUÉS de
            // asignar los campos (mismo patrón que InvokeAwake en PlayerActionButtonsViewTests).
            InvokeNonPublic(_flow, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            // OnDisable explícito → desuscribe del EventManager. Sin esto los triggers de
            // otros fixtures pegarían en un componente muerto (suite flaky).
            InvokeNonPublic(_flow, "OnDisable");
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void should_hide_chips_zone_when_dice_flow_starts()
        {
            // Arrange
            _chipsGroup.alpha = 1f;
            _chipsGroup.interactable = true;

            // Act
            EventManager.Trigger(EventName.OnChainStarted, System.Guid.NewGuid());

            // Assert
            Assert.AreEqual(0f, _chipsGroup.alpha, "La zona de chips debe apagarse al arrancar el flujo de dados.");
            Assert.IsFalse(_chipsGroup.interactable, "La zona de chips no debe ser interactuable durante el roll.");
            Assert.IsTrue(_flow.IsRolling, "El flow debe quedar en estado rolling.");
        }

        [Test]
        public void should_restore_chips_zone_when_behavior_executes()
        {
            // Arrange — entrar al estado rolling primero.
            EventManager.Trigger(EventName.OnChainStarted, System.Guid.NewGuid());
            Assert.AreEqual(0f, _chipsGroup.alpha, "Precondición: chips apagados durante el roll.");

            // Act
            EventManager.Trigger(EventName.OnBehaviorExecuted, System.Guid.NewGuid(), "Base Attack", true);

            // Assert
            Assert.AreEqual(1f, _chipsGroup.alpha, "La zona de chips debe volver al resolverse la acción.");
            Assert.IsTrue(_chipsGroup.interactable, "La zona de chips debe volver a ser interactuable.");
            Assert.IsFalse(_flow.IsRolling, "El flow debe salir del estado rolling.");
        }

        [Test]
        public void should_ignore_flow_end_when_not_rolling()
        {
            // Arrange — chips visibles, sin flujo de dados en curso.
            _chipsGroup.alpha = 1f;
            _chipsGroup.interactable = true;

            // Act — OnTurnStarted llega en cada turno, incluso sin roll previo.
            EventManager.Trigger(EventName.OnTurnStarted, System.Guid.NewGuid());

            // Assert
            Assert.AreEqual(1f, _chipsGroup.alpha, "Sin roll en curso, el fin de flujo no debe tocar la zona.");
            Assert.IsFalse(_flow.IsRolling);
        }

        // ======================================================================
        // Breath / Punch — selección de target
        // ======================================================================

        [Test]
        public void should_enter_breath_state_when_chain_target_selection_starts()
        {
            // Arrange
            WireSelectedChip();

            // Act
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, System.Guid.NewGuid());

            // Assert
            Assert.IsTrue(_flow.IsBreathing, "El chip seleccionado debe respirar mientras espera target.");
            Assert.IsFalse(_flow.IsRolling, "La selección pre-roll no debe arrancar el flujo de dados.");
        }

        [Test]
        public void should_enter_breath_state_when_action_selection_starts()
        {
            // Arrange
            WireSelectedChip();

            // Act — path Movement / ActionRoll (acción sin tirada comprometida).
            EventManager.Trigger(EventName.OnActionSelectionStarted, System.Guid.NewGuid());

            // Assert
            Assert.IsTrue(_flow.IsBreathing, "El chip debe respirar mientras la acción espera el tile target.");
        }

        [Test]
        public void should_stop_breath_and_enter_rolling_when_chain_starts()
        {
            // Arrange — breath activo por la selección de fase 0.
            WireSelectedChip();
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, System.Guid.NewGuid());
            Assert.IsTrue(_flow.IsBreathing, "Precondición: breath activo.");

            // Act — el confirm de la fase 0 llega como OnChainStarted.
            EventManager.Trigger(EventName.OnChainStarted, System.Guid.NewGuid());

            // Assert
            Assert.IsFalse(_flow.IsBreathing, "El punch del confirm corta el breath — el chip queda estático.");
            Assert.IsTrue(_flow.IsRolling, "El confirm arranca el flujo de dados.");
        }

        [Test]
        public void should_stop_breath_when_interactive_target_confirmed()
        {
            // Arrange
            WireSelectedChip();
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, System.Guid.NewGuid());
            Assert.IsTrue(_flow.IsBreathing, "Precondición: breath activo.");

            // Act — el jugador clickeó un enemigo (target real, no Empty).
            EventManager.Trigger(EventName.OnCombatTargetChanged, System.Guid.NewGuid(), System.Guid.NewGuid());

            // Assert
            Assert.IsFalse(_flow.IsBreathing, "Confirmar el target debe frenar el breath (punch + estático).");
        }

        [Test]
        public void should_stop_breath_when_combat_target_cleared()
        {
            // Arrange
            WireSelectedChip();
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, System.Guid.NewGuid());
            Assert.IsTrue(_flow.IsBreathing, "Precondición: breath activo.");

            // Act — target limpiado (cancel de la selección).
            EventManager.Trigger(EventName.OnCombatTargetChanged, System.Guid.NewGuid(), System.Guid.Empty);

            // Assert
            Assert.IsFalse(_flow.IsBreathing, "Limpiar el target debe frenar el breath sin punch.");
        }

        [Test]
        public void should_stop_breath_when_behavior_executes_without_roll()
        {
            // Arrange — path Movement: selección sin tirada.
            WireSelectedChip();
            EventManager.Trigger(EventName.OnActionSelectionStarted, System.Guid.NewGuid());
            Assert.IsTrue(_flow.IsBreathing, "Precondición: breath activo.");

            // Act
            EventManager.Trigger(EventName.OnBehaviorExecuted, System.Guid.NewGuid(), "Movement", true);

            // Assert
            Assert.IsFalse(_flow.IsBreathing, "Al ejecutarse (o cancelarse) la acción el breath debe morir.");
            Assert.IsFalse(_flow.IsRolling);
        }

        [Test]
        public void should_stop_breath_when_combat_ends()
        {
            // Arrange
            WireSelectedChip();
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, System.Guid.NewGuid());
            Assert.IsTrue(_flow.IsBreathing, "Precondición: breath activo.");

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, System.Guid.NewGuid());

            // Assert
            Assert.IsFalse(_flow.IsBreathing, "Fin de combate debe forzar el reset del breath.");
        }

        [Test]
        public void should_not_enter_breath_state_when_no_slot_selected()
        {
            // Arrange
            WireSelectedChip();
            AssignPrivate(_buttonsView, "_selectedSlot", null);

            // Act
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, System.Guid.NewGuid());

            // Assert
            Assert.IsFalse(_flow.IsBreathing, "Sin slot seleccionado no hay chip que respire.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private PlayerActionButtonsView _buttonsView;
        private ActionButton _chip;

        /// <summary>
        /// Cablea un PlayerActionButtonsView con un chip en el slot 0 ya seleccionado —
        /// el mínimo que StartBreath necesita para resolver el botón que respira.
        /// </summary>
        private void WireSelectedChip()
        {
            var buttonsGo = new GameObject("Buttons", typeof(RectTransform));
            buttonsGo.transform.SetParent(_go.transform, worldPositionStays: false);
            _buttonsView = buttonsGo.AddComponent<PlayerActionButtonsView>();

            var chipGo = new GameObject("Chip0", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            chipGo.transform.SetParent(buttonsGo.transform, worldPositionStays: false);
            var uiButton = chipGo.AddComponent<Button>();
            uiButton.targetGraphic = chipGo.GetComponent<Image>();
            _chip = chipGo.AddComponent<ActionButton>();
            InvokeNonPublic(_chip, "Awake");

            var buttons = new ActionButton[4];
            buttons[0] = _chip;
            AssignPrivate(_buttonsView, "_buttons", buttons);
            AssignPrivate(_buttonsView, "_selectedSlot", 0);
            AssignPrivate(_flow, "_buttonsView", _buttonsView);
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Campo privado '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Método '{methodName}' no encontrado en {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
