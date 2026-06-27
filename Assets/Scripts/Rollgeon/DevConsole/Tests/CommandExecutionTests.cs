using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.DevConsole.Commands;
using Rollgeon.Dice;
using Rollgeon.Items;
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
    }
}
