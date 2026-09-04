using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>Tests de <see cref="EffModifyGold"/>.</summary>
    [TestFixture]
    public class EffModifyGoldTests
    {
        private FakeEconomy _economy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _economy = new FakeEconomy(10);
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static EffModifyGold Build(GoldOperation op, int amount, bool failChain = true)
        {
            return new EffModifyGold
            {
                Operation = op,
                Amount = new ReadConstantInt { Value = amount },
                FailChainIfInsufficient = failChain,
            };
        }

        [Test]
        public void Apply_Add_IncreasesGold()
        {
            var result = Build(GoldOperation.Add, 5).Apply(new EffectContext());

            Assert.IsTrue(result);
            Assert.AreEqual(15, _economy.CurrentGold);
        }

        [Test]
        public void Apply_SpendWithEnoughGold_Deducts()
        {
            var result = Build(GoldOperation.Spend, 4).Apply(new EffectContext());

            Assert.IsTrue(result);
            Assert.AreEqual(6, _economy.CurrentGold);
        }

        [Test]
        public void Apply_SpendInsufficient_FailChain_CutsGroupWithoutSpending()
        {
            var ctx = new EffectContext();

            var result = Build(GoldOperation.Spend, 99).Apply(ctx);

            Assert.IsFalse(result, "Spend insuficiente con failChain debe cortar el grupo.");
            Assert.IsFalse(ctx.lastResult);
            Assert.AreEqual(10, _economy.CurrentGold, "All-or-nothing: no gasta parcial.");
        }

        [Test]
        public void Apply_SpendInsufficient_NoFailChain_ContinuesWithoutSpending()
        {
            var result = Build(GoldOperation.Spend, 99, failChain: false).Apply(new EffectContext());

            Assert.IsTrue(result);
            Assert.AreEqual(10, _economy.CurrentGold);
        }

        [Test]
        public void Apply_Set_ClampsToZeroFloor()
        {
            Build(GoldOperation.Set, -5).Apply(new EffectContext());

            Assert.AreEqual(0, _economy.CurrentGold);
        }

        [Test]
        public void Apply_SpendWithoutEconomyService_FailChain_CutsGroup()
        {
            // Arrange — sin economía un Spend no puede afirmarse pagado.
            ServiceLocator.Clear();

            var result = Build(GoldOperation.Spend, 5).Apply(new EffectContext());

            Assert.IsFalse(result);
        }

        // --- OnItemGoldGranted (toast sobre la pila de oro, Fix#0077) ---------------------

        [Test]
        public void test_modify_gold_add_from_item_emits_item_gold_granted_with_player_item_and_amount()
        {
            // Arrange — el dispatcher de hooks setea SourceItemId (Bolsa del Impar).
            var player = Guid.NewGuid();
            var ctx = new EffectContext { SourceGuid = player, SourceItemId = "bolsa.del.impar" };
            object[] captured = null;
            EventManager.EventReceiver handler = args => captured = args;
            EventManager.Subscribe(EventName.OnItemGoldGranted, handler);

            try
            {
                // Act
                Build(GoldOperation.Add, 6).Apply(ctx);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnItemGoldGranted, handler);
            }

            // Assert
            Assert.IsNotNull(captured, "Add con SourceItemId debe emitir OnItemGoldGranted.");
            Assert.AreEqual(3, captured.Length);
            Assert.AreEqual(player, captured[0]);
            Assert.AreEqual("bolsa.del.impar", captured[1]);
            Assert.AreEqual(6, captured[2]);
            Assert.AreEqual(16, _economy.CurrentGold);
        }

        [Test]
        public void test_modify_gold_add_without_item_source_does_not_emit_item_gold_granted()
        {
            // Arrange — oro de un behavior/enemigo, no de un item.
            var ctx = new EffectContext { SourceGuid = Guid.NewGuid() };
            int calls = 0;
            EventManager.EventReceiver handler = _ => calls++;
            EventManager.Subscribe(EventName.OnItemGoldGranted, handler);

            try
            {
                // Act
                Build(GoldOperation.Add, 6).Apply(ctx);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnItemGoldGranted, handler);
            }

            // Assert
            Assert.AreEqual(0, calls);
            Assert.AreEqual(16, _economy.CurrentGold);
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public FakeEconomy(int gold) { CurrentGold = gold; }
            public int CurrentGold { get; private set; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }
    }
}
