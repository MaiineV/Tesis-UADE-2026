using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.UI.HUD;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class CashierTierReadoutTests
    {
        private CashierLedgerService _ledger;
        private FakeEconomyService _economy;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _economy = new FakeEconomyService(100);
            ServiceLocator.AddService<Rollgeon.Economy.IEconomyService>(_economy);

            _ledger = new CashierLedgerService();
        }

        [TearDown]
        public void TearDown()
        {
            _ledger.Dispose();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void LastTier_BeforeTheFirstMark_IsNull()
        {
            // La línea no puede mostrar un escalón inventado antes de que el jefe haya marcado nada.
            Assert.IsNull(_ledger.LastTier);
        }

        [Test]
        public void ReportTier_KeepsTheWholeSnapshot_NotJustTheDamage()
        {
            // La línea explica de dónde sale el número, así que necesita las tres causas.
            _ledger.ReportTier(rank: 2, damage: 35, gold: 140, stepUp: 1, stepDown: 0);

            var tier = _ledger.LastTier;

            Assert.IsNotNull(tier);
            Assert.AreEqual(2, tier.Value.Rank);
            Assert.AreEqual(35, tier.Value.Damage);
            Assert.AreEqual(140, tier.Value.Gold);
            Assert.AreEqual(1, tier.Value.StepUp);
            Assert.AreEqual(0, tier.Value.StepDown);
        }

        [Test]
        public void ReportTier_NotifiesSoTheHudCanRepaint()
        {
            int notifications = 0;
            EventManager.EventReceiver count = _ => notifications++;
            EventManager.Subscribe(EventName.OnCashierTierChanged, count);

            try
            {
                _ledger.ReportTier(1, 28, 65, 0, 0);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnCashierTierChanged, count);
            }

            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void CombatEnd_ForgetsTheTier()
        {
            _ledger.ReportTier(2, 35, 140, 1, 0);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsNull(_ledger.LastTier,
                "El escalón de la pelea pasada no puede aparecer en la barra de la siguiente.");
        }

        [Test]
        public void Format_PlainCase_ShowsTheTierTheDamageAndTheGold()
        {
            var tier = new CashierTierSnapshot(rank: 1, damage: 28, gold: 65, stepUp: 0, stepDown: 0);

            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 0);

            // El escalón va 1-based: "escalón 0" se lee como "ninguno", no como el barato.
            Assert.AreEqual("Escalón 2: pega 28   (oro 65)", line);
        }

        [Test]
        public void Format_WithTheRakeRunning_NamesIt()
        {
            var tier = new CashierTierSnapshot(rank: 2, damage: 35, gold: 65, stepUp: 1, stepDown: 0);

            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 0);

            Assert.AreEqual("Escalón 3: pega 35   (oro 65, rastrillo +1)", line);
        }

        [Test]
        public void Format_WithABribeActive_ShowsTheRoundsItHasLeft()
        {
            var tier = new CashierTierSnapshot(rank: 1, damage: 28, gold: 71, stepUp: 1, stepDown: 1);

            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 2);

            Assert.AreEqual("Escalón 2: pega 28   (oro 71, rastrillo +1, soborno -1 por 2 rondas)", line);
        }

        [Test]
        public void Format_OmitsTheCausesThatAreNotActive()
        {
            var tier = new CashierTierSnapshot(rank: 0, damage: 14, gold: 12, stepUp: 0, stepDown: 0);

            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 0);

            StringAssert.DoesNotContain("rastrillo", line);
            StringAssert.DoesNotContain("soborno", line);
        }
    }
}
