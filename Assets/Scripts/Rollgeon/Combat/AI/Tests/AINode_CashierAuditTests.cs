using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Cashier;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_CashierAudit"/>: el arqueo de caja al 50% de HP — guarda parte del
    /// oro del jugador, se cura con eso (con tope) y duplica el valor de las fichas.
    /// </summary>
    [TestFixture]
    public class AINode_CashierAuditTests
    {
        private const int BossMaxHp = 190;

        private AttributesManager _attributes;
        private FakeCashierLedgerService _ledger;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attributes = new AttributesManager();
            _boss = Guid.NewGuid();

            _ledger = new FakeCashierLedgerService();
            ServiceLocator.AddService<ICashierLedgerService>(_ledger);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void GiveBossHealth(int current)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(current));
            _attributes.Register(_boss, attrs);
        }

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = Guid.NewGuid(),
            Attributes = _attributes,
            SelfMaxHp = BossMaxHp,
        };

        private static AINode_CashierAudit NewNode() => new AINode_CashierAudit
        {
            TaxPercent = 0.4f,
            MaxHeal = 30,
            ChipValueMultiplierAfterAudit = 2,
        };

        private int BossHp() => _attributes.GetAttributeValue<Health, int>(_boss);

        // ---- Caso central ------------------------------------------------

        [Test]
        public void Tick_VaultsFortyPercent_AndHealsByThatMuch()
        {
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 25;

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _ledger.CollectTaxCalls);
            Assert.AreEqual(0.4f, _ledger.LastTaxPercent, 0.0001f, "El arqueo se cobra el 40% (ficha).");
            Assert.AreEqual(115, BossHp(), "Se cura por lo guardado: 90 + 25.");
            Assert.AreEqual(25, _ledger.VaultedGold, "El oro queda en la caja, no desaparece.");
        }

        [Test]
        public void Tick_HealIsCappedAtMaxHeal()
        {
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 120; // Jugador ricachón: el arqueo junta mucho más que el tope.

            NewNode().Tick(NewContext());

            Assert.AreEqual(120, BossHp(), "El tope de curación es +30, aunque secuestre 120 de oro.");
            Assert.AreEqual(120, _ledger.VaultedGold, "Se guarda todo igual — el tope es del heal, no del cobro.");
        }

        [Test]
        public void Tick_HealNeverExceedsMaxHp()
        {
            GiveBossHealth(180);
            _ledger.NextTaxAmount = 30;

            NewNode().Tick(NewContext());

            Assert.AreEqual(BossMaxHp, BossHp(), "El heal se clampea al HP máximo del spawn.");
        }

        [Test]
        public void Tick_BrokePlayer_HealsNothing_ButStillSucceeds()
        {
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 0;

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result,
                "Un Failed acá abortaría el Sequence del gate y la Fase 2 nunca anunciaría.");
            Assert.AreEqual(90, BossHp(), "Sin oro no hay curación: el jefe sólo se cura si te dejaste tentar.");
        }

        [Test]
        public void Tick_MakesChipsWorthDouble()
        {
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 10;

            NewNode().Tick(NewContext());

            Assert.AreEqual(2, _ledger.ChipValueMultiplier,
                "Después del arqueo las fichas valen el doble.");
        }

        [Test]
        public void Tick_EmitsFloatingHeal_ForLegibility()
        {
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 20;
            int floatingRequests = 0;
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, _ => floatingRequests++);

            NewNode().Tick(NewContext());

            Assert.AreEqual(1, floatingRequests,
                "El único jefe que se cura tiene que mostrarlo en pantalla.");
        }

        [Test]
        public void Tick_WithoutSelfMaxHp_StillHeals()
        {
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 20;
            var context = NewContext();
            context.SelfMaxHp = 0; // Contexto sin baseline (harness parcial).

            NewNode().Tick(context);

            Assert.AreEqual(110, BossHp(), "Sin baseline se cura sin clamp antes que perder el heal.");
        }

        [Test]
        public void Tick_NullContextOrEmptySelf_ReturnsFailed()
        {
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(null));
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(new AIContext { SelfGuid = Guid.Empty }));
            Assert.AreEqual(0, _ledger.CollectTaxCalls, "Sin owner no se le cobra nada al jugador.");
        }
    }
}
