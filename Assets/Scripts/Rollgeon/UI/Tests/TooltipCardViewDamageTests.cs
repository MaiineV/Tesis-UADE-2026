using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// El número de una tarjeta de ataque: cuándo aparece y de dónde sale.
    /// </summary>
    /// <remarks>
    /// Va pegado al título y nunca dentro de la frase, por lo mismo que el badge del stack: un
    /// rebalanceo cambia un número del dato y no toca una línea de texto en ningún idioma. Este
    /// fixture es el que cubre el daño del disparo desde que su frase quedó vacía.
    /// </remarks>
    [TestFixture]
    public class TooltipCardViewDamageTests
    {
        private GameObject _go;
        private TooltipCardView _view;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _rule;
        private TextMeshProUGUI _damage;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TooltipCard", typeof(RectTransform))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            _title = Label("Title");
            _rule = Label("Rule");
            _damage = Label("Damage");

            _view = _go.AddComponent<TooltipCardView>();
            SetPrivate("_titleLabel", _title);
            SetPrivate("_ruleLabel", _rule);
            SetPrivate("_damageLabel", _damage);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void ConDanio_ElNumeroSeDibujaSolo()
        {
            // Arrange
            var state = new StatusIconState("intent.ranged_shot", "Te dispara", string.Empty,
                                            null, active: true, damage: 24);

            // Act
            _view.Show(state);

            // Assert — el monto viaja con el indicador de daño pegado a su derecha (03/09).
            Assert.IsTrue(_damage.gameObject.activeSelf);
            Assert.AreEqual(
                Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(24), _damage.text);
            Assert.IsFalse(_rule.gameObject.activeSelf,
                "La tarjeta de sólo título dejó la regla prendida y vacía: un renglón alto de nada " +
                "debajo del nombre del ataque.");
        }

        [Test]
        public void SinDanio_ElNumeroNoOcupaLugar()
        {
            // Arrange — un estado aplicado no pega por sí mismo.
            var state = new StatusIconState("status.stun", "Aturdido", "Este turno no ataca.",
                                            null, active: true);

            // Act
            _view.Show(state);

            // Assert
            Assert.IsFalse(_damage.gameObject.activeSelf);
        }

        [Test]
        public void ElNumeroNuncaVaAdentroDeLaFrase()
        {
            // Arrange — la siembra dice su cantidad en la frase y su golpe en el número: son dos
            // datos distintos y no tienen que pisarse.
            var state = new StatusIconState("intent.bomb_field", "Siembra bombas",
                                            "Siembra 3 bombas.", null, active: true, damage: 12);

            // Act
            _view.Show(state);

            // Assert
            Assert.AreEqual(
                Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(12), _damage.text);
            StringAssert.DoesNotContain("12", _rule.text);
        }

        private TextMeshProUGUI Label(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_go.transform, worldPositionStays: false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private void SetPrivate(string field, Object value)
        {
            typeof(TooltipCardView)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, value);
        }
    }
}
