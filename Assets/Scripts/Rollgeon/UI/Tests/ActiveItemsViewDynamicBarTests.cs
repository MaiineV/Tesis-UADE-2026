using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Items;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// La barra de items activos era un mapa hardcodeado <c>ItemId → Slot</c> con una sola
    /// entrada (la poción): cualquier otro item activo que el jugador consiguiera no tenía
    /// slot en pantalla y por lo tanto no se podía usar. Estos tests cubren el pool
    /// dinámico que lo reemplaza y el gate de activación del click.
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

            AssignPrivate(_view, "_dynamicContainer", container.GetComponent<RectTransform>());
            AssignPrivate(_view, "_dynamicSlotPrefab", BuildSlotPrefab());
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
        // Poblado del pool
        // ------------------------------------------------------------------

        [Test]
        public void test_rebuild_withNoItems_showsNoSlots()
        {
            // Act
            _view.RebuildDynamicSlots();

            // Assert
            Assert.AreEqual(0, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_oneSlotPerDistinctActiveItem()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));
            _inventory.AddActive(NewActive("item.b"));

            // Act
            _view.RebuildDynamicSlots();

            // Assert — esto es lo que antes era imposible: items sin binding en Inspector
            // igual aparecen en pantalla.
            Assert.AreEqual(2, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_duplicateItemIds_stackIntoOneSlot()
        {
            // Arrange — tres cargas del mismo consumible.
            var item = NewActive("item.a");
            _inventory.AddActive(item);
            _inventory.AddActive(item);
            _inventory.AddActive(item);

            // Act
            _view.RebuildDynamicSlots();

            // Assert
            Assert.AreEqual(1, VisibleSlots().Count, "las cargas se agrupan en un solo slot");
        }

        [Test]
        public void test_rebuild_pinnedItemIds_areExcludedFromTheDynamicBar()
        {
            // Arrange — la poción tiene slot pinneado en el prefab y se usa por el botón
            // Heal: no debe duplicarse en la barra.
            SetBindings("potion.healing");
            _inventory.AddActive(NewActive("potion.healing"));
            _inventory.AddActive(NewActive("item.a"));

            // Act
            _view.RebuildDynamicSlots();

            // Assert
            Assert.AreEqual(1, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_ignoresPassiveItems()
        {
            // Arrange
            _inventory.AddPassive(NewPassive("item.passive"));
            _inventory.AddActive(NewActive("item.a"));

            // Act
            _view.RebuildDynamicSlots();

            // Assert
            Assert.AreEqual(1, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_afterConsumingTheLastCharge_hidesTheSlot()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));
            _view.RebuildDynamicSlots();
            Assert.AreEqual(1, VisibleSlots().Count);

            // Act — el pool se reusa y solo se apaga, no se destruye.
            _inventory.ClearActives();
            _view.RebuildDynamicSlots();

            // Assert
            Assert.AreEqual(0, VisibleSlots().Count);
        }

        [Test]
        public void test_rebuild_isIdempotent_doesNotGrowThePool()
        {
            // Arrange
            _inventory.AddActive(NewActive("item.a"));

            // Act
            _view.RebuildDynamicSlots();
            _view.RebuildDynamicSlots();
            _view.RebuildDynamicSlots();

            // Assert
            Assert.AreEqual(1, AllSlots().Count, "no se instancia un slot nuevo por rebuild");
        }

        // ------------------------------------------------------------------
        // Click → activacion
        // ------------------------------------------------------------------

        [Test]
        public void test_click_onUsableSlot_activatesTheMatchingInventoryIndex()
        {
            // Arrange — el índice del click tiene que ser el del inventario, no el visual.
            _inventory.AddActive(NewActive("item.a"));
            _inventory.AddActive(NewActive("item.b"));
            _view.RebuildDynamicSlots();

            // Act
            Click(VisibleSlots()[1]);

            // Assert
            Assert.AreEqual(1, _inventory.ActivatedIndices.Count);
            Assert.AreEqual(1, _inventory.ActivatedIndices[0]);
        }

        [Test]
        public void test_click_onBlockedSlot_doesNotActivate()
        {
            // Arrange — el prerequisito no se cumple.
            _inventory.AddActive(NewActive("item.a"));
            _inventory.Block = ItemActivationBlock.PreconditionFailed;
            _view.RebuildDynamicSlots();

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
            _view.RebuildDynamicSlots();

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            CollectionAssert.IsEmpty(_inventory.ActivatedIndices);
        }

        [Test]
        public void test_click_inCombatOutsidePlayerTurn_doesNotActivate()
        {
            // Arrange — TurnManager no mira de quién es el turno, así que sin este gate
            // el click en turno enemigo igual le cobraría un roll al jugador.
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(
                new FakeRollPool { IsCombatActive = true, Current = 5 });
            _inventory.AddActive(NewActive("item.a"));
            _view.RebuildDynamicSlots();
            AssignPrivate(_view, "_isPlayerTurn", false);

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            CollectionAssert.IsEmpty(_inventory.ActivatedIndices);
        }

        [Test]
        public void test_click_inCombatDuringPlayerTurn_activates()
        {
            // Arrange
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(
                new FakeRollPool { IsCombatActive = true, Current = 5 });
            _inventory.AddActive(NewActive("item.a"));
            _view.RebuildDynamicSlots();
            AssignPrivate(_view, "_isPlayerTurn", true);

            // Act
            Click(VisibleSlots()[0]);

            // Assert — el bloqueo por fase que había antes hacía esto imposible.
            Assert.AreEqual(1, _inventory.ActivatedIndices.Count);
        }

        [Test]
        public void test_click_outOfCombat_activatesWithoutTurnGate()
        {
            // Arrange — en exploración no hay turnos: _isPlayerTurn queda en false.
            _inventory.AddActive(NewActive("item.a"));
            _view.RebuildDynamicSlots();
            AssignPrivate(_view, "_isPlayerTurn", false);

            // Act
            Click(VisibleSlots()[0]);

            // Assert
            Assert.AreEqual(1, _inventory.ActivatedIndices.Count);
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
            var field = typeof(ActiveItemsView).GetField("_dynamicSlots",
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

        private void SetBindings(params string[] pinnedItemIds)
        {
            var list = new List<ActiveItemsView.ItemSlotBinding>();
            foreach (var id in pinnedItemIds)
            {
                var go = new GameObject("Pinned_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_root.transform, false);
                var slot = go.AddComponent<ActiveItemSlotView>();
                AssignPrivate(slot, "_icon", go.GetComponent<Image>());
                AssignPrivate(slot, "_displayOnly", true);
                list.Add(new ActiveItemsView.ItemSlotBinding { ItemId = id, Slot = slot });
            }
            AssignPrivate(_view, "_bindings", list);
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

        // ------------------------------------------------------------------
        // Fakes
        // ------------------------------------------------------------------

        private sealed class FakeInventory : IInventoryService
        {
            private readonly List<InventorySlot> _actives = new List<InventorySlot>();
            private readonly List<InventorySlot> _passives = new List<InventorySlot>();

            /// <summary>Motivo que devuelve <see cref="CanActivateItem"/>.</summary>
            public ItemActivationBlock Block = ItemActivationBlock.None;

            public readonly List<int> ActivatedIndices = new List<int>();

            public void AddActive(ItemSO item) => _actives.Add(new InventorySlot { Item = item });
            public void AddPassive(ItemSO item) => _passives.Add(new InventorySlot { Item = item });
            public void ClearActives() => _actives.Clear();

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
            public int MaxActiveSlots => 4;

#pragma warning disable CS0067
            public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore CS0067
        }

        private sealed class FakeRollPool : Rollgeon.Combat.Rolls.IRollPoolService
        {
            public bool IsCombatActive { get; set; }
            public int Current;

            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count) => true;
            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current;
            public int GetMax(Guid entityId) => 10;
            public int GetRollsPerTurn(Guid entityId) => 1;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) { }
        }
    }
}
