using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Combos.Counters;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Heroes;
using Rollgeon.Meta.Conditions;
using Rollgeon.Player;
using Rollgeon.Run;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Meta.Tests
{
    /// <summary>
    /// Tests de integración por eventos del <see cref="UnlockProgressService"/>
    /// (#164): captura de build/clase al run start, tracking de combates
    /// (flawless / huida / boss), unlock mid-run inmediato para
    /// <see cref="UnlockOutcomeFilter.Any"/>, evaluación de cierre por outcome e
    /// invalidación de condiciones de consistencia.
    /// </summary>
    [TestFixture]
    public class UnlockProgressServiceTests
    {
        private MetaProgressionService _meta;
        private InMemoryMetaSaveStore _store;
        private UnlockCatalogSO _catalog;
        private UnlockProgressService _progress;
        private RunComboCounterState _comboCounters;
        private FakePlayerService _player;
        private FakeRunContext _runContext;
        private ClassHeroSO _hero;
        private DiceBagSO _bag;
        private readonly List<Object> _assets = new List<Object>();
        private readonly Guid _playerGuid = Guid.NewGuid();
        private readonly Guid _runId = Guid.NewGuid();

        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { CurrentHero = hero; RunId = runId; }
            public void SetDiceBag(DiceBagSO bag) => DiceBag = bag;
            public void ClearPlayer() { CurrentHero = null; DiceBag = null; }
#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }

        private sealed class FakeRunContext : IRunContextService
        {
            public Guid RunId { get; set; }
            public int FloorIndex { get; set; }
            public ClassHeroSO SelectedHero { get; set; }
            public bool IsRunActive { get; set; } = true;
            public void AdvanceFloor() => FloorIndex++;
        }

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _store = new InMemoryMetaSaveStore();
            _catalog = ScriptableObject.CreateInstance<UnlockCatalogSO>();
            _assets.Add(_catalog);

            _meta = new MetaProgressionService();
            _meta.ConfigureForTests(_store, _catalog);
            ServiceLocator.AddService<IMetaProgressionService>(_meta, ServiceScope.Global);

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.EntityId = "Warrior";
            _assets.Add(_hero);

            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.Dice = new List<DiceType>
                { DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6 };
            _assets.Add(_bag);

            _player = new FakePlayerService { PlayerGuid = _playerGuid, DiceBag = _bag, CurrentHero = _hero };
            _runContext = new FakeRunContext { RunId = _runId, SelectedHero = _hero };
            ServiceLocator.AddService<IPlayerService>(_player, ServiceScope.Run);
            ServiceLocator.AddService<IRunContextService>(_runContext, ServiceScope.Run);

            _comboCounters = new RunComboCounterState();
            ServiceLocator.AddService<RunComboCounterState>(_comboCounters, ServiceScope.Run);

            _progress = new UnlockProgressService();
            _progress.Register();
        }

        [TearDown]
        public void Teardown()
        {
            _progress.Dispose();
            TypedEvent<UnlockAchievedPayload>.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private UnlockDefinitionSO AddDefinition(
            string unlockId, UnlockableCategory category, string targetId,
            IUnlockCondition condition, UnlockOutcomeFilter appliesTo)
        {
            var def = ScriptableObject.CreateInstance<UnlockDefinitionSO>();
            def.UnlockId = unlockId;
            def.Category = category;
            def.TargetId = targetId;
            def.DisplayName = unlockId;
            def.AppliesTo = appliesTo;
            def.Condition = condition;
            _assets.Add(def);

            _catalog.AddEntry(def);
            _meta.ConfigureForTests(_store, _catalog); // invalida cache de Definitions
            return def;
        }

        private void StartRun() => EventManager.Trigger(EventName.OnRunStart, _runId, "default");

        private RunUnlockState State =>
            ServiceLocator.TryGetService<RunUnlockState>(out var s) ? s : null;

        // ── Captura al run start ────────────────────────────────

        [Test]
        public void OnRunStart_CapturesClassAndDiceBuild()
        {
            StartRun();

            var state = State;
            Assert.IsNotNull(state);
            Assert.AreEqual("Warrior", state.ClassId);
            Assert.AreEqual(5, state.DiceBuild.Count);
            CollectionAssert.AreEqual(
                new[] { DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6 },
                state.DiceBuild);
        }

        // ── Tracking de combate ─────────────────────────────────

        [Test]
        public void CombatVictoryWithoutDamage_CountsAsFlawless()
        {
            StartRun();
            var roomId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnCombatStart, roomId);
            EventManager.Trigger(EventName.OnCombatEnd, roomId, CombatOutcome.Victory);

            Assert.AreEqual(1, State.FlawlessCombats);
        }

        [Test]
        public void CombatVictoryWithPlayerDamage_NotFlawless()
        {
            StartRun();
            var roomId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnCombatStart, roomId);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                TargetGuid = _playerGuid,
                FinalDamage = 3,
            });
            EventManager.Trigger(EventName.OnCombatEnd, roomId, CombatOutcome.Victory);

            Assert.AreEqual(0, State.FlawlessCombats);
        }

        [Test]
        public void CombatVictoryWithEnemyDamageOnly_StillFlawless()
        {
            StartRun();
            var roomId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnCombatStart, roomId);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                TargetGuid = Guid.NewGuid(), // daño a un enemigo, no al player
                FinalDamage = 7,
            });
            EventManager.Trigger(EventName.OnCombatEnd, roomId, CombatOutcome.Victory);

            Assert.AreEqual(1, State.FlawlessCombats);
        }

        [Test]
        public void CombatAborted_CountsAsFled()
        {
            StartRun();
            var roomId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnCombatStart, roomId);
            EventManager.Trigger(EventName.OnCombatEnd, roomId, CombatOutcome.Aborted);

            Assert.AreEqual(1, State.CombatsFled);
        }

        [Test]
        public void BossRoomVictory_CountsBossDefeated()
        {
            StartRun();
            var roomId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnCombatTriggered, roomId, "Boss01", RoomType.Boss);
            EventManager.Trigger(EventName.OnCombatStart, roomId);
            EventManager.Trigger(EventName.OnCombatEnd, roomId, CombatOutcome.Victory);

            Assert.AreEqual(1, State.BossesDefeated);
        }

        [Test]
        public void FloorChanged_TracksFloorsVisited()
        {
            StartRun();

            EventManager.Trigger(EventName.OnFloorChanged, _runId, 1);
            EventManager.Trigger(EventName.OnFloorChanged, _runId, 2);

            Assert.AreEqual(3, State.FloorsVisited);
        }

        // ── Unlock mid-run ──────────────────────────────────────

        [Test]
        public void MidRun_AnyOutcomeConditionMet_UnlocksImmediately()
        {
            var def = AddDefinition(
                "unlock.room.casino", UnlockableCategory.SpecialRoom, "Casino01",
                new ComboExecutedTimesCondition { ComboId = "combo.par", Times = 2 },
                UnlockOutcomeFilter.Any);
            StartRun();

            _comboCounters.Increment("combo.par");
            EventManager.Trigger(EventName.OnComboCounterIncremented, "combo.par", 1);
            Assert.IsFalse(_meta.IsDefinitionCompleted(def), "Con 1 ejecución todavía no debe desbloquear");

            _comboCounters.Increment("combo.par");
            EventManager.Trigger(EventName.OnComboCounterIncremented, "combo.par", 2);

            Assert.IsTrue(_meta.IsDefinitionCompleted(def), "El unlock mid-run no espera el fin de la run");
            CollectionAssert.Contains(_progress.UnlocksThisRun, def);
        }

        [Test]
        public void MidRun_WonOnlyCondition_DoesNotUnlockBeforeRunEnd()
        {
            var def = AddDefinition(
                "unlock.dice.d8", UnlockableCategory.Dice, "D8",
                new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 5 },
                UnlockOutcomeFilter.Won);
            StartRun();

            EventManager.Trigger(EventName.OnComboCounterIncremented, "combo.par", 1);

            Assert.IsFalse(_meta.IsDefinitionCompleted(def), "Exige ganar — no puede resolverse mid-run");
        }

        // ── Evaluación al cierre ────────────────────────────────

        [Test]
        public void RunVictory_WonCondition_Unlocks()
        {
            // Árbol base: D8 se desbloquea ganando con 5×D6.
            var def = AddDefinition(
                "unlock.dice.d8", UnlockableCategory.Dice, "D8",
                new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 5 },
                UnlockOutcomeFilter.Won);
            StartRun();

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.IsTrue(_meta.IsDefinitionCompleted(def));
            Assert.IsTrue(_meta.IsAvailable(UnlockableCategory.Dice, "D8"));
            CollectionAssert.Contains(_progress.UnlocksThisRun, def);
        }

        [Test]
        public void RunDefeat_WonCondition_DoesNotUnlock()
        {
            var def = AddDefinition(
                "unlock.dice.d8", UnlockableCategory.Dice, "D8",
                new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 5 },
                UnlockOutcomeFilter.Won);
            StartRun();

            EventManager.Trigger(EventName.OnPlayerDefeated, _runId);

            Assert.IsFalse(_meta.IsDefinitionCompleted(def));
        }

        [Test]
        public void RunVictory_UpdatesPersistentCountersBeforeEvaluating()
        {
            // ConsecutiveWins == 1 debe verse en la MISMA run que completa la racha.
            var def = AddDefinition(
                "unlock.class.berserker", UnlockableCategory.HeroClass, "Berserker",
                new ConsecutiveWinsCondition { Wins = 1 },
                UnlockOutcomeFilter.Won);
            StartRun();

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.AreEqual(1, _meta.ConsecutiveWins);
            Assert.IsTrue(_meta.IsDefinitionCompleted(def));
        }

        [Test]
        public void RunDefeat_ResetsStreak_ButAccumulatesClass()
        {
            StartRun();

            EventManager.Trigger(EventName.OnPlayerDefeated, _runId);

            Assert.AreEqual(0, _meta.ConsecutiveWins);
            CollectionAssert.Contains(_meta.ClassesPlayed, "Warrior");
        }

        [Test]
        public void RunVictory_SecondTrigger_DoesNotDoubleFinalize()
        {
            StartRun();

            EventManager.Trigger(EventName.OnRunVictory, _runId);
            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.AreEqual(1, _meta.ConsecutiveWins, "Finalize debe ser idempotente por run");
        }

        // ── Invalidación de consistencia ────────────────────────

        [Test]
        public void ConsistencyCondition_BrokenMidRun_DoesNotUnlockAtVictory()
        {
            var def = AddDefinition(
                "unlock.shop.elixir", UnlockableCategory.ShopItem, "item.elixir",
                new NoPotionUsedCondition(),
                UnlockOutcomeFilter.Won);
            StartRun();

            // El jugador usa una poción — la condición se invalida en ese momento.
            EventManager.Trigger(EventName.OnActiveItemUsed, _playerGuid, "item.healing_potion");
            CollectionAssert.Contains(State.InvalidatedUnlockIds, def.UnlockId);

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.IsFalse(_meta.IsDefinitionCompleted(def));
        }

        [Test]
        public void ConsistencyCondition_KeptCleanAllRun_UnlocksAtVictory()
        {
            var def = AddDefinition(
                "unlock.shop.elixir", UnlockableCategory.ShopItem, "item.elixir",
                new NoPotionUsedCondition(),
                UnlockOutcomeFilter.Won);
            StartRun();

            EventManager.Trigger(EventName.OnRunVictory, _runId);

            Assert.IsTrue(_meta.IsDefinitionCompleted(def));
        }

        // ── Resultados de run ───────────────────────────────────

        [Test]
        public void UnlocksThisRun_ClearedOnNextRunStart()
        {
            var def = AddDefinition(
                "unlock.dice.d8", UnlockableCategory.Dice, "D8",
                new DiceCountOfTypeCondition { Type = DiceType.D6, Count = 5 },
                UnlockOutcomeFilter.Won);
            StartRun();
            EventManager.Trigger(EventName.OnRunVictory, _runId);
            Assert.AreEqual(1, _progress.UnlocksThisRun.Count);

            StartRun();

            Assert.AreEqual(0, _progress.UnlocksThisRun.Count);
        }

        // ── Round-trip del state run-scoped ─────────────────────

        [Test]
        public void RunUnlockState_SaveRoundTrip_PreservesProgress()
        {
            StartRun();
            var state = State;
            state.FlawlessCombats = 2;
            state.CombatsFled = 1;
            state.BossesDefeated = 1;
            state.FloorsVisited = 2;
            state.UsedActiveItemIds.Add("item.healing_potion");
            state.InvalidatedUnlockIds.Add("unlock.shop.elixir");

            var restored = new RunUnlockState();
            restored.RestoreState(state.CaptureState());

            Assert.AreEqual("Warrior", restored.ClassId);
            CollectionAssert.AreEqual(state.DiceBuild, restored.DiceBuild);
            Assert.AreEqual(2, restored.FlawlessCombats);
            Assert.AreEqual(1, restored.CombatsFled);
            Assert.AreEqual(1, restored.BossesDefeated);
            Assert.AreEqual(2, restored.FloorsVisited);
            CollectionAssert.Contains(restored.UsedActiveItemIds, "item.healing_potion");
            CollectionAssert.Contains(restored.InvalidatedUnlockIds, "unlock.shop.elixir");
        }
    }
}
