using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.DevConsole.Commands;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Items;
using Rollgeon.Shop;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.DevConsole.Tests
{
    public class CommandExecutionTests
    {
        [TearDown]
        public void TearDown() => EventManager.ResetEventDictionary();

        [Test]
        public void gold_should_add_when_amount_positive()
        {
            var ctx = new FakeConsoleContext();
            var eco = new FakeEconomyService(10);
            ctx.Register<Rollgeon.Economy.IEconomyService>(eco);

            var res = new GoldCommand().Execute(new[] { "50" }, ctx);

            Assert.IsTrue(res.Success);
            Assert.AreEqual(60, eco.CurrentGold);
        }

        [Test]
        public void gold_should_spend_when_amount_negative()
        {
            var ctx = new FakeConsoleContext();
            var eco = new FakeEconomyService(100);
            ctx.Register<Rollgeon.Economy.IEconomyService>(eco);

            var res = new GoldCommand().Execute(new[] { "-30" }, ctx);

            Assert.IsTrue(res.Success);
            Assert.AreEqual(70, eco.CurrentGold);
        }

        [Test]
        public void heal_should_fail_when_no_run_active()
        {
            var ctx = new FakeConsoleContext { IsRunActive = false };

            var res = new HealCommand().Execute(Array.Empty<string>(), ctx);

            Assert.IsFalse(res.Success);
        }

        [Test]
        public void giveitem_should_fail_when_item_unknown()
        {
            var ctx = new FakeConsoleContext();
            ctx.Register<IInventoryService>(new FakeInventoryService());
            var catalog = ScriptableObject.CreateInstance<ItemCatalogSO>(); // catálogo vacío
            ctx.Register<ItemCatalogSO>(catalog);

            var res = new GiveItemCommand().Execute(new[] { "item.nope" }, ctx);

            Assert.IsFalse(res.Success);
        }

        [Test]
        public void setstat_should_write_value_through_attributes_manager()
        {
            var pid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Attack>(new Attack(1));
            var am = new AttributesManager();
            am.Register(pid, attrs);

            var ctx = new FakeConsoleContext { PlayerGuid = pid, IsRunActive = true };
            ctx.Register<AttributesManager>(am);

            var res = new SetStatCommand().Execute(new[] { "attack", "9" }, ctx);

            Assert.IsTrue(res.Success);
            Assert.AreEqual(9, am.GetAttributeValue<Attack, int>(pid));
        }

        [Test]
        public void setdiceroll_should_queue_values_in_rig_state()
        {
            var ctx = new FakeConsoleContext();
            var rig = new RiggedRollState();
            ctx.Register<RiggedRollState>(rig);

            var res = new SetDiceRollCommand().Execute(new[] { "3", "4" }, ctx);

            Assert.IsTrue(res.Success);
            Assert.IsTrue(rig.HasPending);
        }

        // =================================================================
        // potion
        // =================================================================

        [Test]
        public void potion_should_add_the_requested_amount()
        {
            // Arrange
            var inv = new FakeInventoryService();
            var ctx = BuildPotionContext(inv, out _);

            // Act
            var res = new PotionCommand().Execute(new[] { "2" }, ctx);

            // Assert
            Assert.IsTrue(res.Success);
            Assert.AreEqual(2, inv.Added.Count);
            Assert.AreEqual(PotionItemId, inv.Added[0].ItemId);
        }

        [Test]
        public void potion_should_report_the_active_slot_cap_when_the_inventory_rejects_it()
        {
            // Arrange — AddItem rechaza los activos cuando no quedan slots libres.
            var inv = new FakeInventoryService { RejectAdd = true, MaxActiveSlots = 1 };
            var ctx = BuildPotionContext(inv, out _);

            // Act
            var res = new PotionCommand().Execute(Array.Empty<string>(), ctx);

            // Assert
            Assert.IsFalse(res.Success);
            StringAssert.Contains("1 slots", res.Message);
        }

        [Test]
        public void potion_should_fail_when_the_catalog_has_no_potion()
        {
            // Arrange
            var ctx = new FakeConsoleContext();
            ctx.Register<IInventoryService>(new FakeInventoryService());
            ctx.Register<ItemCatalogSO>(ScriptableObject.CreateInstance<ItemCatalogSO>());

            // Act
            var res = new PotionCommand().Execute(Array.Empty<string>(), ctx);

            // Assert
            Assert.IsFalse(res.Success);
            StringAssert.Contains(PotionItemId, res.Message);
        }

        // =================================================================
        // shop
        // =================================================================

        [Test]
        public void shop_should_fail_when_the_room_was_not_entered_yet()
        {
            // Arrange — la room existe pero el manager no la roleó todavía. No hay que
            // inicializarla desde el comando: dejaría la tienda vacía toda la run.
            var ctx = BuildShopContext(price: 50, gold: 100, out var shop, out var roomId, out _, out _);
            shop.InitializedRooms.Remove(roomId);

            // Act
            var res = new ShopCommand().Execute(Array.Empty<string>(), ctx);

            // Assert
            Assert.IsFalse(res.Success);
            StringAssert.Contains("floor", res.Message);
        }

        [Test]
        public void shop_buy_should_charge_gold_and_deliver_the_item()
        {
            // Arrange
            var ctx = BuildShopContext(price: 50, gold: 100, out var shop, out var roomId,
                                       out var economy, out var inv);

            // Act
            var res = new ShopCommand().Execute(new[] { "buy", "0" }, ctx);

            // Assert
            Assert.IsTrue(res.Success);
            Assert.AreEqual(50, economy.CurrentGold);
            Assert.AreEqual(1, inv.Added.Count);
            Assert.AreEqual(1, shop.Purchases.Count);
            Assert.IsTrue(shop.GetSlots(roomId)[0].Purchased);
        }

        [Test]
        public void shop_buy_should_not_deliver_anything_when_gold_is_insufficient()
        {
            // Arrange
            var ctx = BuildShopContext(price: 50, gold: 10, out var shop, out _,
                                       out var economy, out var inv);

            // Act
            var res = new ShopCommand().Execute(new[] { "buy", "0" }, ctx);

            // Assert
            Assert.IsFalse(res.Success);
            Assert.AreEqual(10, economy.CurrentGold);
            Assert.IsEmpty(inv.Added);
            Assert.IsEmpty(shop.Purchases);
        }

        [Test]
        public void shop_buy_should_fail_when_the_slot_index_is_out_of_range()
        {
            // Arrange
            var ctx = BuildShopContext(price: 50, gold: 100, out _, out _, out var economy, out _);

            // Act
            var res = new ShopCommand().Execute(new[] { "buy", "7" }, ctx);

            // Assert
            Assert.IsFalse(res.Success);
            Assert.AreEqual(100, economy.CurrentGold);
        }

        // =================================================================
        // ench random / roll
        // =================================================================

        [Test]
        public void ench_random_should_apply_an_enchantment_the_service_accepts()
        {
            // Arrange
            var svc = new FakeDiceEnchantmentService(DiceType.D6, DiceType.D6);
            var ctx = BuildEnchantContext(svc, enchantmentCount: 3);

            // Act — sin dado ni slot: los elige solo.
            var res = new EnchantCommand().Execute(new[] { "random" }, ctx);

            // Assert
            Assert.IsTrue(res.Success, res.Message);
            Assert.AreEqual(1, svc.Applied.Count);
            Assert.IsNotNull(svc.Bag.GetEnchantmentAt(svc.Applied[0].bag, svc.Applied[0].slot));
        }

        [Test]
        public void ench_random_should_target_the_die_that_was_passed()
        {
            // Arrange — stack append-only: ya no hay slot para pasar, solo el dado.
            var svc = new FakeDiceEnchantmentService(DiceType.D6, DiceType.D6);
            var ctx = BuildEnchantContext(svc, enchantmentCount: 1);

            // Act
            var res = new EnchantCommand().Execute(new[] { "random", "1" }, ctx);

            // Assert
            Assert.IsTrue(res.Success, res.Message);
            Assert.AreEqual(1, svc.Applied[0].bag);
        }

        [Test]
        public void ench_random_should_fail_when_nothing_in_the_catalog_is_compatible()
        {
            // Arrange — ValidateApply rechaza todo (caras incompatibles / redundancia).
            var svc = new FakeDiceEnchantmentService(DiceType.D6) { Accepts = _ => false };
            var ctx = BuildEnchantContext(svc, enchantmentCount: 2);

            // Act
            var res = new EnchantCommand().Execute(new[] { "random" }, ctx);

            // Assert
            Assert.IsFalse(res.Success);
            Assert.IsEmpty(svc.Applied);
        }

        [Test]
        public void ench_roll_should_go_through_the_altar_and_report_the_gold_paid()
        {
            // Arrange — RollOffer (paga y revela) + ConfirmChoice (opción + dado)
            // del altar fakeado.
            var svc = new FakeDiceEnchantmentService(DiceType.D6);
            var ctx = BuildEnchantContext(svc, enchantmentCount: 1);
            var chosenEnchantment = ScriptableObject.CreateInstance<EnchantmentSO>();
            var offer = new EnchantmentOffer(Guid.Empty,
                options: new[] { chosenEnchantment }, goldPaid: 75);
            var altar = new FakeEnchantmentRoomService
            {
                NextOffer = EnchantmentOfferResult.Ok(offer),
                NextChoice = EnchantmentRollResult.Ok(chosenEnchantment, 75, null),
            };
            ctx.Register<IEnchantmentRoomService>(altar);

            // Act
            var res = new EnchantCommand().Execute(new[] { "roll", "0" }, ctx);

            // Assert
            Assert.IsTrue(res.Success, res.Message);
            StringAssert.Contains("75G", res.Message);
            Assert.AreEqual(1, altar.RollOfferCalls);
            Assert.AreEqual(1, altar.ConfirmCalls.Count);
            Assert.AreEqual(0, altar.ConfirmCalls[0].bag, "el dado forzado por argumento");
            // El apply lo hace el service (vía ConfirmChoice), no el comando.
            Assert.IsEmpty(svc.Applied);
        }

        [Test]
        public void ench_roll_should_propagate_the_altar_error()
        {
            // Arrange — el fallo ocurre en RollOffer (oro insuficiente para pagar la
            // palanca), antes de que haya nada para elegir.
            var svc = new FakeDiceEnchantmentService(DiceType.D6);
            var ctx = BuildEnchantContext(svc, enchantmentCount: 1);
            var altar = new FakeEnchantmentRoomService
            {
                NextOffer = EnchantmentOfferResult.Fail("Oro insuficiente (10/40)."),
            };
            ctx.Register<IEnchantmentRoomService>(altar);

            // Act
            var res = new EnchantCommand().Execute(new[] { "roll" }, ctx);

            // Assert
            Assert.IsFalse(res.Success);
            StringAssert.Contains("Oro insuficiente", res.Message);
            Assert.AreEqual(0, altar.ConfirmCalls.Count, "no debe confirmar si la oferta falló");
        }

        // =================================================================
        // Helpers
        // =================================================================

        private const string PotionItemId = "potion.healing";

        private static FakeConsoleContext BuildPotionContext(FakeInventoryService inv, out ItemSO potion)
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            potion = ScriptableObject.CreateInstance<ItemSO>();
            potion.ItemId = PotionItemId;
            potion.DisplayName = "Potion";
            potion.Type = ItemType.Active;
            catalog.EditorAdd(potion);

            var ctx = new FakeConsoleContext();
            ctx.Register<IInventoryService>(inv);
            ctx.Register<ItemCatalogSO>(catalog);
            return ctx;
        }

        private static FakeConsoleContext BuildShopContext(int price, int gold,
            out FakeShopManagerService shop, out Guid roomId,
            out FakeEconomyService economy, out FakeInventoryService inv)
        {
            var dungeon = new FakeDungeonService();
            roomId = dungeon.AddRoom(RoomType.Shop, out var room);
            dungeon.CurrentRoomInstance = room;

            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.test";
            item.DisplayName = "Test Item";

            shop = new FakeShopManagerService();
            shop.InitializedRooms.Add(roomId);
            shop.SlotsByRoom[roomId] = new List<ShopSlot>
            {
                new ShopSlot { SpawnPointId = "reward_0", Item = item, Price = price }
            };

            economy = new FakeEconomyService(gold);
            inv = new FakeInventoryService();

            var ctx = new FakeConsoleContext { IsRunActive = true };
            ctx.Register<IDungeonService>(dungeon);
            ctx.Register<IShopManagerService>(shop);
            ctx.Register<Rollgeon.Economy.IEconomyService>(economy);
            ctx.Register<IInventoryService>(inv);
            return ctx;
        }

        private static FakeConsoleContext BuildEnchantContext(FakeDiceEnchantmentService svc, int enchantmentCount)
        {
            var catalog = ScriptableObject.CreateInstance<EnchantmentCatalogSO>();
            for (int i = 0; i < enchantmentCount; i++)
                catalog.EditorAdd(ScriptableObject.CreateInstance<EnchantmentSO>());

            var ctx = new FakeConsoleContext { IsRunActive = true };
            ctx.Register<IDiceEnchantmentService>(svc);
            ctx.Register<EnchantmentCatalogSO>(catalog);
            return ctx;
        }
    }
}
