using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;

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
        // Helpers
        // ======================================================================

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
