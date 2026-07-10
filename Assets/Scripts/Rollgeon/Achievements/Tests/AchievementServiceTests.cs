using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Achievements.Tests
{
    /// <summary>
    /// Tests por eventos del <see cref="AchievementService"/> (Feature#0019):
    /// mapeo config→Steam por trigger, de-dupe de sesión, y degradación sin
    /// throw cuando Steam falta o no está disponible.
    /// </summary>
    [TestFixture]
    public class AchievementServiceTests
    {
        private SteamConfigSO _config;
        private FakeSteamService _steam;
        private AchievementService _service;
        private readonly Guid _runId = Guid.NewGuid();
        private readonly Guid _roomId = Guid.NewGuid();

        private sealed class FakeSteamService : ISteamService
        {
            public bool Available { get; set; } = true;
            public string PlayerName => "TestUser";

            public readonly List<string> UnlockCalls = new List<string>();
            public readonly List<string> ClearCalls = new List<string>();
            public readonly HashSet<string> UnlockedOnSteam = new HashSet<string>();

            public bool UnlockAchievement(string apiName)
            {
                if (!Available) return false;
                UnlockCalls.Add(apiName);
                UnlockedOnSteam.Add(apiName);
                return true;
            }

            public bool ClearAchievement(string apiName)
            {
                if (!Available) return false;
                ClearCalls.Add(apiName);
                UnlockedOnSteam.Remove(apiName);
                return true;
            }

            public bool TryGetAchievementState(string apiName, out bool unlocked)
            {
                unlocked = UnlockedOnSteam.Contains(apiName);
                return Available;
            }
        }

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _config = ScriptableObject.CreateInstance<SteamConfigSO>();
            ServiceLocator.AddService<SteamConfigSO>(_config, ServiceScope.Global);

            _steam = new FakeSteamService();
            ServiceLocator.AddService<ISteamService>(_steam, ServiceScope.Global);

            _service = new AchievementService();
            _service.Register();
        }

        [TearDown]
        public void Teardown()
        {
            _service.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            Object.DestroyImmediate(_config);
        }

        private AchievementDefinition AddDef(
            string key, string apiName, AchievementTrigger trigger,
            string stringParam = null, int intParam = 0)
        {
            var def = new AchievementDefinition
            {
                Key = key,
                SteamApiName = apiName,
                Trigger = trigger,
                StringParam = stringParam,
                IntParam = intParam,
            };
            _config.Achievements.Add(def);
            return def;
        }

        // ====================================================================
        // Triggers por evento
        // ====================================================================

        [Test]
        public void RunVictory_event_unlocks_mapped_achievement()
        {
            AddDef("first_win", "ACH_WIN_FIRST_RUN", AchievementTrigger.RunVictory);

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.That(_steam.UnlockCalls, Is.EqualTo(new[] { "ACH_WIN_FIRST_RUN" }));
        }

        [Test]
        public void PlayerDefeated_event_unlocks_run_defeat_achievement()
        {
            AddDef("first_death", "ACH_FIRST_DEATH", AchievementTrigger.RunDefeat);

            EventManager.Trigger(EventName.OnPlayerDefeated, _runId);

            Assert.That(_steam.UnlockCalls, Is.EqualTo(new[] { "ACH_FIRST_DEATH" }));
        }

        [Test]
        public void Boss_combat_victory_unlocks_boss_achievement()
        {
            AddDef("first_boss", "ACH_FIRST_BOSS", AchievementTrigger.BossDefeated);

            EventManager.Trigger(EventName.OnCombatTriggered, _roomId, "boss_room", RoomType.Boss);
            EventManager.Trigger(EventName.OnCombatEnd, _roomId, CombatOutcome.Victory);

            Assert.That(_steam.UnlockCalls, Is.EqualTo(new[] { "ACH_FIRST_BOSS" }));
        }

        [Test]
        public void NonBoss_combat_victory_does_not_unlock_boss_achievement()
        {
            AddDef("first_boss", "ACH_FIRST_BOSS", AchievementTrigger.BossDefeated);

            EventManager.Trigger(EventName.OnCombatTriggered, _roomId, "normal_room", RoomType.Combat);
            EventManager.Trigger(EventName.OnCombatEnd, _roomId, CombatOutcome.Victory);

            Assert.That(_steam.UnlockCalls, Is.Empty);
        }

        [Test]
        public void Boss_combat_defeat_does_not_unlock_boss_achievement()
        {
            AddDef("first_boss", "ACH_FIRST_BOSS", AchievementTrigger.BossDefeated);

            EventManager.Trigger(EventName.OnCombatTriggered, _roomId, "boss_room", RoomType.Boss);
            EventManager.Trigger(EventName.OnCombatEnd, _roomId, CombatOutcome.Defeat);

            Assert.That(_steam.UnlockCalls, Is.Empty);
        }

        [Test]
        public void Floor_cleared_below_required_index_does_not_unlock()
        {
            AddDef("floor_two", "ACH_CLEAR_FLOOR_TWO", AchievementTrigger.FloorCleared, intParam: 1);

            EventManager.Trigger(EventName.OnFloorCleared, _runId, 0);

            Assert.That(_steam.UnlockCalls, Is.Empty);
        }

        [Test]
        public void Floor_cleared_at_required_index_unlocks()
        {
            AddDef("floor_two", "ACH_CLEAR_FLOOR_TWO", AchievementTrigger.FloorCleared, intParam: 1);

            EventManager.Trigger(EventName.OnFloorCleared, _runId, 1);

            Assert.That(_steam.UnlockCalls, Is.EqualTo(new[] { "ACH_CLEAR_FLOOR_TWO" }));
        }

        [Test]
        public void Combo_counter_reaching_threshold_unlocks_matching_combo_only()
        {
            AddDef("combo_master", "ACH_COMBO_MASTER", AchievementTrigger.ComboCounterReached,
                stringParam: "combo_a", intParam: 3);

            EventManager.Trigger(EventName.OnComboCounterIncremented, "combo_a", 2);
            EventManager.Trigger(EventName.OnComboCounterIncremented, "combo_b", 5);
            Assert.That(_steam.UnlockCalls, Is.Empty);

            EventManager.Trigger(EventName.OnComboCounterIncremented, "combo_a", 3);
            Assert.That(_steam.UnlockCalls, Is.EqualTo(new[] { "ACH_COMBO_MASTER" }));
        }

        // ====================================================================
        // Degradación sin Steam
        // ====================================================================

        [Test]
        public void No_steam_service_registered_does_not_throw()
        {
            AddDef("first_win", "ACH_WIN_FIRST_RUN", AchievementTrigger.RunVictory);
            ServiceLocator.RemoveService<ISteamService>();

            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnRunVictory, _runId));
        }

        [Test]
        public void Steam_unavailable_skips_unlock_call()
        {
            AddDef("first_win", "ACH_WIN_FIRST_RUN", AchievementTrigger.RunVictory);
            _steam.Available = false;

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.That(_steam.UnlockCalls, Is.Empty);
        }

        [Test]
        public void No_config_registered_does_not_throw()
        {
            ServiceLocator.RemoveService<SteamConfigSO>();

            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnRunVictory, _runId));
        }

        // ====================================================================
        // De-dupe y API pública
        // ====================================================================

        [Test]
        public void Duplicate_event_only_calls_unlock_once()
        {
            AddDef("first_win", "ACH_WIN_FIRST_RUN", AchievementTrigger.RunVictory);

            EventManager.Trigger(EventName.OnRunVictory, _runId);
            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.That(_steam.UnlockCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void Already_unlocked_on_steam_is_not_requested_again()
        {
            AddDef("first_win", "ACH_WIN_FIRST_RUN", AchievementTrigger.RunVictory);
            _steam.UnlockedOnSteam.Add("ACH_WIN_FIRST_RUN");

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.That(_steam.UnlockCalls, Is.Empty);
        }

        [Test]
        public void TryUnlockByKey_unknown_key_returns_false()
        {
            Assert.That(_service.TryUnlockByKey("nope"), Is.False);
            Assert.That(_steam.UnlockCalls, Is.Empty);
        }

        [Test]
        public void TryUnlockByKey_manual_definition_unlocks()
        {
            AddDef("test", "ACH_WIN_ONE_GAME", AchievementTrigger.Manual);

            Assert.That(_service.TryUnlockByKey("test"), Is.True);
            Assert.That(_steam.UnlockCalls, Is.EqualTo(new[] { "ACH_WIN_ONE_GAME" }));
        }

        [Test]
        public void TryClearByKey_allows_reunlock()
        {
            AddDef("test", "ACH_WIN_ONE_GAME", AchievementTrigger.Manual);

            Assert.That(_service.TryUnlockByKey("test"), Is.True);
            Assert.That(_service.TryClearByKey("test"), Is.True);
            Assert.That(_service.TryUnlockByKey("test"), Is.True);

            Assert.That(_steam.UnlockCalls, Has.Count.EqualTo(2));
            Assert.That(_steam.ClearCalls, Is.EqualTo(new[] { "ACH_WIN_ONE_GAME" }));
        }

        [Test]
        public void ListWithStatus_reports_unknown_state_when_steam_unavailable()
        {
            AddDef("first_win", "ACH_WIN_FIRST_RUN", AchievementTrigger.RunVictory);
            _steam.Available = false;

            var rows = _service.ListWithStatus().ToList();

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Unlocked, Is.Null);
        }
    }
}
