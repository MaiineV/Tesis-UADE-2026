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
    /// Cubre el indicador de costo de <see cref="RerollCountView"/>: cuando el próximo
    /// roll se paga, el botón lo muestra con el icono de energía.
    /// </summary>
    /// <remarks>
    /// El "(1E)" pelado se leía como bug de traducción — el fix fue mostrar el costo con
    /// el glifo del atlas. Los tests asertan la <b>expansión del placeholder</b>, no la
    /// copia: el texto depende del locale activo y ya lo guarda <c>LocalizationTablesTests</c>.
    /// </remarks>
    [TestFixture]
    public class RerollCountViewCostIndicatorTests
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
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
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
        public void test_reroll_button_label_omits_the_cost_when_the_roll_is_free()
        {
            // Arrange — con rolls gratis no hay costo que anunciar.
            _budget.NextQuery = RerollQueryResult.Free();

            // Act
            _view.Bind(_playerGuid);

            // Assert
            StringAssert.DoesNotContain("<sprite name=", _buttonLabel.text);
            StringAssert.DoesNotContain("-1", _buttonLabel.text);
        }

        [Test]
        public void test_reroll_button_label_resets_to_the_first_roll_text_after_the_roll_resolves()
        {
            // Arrange
            _budget.NextQuery = RerollQueryResult.Paid();
            _view.Bind(_playerGuid);
            Assume.That(_buttonLabel.text, Does.Contain("<sprite name="),
                "Precondición: el botón arranca mostrando el costo.");

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert — resuelto el roll ya no hay costo pendiente.
            StringAssert.DoesNotContain("<sprite name=", _buttonLabel.text);
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
