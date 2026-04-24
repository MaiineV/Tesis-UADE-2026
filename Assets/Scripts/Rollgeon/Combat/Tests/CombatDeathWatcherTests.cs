using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Balance;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.FSM;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    [TestFixture]
    public class CombatDeathWatcherTests
    {
        private StubPlayerService _player;
        private SpySignaller _signaller;
        private TurnOrderService _turnOrder;
        private SpyVisuals _visuals;
        private StubDungeon _dungeon;
        private CombatDeathWatcher _watcher;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();

            _player = new StubPlayerService { PlayerGuid = Guid.NewGuid() };
            _signaller = new SpySignaller();
            _turnOrder = new TurnOrderService();
            _visuals = new SpyVisuals();
            _dungeon = new StubDungeon();

            _watcher = new CombatDeathWatcher(
                _player, _signaller, _turnOrder, _visuals, _dungeon);
        }

        [TearDown]
        public void TearDown()
        {
            _watcher?.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
        }

        [Test]
        public void EnemyLethal_FiresOnEntityDestroyed()
        {
            var enemyId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();
            SetupRoom(enemyId);

            Guid? destroyedGuid = null;
            EventManager.Subscribe(EventName.OnEntityDestroyed, args =>
            {
                if (args?.Length >= 1 && args[0] is Guid g) destroyedGuid = g;
            });

            RaiseLethal(sourceId, enemyId);

            Assert.AreEqual(enemyId, destroyedGuid);
        }

        [Test]
        public void EnemyLethal_DespawnsVisual()
        {
            var enemyId = Guid.NewGuid();
            SetupRoom(enemyId);

            RaiseLethal(Guid.NewGuid(), enemyId);

            Assert.IsTrue(_visuals.DespawnedGuids.Contains(enemyId));
        }

        [Test]
        public void EnemyLethal_RemovesFromTurnOrder()
        {
            var enemyId = Guid.NewGuid();
            SetupRoomAndTurnOrder(_player.PlayerGuid, enemyId);

            RaiseLethal(_player.PlayerGuid, enemyId);

            Assert.AreEqual(1, _turnOrder.ParticipantCount);
            Assert.AreEqual(_player.PlayerGuid, _turnOrder.Current);
        }

        [Test]
        public void AllEnemiesDead_TriggersVictory()
        {
            var enemyId = Guid.NewGuid();
            SetupRoom(enemyId);

            RaiseLethal(_player.PlayerGuid, enemyId);

            Assert.AreEqual(CombatOutcome.Victory, _signaller.LastOutcome);
        }

        [Test]
        public void PlayerLethal_TriggersDefeat()
        {
            RaiseLethal(Guid.NewGuid(), _player.PlayerGuid);

            Assert.AreEqual(CombatOutcome.Defeat, _signaller.LastOutcome);
        }

        [Test]
        public void PlayerLethal_DoesNotFireOnEntityDestroyed()
        {
            bool destroyed = false;
            EventManager.Subscribe(EventName.OnEntityDestroyed, _ => destroyed = true);

            RaiseLethal(Guid.NewGuid(), _player.PlayerGuid);

            Assert.IsFalse(destroyed);
        }

        [Test]
        public void DoubleLethal_SameEntity_OnlyProcessedOnce()
        {
            var enemyId = Guid.NewGuid();
            var other = Guid.NewGuid();
            SetupRoom(enemyId, other);

            int destroyedCount = 0;
            EventManager.Subscribe(EventName.OnEntityDestroyed, _ => destroyedCount++);

            RaiseLethal(Guid.NewGuid(), enemyId);
            RaiseLethal(Guid.NewGuid(), enemyId);

            Assert.AreEqual(1, destroyedCount);
        }

        [Test]
        public void NonLethal_NoEventsTriggered()
        {
            bool destroyed = false;
            EventManager.Subscribe(EventName.OnEntityDestroyed, _ => destroyed = true);

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.NewGuid(),
                FinalDamage = 5,
                WasLethal = false,
            });

            Assert.IsFalse(destroyed);
            Assert.IsNull(_signaller.LastOutcome);
        }

        [Test]
        public void TwoEnemies_KillOne_NoVictoryYet()
        {
            var e1 = Guid.NewGuid();
            var e2 = Guid.NewGuid();
            SetupRoom(e1, e2);

            RaiseLethal(_player.PlayerGuid, e1);

            Assert.IsNull(_signaller.LastOutcome,
                "Victory should not trigger while enemies remain.");
        }

        [Test]
        public void TwoEnemies_KillBoth_TriggersVictory()
        {
            var e1 = Guid.NewGuid();
            var e2 = Guid.NewGuid();
            SetupRoom(e1, e2);

            RaiseLethal(_player.PlayerGuid, e1);
            RaiseLethal(_player.PlayerGuid, e2);

            Assert.AreEqual(CombatOutcome.Victory, _signaller.LastOutcome);
        }

        // ---- Helpers ----

        private void RaiseLethal(Guid source, Guid target)
        {
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = source,
                TargetGuid = target,
                FinalDamage = 99,
                WasLethal = true,
            });
        }

        private void SetupRoom(params Guid[] enemies)
        {
            var room = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                State = RoomState.Uncleared,
            };
            foreach (var e in enemies) room.SpawnedEnemies.Add(e);
            _dungeon.Room = room;

            // Simulate DungeonManager.OnEntityDestroyed bookkeeping: remove
            // destroyed enemies from SpawnedEnemies so the watcher's "all dead"
            // check works correctly (EventManager.Trigger is synchronous).
            EventManager.Subscribe(EventName.OnEntityDestroyed, args =>
            {
                if (args?.Length >= 1 && args[0] is Guid g)
                    room.SpawnedEnemies.Remove(g);
            });
        }

        private void SetupRoomAndTurnOrder(Guid playerId, params Guid[] enemies)
        {
            SetupRoom(enemies);

            var registry = new InMemoryEntityRegistry();
            var ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            ruleset.TurnOrder = new TurnOrderConfig
            {
                SpeedDieMin = 1, SpeedDieMax = 1,
                FallbackInitiativeForMissingSpeed = 0,
            };
            ServiceLocator.AddService<RulesetSO>(ruleset);

            var allParticipants = new List<Guid> { playerId };
            foreach (var e in enemies) allParticipants.Add(e);

            foreach (var g in allParticipants)
            {
                var attrs = new Rollgeon.Attributes.ModifiableAttributes();
                attrs.SetAttribute<Rollgeon.Attributes.Stats.Speed>(
                    new Rollgeon.Attributes.Stats.Speed(5));
                registry.Register(g, attrs);
            }

            var rng = new FixedInitiativeRng(fallback: 1, values: new int[allParticipants.Count]);
            var provider = new DefaultInitiativeProvider(registry, rng, ruleset);
            ServiceLocator.AddService<IInitiativeProvider>(provider);

            _turnOrder.BuildForCombat(allParticipants);
        }

        // ---- Stubs / Spies ----

        private class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { }
            public void ClearPlayer() { }
