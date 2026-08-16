using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica el estado de <see cref="EndTurnEnergyHighlight"/>: se enciende con
    /// energía 0 durante el turno del player y vuelve a reposo al recuperar energía,
    /// terminar el turno o desbindear. En EditMode los tweens se saltean — acá se
    /// testea solo la máquina de estados.
    /// </summary>
    [TestFixture]
    public class EndTurnEnergyHighlightTests
    {
        private GameObject _go;
        private EndTurnEnergyHighlight _highlight;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("EnergyHighlight", typeof(RectTransform));
            _highlight = _go.AddComponent<EndTurnEnergyHighlight>();

            var awake = typeof(EndTurnEnergyHighlight).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(_highlight, null);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        private void EnterPlayerTurnWithZeroEnergy()
        {
            _highlight.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 0, 3);
        }

        [Test]
        public void ZeroEnergyDuringPlayerTurn_ActivatesHighlight()
        {
            EnterPlayerTurnWithZeroEnergy();

            Assert.IsTrue(_highlight.IsHighlightActive,
                "Con energía 0 en el turno del player, el highlight debe encenderse.");
        }

        [Test]
        public void ZeroEnergyWithoutPlayerTurn_KeepsHighlightOff()
        {
            _highlight.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 0, 3);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Fuera del turno del player la energía 0 no debe resaltar el botón.");
        }

        [Test]
        public void EnergyRecovered_DeactivatesHighlight()
        {
            EnterPlayerTurnWithZeroEnergy();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 2, 3);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Al recuperar energía el botón debe volver al estado default.");
        }

        [Test]
        public void OtherGuidEnergy_IsIgnored()
        {
            _highlight.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, Guid.NewGuid(), 0, 3);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "La energía de otra entidad no debe activar el highlight.");
        }

        [Test]
        public void TurnFinished_DeactivatesHighlight()
        {
            EnterPlayerTurnWithZeroEnergy();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Terminar el turno debe apagar el highlight.");
        }

        [Test]
        public void DiceRolled_SuppressesHighlightUntilResolved()
        {
            EnterPlayerTurnWithZeroEnergy();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_highlight.IsHighlightActive,
                "Con un roll en el aire el botón está disabled — no debe resaltar.");

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);
            Assert.IsTrue(_highlight.IsHighlightActive,
                "Resuelto el roll, la energía sigue en 0: el highlight vuelve.");
        }

        [Test]
        public void CombatEnd_DeactivatesHighlight()
        {
            EnterPlayerTurnWithZeroEnergy();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            EventManager.Trigger(EventName.OnCombatEnd, _playerGuid);

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Fin de combate debe forzar el reposo.");
        }

        [Test]
        public void Unbind_DeactivatesHighlight()
        {
            EnterPlayerTurnWithZeroEnergy();
            Assert.IsTrue(_highlight.IsHighlightActive, "Precondición: highlight activo.");

            _highlight.Unbind();

            Assert.IsFalse(_highlight.IsHighlightActive,
                "Unbind debe dejar el botón en reposo y sin suscripciones.");

            // Post-unbind los eventos no deben pegar en el componente.
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, _playerGuid, 0, 3);
            Assert.IsFalse(_highlight.IsHighlightActive,
                "Después de Unbind los eventos deben ser ignorados.");
        }
    }
}
