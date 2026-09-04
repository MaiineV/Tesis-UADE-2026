using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Economy;

namespace Rollgeon.Combat.AI.Tests
{
    // El servicio se suscribe a TypedEvent<DamageResolvedPayload>, que ServiceLocator.Clear() no
    // desengancha: de ahí el Dispose en el teardown.
    [TestFixture]
    public class CashierLedgerServiceTests
    {
        private CashierLedgerService _ledger;
        private FakeEconomyService _economy;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _economy = new FakeEconomyService(100);
            ServiceLocator.AddService<IEconomyService>(_economy);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
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

        private void RaiseDamage(Guid target, int amount) =>
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _player,
                TargetGuid = target,
                FinalDamage = amount,
            });

        private static void FireRound(int roundIndex) =>
            EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>(), roundIndex);

        [Test]
        public void ConsumeDamageTaken_TrueOnceAfterAHit_ThenFalse()
        {
            RaiseDamage(_boss, 12);

            Assert.IsTrue(_ledger.ConsumeDamageTaken(_boss), "Le pegaron: la ficha se paga una vez.");
            Assert.IsFalse(_ledger.ConsumeDamageTaken(_boss), "El flag se consume — no paga dos veces.");
        }

        [Test]
        public void ConsumeDamageTaken_IgnoresZeroDamageAndOtherTargets()
        {
            RaiseDamage(_boss, 0);
            Assert.IsFalse(_ledger.ConsumeDamageTaken(_boss), "Un golpe de 0 no suelta ficha.");

            RaiseDamage(_player, 20);
            Assert.IsFalse(_ledger.ConsumeDamageTaken(_boss), "El daño al jugador no es daño al jefe.");
        }

        [Test]
        public void ConsumeDamageTaken_WorksWhenTheHitLandsBeforeTheBossEverTicks()
        {
            // Cola player-first: el jugador pega en la ronda 1 antes de que ningún nodo del jefe
            // haya corrido. El flag se anota por guid dañado, así que no se pierde.
            RaiseDamage(_boss, 30);
            Assert.IsTrue(_ledger.ConsumeDamageTaken(_boss));
        }

        [Test]
        public void CollectTax_TakesThePercent_AndVaultsIt()
        {
            int collected = _ledger.CollectTax(_boss, 0.4f);

            Assert.AreEqual(40, collected);
            Assert.AreEqual(60, _economy.CurrentGold, "El 40% sale del bolsillo del jugador.");
            Assert.AreEqual(40, _ledger.VaultedGold, "…y entra a la caja del jefe.");
        }

        [Test]
        public void CollectTax_FloorsTheAmount_NeverRoundsForTheHouse()
        {
            _economy.ResetTo(99);

            Assert.AreEqual(39, _ledger.CollectTax(_boss, 0.4f), "40% de 99 = 39.6 ⇒ 39.");
            Assert.AreEqual(60, _economy.CurrentGold);
        }

        [Test]
        public void CollectTax_BrokePlayer_TakesNothing()
        {
            _economy.ResetTo(0);

            Assert.AreEqual(0, _ledger.CollectTax(_boss, 0.4f));
            Assert.AreEqual(0, _ledger.VaultedGold);
        }

        [Test]
        public void CollectTax_WithoutRefundOnDeath_TakesTheGoldOutOfTheGame()
        {
            int collected = _ledger.CollectTax(_boss, 0.4f, refundOnDeath: false);

            Assert.AreEqual(40, collected, "Cobrar es cobrar: el reembolso no cambia cuánto saca.");
            Assert.AreEqual(60, _economy.CurrentGold);
            Assert.AreEqual(0, _ledger.VaultedGold,
                "Entró a la caja, y la caja se le devuelve entera al jugador cuando el jefe muere: " +
                "el cobro del empujón sería un préstamo, y con las monedas del piso encima el " +
                "jugador terminaría la pelea con más oro del que le sacaron.");

            EventManager.Trigger(EventName.OnEntityDestroyed, _boss);

            Assert.AreEqual(60, _economy.CurrentGold,
                "Matarlo le devolvió la plata. Lo que el jugador no levanta del piso se pierde.");
        }

        [Test]
        public void CollectTax_ThinWallet_ChargesTheFloorInsteadOfThePercent()
        {
            _economy.ResetTo(50);

            Assert.AreEqual(10, _ledger.CollectTax(_boss, 0.1f, 10),
                "10% de 50 son 5, y el piso lo levanta a 10.");
            Assert.AreEqual(40, _economy.CurrentGold);
        }

        [Test]
        public void CollectTax_FatWallet_IgnoresTheFloor()
        {
            _economy.ResetTo(400);

            Assert.AreEqual(40, _ledger.CollectTax(_boss, 0.1f, 10),
                "El piso es un piso, no un techo ni un monto fijo.");
        }

        [Test]
        public void CollectTax_LessGoldThanTheFloor_TakesWhatIsLeft()
        {
            _economy.ResetTo(4);

            Assert.AreEqual(4, _ledger.CollectTax(_boss, 0.1f, 10),
                "El piso no puede cobrar lo que no hay; si pidiera 10 el Spend fallaría y el " +
                "jugador casi seco saldría gratis, que es justo lo que el piso viene a impedir.");
            Assert.AreEqual(0, _economy.CurrentGold);
            Assert.AreEqual(4, _ledger.VaultedGold);
        }

        [Test]
        public void CollectTax_WithoutEconomy_IsNoOp()
        {
            ServiceLocator.RemoveService<IEconomyService>();

            Assert.AreEqual(0, _ledger.CollectTax(_boss, 0.4f));
            Assert.AreEqual(0, _ledger.VaultedGold);
        }

        [Test]
        public void BossDeath_OpensTheVault_AndRefundsEverything()
        {
            _ledger.CollectTax(_boss, 0.4f);

            EventManager.Trigger(EventName.OnEntityDestroyed, _boss);

            Assert.AreEqual(100, _economy.CurrentGold, "Al vencerlo se recupera todo lo secuestrado.");
            Assert.AreEqual(0, _ledger.VaultedGold);
        }

        [Test]
        public void OtherEntityDeath_DoesNotOpenTheVault()
        {
            _ledger.CollectTax(_boss, 0.4f);

            EventManager.Trigger(EventName.OnEntityDestroyed, Guid.NewGuid());

            Assert.AreEqual(60, _economy.CurrentGold);
            Assert.AreEqual(40, _ledger.VaultedGold);
        }

        [Test]
        public void RunEnd_LosesTheVault_TheHouseWins()
        {
            _ledger.CollectTax(_boss, 0.4f);

            EventManager.Trigger(EventName.OnRunEnd);

            Assert.AreEqual(60, _economy.CurrentGold, "Si el jugador muere con la caja llena, gana la banca.");
            Assert.AreEqual(0, _ledger.VaultedGold);
        }

        [Test]
        public void CombatEnd_ResetsPerCombatState()
        {
            _ledger.CollectTax(_boss, 0.4f);
            _ledger.SetChipValueMultiplier(2);
            _ledger.TryBribe();
            RaiseDamage(_boss, 5);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.AreEqual(0, _ledger.VaultedGold);
            Assert.AreEqual(1, _ledger.ChipValueMultiplier, "El ×2 del arqueo no se filtra a la pelea siguiente.");
            Assert.AreEqual(0, _ledger.DamageStepDown);
            Assert.IsFalse(_ledger.ConsumeDamageTaken(_boss));
        }

        [Test]
        public void TryBribe_ChargesTheCost_AndBuysOneTierForThreeRounds()
        {
            bool paid = _ledger.TryBribe();

            Assert.IsTrue(paid);
            Assert.AreEqual(65, _economy.CurrentGold, "El soborno cuesta 35 de oro (ficha).");
            Assert.AreEqual(1, _ledger.DamageStepDown);

            FireRound(1);
            Assert.AreEqual(1, _ledger.DamageStepDown, "Ronda 2 de 3: sigue vigente.");
            FireRound(2);
            Assert.AreEqual(1, _ledger.DamageStepDown, "Ronda 3 de 3: sigue vigente.");
            FireRound(3);
            Assert.AreEqual(0, _ledger.DamageStepDown, "Cumplidas las 3 rondas, el descuento se cae.");
        }

        [Test]
        public void TryBribe_WithoutFunds_ChangesNothing()
        {
            _economy.ResetTo(34);

            Assert.IsFalse(_ledger.TryBribe(), "Sin 35 de oro no hay soborno.");
            Assert.AreEqual(34, _economy.CurrentGold);
            Assert.AreEqual(0, _ledger.DamageStepDown);
        }

        [Test]
        public void TryBribe_Twice_RestartsTheWindow_NeverStacksTiers()
        {
            _economy.ResetTo(200);

            _ledger.TryBribe();
            FireRound(1);
            FireRound(2);
            _ledger.TryBribe();

            Assert.AreEqual(1, _ledger.DamageStepDown, "Dos sobornos no bajan dos escalones.");
            FireRound(3);
            FireRound(4);
            Assert.AreEqual(1, _ledger.DamageStepDown, "La ventana arrancó de nuevo con el segundo pago.");
            FireRound(5);
            Assert.AreEqual(0, _ledger.DamageStepDown);
        }

        [Test]
        public void SameRoundIndexFiredTwice_DoesNotBurnTheBribeWindow()
        {
            _ledger.TryBribe();

            FireRound(1);
            FireRound(1); // Re-broadcast del mismo round (ej. un Append a la cola).
            FireRound(2);

            Assert.AreEqual(1, _ledger.DamageStepDown, "La ventana cuenta rondas, no eventos.");
        }

        [Test]
        public void Chip_PaysThePlayerOnPickup_Once()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 8, _boss);

            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);
            Assert.AreEqual(108, _economy.CurrentGold, "Pisar la ficha paga su valor.");

            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);
            Assert.AreEqual(108, _economy.CurrentGold, "La ficha ya cobrada no vuelve a pagar.");
        }

        [Test]
        public void Chip_SteppedOnByItsOwner_PaysNothing()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 8, _boss);

            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _boss);

            Assert.AreEqual(100, _economy.CurrentGold, "El jefe kitea sobre su columna: no se cobra su propia ficha.");
            Assert.AreEqual(8, _ledger.GetChipValue(chipId), "…y la ficha sigue viva para el jugador.");
        }

        [Test]
        public void Chip_ExpiredWithoutPickup_PaysNobody()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 9, _boss);

            EventManager.Trigger(EventName.OnHazardExpired, chipId);
            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);

            Assert.AreEqual(100, _economy.CurrentGold, "La ficha que rodó de vuelta a la caja no paga.");
            Assert.AreEqual(0, _ledger.GetChipValue(chipId));
        }

        [Test]
        public void Chip_UnknownHazardInstance_IsIgnored()
        {
            EventManager.Trigger(EventName.OnHazardTriggered, Guid.NewGuid(), _player);

            Assert.AreEqual(100, _economy.CurrentGold, "Un hazard ajeno (fuego, hielo) no paga oro.");
        }

        [Test]
        public void Chip_PickedUp_PaysAndAnnouncesNothing()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 7, _boss);

            var announced = new List<object>();
            EventManager.EventReceiver capture = args =>
            {
                if (args == null || args.Length < 3) return;
                if (!(args[1] is Rollgeon.UI.HUD.FloatingNumberType type)) return;
                if (type != Rollgeon.UI.HUD.FloatingNumberType.Status) return;

                announced.Add(args[2]);
            };
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, capture);

            try
            {
                EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnFloatingNumberRequested, capture);
            }

            Assert.AreEqual(107, _economy.CurrentGold, "La ficha paga su valor y nada más.");
            Assert.AreEqual(0, _ledger.DamageStepDown,
                "Los escalones se fueron con el rediseno: levantar una ficha no le descuenta nada.");
            CollectionAssert.IsEmpty(announced,
                "Sin escalones no hay descuento que anunciar, y el cartel prometia una mecanica que no corre.");
        }

        [Test]
        public void SetChipValueMultiplier_ClampsToOne()
        {
            _ledger.SetChipValueMultiplier(0);
            Assert.AreEqual(1, _ledger.ChipValueMultiplier);

            _ledger.SetChipValueMultiplier(2);
            Assert.AreEqual(2, _ledger.ChipValueMultiplier, "El arqueo duplica el valor de las fichas.");
        }

        [Test]
        public void ResolveOrCreate_IsIdempotent()
        {
            ServiceLocator.AddService<ICashierLedgerService>(_ledger);

            Assert.AreSame(_ledger, CashierLedgerService.ResolveOrCreate());
        }
    }
}