#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }

        private class SpySignaller : ICombatSignaller
        {
            public CombatOutcome? LastOutcome { get; private set; }
            public void SignalEnemyDone() { }
            public void NotifyCombatEnded(CombatOutcome outcome) => LastOutcome = outcome;
        }

        private class SpyVisuals : IEntityVisualService
        {
            public HashSet<Guid> DespawnedGuids { get; } = new();
            public EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord) => null;
            public EntityPawn SpawnEnemy(Guid guid, EnemyDataSO data, GridCoord coord) => null;
            public void Despawn(Guid guid) => DespawnedGuids.Add(guid);
            public void DespawnAll() { }
            public bool TryGetPawn(Guid guid, out EntityPawn pawn) { pawn = null; return false; }
            public Vector3? TryGetWorldPosition(Guid entityId) => null;
        }

        private class StubDungeon : IDungeonService
        {
            public RoomInstance Room { get; set; }
            public RoomSO CurrentRoom => Room?.Template;
            public RoomInstance CurrentRoomInstance => Room;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId) { neighborInstanceId = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection direction) => false;
            public bool EnterRoomByInstanceId(Guid instanceId) => false;
            public UnityEngine.Bounds GetFloorBounds() => default;
            public System.Collections.Generic.IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() => Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }
    }
}
