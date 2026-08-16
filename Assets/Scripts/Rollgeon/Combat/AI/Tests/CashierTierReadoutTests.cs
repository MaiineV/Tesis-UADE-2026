using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.UI.HUD;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// La lectura del escalón del Cajero: qué dice la línea bajo su barra, y que el número salga del
    /// mismo lugar del que sale el golpe.
    /// </summary>
    /// <remarks>
    /// Es lo que hace visible la trampa de las fichas — el daño del jefe lo decide el oro que llevás
    /// encima, y lo único que el jefe suelta es oro. Sin la línea, el jugador ve un jefe que a veces
    /// pega 14 y a veces 35 sin ninguna pista de por qué.
    /// </remarks>
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

        // =====================================================================
        // El servicio: guardar y avisar el escalón
        // =====================================================================

        [Test]
        public void LastTier_BeforeTheFirstMark_IsNull()
        {
            // Arrange / Act / Assert — la línea no puede mostrar un escalón inventado antes de que
            // el jefe haya marcado nada.
            Assert.IsNull(_ledger.LastTier);
        }

        [Test]
        public void ReportTier_KeepsTheWholeSnapshot_NotJustTheDamage()
        {
            // Arrange — la línea explica de dónde sale el número, así que necesita las tres causas.
            _ledger.ReportTier(rank: 2, damage: 35, gold: 140, stepUp: 1, stepDown: 0);

            // Act
            var tier = _ledger.LastTier;

            // Assert
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
            // Arrange
            int notifications = 0;
            EventManager.EventReceiver count = _ => notifications++;
            EventManager.Subscribe(EventName.OnCashierTierChanged, count);

            try
            {
                // Act
                _ledger.ReportTier(1, 28, 65, 0, 0);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnCashierTierChanged, count);
            }

            // Assert
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void CombatEnd_ForgetsTheTier()
        {
            // Arrange
            _ledger.ReportTier(2, 35, 140, 1, 0);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.IsNull(_ledger.LastTier,
                "El escalón de la pelea pasada no puede aparecer en la barra de la siguiente.");
        }

        // =====================================================================
        // El texto
        // =====================================================================

        [Test]
        public void Format_PlainCase_ShowsTheTierTheDamageAndTheGold()
        {
            // Arrange — el 90% de la pelea: sin rastrillo y sin soborno.
            var tier = new CashierTierSnapshot(rank: 1, damage: 28, gold: 65, stepUp: 0, stepDown: 0);

            // Act
            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 0);

            // Assert — el escalón va 1-based: "escalón 0" se lee como "ninguno", no como el barato.
            Assert.AreEqual("Escalón 2: pega 28   (oro 65)", line);
        }

        [Test]
        public void Format_WithTheRakeRunning_NamesIt()
        {
            // Arrange — el rastrillo sube solo con las rondas y no baja nunca; si no se nombra, el
            // jugador ve que el daño creció sin haber tocado una moneda.
            var tier = new CashierTierSnapshot(rank: 2, damage: 35, gold: 65, stepUp: 1, stepDown: 0);

            // Act
            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 0);

            // Assert
            Assert.AreEqual("Escalón 3: pega 35   (oro 65, rastrillo +1)", line);
        }

        [Test]
        public void Format_WithABribeActive_ShowsTheRoundsItHasLeft()
        {
            // Arrange — sin la cuenta atrás, el jugador no puede decidir si vale la pena ir a buscar
            // otra ficha o aguantar.
            var tier = new CashierTierSnapshot(rank: 1, damage: 28, gold: 71, stepUp: 1, stepDown: 1);

            // Act
            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 2);

            // Assert
            Assert.AreEqual("Escalón 2: pega 28   (oro 71, rastrillo +1, soborno -1 por 2 rondas)", line);
        }

        [Test]
        public void Format_OmitsTheCausesThatAreNotActive()
        {
            // Arrange — tres cifras para decir una cosa es ruido: sólo aparecen las que suman.
            var tier = new CashierTierSnapshot(rank: 0, damage: 14, gold: 12, stepUp: 0, stepDown: 0);

            // Act
            string line = CashierTierReadoutView.Format(tier, bribeRoundsLeft: 0);

            // Assert
            StringAssert.DoesNotContain("rastrillo", line);
            StringAssert.DoesNotContain("soborno", line);
        }

        // =====================================================================
        // La cadena completa: pisar una ficha se ve en la línea
        // =====================================================================

        [Test]
        public void SteppingOnAChip_ChangesWhatTheLineSays()
        {
            // Arrange — el bucle entero del jefe en un test: le pegás, suelta ficha, la levantás,
            // el escalón baja y la barra lo dice.
            var boss = Guid.NewGuid();
            var player = Guid.NewGuid();

            _ledger.ReportTier(rank: 2, damage: 35, gold: 140, stepUp: 1, stepDown: 0);
            string before = CashierTierReadoutView.Format(_ledger.LastTier.Value, _ledger.BribeRoundsLeft);
            Assume.That(before, Does.Not.Contain("soborno"));

            var chipId = Guid.NewGuid();
            _ledger.RegisterChip(chipId, 8, boss);

            // Act
            EventManager.Trigger(EventName.OnHazardTriggered, chipId, player);
            _ledger.ReportTier(rank: 1, damage: 28, gold: 148, stepUp: 1, stepDown: _ledger.DamageStepDown);
            string after = CashierTierReadoutView.Format(_ledger.LastTier.Value, _ledger.BribeRoundsLeft);

            // Assert
            StringAssert.Contains("soborno -1", after);
            StringAssert.Contains("por 3 rondas", after);
            StringAssert.Contains("pega 28", after);
        }
    }
}
