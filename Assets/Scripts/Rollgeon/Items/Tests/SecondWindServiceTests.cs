using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// <see cref="SecondWindService"/>: la carga se consume una sola vez y avisa por
    /// <see cref="EventName.OnSecondWindTriggered"/> con el SO del item (ya fuera del
    /// inventario) y el resto de vida.
    /// </summary>
    [TestFixture]
    public class SecondWindServiceTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private Guid _playerGuid;
        private FakeInventoryService _inventory;
        private object[] _captured;
        private int _eventCount;
        private EventManager.EventReceiver _receiver;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _playerGuid = Guid.NewGuid();
            _inventory = new FakeInventoryService();
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid), ServiceScope.Global);

            _captured = null;
            _eventCount = 0;
            _receiver = args => { _captured = args; _eventCount++; };
            EventManager.Subscribe(EventName.OnSecondWindTriggered, _receiver);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnSecondWindTriggered, _receiver);
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private ItemSO MakeSecondWindItem(int remainingHp = 1)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.name = "Item_TestSecondWind";
            item.ItemId = "test.second.wind";
            item.DisplayName = "Ficha de prueba";
            item.Type = ItemType.Passive;
            item.SecondWind = true;
            item.SecondWindRemainingHp = remainingHp;
            _created.Add(item);
            _inventory.PassiveSlots.Add(new InventorySlot { Item = item });
            return item;
        }

        [Test]
        public void NotifyLethalPrevented_ConsumesChargeAndEmitsEventWithItemAndRemainingHp()
        {
            var item = MakeSecondWindItem(remainingHp: 1);
            var svc = new SecondWindService();
            Assert.IsTrue(svc.ShouldPreventLethal(_playerGuid), "pre: la carga está disponible");

            svc.NotifyLethalPrevented(_playerGuid);

            Assert.AreEqual(1, _eventCount, "un consumo = un evento");
            Assert.AreEqual(_playerGuid, _captured[0]);
            Assert.AreSame(item, _captured[1], "el payload lleva el SO, no el id");
            Assert.AreEqual(1, _captured[2]);
            Assert.IsEmpty(_inventory.PassiveSlots, "el item se remueve del inventario");
            Assert.IsFalse(svc.ShouldPreventLethal(_playerGuid), "la carga es única: el próximo letal mata");
        }

        [Test]
        public void NotifyLethalPrevented_TwiceInARow_OnlyFirstConsumesAndEmits()
        {
            MakeSecondWindItem();
            var svc = new SecondWindService();

            svc.NotifyLethalPrevented(_playerGuid);
            svc.NotifyLethalPrevented(_playerGuid);

            Assert.AreEqual(1, _eventCount);
        }

        [Test]
        public void NotifyLethalPrevented_OtherTarget_DoesNothing()
        {
            MakeSecondWindItem();
            var svc = new SecondWindService();

            svc.NotifyLethalPrevented(Guid.NewGuid());

            Assert.AreEqual(0, _eventCount);
            Assert.AreEqual(1, _inventory.PassiveSlots.Count, "el item de otro target no se toca");
        }

        // ---- Fakes ------------------------------------------------------------

        private sealed class FakeInventoryService : IInventoryService
        {
            public readonly List<InventorySlot> PassiveSlots = new List<InventorySlot>();
            public readonly List<InventorySlot> ActiveSlots = new List<InventorySlot>();

            public IReadOnlyList<InventorySlot> PassiveItems => PassiveSlots;
            public IReadOnlyList<InventorySlot> ActiveItems => ActiveSlots;
            public int MaxActiveSlots => 4;
            public void AddActiveSlotBonus(int amount) { }

#pragma warning disable CS0067
            public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore CS0067

            public bool AddItem(ItemSO item) => false;

            public bool RemoveItem(string itemId)
            {
                int idx = PassiveSlots.FindIndex(s => s?.Item != null && s.Item.ItemId == itemId);
                if (idx < 0) return false;
                PassiveSlots.RemoveAt(idx);
                return true;
            }

            public bool HasItem(string itemId) => PassiveSlots.Exists(s => s?.Item != null && s.Item.ItemId == itemId);
            public ItemSO GetItem(string itemId) => PassiveSlots.Find(s => s?.Item != null && s.Item.ItemId == itemId)?.Item;
            public bool ActivateItem(int activeSlotIndex, EffectContext ctx) => false;
            public ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx)
                => ItemActivationBlock.InvalidSlot;
            public int GetComboDamageBonusPreview(string comboId) => 0;
            public void TickCooldowns() { }
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid playerGuid) { PlayerGuid = playerGuid; }

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
