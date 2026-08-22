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

        // ---- El anuncio --------------------------------------------------

        [Test]
        public void Tick_AnnouncesTheGoldItTook_OnThePlayer()
        {
            // Arrange
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 25;
            var context = NewContext();

            var captured = new System.Collections.Generic.List<(Guid target, Rollgeon.UI.HUD.FloatingNumberType type, object value)>();
            EventManager.EventReceiver capture = args =>
            {
                if (args == null || args.Length < 3) return;
                captured.Add(((Guid)args[0], (Rollgeon.UI.HUD.FloatingNumberType)args[1], args[2]));
            };
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, capture);

            try
            {
                // Act
                NewNode().Tick(context);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnFloatingNumberRequested, capture);
            }

            // Assert
            var lost = captured.Find(c => c.type == Rollgeon.UI.HUD.FloatingNumberType.GoldLost);
            Assert.AreEqual(context.PlayerGuid, lost.target,
                "El cobro va sobre el jugador: es su oro el que sale del bolsillo.");
            Assert.AreEqual(25f, lost.value,
                "Se manda el monto en positivo — el signo lo pone el formato de GoldLost.");

            var promise = captured.Find(c => c.type == Rollgeon.UI.HUD.FloatingNumberType.Status);
            Assert.AreEqual(_boss, promise.target,
                "La promesa de devolución va sobre el jefe: la caja es suya.");
            Assert.IsInstanceOf<string>(promise.value,
                "'vuelve si lo vencés' no es una cantidad — viaja como texto.");
        }

        [Test]
        public void Tick_BrokePlayer_AnnouncesNothing()
        {
            // Arrange — un "-0 G" enseñaría una regla que en esa pelea no se aplicó.
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 0;

            int announcements = 0;
            EventManager.EventReceiver count = _ => announcements++;
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, count);

            try
            {
                // Act
                NewNode().Tick(NewContext());
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnFloatingNumberRequested, count);
            }

            // Assert
            Assert.AreEqual(0, announcements);
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
            // Arrange — se afirma el heal en concreto y no "cuántos avisos salieron": el arqueo
            // además anuncia el oro cobrado y la promesa de devolución, y contarlos a todos ataba
            // este test a cuántos mensajes tiene el arqueo en vez de a que el heal se vea.
            GiveBossHealth(90);
            _ledger.NextTaxAmount = 20;

            int healRequests = 0;
            EventManager.EventReceiver capture = args =>
            {
                if (args == null || args.Length < 3) return;
                if (args[1] is Rollgeon.UI.HUD.FloatingNumberType.Heal) healRequests++;
            };
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, capture);

            try
            {
                // Act
                NewNode().Tick(NewContext());
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnFloatingNumberRequested, capture);
            }

            // Assert
            Assert.AreEqual(1, healRequests,
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
