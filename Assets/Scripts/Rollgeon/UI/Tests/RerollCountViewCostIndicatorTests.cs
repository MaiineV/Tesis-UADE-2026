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

        private readonly System.Collections.Generic.List<UnityEngine.Object> _spriteCleanup =
            new System.Collections.Generic.List<UnityEngine.Object>();

        private GameObject _go;
        private RerollCountView _view;
        private TextMeshProUGUI _buttonLabel;
        private FakeBudget _budget;
        private Guid _playerGuid;
        private Button _button;
        private Image _buttonImage;
        private Sprite _freeNormal;
        private Sprite _paidNormal;

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
            _button = MakeButton("RollBtn");
            AssignPrivate(_view, "_extraRollButton", _button);

            _buttonLabel = MakeLabel("ButtonLabel");
            AssignPrivate(_view, "_buttonLabel", _buttonLabel);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            foreach (var o in _spriteCleanup)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spriteCleanup.Clear();
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

        // ==================================================================
        // Sprite contextual del botón (Roll2 gratis / CircleButton3 pago)
        // ==================================================================

        [Test]
        public void test_reroll_button_shows_the_paid_sprite_when_the_next_roll_costs_energy()
        {
            // Arrange
            WireButtonSprites();
            _budget.NextQuery = RerollQueryResult.Paid();

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.AreSame(_paidNormal, _buttonImage.sprite,
                "Con el próximo tiro pago, el botón usa el arte de reroll con energía.");
        }

        [Test]
        public void test_reroll_button_shows_the_free_sprite_when_the_next_roll_is_free()
        {
            // Arrange
            WireButtonSprites();
            _budget.NextQuery = RerollQueryResult.Free();

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.AreSame(_freeNormal, _buttonImage.sprite,
                "Con rolls gratis, el botón usa el arte de reroll gratis.");
        }

        [Test]
        public void test_reroll_button_returns_to_the_free_sprite_after_the_roll_resolves()
        {
            // Arrange — el reset del sprite debe ser explícito (como el del label): el
            // query del budget viejo puede seguir contestando "pago" tras resolver.
            WireButtonSprites();
            _budget.NextQuery = RerollQueryResult.Paid();
            _view.Bind(_playerGuid);
            Assume.That(_buttonImage.sprite, Is.SameAs(_paidNormal),
                "Precondición: el botón arranca con el arte pago.");

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.AreSame(_freeNormal, _buttonImage.sprite,
                "Resuelto el roll, el botón vuelve al arte de primer roll.");
        }

        // ==================================================================
        // Sink "sin rerolls" (media ficha bajo el borde, como la acción usada)
        // ==================================================================

        [Test]
        public void test_reroll_button_sinks_when_out_of_free_rolls_and_energy()
        {
            // Arrange
            _budget.NextQuery = RerollQueryResult.Blocked(RerollBudgetService.BlockedReasonNoEnergy);
            _view.Bind(_playerGuid);

            // Act + Assert
            Assert.IsTrue(InvokeIsOutOfRerolls(),
                "Sin free rolls ni energía el botón debe hundirse a la mitad.");
        }

        [Test]
        public void test_reroll_button_sinks_when_the_action_forbids_energy_rerolls()
        {
            // Arrange — sin free rolls y la acción no permite pagar: tampoco hay próximo tiro.
            _budget.NextQuery = RerollQueryResult.Blocked(
                RerollBudgetService.BlockedReasonActionForbidsEnergyReroll);
            _view.Bind(_playerGuid);

            // Act + Assert
            Assert.IsTrue(InvokeIsOutOfRerolls(),
                "Con la acción sin paid rerolls y sin free rolls el botón debe hundirse.");
        }

        [Test]
        public void test_reroll_button_does_not_sink_between_actions()
        {
            // Arrange — sin budget abierto el botón es el "Roll" de la próxima acción.
            _budget.NextQuery = RerollQueryResult.Blocked(
                RerollBudgetService.BlockedReasonNoActiveBudget);
            _view.Bind(_playerGuid);

            // Act + Assert
            Assert.IsFalse(InvokeIsOutOfRerolls(),
                "Entre acciones no hay hundimiento: el botón espera el próximo Roll.");
        }

        [Test]
        public void test_reroll_button_does_not_sink_while_a_reroll_is_available()
        {
            // Arrange
            _budget.NextQuery = RerollQueryResult.Paid();
            _view.Bind(_playerGuid);

            // Act + Assert
            Assert.IsFalse(InvokeIsOutOfRerolls(),
                "Con un reroll pago disponible el botón queda en su lugar.");
        }

        private bool InvokeIsOutOfRerolls()
        {
            var method = typeof(RerollCountView).GetMethod("IsOutOfRerolls",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Método 'IsOutOfRerolls' no encontrado.");
            return (bool)method.Invoke(_view, null);
        }

        /// <summary>
        /// Cablea el swap de sprites sobre el botón ya creado en SetUp. Solo lo usan
        /// los tests de sprite para no tocar el harness de los tests de label.
        /// </summary>
        private void WireButtonSprites()
        {
            _buttonImage = _button.gameObject.AddComponent<Image>();
            var swap = _button.gameObject.AddComponent<HudButtonSpriteSwap>();

            _freeNormal = MakeSprite();
            _paidNormal = MakeSprite();
            AssignPrivate(_view, "_buttonSprites", swap);
            AssignPrivate(_view, "_freeRollSprites", new ButtonSpriteSet(_freeNormal, null));
            AssignPrivate(_view, "_paidRollSprites", new ButtonSpriteSet(_paidNormal, null));
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(2, 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _spriteCleanup.Add(tex);
            _spriteCleanup.Add(sprite);
            return sprite;
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
