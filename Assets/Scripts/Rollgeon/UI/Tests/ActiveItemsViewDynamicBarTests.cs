using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// La barra de items activos era un mapa hardcodeado <c>ItemId → Slot</c> con una sola
    /// entrada (la poción): cualquier otro item activo que el jugador consiguiera no tenía
    /// slot en pantalla y por lo tanto no se podía usar.
    /// <para>
    /// El GDD manda un slot <b>por carga</b> (dos pociones = dos slots) y que se consuma
    /// la que tocaste, así que el índice visual tiene que mapear al índice del inventario.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemsViewDynamicBarTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private GameObject _root;
        private ActiveItemsView _view;
        private FakeInventory _inventory;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _playerGuid = Guid.NewGuid();
            _inventory = new FakeInventory();
            ServiceLocator.AddService<IInventoryService>(_inventory);

            _root = new GameObject("ActiveItemsView", typeof(RectTransform));
            _view = _root.AddComponent<ActiveItemsView>();

            var container = new GameObject("Bar", typeof(RectTransform));
            container.transform.SetParent(_root.transform, false);

            AssignPrivate(_view, "_slotsContainer", container.GetComponent<RectTransform>());
            AssignPrivate(_view, "_slotPrefab", BuildSlotPrefab());
            AssignPrivate(_view, "_playerGuid", _playerGuid);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Poblado — una carga, un slot
        // ------------------------------------------------------------------

        [Test]
        public void test_rebuild_withNoItems_showsNoSlots()
        {
            // Act
            _view.Rebuild();

            // Assert
            Assert.AreEqual(0, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_oneSlotPerActiveItem()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));
            _inventory.AddActive(NewActive("item.b"));

            // Act
            _view.Rebuild();

            // Assert — esto es lo que antes era imposible: items sin binding en Inspector
            // igual aparecen en pantalla.
            Assert.AreEqual(2, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_repeatedCharges_getOneSlotEach()
        {
            // Arrange — el GDD: "varias cargas del mismo item = varios slots".
            var item = NewActive("item.a");
            _inventory.AddActive(item);
            _inventory.AddActive(item);
            _inventory.AddActive(item);

            // Act
            _view.Rebuild();

            // Assert
            Assert.AreEqual(3, VisibleSlots().Count, "cada carga es un slot, no un contador");
        }

        [Test]
        public void test_rebuild_neverExceedsMaxActiveSlots()
        {
            // Arrange — el inventario no deberia pasarse, pero la barra no puede confiar
            // en eso: MaxActiveSlots es el limite de pantalla.
            _inventory.MaxSlots = 2;
            for (int i = 0; i < 5; i++) _inventory.AddActive(NewActive("item." + i));

            // Act
            _view.Rebuild();

            // Assert
            Assert.AreEqual(2, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_ignoresPassiveItems()
        {
            // Arrange
            _inventory.AddPassive(NewPassive("item.passive"));
            _inventory.AddActive(NewActive("item.a"));

            // Act
            _view.Rebuild();

            // Assert
            Assert.AreEqual(1, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_afterConsumingACharge_shrinksTheBar()
        {
            // Arrange
            var item = NewActive("item.a");
            _inventory.AddActive(item);
            _inventory.AddActive(item);
            _view.Rebuild();
            Assert.AreEqual(2, VisibleSlots().Count);

            // Act — el pool se reusa y solo se apaga, no se destruye.
            _inventory.RemoveActiveAt(0);
            _view.Rebuild();

            // Assert
            Assert.AreEqual(1, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_isIdempotent_doesNotGrowThePool()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));

            // Act
            _view.Rebuild();
            _view.Rebuild();
            _view.Rebuild();

            // Assert
            Assert.AreEqual(1, AllSlots().Count, "no se instancia un slot nuevo por rebuild");
        }

        // ------------------------------------------------------------------
        // Cooldown visible
        // ------------------------------------------------------------------

        [Test]
        public void test_rebuild_showsRemainingCooldownTurnsOnTheSlot()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));
            _inventory.SetCooldown(0, 3);

            // Act
            _view.Rebuild();

            // Assert
            Assert.AreEqual(3, VisibleSlots()[0].CurrentCooldown);
        }

        [Test]
        public void test_turnEvent_refreshesTheCooldownWithoutRepopulating()
        {
            // Arrange — el cooldown baja en OnTurnFinished; la barra tiene que repintar
            // sin repoblar (el inventario no cambio).
            _inventory.AddActive(NewActive("item.a"));
            _inventory.SetCooldown(0, 2);
            _view.Rebuild();

            // Act
            _inventory.SetCooldown(0, 1);
            InvokePrivate(_view, "RefreshSlotStates");

            // Assert
            Assert.AreEqual(1, VisibleSlots()[0].CurrentCooldown);
            Assert.AreEqual(1, AllSlots().Count);
        }

        // ------------------------------------------------------------------
        // Click → activacion
        // ------------------------------------------------------------------

        [Test]
        public void test_click_consumesTheChargeYouTouched()
        {
            // Arrange — tres cargas del MISMO ItemId: buscar por id gastaria siempre la
            // primera. El indice visual tiene que mapear al del inventario.
            var item = NewActive("item.a");
            _inventory.AddActive(item);
            _inventory.AddActive(item);
            _inventory.AddActive(item);
            _view.Rebuild();

            // Act
            Click(VisibleSlots()[2]);

            // Assert
            Assert.AreEqual(1, _inventory.ActivatedIndices.Count);
            Assert.AreEqual(2, _inventory.ActivatedIndices[0]);
        }

        [Test]
        public void test_click_onSecondItem_activatesItsOwnIndex()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));
            _inventory.AddActive(NewActive("item.b"));
            _view.Rebuild();

            // Act
            Click(VisibleSlots()[1]);

            // Assert
            Assert.AreEqual(1, _inventory.ActivatedIndices[0]);
        }

        [Test]
        public void test_click_onBlockedSlot_doesNotActivate()
        {
            // Arrange — el prerequisito no se cumple.
            _inventory.AddActive(NewActive("item.a"));
            _inventory.Block = ItemActivationBlock.PreconditionFailed;
            _view.Rebuild();

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            CollectionAssert.IsEmpty(_inventory.ActivatedIndices,
                "un item bloqueado no se ejecuta — el click solo explica el rechazo");
        }

        [Test]
        public void test_click_onCooldownSlot_doesNotActivate()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));
            _inventory.Block = ItemActivationBlock.OnCooldown;
            _view.Rebuild();

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            CollectionAssert.IsEmpty(_inventory.ActivatedIndices);
        }

        [Test]
        public void test_click_outsideYourTurn_doesNotActivate()
        {
            // Arrange — el gate de turno vive en TurnManager y llega como bloqueo; la
            // vista solo tiene que respetarlo.
            _inventory.AddActive(NewActive("item.a"));
            _inventory.Block = ItemActivationBlock.NotYourTurn;
            _view.Rebuild();

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            CollectionAssert.IsEmpty(_inventory.ActivatedIndices);
        }

        // ------------------------------------------------------------------
        // Delegacion a behaviors (la pocion)
        // ------------------------------------------------------------------

        [Test]
        public void test_click_onBehaviorDelegatedItem_doesNotGoThroughActivateItem()
        {
            // Arrange — la poción se resuelve por el behavior Healing, el mismo que el
            // botón Heal, para que los dos caminos den exactamente lo mismo.
            _inventory.AddActive(NewActive("potion.healing"));
            SetBehaviorDelegates("potion.healing", HeroBehaviorSlot.Healing);
            _view.Rebuild();

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            CollectionAssert.IsEmpty(_inventory.ActivatedIndices,
                "el item delegado no pasa por ActivateItem — lo resuelve el behavior");
        }

        [Test]
        public void test_behaviorDelegatedItem_isNeverPaintedUnaffordableByTheItemGate()
        {
            // Arrange — su gating lo decide el behavior; CanActivateItem no aplica.
            _inventory.AddActive(NewActive("potion.healing"));
            _inventory.Block = ItemActivationBlock.PreconditionFailed;
            SetBehaviorDelegates("potion.healing", HeroBehaviorSlot.Healing);

            // Act
            _view.Rebuild();

            // Assert
            Assert.IsTrue(VisibleSlots()[0].IsAffordableForTests);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void Click(ActiveItemSlotView slot)
        {
            var method = typeof(ActiveItemsView).GetMethod("HandleSlotClicked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "HandleSlotClicked no encontrado");
            method.Invoke(_view, new object[] { slot });
        }

        private List<ActiveItemSlotView> AllSlots()
        {
            var field = typeof(ActiveItemsView).GetField("_slots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<ActiveItemSlotView>)field.GetValue(_view);
        }

        private List<ActiveItemSlotView> VisibleSlots()
        {
            var visible = new List<ActiveItemSlotView>();
            foreach (var s in AllSlots())
            {
                if (s != null && s.gameObject.activeSelf) visible.Add(s);
            }
            return visible;
        }

        private void SetBehaviorDelegates(string itemId, HeroBehaviorSlot slot)
        {
            var list = new List<ActiveItemsView.BehaviorDelegate>
            {
                new ActiveItemsView.BehaviorDelegate { ItemId = itemId, Slot = slot },
            };
            AssignPrivate(_view, "_behaviorDelegates", list);
        }

        /// <summary>
        /// Slot "prefab": un GameObject inactivo fuera de la jerarquía de la vista.
        /// <c>Instantiate</c> sobre un objeto de escena funciona igual que sobre un asset
        /// y evita depender del prefab real en un EditMode test.
        /// </summary>
        private ActiveItemSlotView BuildSlotPrefab()
        {
            var go = new GameObject("SlotPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.SetActive(false);
            var slot = go.AddComponent<ActiveItemSlotView>();
            AssignPrivate(slot, "_icon", go.GetComponent<Image>());
            _spawned.Add(go);
            return slot;
        }

        private ItemSO NewActive(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData();
            _spawned.Add(item);
            return item;
        }

        private ItemSO NewPassive(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Passive;
            _spawned.Add(item);
            return item;
        }

        private static void AssignPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, null);
        }

        // ------------------------------------------------------------------
        // Fake
        // ------------------------------------------------------------------

        private sealed class FakeInventory : IInventoryService
        {
            private readonly List<InventorySlot> _actives = new List<InventorySlot>();
            private readonly List<InventorySlot> _passives = new List<InventorySlot>();

            /// <summary>Motivo que devuelve <see cref="CanActivateItem"/>.</summary>
            public ItemActivationBlock Block = ItemActivationBlock.None;

            public int MaxSlots = 4;
            public readonly List<int> ActivatedIndices = new List<int>();

            public void AddActive(ItemSO item) => _actives.Add(new InventorySlot { Item = item });
            public void AddPassive(ItemSO item) => _passives.Add(new InventorySlot { Item = item });
            public void RemoveActiveAt(int index) => _actives.RemoveAt(index);
            public void SetCooldown(int index, int turns) => _actives[index].CurrentCooldown = turns;

            public IReadOnlyList<InventorySlot> PassiveItems => _passives;
            public IReadOnlyList<InventorySlot> ActiveItems => _actives;

            public bool AddItem(ItemSO item) => false;
            public bool RemoveItem(string itemId) => false;
            public bool HasItem(string itemId) => false;
            public ItemSO GetItem(string itemId) => null;

            public bool ActivateItem(int activeSlotIndex, EffectContext ctx)
            {
                ActivatedIndices.Add(activeSlotIndex);
                return true;
            }

            public ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx) => Block;

            public int GetComboDamageBonusPreview(string comboId) => 0;
            public void TickCooldowns() { }
            public int MaxActiveSlots => MaxSlots;

#pragma warning disable CS0067
            public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore CS0067
        }
    }
}
