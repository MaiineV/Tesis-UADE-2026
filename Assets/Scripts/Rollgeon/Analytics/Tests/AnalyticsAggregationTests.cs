using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Run;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Analytics.Tests
{
    /// <summary>
    /// Tests de agregación per-combat/per-run del <see cref="AnalyticsTrackerService"/>
    /// (Feature#0029): acumuladores campo por campo, reset entre combates,
    /// acumulación de run a través de combates, top_combos y fase de boss.
    /// </summary>
    [TestFixture]
    public class AnalyticsAggregationTests
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
        private readonly Guid _enemyGuid = Guid.NewGuid();
        private readonly Guid _roomGuid = Guid.NewGuid();

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            PendingRunRequest.Clear();
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

            EventManager.Trigger(EventName.OnRunStart, _runId, "ruleset.base");
        }

        [TearDown]
        public void Teardown()
        {
            _service.Dispose();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<Rollgeon.Meta.UnlockAchievedPayload>.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            PendingRunRequest.Clear();
            Object.DestroyImmediate(_hero);
        }

        private void RaiseDamage(Guid source, Guid target, int finalDamage, int shieldAbsorbed = 0)
        {
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = source,
                TargetGuid = target,
                FinalDamage = finalDamage,
                ShieldAbsorbed = shieldAbsorbed,
            });
        }

        private void RaisePlayerCombo(string comboId)
        {
            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = comboId,
                BaseDamage = 3,
            });
        }

        // ====================================================================
        // combat_ended — agregación completa
        // ====================================================================

        [Test]
        public void CombatEnd_AggregatesTurnsDamageRerollsRollsFromEvents()
        {
            EventManager.Trigger(EventName.OnCombatTriggered, _roomGuid, "room.boss", RoomType.Boss);
            EventManager.Trigger(EventName.OnCombatStart, _roomGuid);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _enemyGuid); // no cuenta
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            RaiseDamage(_playerGuid, _enemyGuid, 10, shieldAbsorbed: 2); // dealt 12
            RaiseDamage(_enemyGuid, _playerGuid, 5, shieldAbsorbed: 3);  // taken 8
            RaiseDamage(_enemyGuid, Guid.NewGuid(), 99);                 // enemigo↔enemigo, no cuenta

            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 0);
            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 1);
            EventManager.Trigger(EventName.OnRerollStarted, _enemyGuid, 0); // no cuenta

            // Energía: 3 (baseline) → 2 (gasto 1) → 0 (gasto 2) → 3 (refill, no cuenta)
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 3, 15);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 2, 15);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 15);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 3, 15);

            EventManager.Trigger(EventName.OnBossPhaseChanged, _enemyGuid, 2);
            EventManager.Trigger(EventName.OnPlayerHealthChanged, _playerGuid, 25, 60);

            _fakeTime += 12.5;
            EventManager.Trigger(EventName.OnCombatEnd, _roomGuid, CombatOutcome.Victory);

            var sent = _sink.Last(AnalyticsEvents.CombatEnded);
            Assert.That(sent, Is.Not.Null);
            Assert.That(sent[AnalyticsEvents.Params.RoomType], Is.EqualTo("Boss"));
            Assert.That(sent[AnalyticsEvents.Params.Outcome], Is.EqualTo("Victory"));
            Assert.That(sent[AnalyticsEvents.Params.TurnCount], Is.EqualTo(3));
            Assert.That((float)sent[AnalyticsEvents.Params.DurationSec], Is.EqualTo(12.5f).Within(0.001f));
            Assert.That(sent[AnalyticsEvents.Params.DamageDealt], Is.EqualTo(12));
            Assert.That(sent[AnalyticsEvents.Params.DamageTaken], Is.EqualTo(8));
            Assert.That(sent[AnalyticsEvents.Params.RerollsUsed], Is.EqualTo(2));
            Assert.That(sent[AnalyticsEvents.Params.RollsSpent], Is.EqualTo(3));
            Assert.That(sent[AnalyticsEvents.Params.HpRemaining], Is.EqualTo(25));
            Assert.That(sent[AnalyticsEvents.Params.BossPhaseReached], Is.EqualTo(2));
        }

        [Test]
        public void CombatAggregator_SecondCombat_StartsFromZero()
        {
            EventManager.Trigger(EventName.OnCombatTriggered, _roomGuid, "room.a", RoomType.Combat);
            EventManager.Trigger(EventName.OnCombatStart, _roomGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            RaiseDamage(_playerGuid, _enemyGuid, 10);
            EventManager.Trigger(EventName.OnCombatEnd, _roomGuid, CombatOutcome.Victory);

            var second = Guid.NewGuid();
            EventManager.Trigger(EventName.OnCombatTriggered, second, "room.b", RoomType.Combat);
            EventManager.Trigger(EventName.OnCombatStart, second);
            EventManager.Trigger(EventName.OnCombatEnd, second, CombatOutcome.Victory);

            var sent = _sink.Last(AnalyticsEvents.CombatEnded);
            Assert.That(sent[AnalyticsEvents.Params.TurnCount], Is.EqualTo(0));
            Assert.That(sent[AnalyticsEvents.Params.DamageDealt], Is.EqualTo(0));
            Assert.That(sent[AnalyticsEvents.Params.TopCombos], Is.EqualTo(string.Empty));
        }

        // ====================================================================
        // run_ended — acumulación a través de combates
        // ====================================================================

        [Test]
        public void RunEnded_AccumulatesGoldCombatsAndCombosAcrossCombats()
        {
            EventManager.Trigger(EventName.OnGoldChanged, 30, 30);   // earned 30
            EventManager.Trigger(EventName.OnGoldChanged, 10, -20);  // spent 20

            EventManager.Trigger(EventName.OnCombatStart, _roomGuid);
            RaisePlayerCombo("combo.pair");
            EventManager.Trigger(EventName.OnCombatEnd, _roomGuid, CombatOutcome.Victory);

            var second = Guid.NewGuid();
            EventManager.Trigger(EventName.OnCombatStart, second);
            RaisePlayerCombo("combo.trio");
            EventManager.Trigger(EventName.OnCombatEnd, second, CombatOutcome.Victory);

            EventManager.Trigger(EventName.OnFloorCleared, _runId, 0);
            EventManager.Trigger(EventName.OnRunVictory, _runId);

            var sent = _sink.Last(AnalyticsEvents.RunEnded);
            Assert.That(sent[AnalyticsEvents.Params.CombatsWon], Is.EqualTo(2));
            Assert.That(sent[AnalyticsEvents.Params.GoldEarned], Is.EqualTo(30));
            Assert.That(sent[AnalyticsEvents.Params.GoldSpent], Is.EqualTo(20));
            Assert.That(sent[AnalyticsEvents.Params.CombosMatched], Is.EqualTo(2));
            Assert.That(sent[AnalyticsEvents.Params.FloorsCleared], Is.EqualTo(1));
        }

        // ====================================================================
        // top_combos
        // ====================================================================

        [Test]
        public void CombatEnded_TopCombos_OrderedByCountDesc()
        {
            EventManager.Trigger(EventName.OnCombatStart, _roomGuid);
            RaisePlayerCombo("combo.b");
            RaisePlayerCombo("combo.a");
            RaisePlayerCombo("combo.a");
            RaisePlayerCombo("combo.a");
            EventManager.Trigger(EventName.OnCombatEnd, _roomGuid, CombatOutcome.Victory);

            var sent = _sink.Last(AnalyticsEvents.CombatEnded);
            Assert.That(sent[AnalyticsEvents.Params.TopCombos], Is.EqualTo("combo.a:3,combo.b:1"));
        }

        [Test]
        public void BuildTopCombos_CapsAtMaxLength_DroppingWholeEntries()
        {
            var aggregator = new CombatAggregator();
            aggregator.ComboCounts["combo.short"] = 5;
            aggregator.ComboCounts[new string('x', 120)] = 9; // entra primero por count, pero no cabe

            var result = aggregator.BuildTopCombos(AnalyticsTrackerService.TopCombosMaxLength);

            Assert.That(result, Is.EqualTo("combo.short:5"));
            Assert.That(result.Length, Is.LessThanOrEqualTo(AnalyticsTrackerService.TopCombosMaxLength));
        }

        // ====================================================================
        // Boss phase en player_death
        // ====================================================================

        [Test]
        public void BossPhaseChanged_ReflectedInPlayerDeath()
        {
            EventManager.Trigger(EventName.OnCombatTriggered, _roomGuid, "room.boss", RoomType.Boss);
            EventManager.Trigger(EventName.OnCombatStart, _roomGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnBossPhaseChanged, _enemyGuid, 2);
            EventManager.Trigger(EventName.OnBossPhaseChanged, _enemyGuid, 1); // no baja

            EventManager.Trigger(EventName.OnPlayerDefeated, _runId);

            var sent = _sink.Last(AnalyticsEvents.PlayerDeath);
            Assert.That(sent[AnalyticsEvents.Params.RoomType], Is.EqualTo("Boss"));
            Assert.That(sent[AnalyticsEvents.Params.TurnCount], Is.EqualTo(1));
            Assert.That(sent[AnalyticsEvents.Params.BossPhase], Is.EqualTo(2));
        }
    }
}
