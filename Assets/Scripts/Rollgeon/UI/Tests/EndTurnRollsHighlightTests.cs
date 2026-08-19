using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica el estado de <see cref="EndTurnRollsHighlight"/>: se enciende con el
    /// pool en 0 durante el turno del player y vuelve a reposo al recuperar rolls,
    /// terminar el turno o desbindear. En EditMode los tweens se saltean — acá se
    /// testea solo la máquina de estados.
    /// </summary>
    [TestFixture]
    public class EndTurnRollsHighlightTests
    {
        private GameObject _go;
        private EndTurnRollsHighlight _highlight;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("RollsHighlight", typeof(RectTransform));
            _highlight = _go.AddComponent<EndTurnRollsHighlight>();

            var awake = typeof(EndTurnRollsHighlight).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(_highlight, null);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        private RectTransform _follow;
        private UnityEngine.UI.Image _glow;

        private void EnterPlayerTurnWithEmptyPool()
        {
            _highlight.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 3);
        }

        /// <summary>
        /// Cablea el botón a seguir + glow, ambos en pose de reposo (escala 1,
        /// posición 0). Bind captura los reposos.
        /// </summary>
        private void WireFollowTargets()
        {
            _follow = NewChildRect("Button");
            var glowGo = new GameObject("Glow", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            glowGo.transform.SetParent(_go.transform, worldPositionStays: false);
            _glow = glowGo.GetComponent<UnityEngine.UI.Image>();

            AssignPrivate("_followButton", _follow);
            AssignPrivate("_glowImage", _glow);
        }

        private RectTransform NewChildRect(string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(_go.transform, worldPositionStays: false);
            return rect;
        }

        private void AssignPrivate(string fieldName, object value)
        {
            var field = typeof(EndTurnRollsHighlight).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Campo privado '{fieldName}' no encontrado.");
            field.SetValue(_highlight, value);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Método '{methodName}' no encontrado.");
            method.Invoke(target, null);
        }

        [Test]
        public void EmptyPoolDuringPlayerTurn_ActivatesHighlight()
        {
            EnterPlayerTurnWithEmptyPool();

            Assert.IsTrue(_highlight.IsHighlightActive,
                "Con el pool en 0 en el turno del player, el highlight debe encenderse.");
        }

        [Test]
        public void EmptyPoolWithoutPlayerTurn_KeepsHighlightOff()
        {
            _highlight.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 3);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Fuera del turno del player el pool en 0 no debe resaltar el botón.");
        }

        [Test]
        public void RollsRecovered_DeactivatesHighlight()
        {
            EnterPlayerTurnWithEmptyPool();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 2, 3);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Al recuperar rolls el botón debe volver al estado default.");
        }

        [Test]
        public void OtherGuidRolls_IsIgnored()
        {
            _highlight.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, Guid.NewGuid(), 0, 3);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "El pool de otra entidad no debe activar el highlight.");
        }

        [Test]
        public void TurnFinished_DeactivatesHighlight()
        {
            EnterPlayerTurnWithEmptyPool();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Terminar el turno debe apagar el highlight.");
        }

        [Test]
        public void DiceRolled_SuppressesHighlightUntilResolved()
        {
            EnterPlayerTurnWithEmptyPool();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_highlight.IsHighlightActive,
                "Con un roll en el aire el botón está disabled — no debe resaltar.");

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);
            Assert.IsTrue(_highlight.IsHighlightActive,
                "Resuelto el roll, el pool sigue en 0: el highlight vuelve.");
        }

        [Test]
        public void CombatEnd_DeactivatesHighlight()
        {
            EnterPlayerTurnWithEmptyPool();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnCombatEnd, _playerGuid);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Fin de combate debe forzar el reposo.");
        }

        [Test]
        public void HoverMovesButton_GlowMirrorsPose()
        {
            // Arrange
            WireFollowTargets();
            EnterPlayerTurnWithEmptyPool();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            // Act — JuicyMenuButton agranda y corre el botón en hover; el espejo
            // corre en LateUpdate.
            _follow.localScale = new Vector3(1.15f, 1.15f, 1f);
            _follow.anchoredPosition = new Vector2(-12f, 0f);
            InvokeNonPublic(_highlight, "LateUpdate");

            // Assert
            Assert.AreEqual(1.15f, _glow.rectTransform.localScale.x, 1e-4f,
                "El glow debe escalar junto al botón hovereado.");
            Assert.AreEqual(-12f, _glow.rectTransform.anchoredPosition.x, 1e-3f,
                "El glow debe acompañar el offset X del hover.");
        }

        [Test]
        public void RollsRecoveredMidHover_RestoresGlowPose()
        {
            // Arrange — highlight activo y botón hovereado ya espejado.
            WireFollowTargets();
            EnterPlayerTurnWithEmptyPool();
            _follow.localScale = new Vector3(1.15f, 1.15f, 1f);
            _follow.anchoredPosition = new Vector2(-12f, 0f);
            InvokeNonPublic(_highlight, "LateUpdate");

            // Act
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 2, 3);

            // Assert
            Assert.AreEqual(1f, _glow.rectTransform.localScale.x, 1e-4f,
                "Al apagarse el highlight el glow vuelve a su escala de reposo.");
            Assert.AreEqual(0f, _glow.rectTransform.anchoredPosition.x, 1e-3f,
                "Al apagarse el highlight el glow vuelve a su posición de reposo.");
        }

        [Test]
        public void Unbind_DeactivatesHighlight()
        {
            EnterPlayerTurnWithEmptyPool();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            _highlight.Unbind();

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Unbind debe dejar el botón en reposo y sin suscripciones.");

            // Post-unbind los eventos no deben pegar en el componente.
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 3);
            Assert.IsFalse(_highlight.IsHighlightActive,
                "Después de Unbind los eventos deben ser ignorados.");
        }
    }
}
