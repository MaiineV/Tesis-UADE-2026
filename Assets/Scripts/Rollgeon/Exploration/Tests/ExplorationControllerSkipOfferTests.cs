using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Phase;
using UnityEngine;

namespace Rollgeon.Exploration.Tests
{
    /// <summary>
    /// Seam <see cref="ICombatSkipOffer"/> (Peaje): una sala Combat consulta la oferta antes
    /// de disparar el combate; Boss nunca. El callback <c>fight</c> arranca el combate normal.
    /// </summary>
    [TestFixture]
    public class ExplorationControllerSkipOfferTests
    {
        private StubDungeonService _dungeon;
        private StubPhaseService _phase;
        private ExplorationController _controller;
        private FakeOffer _offer;
        private readonly List<UnityEngine.Object> _created = new();
        private int _combatTriggered;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _dungeon = new StubDungeonService();
            _phase = new StubPhaseService();
            _offer = new FakeOffer();
            ServiceLocator.AddService<ICombatSkipOffer>(_offer, ServiceScope.Global);
            _controller = new ExplorationController(_dungeon, _phase);
            _combatTriggered = 0;
            EventManager.Subscribe(EventName.OnCombatTriggered, _ => _combatTriggered++);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private RoomInstance Room(RoomType type)
        {
            var so = ScriptableObject.CreateInstance<RoomSO>();
            so.RoomId = type.ToString();
            so.Type = type;
            _created.Add(so);
            var instance = new RoomInstance { InstanceId = Guid.NewGuid(), Template = so, State = RoomState.Uncleared };
            _dungeon.CurrentRoomInstance = instance;
            return instance;
        }

        [Test]
        public void CombatRoom_WithAcceptedOffer_DoesNotStartCombat()
        {
            _offer.Accepts = true;
            Room(RoomType.Combat);

            _controller.BeginExploration();

            Assert.AreEqual(1, _offer.Offered.Count);
            Assert.AreEqual(0, _combatTriggered);
            Assert.IsTrue(_controller.IsExploring);
            Assert.IsFalse(_phase.ReplacePhaseCalls.Contains(GamePhase.Combat));
        }

        [Test]
        public void Fight_StartsTheNormalCombat()
        {
            _offer.Accepts = true;
            Room(RoomType.Combat);
            _controller.BeginExploration();

            _offer.LastFight();

            Assert.AreEqual(1, _combatTriggered);
            Assert.IsFalse(_controller.IsExploring);
            Assert.IsTrue(_phase.ReplacePhaseCalls.Contains(GamePhase.Combat));
        }

        [Test]
        public void Fight_AfterTheRoomWasCleared_IsANoOp()
        {
            _offer.Accepts = true;
            var room = Room(RoomType.Combat);
            _controller.BeginExploration();

            room.State = RoomState.Cleared;
            _offer.LastFight();

            Assert.AreEqual(0, _combatTriggered);
            Assert.IsTrue(_controller.IsExploring);
        }

        [Test]
        public void CombatRoom_WithDeclinedOffer_StartsCombatImmediately()
        {
            _offer.Accepts = false;
            Room(RoomType.Combat);

            _controller.BeginExploration();

            Assert.AreEqual(1, _offer.Offered.Count);
            Assert.AreEqual(1, _combatTriggered);
        }

        [Test]
        public void BossRoom_NeverConsultsTheOffer()
        {
            _offer.Accepts = true;
            Room(RoomType.Boss);

            _controller.BeginExploration();

            Assert.AreEqual(0, _offer.Offered.Count);
            Assert.AreEqual(1, _combatTriggered);
        }

        // ------------------------------------------------------------ stubs

        private sealed class FakeOffer : ICombatSkipOffer
        {
            public bool Accepts;
            public readonly List<Guid> Offered = new();
            public Action LastFight;

            public bool TryOffer(RoomInstance instance, Action fight)
            {
                Offered.Add(instance.InstanceId);
                LastFight = fight;
                return Accepts;
            }
        }

        private sealed class StubDungeonService : IDungeonService
        {
            public RoomInstance CurrentRoomInstance { get; set; }
            public RoomSO CurrentRoom => CurrentRoomInstance?.Template;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id) { id = Guid.Empty; return false; }
            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public bool SetRoomState(Guid id, RoomState state) => false;
            public void ResyncDoorVisuals(Guid id) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders()
                => Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private sealed class StubPhaseService : IPhaseService
        {
            public GamePhase CurrentBase { get; private set; }
            public PhaseOverlay CurrentOverlay { get; private set; }
            public List<GamePhase> ReplacePhaseCalls { get; } = new();
            public void ReplacePhase(GamePhase next) { CurrentBase = next; ReplacePhaseCalls.Add(next); }
            public void PushOverlay(PhaseOverlay overlay) => CurrentOverlay = overlay;
            public void PopOverlay() => CurrentOverlay = PhaseOverlay.None;
        }
    }
}
