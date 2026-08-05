using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Dice;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre el indicador de costo de <see cref="RerollCountView"/>: el botón muestra el
    /// costo con el icono de energía y la línea de ayuda aparece solo cuando el próximo
    /// roll efectivamente se paga.
    /// </summary>
    /// <remarks>
    /// El "(1E)" pelado se leía como bug de traducción — el fix fue icono + explicación.
    /// Los tests asertan <b>visibilidad y expansión del placeholder</b>, no la copia:
    /// el texto depende del locale activo y ya lo guarda <c>LocalizationTablesTests</c>.
    /// </remarks>
    [TestFixture]
    public class RerollCountViewPaidHintTests
    {
        /// <summary>Budget de mentira: solo tiene que contestar el query de costo.</summary>
        private sealed class FakeBudget : IRerollBudgetService
        {
            public RerollQueryResult NextQuery = RerollQueryResult.Free();

            public RerollBudget Current => null;

#pragma warning disable CS0067 // La vista se suscribe pero el test dispara los refresh por Bind.
            public event Action<RerollStartedPayload> OnRerollStarted;
            public event Action<RerollBudget> OnBudgetStarted;
#pragma warning restore CS0067

            public void StartBudget(ActionDefinitionSO action) { }
            public void EndBudget() { }
            public RerollQueryResult QueryExtraRoll(Guid playerGuid) => NextQuery;
            public bool TryExtraRoll(Guid playerGuid) => false;
        }

        private GameObject _go;
        private RerollCountView _view;
        private TextMeshProUGUI _buttonLabel;
        private TextMeshProUGUI _hint;
        private FakeBudget _budget;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _playerGuid = Guid.NewGuid();
            _budget = new FakeBudget();
            ServiceLocator.AddService<IRerollBudgetService>(_budget);

            _go = new GameObject("RerollCount");
            _view = _go.AddComponent<RerollCountView>();

            AssignPrivate(_view, "_countLabel", MakeLabel("Count"));
            AssignPrivate(_view, "_extraRollButton", MakeButton("RollBtn"));

            _buttonLabel = MakeLabel("ButtonLabel");
            AssignPrivate(_view, "_buttonLabel", _buttonLabel);

            _hint = MakeLabel("PaidHint");
            AssignPrivate(_view, "_paidHintLabel", _hint);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void test_reroll_hint_is_visible_when_the_next_roll_costs_energy()
        {
            // Arrange
            _budget.NextQuery = RerollQueryResult.Paid();

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.IsTrue(_hint.gameObject.activeSelf,
                "Con el próximo roll pago, la línea que explica el costo tiene que estar.");
            Assert.IsNotEmpty(_hint.text);
        }

        [Test]
        public void test_reroll_hint_is_hidden_when_the_next_roll_is_free()
        {
            // Arrange — con rolls gratis disponibles la explicación de costo sería ruido.
            _budget.NextQuery = RerollQueryResult.Free();

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.IsFalse(_hint.gameObject.activeSelf);
        }

        [Test]
        public void test_reroll_hint_is_hidden_when_no_reroll_is_available()
        {
            // Arrange
            _budget.NextQuery = RerollQueryResult.Blocked("no-energy");

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.IsFalse(_hint.gameObject.activeSelf);
        }

        [Test]
        public void test_reroll_hint_is_hidden_after_the_roll_resolves()
        {
            // Arrange — resuelto el roll ya no hay costo pendiente que explicar.
            _budget.NextQuery = RerollQueryResult.Paid();
            _view.Bind(_playerGuid);
            Assume.That(_hint.gameObject.activeSelf, Is.True, "Precondición: el hint arranca visible.");

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.IsFalse(_hint.gameObject.activeSelf);
        }

        [Test]
        public void test_reroll_button_label_expands_the_energy_placeholder_into_a_sprite_tag()
        {
            // Arrange
            _budget.NextQuery = RerollQueryResult.Paid();

            // Act
            _view.Bind(_playerGuid);

            // Assert — un {ENERGY} crudo en pantalla significa que falta el glifo en el atlas.
            StringAssert.DoesNotContain("{ENERGY}", _buttonLabel.text);
            StringAssert.Contains("<sprite name=", _buttonLabel.text);
        }

        [Test]
        public void test_reroll_view_does_not_throw_when_the_hint_label_is_not_wired()
        {
            // Arrange — prefab sin el label de ayuda (setup viejo / rollback).
            AssignPrivate(_view, "_paidHintLabel", null);
            _budget.NextQuery = RerollQueryResult.Paid();

            // Act / Assert
            Assert.DoesNotThrow(() => _view.Bind(_playerGuid));
        }

        private TextMeshProUGUI MakeLabel(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_go.transform, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private Button MakeButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_go.transform, false);
            return go.AddComponent<Button>();
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
