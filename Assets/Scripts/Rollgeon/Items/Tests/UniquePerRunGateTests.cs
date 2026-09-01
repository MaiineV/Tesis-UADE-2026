using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Cobertura del gate estilo Isaac (<see cref="UniquePerRunGate"/>): las dos ramas
    /// (UniquePerRun poseído/innato y FamilyExclusive por familia compartida), el
    /// degrade permisivo sin servicios, y la auditoría de datos del par fortuna.
    /// </summary>
    [TestFixture]
    public class UniquePerRunGateTests
    {
        private readonly List<Object> _assets = new List<Object>();
        private FakeInventoryService _inventory;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            _inventory = new FakeInventoryService();
        }

        [TearDown]
        public void Teardown()
        {
            ServiceLocator.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // ---------------- Rama UniquePerRun ----------------

        [Test]
        public void IsBlocked_ReturnsTrue_WhenInventoryHasUniquePerRunItem()
        {
            // Arrange
            var item = NewItem("pico.de.minero", uniquePerRun: true);
            _inventory.OwnedIds.Add("pico.de.minero");
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsTrue(UniquePerRunGate.IsBlocked(item));
        }

        [Test]
        public void IsBlocked_ReturnsTrue_WhenHeroInnateIdsContainItemId()
        {
            // Arrange
            var item = NewItem("instinto.supervivencia", uniquePerRun: true);
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.InnateItemIds.Add("instinto.supervivencia");
            _assets.Add(hero);
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(hero), ServiceScope.Global);

            // Act + Assert
            Assert.IsTrue(UniquePerRunGate.IsBlocked(item));
        }

        [Test]
        public void IsBlocked_ReturnsFalse_WhenUniquePerRunItemNotOwned()
        {
            // Arrange
            var item = NewItem("pico.de.minero", uniquePerRun: true);
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsFalse(UniquePerRunGate.IsBlocked(item));
        }

        [Test]
        public void IsBlocked_ReturnsFalse_WithoutServicesRegistered()
        {
            // Arrange — degrade permisivo: sin inventario no se puede afirmar posesión.
            var unique = NewItem("pico.de.minero", uniquePerRun: true);
            var exclusive = NewItem("corazon.fortuna", familyId: "fortuna", familyExclusive: true);

            // Act + Assert
            Assert.IsFalse(UniquePerRunGate.IsBlocked(unique));
            Assert.IsFalse(UniquePerRunGate.IsBlocked(exclusive));
        }

        // ---------------- Rama FamilyExclusive ----------------

        [Test]
        public void IsBlocked_ReturnsTrue_WhenInventoryHasOtherFamilyExclusiveItemOfSameFamily()
        {
            // Arrange — corazón en el inventario, tesoro como candidato.
            var owned = NewItem("corazon.fortuna", familyId: "fortuna", familyExclusive: true);
            var candidate = NewItem("tesoro.de.la.fortuna", familyId: "fortuna", familyExclusive: true);
            _inventory.PassiveSlots.Add(new InventorySlot { Item = owned });
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsTrue(UniquePerRunGate.IsBlocked(candidate));
        }

        [Test]
        public void IsBlocked_ReturnsTrue_WhenInventoryHasSameFamilyExclusiveItem()
        {
            // Arrange — duplicado de sí mismo: corazón bloquea otro corazón.
            var owned = NewItem("corazon.fortuna", familyId: "fortuna", familyExclusive: true);
            var candidate = NewItem("corazon.fortuna", familyId: "fortuna", familyExclusive: true);
            _inventory.PassiveSlots.Add(new InventorySlot { Item = owned });
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsTrue(UniquePerRunGate.IsBlocked(candidate));
        }

        [Test]
        public void IsBlocked_ReturnsTrue_WhenFamilyItemLivesInActiveSlots()
        {
            // Arrange — la familia también se busca entre los items activos.
            var owned = NewItem("corazon.fortuna", familyId: "fortuna", familyExclusive: true);
            var candidate = NewItem("tesoro.de.la.fortuna", familyId: "fortuna", familyExclusive: true);
            _inventory.ActiveSlots.Add(new InventorySlot { Item = owned });
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsTrue(UniquePerRunGate.IsBlocked(candidate));
        }

        [Test]
        public void IsBlocked_ReturnsFalse_WhenFamilyMatchesButFlagOff()
        {
            // Arrange — familias de variantes (corona, botas...) conviven sin el flag.
            var owned = NewItem("corona.par", familyId: "corona");
            var candidate = NewItem("corona.trio", familyId: "corona");
            _inventory.PassiveSlots.Add(new InventorySlot { Item = owned });
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsFalse(UniquePerRunGate.IsBlocked(candidate));
        }

        [Test]
        public void IsBlocked_ReturnsFalse_WhenFamilyExclusiveButFamilyIdEmpty()
        {
            // Arrange — flag sin familia: no hay nada contra qué matchear.
            var owned = NewItem("suelto.a", familyExclusive: true);
            var candidate = NewItem("suelto.b", familyExclusive: true);
            _inventory.PassiveSlots.Add(new InventorySlot { Item = owned });
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);

            // Act + Assert
            Assert.IsFalse(UniquePerRunGate.IsBlocked(candidate));
        }

        // ---------------- Audit de datos (par fortuna) ----------------

        [Test]
        public void FortunaItems_AreFamilyExclusive_AndShareFamilyId()
        {
            // Arrange + Act — los assets reales del par excluyente por GDD.
            var corazon = LoadItemByItemId("corazon.fortuna");
            var tesoro = LoadItemByItemId("tesoro.de.la.fortuna");

            // Assert
            Assert.IsNotNull(corazon, "No se encontró el asset de corazon.fortuna");
            Assert.IsNotNull(tesoro, "No se encontró el asset de tesoro.de.la.fortuna");
            Assert.AreEqual("fortuna", corazon.FamilyId);
            Assert.AreEqual("fortuna", tesoro.FamilyId);
            Assert.IsTrue(corazon.FamilyExclusive, "corazon.fortuna debe ser FamilyExclusive");
            Assert.IsTrue(tesoro.FamilyExclusive, "tesoro.de.la.fortuna debe ser FamilyExclusive");
        }

        // ---------------- Helpers ----------------

        private ItemSO NewItem(string itemId, bool uniquePerRun = false,
            string familyId = null, bool familyExclusive = false)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = itemId;
            item.DisplayName = itemId;
            item.UniquePerRun = uniquePerRun;
            item.FamilyId = familyId;
            item.FamilyExclusive = familyExclusive;
            _assets.Add(item);
            return item;
        }

        private static ItemSO LoadItemByItemId(string itemId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ItemSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
                if (item != null && string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public readonly List<InventorySlot> PassiveSlots = new List<InventorySlot>();
            public readonly List<InventorySlot> ActiveSlots = new List<InventorySlot>();
            public readonly HashSet<string> OwnedIds = new HashSet<string>();

            public IReadOnlyList<InventorySlot> PassiveItems => PassiveSlots;
            public IReadOnlyList<InventorySlot> ActiveItems => ActiveSlots;
            public int MaxActiveSlots => 4;
            public void AddActiveSlotBonus(int amount) { }

#pragma warning disable CS0067
            public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore CS0067

            public bool AddItem(ItemSO item) => false;
            public bool RemoveItem(string itemId) => false;
            public bool HasItem(string itemId) => OwnedIds.Contains(itemId);
            public ItemSO GetItem(string itemId) => null;
            public bool ActivateItem(int activeSlotIndex, EffectContext ctx) => false;
            public ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx)
                => ItemActivationBlock.InvalidSlot;
            public int GetComboDamageBonusPreview(string comboId) => 0;
            public void TickCooldowns() { }
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(ClassHeroSO hero) { CurrentHero = hero; }

            public Guid PlayerGuid => Guid.Empty;
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero { get; }
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
