using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items.Active;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Slot unico de item activo (GDD "Ítems Activos": <c>ActiveItemSlots = 1</c>, no
    /// configurable). Equipar otro descarta el que habia, y lo descartado se pierde —
    /// no vuelve al inventario.
    /// </summary>
    [TestFixture]
    public sealed class EquippedActiveItemServiceTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private EquippedActiveItemService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new EquippedActiveItemService(catalog: null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            _service = null;
        }

        [Test]
        public void test_equipped_startsEmpty()
        {
            // Assert
            Assert.IsFalse(_service.HasItem);
            Assert.IsNull(_service.Current);
        }

        [Test]
        public void test_equip_onEmptySlot_discardsNothing()
        {
            // Arrange
            var item = NewActive("item.a");

            // Act
            var discarded = _service.Equip(item);

            // Assert
            Assert.AreSame(item, _service.Current);
            Assert.IsNull(discarded);
        }

        [Test]
        public void test_equip_replacesAndReturnsTheDiscardedItem()
        {
            // Arrange
            var first = NewActive("item.a");
            var second = NewActive("item.b");
            _service.Equip(first);

            // Act
            var discarded = _service.Equip(second);

            // Assert — un solo item a la vez; el anterior se pierde.
            Assert.AreSame(second, _service.Current);
            Assert.AreSame(first, discarded);
        }

        [Test]
        public void test_equip_theSameItemAgain_doesNotReportADiscard()
        {
            // Arrange — sin este guard, reequipar lo mismo reportaria que se descarto a
            // si mismo y el HUD mostraria un descarte fantasma.
            var item = NewActive("item.a");
            _service.Equip(item);

            // Act
            var discarded = _service.Equip(item);

            // Assert
            Assert.AreSame(item, _service.Current);
            Assert.IsNull(discarded);
        }

        [Test]
        public void test_equip_passiveItem_isRejectedAndKeepsTheSlot()
        {
            // Arrange
            var active = NewActive("item.a");
            _service.Equip(active);
            // El rechazo avisa por consola; el warning es esperado, no una falla.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no es ItemType.Active"));

            // Act
            var discarded = _service.Equip(NewPassive("item.passive"));

            // Assert
            Assert.AreSame(active, _service.Current, "el slot no se toca");
            Assert.IsNull(discarded);
        }

        [Test]
        public void test_clear_emptiesTheSlotAndReturnsWhatWasThere()
        {
            // Arrange
            var item = NewActive("item.a");
            _service.Equip(item);

            // Act
            var discarded = _service.Clear();

            // Assert
            Assert.IsFalse(_service.HasItem);
            Assert.AreSame(item, discarded);
        }

        [Test]
        public void test_equip_raisesTheChangedEventWithBothItems()
        {
            // Arrange
            var first = NewActive("item.a");
            var second = NewActive("item.b");
            _service.Equip(first);

            ItemSO gotEquipped = null;
            ItemSO gotDiscarded = null;
            _service.OnEquippedChanged += (e, d) => { gotEquipped = e; gotDiscarded = d; };

            // Act
            _service.Equip(second);

            // Assert
            Assert.AreSame(second, gotEquipped);
            Assert.AreSame(first, gotDiscarded);
        }

        // ------------------------------------------------------------------
        // Persistencia
        // ------------------------------------------------------------------

        [Test]
        public void test_captureState_withEmptySlot_isNull()
        {
            // Assert
            Assert.IsNull(_service.CaptureState());
        }

        [Test]
        public void test_captureState_storesTheItemId()
        {
            // Arrange — el dado y la familia viven en el catalogo, no en la instancia.
            _service.Equip(NewActive("item.a"));

            // Act + Assert
            Assert.AreEqual("item.a", _service.CaptureState());
        }

        [Test]
        public void test_restoreState_withNull_emptiesTheSlot()
        {
            // Arrange
            _service.Equip(NewActive("item.a"));

            // Act
            _service.RestoreState(null);

            // Assert
            Assert.IsFalse(_service.HasItem);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ItemSO NewActive(string id) => New(id, ItemType.Active);
        private ItemSO NewPassive(string id) => New(id, ItemType.Passive);

        private ItemSO New(string id, ItemType type)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = type;
            _spawned.Add(item);
            return item;
        }
    }
}
