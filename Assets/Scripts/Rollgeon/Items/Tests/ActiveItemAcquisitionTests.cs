using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Adquisición del ítem activo. El desvío vive en <see cref="InventoryService.AddItem"/>
    /// y no en cada fuente, así la tienda, los cofres y cualquier camino futuro caen solos
    /// — todos pasan por ahí.
    /// <para>
    /// <b>El flag es de migración.</b> Solo los ítems con <c>UsesActiveSlot</c> van al slot
    /// único; el resto sigue entrando al inventario. El GDD dice que el catálogo todavía
    /// no está migrado, y la poción depende del camino viejo.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemAcquisitionTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private InventoryService _inventory;
        private EquippedActiveItemService _slot;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));

            _slot = new EquippedActiveItemService(catalog: null);
            ServiceLocator.AddService<IEquippedActiveItemService>(_slot);

            _inventory = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _inventory?.Dispose();
            _slot?.Dispose();
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // El desvío
        // ------------------------------------------------------------------

        [Test]
        public void test_addItem_newModelActive_goesToTheSingleSlotNotTheInventory()
        {
            // Arrange
            var item = NewActive("item.nuevo", usesSlot: true);

            // Act
            bool ok = _inventory.AddItem(item);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreSame(item, _slot.Current, "quedo equipado en el slot unico");
            CollectionAssert.IsEmpty(_inventory.ActiveItems, "no entro al inventario");
        }

        [Test]
        public void test_addItem_secondNewModelActive_discardsTheFirst()
        {
            // Arrange — el GDD: conseguir otro descarta el que tenias, sin confirmacion
            // ni recuperacion.
            var first = NewActive("item.a", usesSlot: true);
            var second = NewActive("item.b", usesSlot: true);
            _inventory.AddItem(first);

            // Act
            _inventory.AddItem(second);

            // Assert
            Assert.AreSame(second, _slot.Current);
            CollectionAssert.IsEmpty(_inventory.ActiveItems, "el descartado no cae al inventario");
        }

        [Test]
        public void test_addItem_newModelActive_raisesObtainedSoTheHudRefreshes()
        {
            // Arrange
            string obtained = null;
            EventManager.Subscribe(EventName.OnItemObtained, delegate(object[] args)
            {
                if (args != null && args.Length > 1) obtained = args[1] as string;
            });

            // Act
            _inventory.AddItem(NewActive("item.nuevo", usesSlot: true));

            // Assert
            Assert.AreEqual("item.nuevo", obtained);
        }

        [Test]
        public void test_addItem_withoutTheSlotService_failsInsteadOfLosingTheItem()
        {
            // Arrange — la tienda mira el resultado para avisar. Devolver true aca seria
            // cobrarle al jugador por un item que se perdio en el aire.
            ServiceLocator.Clear();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));
            LogAssert.ignoreFailingMessages = true;

            // Act
            bool ok = _inventory.AddItem(NewActive("item.nuevo", usesSlot: true));

            // Assert
            Assert.IsFalse(ok);
        }

        // ------------------------------------------------------------------
        // El camino viejo sigue intacto
        // ------------------------------------------------------------------

        [Test]
        public void test_addItem_legacyActive_stillGoesToTheInventory()
        {
            // Arrange — es el caso de la pocion: sin el flag, nada cambia.
            var item = NewActive("potion.healing", usesSlot: false);

            // Act
            _inventory.AddItem(item);

            // Assert
            Assert.AreEqual(1, _inventory.ActiveItems.Count);
            Assert.IsNull(_slot.Current, "no toca el slot unico");
        }

        [Test]
        public void test_legacyActive_isStillFoundByHasItem()
        {
            // Arrange — la precondicion PCHasInventoryItem del boton Heal depende de esto.
            _inventory.AddItem(NewActive("potion.healing", usesSlot: false));

            // Act + Assert
            Assert.IsTrue(_inventory.HasItem("potion.healing"));
        }

        [Test]
        public void test_legacyActive_stillRespectsTheSlotLimit()
        {
            // Arrange — MaxActiveSlots sigue gateando el camino viejo.
            for (int i = 0; i < 4; i++) _inventory.AddItem(NewActive("item." + i, usesSlot: false));

            // Act
            bool ok = _inventory.AddItem(NewActive("item.extra", usesSlot: false));

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual(4, _inventory.ActiveItems.Count);
        }

        [Test]
        public void test_passiveItems_areUntouchedByTheDetour()
        {
            // Act
            _inventory.AddItem(NewPassive("item.pasivo"));

            // Assert
            Assert.AreEqual(1, _inventory.PassiveItems.Count);
            Assert.IsNull(_slot.Current);
        }

        // ------------------------------------------------------------------
        // Consultas de inventario
        // ------------------------------------------------------------------

        [Test]
        public void test_hasItem_findsTheEquippedActiveItem()
        {
            // Arrange — "tenes este item" tiene que ser true si lo tenes equipado; si no,
            // una precondicion sobre un item del modelo nuevo nunca se cumpliria.
            _inventory.AddItem(NewActive("item.nuevo", usesSlot: true));

            // Act + Assert
            Assert.IsTrue(_inventory.HasItem("item.nuevo"));
        }

        [Test]
        public void test_getItem_returnsTheEquippedActiveItem()
        {
            // Arrange
            var item = NewActive("item.nuevo", usesSlot: true);
            _inventory.AddItem(item);

            // Act + Assert
            Assert.AreSame(item, _inventory.GetItem("item.nuevo"));
        }

        [Test]
        public void test_hasItem_isFalseAfterTheItemIsReplaced()
        {
            // Arrange
            _inventory.AddItem(NewActive("item.a", usesSlot: true));
            _inventory.AddItem(NewActive("item.b", usesSlot: true));

            // Act + Assert
            Assert.IsFalse(_inventory.HasItem("item.a"), "el descartado ya no se tiene");
            Assert.IsTrue(_inventory.HasItem("item.b"));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ItemSO NewActive(string id, bool usesSlot)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.UsesActiveSlot = usesSlot;
            item.ActiveDie = DiceType.D6;
            item.OnActivate = new EffectData();
            item.OnNegativeBand = new EffectData();
            item.OnMixedBand = new EffectData();
            item.OnPositiveBand = new EffectData();
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

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
