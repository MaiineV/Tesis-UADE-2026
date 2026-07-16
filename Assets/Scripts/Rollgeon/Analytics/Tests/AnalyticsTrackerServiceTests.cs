using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Meta;
using Rollgeon.Run;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Analytics.Tests
{
    /// <summary>
    /// Tests de traducción bus→sink y gating de consentimiento del
    /// <see cref="AnalyticsTrackerService"/> (Feature#0029): ciclo de run
    /// (started/ended con outcome derivado), gate de tutorial, dedupe del
    /// run_ended eager, y degradación sin throw.
    /// </summary>
    [TestFixture]
    public class AnalyticsTrackerServiceTests
    {
        private FakeAnalyticsSink _sink;
        private FakeConsentService _consent;
        private FakeRunContextService _runContext;
        private FakePlayerService _player;
        private Rollgeon.Heroes.ClassHeroSO _hero;
        private AnalyticsTrackerService _service;
        private double _fakeTime;

        private readonly Guid _runId = Guid.NewGuid();
        private readonly Guid _playerGuid = Guid.NewGuid();

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            PendingRunRequest.Clear();
            AnalyticsPrefs.ClearDecision();
            _fakeTime = 100.0;

            _sink = new FakeAnalyticsSink();
            ServiceLocator.AddService<IAnalyticsSink>(_sink, ServiceScope.Global);

            _consent = new FakeConsentService { HasDecision = true, IsGranted = true };
            ServiceLocator.AddService<IAnalyticsConsentService>(_consent, ServiceScope.Global);

            _hero = ScriptableObject.CreateInstance<Rollgeon.Heroes.ClassHeroSO>();
            _hero.EntityId = "hero.test";
            _runContext = new FakeRunContextService { RunId = _runId, SelectedHero = _hero };
            ServiceLocator.AddService<IRunContextService>(_runContext, ServiceScope.Global);

            _player = new FakePlayerService { PlayerGuid = _playerGuid };
            ServiceLocator.AddService<Rollgeon.Player.IPlayerService>(_player, ServiceScope.Global);

            _service = new AnalyticsTrackerService { TimeProvider = () => _fakeTime };
            _service.Register();
        }

        [TearDown]
        public void Teardown()
        {
            _service.Dispose();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<UnlockAchievedPayload>.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            PendingRunRequest.Clear();
            AnalyticsPrefs.ClearDecision();
            Object.DestroyImmediate(_hero);
        }

        private void StartRun() =>
            EventManager.Trigger(EventName.OnRunStart, _runId, "ruleset.base");

        // ====================================================================
        // run_started
        // ====================================================================

        [Test]
        public void RunStart_SendsRunStarted_WithHeroSeedAndCommonParams()
        {
            StartRun();

            var sent = _sink.Last(AnalyticsEvents.RunStarted);
            Assert.That(sent, Is.Not.Null);
            Assert.That(sent[AnalyticsEvents.Params.HeroId], Is.EqualTo("hero.test"));
            Assert.That(sent[AnalyticsEvents.Params.RulesetId], Is.EqualTo("ruleset.base"));
            Assert.That(sent[AnalyticsEvents.Params.IsContinue], Is.False);
            Assert.That(sent[AnalyticsEvents.Params.Seed], Is.EqualTo(_runId.GetHashCode()));
            Assert.That(sent[AnalyticsEvents.Params.FloorIndex], Is.EqualTo(0));
            Assert.That(sent[AnalyticsEvents.Params.RunId], Is.EqualTo(_runId.ToString("N")));
            Assert.That(sent[AnalyticsEvents.Params.IsEditor], Is.True);
            Assert.That(sent[AnalyticsEvents.Params.AppVersion], Is.EqualTo(Application.version));
        }

        [Test]
        public void RunStart_WhenResumeRequest_SendsIsContinueTrue()
        {
            _runContext.FloorIndex = 2;
            PendingRunRequest.Set(_hero, _runId, "ruleset.base", isResume: true);

            StartRun();

            var sent = _sink.Last(AnalyticsEvents.RunStarted);
            Assert.That(sent[AnalyticsEvents.Params.IsContinue], Is.True);
            Assert.That(sent[AnalyticsEvents.Params.FloorIndex], Is.EqualTo(2));
        }

        [Test]
        public void RunStart_WhenTutorial_SendsNothing_UntilNextRealRun()
        {
            PendingRunRequest.Set(_hero, _runId, "ruleset.base", isTutorial: true);
            StartRun();
            EventManager.Trigger(EventName.OnFloorChanged, _runId, 1);
            EventManager.Trigger(EventName.OnRunEnd, _runId, null);

            Assert.That(_sink.Sent, Is.Empty);

            // La próxima run real vuelve a trackear normal.
            PendingRunRequest.Clear();
            StartRun();
            Assert.That(_sink.CountOf(AnalyticsEvents.RunStarted), Is.EqualTo(1));
        }

        // ====================================================================
        // run_ended — outcome derivado + dedupe
        // ====================================================================

        [Test]
        public void RunVictory_SendsRunEndedVictory_AndOnRunEndDoesNotDuplicate()
        {
            StartRun();
            _fakeTime += 90.0;

            EventManager.Trigger(EventName.OnRunVictory, _runId);
            EventManager.Trigger(EventName.OnRunEnd, _runId, null);

            Assert.That(_sink.CountOf(AnalyticsEvents.RunEnded), Is.EqualTo(1));
            var sent = _sink.Last(AnalyticsEvents.RunEnded);
            Assert.That(sent[AnalyticsEvents.Params.Outcome], Is.EqualTo(AnalyticsEvents.Outcomes.Victory));
            Assert.That((float)sent[AnalyticsEvents.Params.DurationSec], Is.EqualTo(90f).Within(0.001f));
            Assert.That(_sink.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void PlayerDefeated_SendsPlayerDeathThenRunEndedDefeat()
        {
            StartRun();

            EventManager.Trigger(EventName.OnPlayerDefeated, _runId);

            Assert.That(_sink.Sent.Count, Is.EqualTo(3)); // run_started, player_death, run_ended
            Assert.That(_sink.Sent[1].Name, Is.EqualTo(AnalyticsEvents.PlayerDeath));
            Assert.That(_sink.Sent[2].Name, Is.EqualTo(AnalyticsEvents.RunEnded));
            Assert.That(_sink.Sent[2].Params[AnalyticsEvents.Params.Outcome],
                Is.EqualTo(AnalyticsEvents.Outcomes.Defeat));
        }

        [Test]
        public void RunEnd_WithoutOutcomeMarker_SendsAbandon_AndFlushes()
        {
            StartRun();

            EventManager.Trigger(EventName.OnRunEnd, _runId, null);

            var sent = _sink.Last(AnalyticsEvents.RunEnded);
            Assert.That(sent[AnalyticsEvents.Params.Outcome], Is.EqualTo(AnalyticsEvents.Outcomes.Abandon));
            Assert.That(_sink.FlushCount, Is.EqualTo(1));
        }

        // ====================================================================
        // floor / shop / items / unlock
        // ====================================================================

        [Test]
        public void FloorChanged_SendsFloorReached_WithLastKnownHpAndGold()
        {
            StartRun();
            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 42, 60);
            EventManager.Trigger(EventName.OnGoldChanged, 17, 17);

            EventManager.Trigger(EventName.OnFloorChanged, _runId, 1);

            var sent = _sink.Last(AnalyticsEvents.FloorReached);
            Assert.That(sent[AnalyticsEvents.Params.FloorIndex], Is.EqualTo(1));
            Assert.That(sent[AnalyticsEvents.Params.HpAtEntry], Is.EqualTo(42));
            Assert.That(sent[AnalyticsEvents.Params.GoldAtEntry], Is.EqualTo(17));
        }

        [Test]
        public void ShopPurchase_SendsShopPurchase_WithGoldRemaining()
        {
            StartRun();
            EventManager.Trigger(EventName.OnGoldChanged, 80, -20);

            EventManager.Trigger(EventName.OnShopItemPurchased, "spawn.1", "item.sword", 20);

            var sent = _sink.Last(AnalyticsEvents.ShopPurchase);
            Assert.That(sent[AnalyticsEvents.Params.ItemId], Is.EqualTo("item.sword"));
            Assert.That(sent[AnalyticsEvents.Params.Price], Is.EqualTo(20));
            Assert.That(sent[AnalyticsEvents.Params.GoldRemaining], Is.EqualTo(80));
        }

        [Test]
        public void ComboMatched_FromEnemyGuid_IsIgnored()
        {
            StartRun();

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(), // no es el player
                ComboId = "combo.pair",
                BaseDamage = 5,
            });

            Assert.That(_sink.CountOf(AnalyticsEvents.ComboMatched), Is.EqualTo(0));
        }

        [Test]
        public void ComboMatched_FromPlayer_SendsComboMatched_WithMultiplierFallback()
        {
            StartRun();

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = "combo.pair",
                BaseDamage = 5,
                MultiDmgCombo = 0f, // "no calculado" → 1.0
            });

            var sent = _sink.Last(AnalyticsEvents.ComboMatched);
            Assert.That(sent[AnalyticsEvents.Params.ComboId], Is.EqualTo("combo.pair"));
            Assert.That(sent[AnalyticsEvents.Params.BaseDamage], Is.EqualTo(5));
            Assert.That(sent[AnalyticsEvents.Params.Multiplier], Is.EqualTo(1f));
        }

        [Test]
        public void ItemEvents_TranslateWithCorrectParams()
        {
            StartRun();

            EventManager.Trigger(EventName.OnItemObtained, _playerGuid, "item.ring");
            EventManager.Trigger(EventName.OnItemObtained, Guid.NewGuid(), "item.enemy"); // owner no-player
            EventManager.Trigger(EventName.OnActiveItemUsed, _playerGuid, "item.potion");

            Assert.That(_sink.CountOf(AnalyticsEvents.ItemObtained), Is.EqualTo(1));
            Assert.That(_sink.Last(AnalyticsEvents.ItemObtained)[AnalyticsEvents.Params.ItemId],
                Is.EqualTo("item.ring"));
            Assert.That(_sink.Last(AnalyticsEvents.ActiveItemUsed)[AnalyticsEvents.Params.ItemId],
                Is.EqualTo("item.potion"));
        }

        [Test]
        public void UnlockAchieved_Translates_EvenOutsideActiveRun()
        {
            // Sin run activa: los unlocks del cierre de run llegan post-OnRunEnd.
            TypedEvent<UnlockAchievedPayload>.Raise(new UnlockAchievedPayload
            {
                UnlockId = "unlock.hero2",
                Category = default,
                DuringRun = false,
            });

            var sent = _sink.Last(AnalyticsEvents.UnlockAchieved);
            Assert.That(sent, Is.Not.Null);
            Assert.That(sent[AnalyticsEvents.Params.UnlockId], Is.EqualTo("unlock.hero2"));
            Assert.That(sent[AnalyticsEvents.Params.DuringRun], Is.False);
        }

        // ====================================================================
        // Consentimiento y degradación
        // ====================================================================

        [Test]
        public void WhenConsentDenied_NoEventIsSent_ButAggregationStillRuns()
        {
            _consent.IsGranted = false;
            StartRun();
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.That(_sink.Sent, Is.Empty);

            // Acepta a mitad de combate: el combat_ended sale completo.
            _consent.IsGranted = true;
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(),
                Rollgeon.Combat.FSM.CombatOutcome.Victory);

            var sent = _sink.Last(AnalyticsEvents.CombatEnded);
            Assert.That(sent, Is.Not.Null);
            Assert.That(sent[AnalyticsEvents.Params.TurnCount], Is.EqualTo(2));
        }

        [Test]
        public void WhenConsentGrantedMidSession_SubsequentEventsFlow()
        {
            _consent.IsGranted = false;
            StartRun();
            Assert.That(_sink.Sent, Is.Empty);

            _consent.IsGranted = true;
            EventManager.Trigger(EventName.OnFloorChanged, _runId, 1);

            Assert.That(_sink.CountOf(AnalyticsEvents.FloorReached), Is.EqualTo(1));
        }

        [Test]
        public void WhenNoSinkRegistered_EventsDoNotThrow()
        {
            // Re-setup sin sink: mismo entorno, ServiceLocator sin IAnalyticsSink.
            _service.Dispose();
            ServiceLocator.Clear();
            ServiceLocator.AddService<IAnalyticsConsentService>(_consent, ServiceScope.Global);
            ServiceLocator.AddService<IRunContextService>(_runContext, ServiceScope.Global);
            ServiceLocator.AddService<Rollgeon.Player.IPlayerService>(_player, ServiceScope.Global);
            _service = new AnalyticsTrackerService { TimeProvider = () => _fakeTime };
            _service.Register();

            Assert.DoesNotThrow(() =>
            {
                StartRun();
                EventManager.Trigger(EventName.OnRunVictory, _runId);
                EventManager.Trigger(EventName.OnRunEnd, _runId, null);
            });
        }

        [Test]
        public void WhenSinkNotReady_TrackerDropsSilently()
        {
            _sink.Ready = false;

            Assert.DoesNotThrow(() => StartRun());

            Assert.That(_sink.Sent, Is.Empty);
            Assert.That(_sink.DroppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_Unsubscribes_NoSendsAfterDispose()
        {
            _service.Dispose();

            StartRun();
            TypedEvent<UnlockAchievedPayload>.Raise(new UnlockAchievedPayload { UnlockId = "u" });

            Assert.That(_sink.Sent, Is.Empty);
        }

        [Test]
        public void Register_RegistersConsentService_WhenMissing()
        {
            // Setup sin consent service pre-registrado.
            _service.Dispose();
            ServiceLocator.Clear();
            ServiceLocator.AddService<IAnalyticsSink>(_sink, ServiceScope.Global);
            _service = new AnalyticsTrackerService();
            _service.Register();

            Assert.That(ServiceLocator.TryGetService<IAnalyticsConsentService>(out var consent), Is.True);
            Assert.That(consent, Is.InstanceOf<AnalyticsConsentService>());
        }
    }
}
