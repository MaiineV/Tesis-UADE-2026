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
        public void Chip_PickedUpByThePlayer_AlsoBribesTheBoss_ForThreeRounds()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 8, _boss);

            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);

            Assert.AreEqual(1, _ledger.DamageStepDown, "Devolverle la ficha le baja un escalón.");
            Assert.AreEqual(3, _ledger.BribeRoundsLeft);
            Assert.AreEqual(108, _economy.CurrentGold, "…y el oro de la ficha se cobra igual: son dos cosas.");

            FireRound(1);
            FireRound(2);
            Assert.AreEqual(1, _ledger.DamageStepDown, "Misma ventana que el soborno pago: 3 rondas.");
            FireRound(3);
            Assert.AreEqual(0, _ledger.DamageStepDown);
        }

        [Test]
        public void Chip_BribeIsFree_NeverChargesTheBribeCost()
        {
            // El soborno de lista cuesta 35; el de la ficha es el pago EN ficha.
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 6, _boss);

            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);

            Assert.AreEqual(106, _economy.CurrentGold,
                "Cobrarle además los 35 dejaría la ficha en pérdida neta y nadie la levantaría.");
        }

        [Test]
        public void Chip_SteppedOnByItsOwner_DoesNotBribe()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 8, _boss);

            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _boss);

            Assert.AreEqual(0, _ledger.DamageStepDown, "El jefe no se soborna solo pisando sus fichas.");
        }

        [Test]
        public void Chip_ExpiredWithoutPickup_DoesNotBribe()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 9, _boss);

            EventManager.Trigger(EventName.OnHazardExpired, chipId);
            EventManager.Trigger(EventName.OnHazardTriggered, chipId, _player);

            Assert.AreEqual(0, _ledger.DamageStepDown, "Ignorar las fichas no soborna.");
        }

        [Test]
        public void Chip_TwoInARow_RestartTheWindow_NeverStackTiers()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            _ledger.RegisterChip(first, 6, _boss);
            _ledger.RegisterChip(second, 6, _boss);

            EventManager.Trigger(EventName.OnHazardTriggered, first, _player);
            FireRound(1);
            FireRound(2);
            EventManager.Trigger(EventName.OnHazardTriggered, second, _player);

            Assert.AreEqual(1, _ledger.DamageStepDown,
                "Juntar fichas no apila escalones — si no, tres seguidas lo congelan.");
            FireRound(3);
            FireRound(4);
            Assert.AreEqual(1, _ledger.DamageStepDown, "La ventana arrancó de nuevo con la segunda ficha.");
            FireRound(5);
            Assert.AreEqual(0, _ledger.DamageStepDown);
        }

        [Test]
        public void Chip_Bribe_AnnouncesItselfOverTheBoss_AsText()
        {
            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 7, _boss);

            var announcedOver = new List<Guid>();
            var announcedText = new List<object>();
            EventManager.EventReceiver capture = args =>
            {
                if (args == null || args.Length < 3) return;
                if (!(args[1] is Rollgeon.UI.HUD.FloatingNumberType type)) return;
                if (type != Rollgeon.UI.HUD.FloatingNumberType.Status) return;

                announcedOver.Add((Guid)args[0]);
                announcedText.Add(args[2]);
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

            CollectionAssert.AreEqual(new[] { _boss }, announcedOver,
                "El soborno se anuncia sobre el jefe, no sobre quien levantó la ficha.");
            Assert.AreEqual(1, announcedText.Count);
            Assert.IsInstanceOf<string>(announcedText[0],
                "'-1 escalón' no es una cantidad: viaja como texto o el formato le antepone un '+'.");
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
