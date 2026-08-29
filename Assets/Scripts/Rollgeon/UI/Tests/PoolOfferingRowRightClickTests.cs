using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Click derecho sobre el dado del pool = sacar una copia de la bolsa, el mismo
    /// resultado que clickear ese dado en la tira.
    /// </summary>
    /// <remarks>
    /// El relay se agrega sobre el GameObject del Button y no sobre el root de la fila:
    /// el Button es el único raycast target y uGUI entrega el click al primer ancestro
    /// que maneja <see cref="IPointerClickHandler"/>. Un handler en el root no se
    /// enteraría nunca — por eso los tests clickean el relay del botón.
    /// </remarks>
    [TestFixture]
    public class PoolOfferingRowRightClickTests
    {
        private GameObject _rowGo;
        private PoolOfferingRow _row;
        private Button _addButton;
        private readonly List<DiceType> _removed = new();
        private readonly List<DiceType> _added = new();

        [SetUp]
        public void Setup()
        {
            _rowGo = new GameObject("PoolOfferingRow", typeof(RectTransform));

            var dieGo = new GameObject("DieButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dieGo.transform.SetParent(_rowGo.transform, worldPositionStays: false);
            _addButton = dieGo.AddComponent<Button>();
            _addButton.targetGraphic = dieGo.GetComponent<Image>();

            _row = _rowGo.AddComponent<PoolOfferingRow>();
            AssignPrivate(_row, "_addButton", _addButton);

            _removed.Clear();
            _added.Clear();
            _row.OnRemoveRequested += t => _removed.Add(t);
            _row.OnAddRequested += t => _added.Add(t);

            _row.Bind(DiceType.D6);
        }

        [TearDown]
        public void TearDown()
        {
            if (_rowGo != null) Object.DestroyImmediate(_rowGo);
        }

        private void RightClickTheDie()
        {
            var relay = _addButton.GetComponent<PointerRightClickRelay>();
            Assert.IsNotNull(relay, "Bind tiene que haber agregado el relay al GameObject del Button");
            relay.OnPointerClick(new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right,
            });
        }

        [Test]
        public void should_request_remove_when_die_is_right_clicked_and_type_is_in_bag()
        {
            // Arrange — un D6 ya puesto.
            _row.Refresh(currentCount: 1, bagHasRoom: true);

            // Act
            RightClickTheDie();

            // Assert
            CollectionAssert.AreEqual(new[] { DiceType.D6 }, _removed);
            CollectionAssert.IsEmpty(_added, "el derecho no debe agregar");
        }

        [Test]
        public void should_ignore_right_click_when_type_is_not_in_bag()
        {
            // Arrange — nada de este tipo puesto: no hay nada que sacar.
            _row.Refresh(currentCount: 0, bagHasRoom: true);

            // Act
            RightClickTheDie();

            // Assert
            CollectionAssert.IsEmpty(_removed);
        }

        [Test]
        public void should_request_remove_even_when_bag_is_full()
        {
            // Arrange — bolsa llena: Refresh apaga el botón de agregar. Es justo cuando
            // más querés poder sacar, así que el derecho NO puede mirar interactable.
            _row.Refresh(currentCount: 2, bagHasRoom: false);
            Assert.IsFalse(_addButton.interactable, "precondición del test");

            // Act
            RightClickTheDie();

            // Assert
            CollectionAssert.AreEqual(new[] { DiceType.D6 }, _removed);
        }

        [Test]
        public void should_ignore_left_click_on_the_relay()
        {
            // Arrange — el izquierdo es del Button; el relay debe dejarlo pasar de largo.
            _row.Refresh(currentCount: 1, bagHasRoom: true);
            var relay = _addButton.GetComponent<PointerRightClickRelay>();

            // Act
            relay.OnPointerClick(new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            });

            // Assert
            CollectionAssert.IsEmpty(_removed);
        }

        [Test]
        public void should_stop_relaying_after_unbind()
        {
            // Arrange
            _row.Refresh(currentCount: 1, bagHasRoom: true);
            _row.Unbind();

            // Act
            RightClickTheDie();

            // Assert — la fila se reusa por pooling; un callback colgado sacaría dados
            // de la bolsa del hero siguiente.
            CollectionAssert.IsEmpty(_removed);
        }

        private static void AssignPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }
    }
}
