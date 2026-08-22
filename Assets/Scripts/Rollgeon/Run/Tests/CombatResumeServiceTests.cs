using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Combat.Resume;
using Rollgeon.Combat.Threat;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Run.Tests
{
    /// <summary>
    /// Restauración del turno de combate exacto (Feature#0028 Fase 3):
    /// <see cref="CombatResumeService"/> como <c>ISaveable</c> + <c>ICombatResumeCoordinator</c>.
    /// </summary>
    [TestFixture]
    public class CombatResumeServiceTests
    {
        private FakePhase _phase;
        private FakeDungeon _dungeon;
        private TurnOrderService _turnOrder;
        private CombatResumeService _svc;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _phase = new FakePhase();
            _dungeon = new FakeDungeon();
            _turnOrder = new TurnOrderService();
            _svc = new CombatResumeService();

            ServiceLocator.AddService<IPhaseService>(_phase);
            ServiceLocator.AddService<IDungeonService>(_dungeon);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);
        }

        [TearDown]
        public void TearDown()
        {
            _svc.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void SetRoomCell(Vector2Int cell) =>
            _dungeon.Current = new RoomInstance { InstanceId = Guid.NewGuid(), GridCell = cell };

        // ================================================================
        // CaptureState
        // ================================================================

        [Test]
        public void CaptureState_NotInCombat_ReturnsInactiveSentinel()
        {
            _phase.CurrentBase = GamePhase.Exploration;

            var snap = (CombatResumeSnapshot)_svc.CaptureState();

            Assert.IsFalse(snap.Active);
        }

        [Test]
        public void CaptureState_InCombat_CapturesOrderCursorRoundAndCell()
        {
            _phase.CurrentBase = GamePhase.Combat;
            SetRoomCell(new Vector2Int(1, 2));
            var player = Guid.NewGuid();
            var enemy = Guid.NewGuid();
            _turnOrder.RestoreState(new[] { player, enemy }, cursor: 1, roundIndex: 4);

            var snap = (CombatResumeSnapshot)_svc.CaptureState();

            Assert.IsTrue(snap.Active);
            Assert.AreEqual(new Vector2Int(1, 2), snap.CurrentCell);
            Assert.AreEqual(new[] { player.ToString(), enemy.ToString() }, snap.Order.ToArray());
            Assert.AreEqual(1, snap.Cursor);
            Assert.AreEqual(4, snap.RoundIndex);
            Assert.AreEqual(enemy.ToString(), snap.ActiveEntityId);
        }

        // ================================================================
        // Odin round-trip
        // ================================================================

        [Test]
        public void Snapshot_RoundTripsThroughOdin()
        {
            var src = new CombatResumeSnapshot
            {
                Active = true,
                CurrentCell = new Vector2Int(-1, 3),
                Order = new List<string> { "a", "b" },
                Cursor = 1,
                RoundIndex = 2,
                ActiveEntityId = "b",
                PlayerRolls = 5,
            };

            byte[] bytes = SerializationUtility.SerializeValue(src, DataFormat.JSON);
            var restored = SerializationUtility.DeserializeValue<CombatResumeSnapshot>(bytes, DataFormat.JSON);

            Assert.IsTrue(restored.Active);
            Assert.AreEqual(new Vector2Int(-1, 3), restored.CurrentCell);
            Assert.AreEqual(new[] { "a", "b" }, restored.Order.ToArray());
            Assert.AreEqual(1, restored.Cursor);
            Assert.AreEqual(2, restored.RoundIndex);
            Assert.AreEqual(5, restored.PlayerRolls);
        }

        // ================================================================
        // TryBeginResume
        // ================================================================

        private void Stage(CombatResumeSnapshot snap) => _svc.RestoreState(snap);

        private CombatResumeSnapshot ActiveSnapshot(Vector2Int cell, Guid[] order, int cursor, int round)
        {
            var s = new CombatResumeSnapshot
            {
                Active = true, CurrentCell = cell, Cursor = cursor, RoundIndex = round,
                ActiveEntityId = order[cursor].ToString(),
            };
            foreach (var g in order) s.Order.Add(g.ToString());
            return s;
        }

        [Test]
        public void TryBeginResume_NoPending_ReturnsFalse()
        {
            Assert.IsFalse(_svc.TryBeginResume(_turnOrder, new List<Guid> { Guid.NewGuid() }, Guid.NewGuid()));
        }

        [Test]
        public void TryBeginResume_InactiveSnapshot_ReturnsFalse()
        {
            Stage(new CombatResumeSnapshot { Active = false });
            Assert.IsFalse(_svc.TryBeginResume(_turnOrder, new List<Guid> { Guid.NewGuid() }, Guid.NewGuid()));
        }

        [Test]
        public void TryBeginResume_CellMismatch_ReturnsFalse()
        {
            SetRoomCell(new Vector2Int(0, 0));
            var player = Guid.NewGuid();
            Stage(ActiveSnapshot(new Vector2Int(9, 9), new[] { player }, 0, 0));

            Assert.IsFalse(_svc.TryBeginResume(_turnOrder, new List<Guid> { player }, player));
        }

        [Test]
        public void TryBeginResume_CellMatch_RestoresExactTurnOrder()
        {
            SetRoomCell(new Vector2Int(2, 2));
            var player = Guid.NewGuid();
            var enemy = Guid.NewGuid();
            Stage(ActiveSnapshot(new Vector2Int(2, 2), new[] { player, enemy }, cursor: 1, round: 3));

            bool ok = _svc.TryBeginResume(_turnOrder, new List<Guid> { player, enemy }, player);

            Assert.IsTrue(ok);
            Assert.AreEqual(new[] { player, enemy }, new List<Guid>(_turnOrder.OrderForRound).ToArray());
            Assert.AreEqual(enemy, _turnOrder.Current, "cursor 1 restaurado");
            Assert.AreEqual(3, _turnOrder.RoundIndex);
        }

        [Test]
        public void TryBeginResume_DeadParticipant_FilteredOut_CursorClampedToSurvivor()
        {
            SetRoomCell(new Vector2Int(2, 2));
            var player = Guid.NewGuid();
            var deadEnemy = Guid.NewGuid();
            // Guardado: turno del enemigo muerto (cursor 1). Solo el player respawnea.
            Stage(ActiveSnapshot(new Vector2Int(2, 2), new[] { player, deadEnemy }, cursor: 1, round: 0));

            bool ok = _svc.TryBeginResume(_turnOrder, new List<Guid> { player }, player);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, _turnOrder.ParticipantCount, "el muerto se filtra de la cola");
            Assert.AreEqual(player, _turnOrder.Current, "cursor cae al sobreviviente");
        }

        [Test]
        public void TryBeginResume_IsOneShot_SecondCallReturnsFalse()
        {
            SetRoomCell(new Vector2Int(0, 0));
            var player = Guid.NewGuid();
            Stage(ActiveSnapshot(new Vector2Int(0, 0), new[] { player }, 0, 0));

            Assert.IsTrue(_svc.TryBeginResume(_turnOrder, new List<Guid> { player }, player));
            Assert.IsFalse(_svc.TryBeginResume(_turnOrder, new List<Guid> { player }, player),
                "el snapshot se consume en el primer intento");
        }

        [Test]
        public void TryBeginResume_RemapsSavedPlayerGuidToLivePlayer()
        {
            // Repro del bug reportado: el player guardado tiene un GUID que NO coincide con el
            // vivo (la preservación cross-sesión falló). El resume debe re-mapearlo, no filtrarlo
            // — si no, la cola queda solo con el enemigo y arranca turno enemigo (insta-kill).
            SetRoomCell(new Vector2Int(0, 0));
            var savedPlayer = Guid.NewGuid();
            var enemy = Guid.NewGuid();
            var livePlayer = Guid.NewGuid(); // distinto del guardado

            var snap = new CombatResumeSnapshot
            {
                Active = true,
                CurrentCell = new Vector2Int(0, 0),
                PlayerGuid = savedPlayer.ToString(),
                ActiveEntityId = savedPlayer.ToString(), // era el turno del player al guardar
                RoundIndex = 1,
            };
            snap.Order.Add(savedPlayer.ToString());
            snap.Order.Add(enemy.ToString());
            Stage(snap);

            // Vivos: player NUEVO (guid distinto) + enemigo con su guid preservado.
            bool ok = _svc.TryBeginResume(_turnOrder, new List<Guid> { livePlayer, enemy }, livePlayer);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, _turnOrder.ParticipantCount, "el player re-mapeado NO se filtra de la cola");
            Assert.AreEqual(livePlayer, _turnOrder.Current,
                "es el turno del player vivo, no del enemigo");
        }

        // ================================================================
        // Fase 4 — buffs/shields
        // ================================================================

        [Test]
        public void CaptureAndRestore_Shield_RoundTrips()
        {
            _phase.CurrentBase = GamePhase.Combat;
            SetRoomCell(new Vector2Int(0, 0));

            var attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(attrs);
            var guid = Guid.NewGuid();
            var ma = new ModifiableAttributes();
            ma.EnsureInitialized();
            ma.SetAttribute<Shield>(new Shield(0));
            attrs.Register(guid, ma);
            attrs.SetAttributeValue<Shield, int>(guid, 6);

            _turnOrder.RestoreState(new[] { guid }, 0, 0);

            var snap = (CombatResumeSnapshot)_svc.CaptureState();
            Assert.AreEqual(1, snap.Buffs.Count);
            Assert.IsTrue(snap.Buffs[0].HasShield);
            Assert.AreEqual(6, snap.Buffs[0].Shield);

            // Reset y restaurar.
            attrs.SetAttributeValue<Shield, int>(guid, 0);
            Stage(snap);
            Assert.IsTrue(_svc.TryBeginResume(_turnOrder, new List<Guid> { guid }, guid));
            Assert.AreEqual(6, attrs.GetAttribute<Shield>(guid).Value, "shield restaurado");
        }

        [Test]
        public void TryBeginResume_SkipsBuffsForUnregisteredEntity()
        {
            _phase.CurrentBase = GamePhase.Combat;
            SetRoomCell(new Vector2Int(0, 0));
            var attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(attrs);
            var deadGuid = Guid.NewGuid(); // nunca registrado (murió / no respawneó)

            var player = Guid.NewGuid();
            var snap = ActiveSnapshot(new Vector2Int(0, 0), new[] { player }, 0, 0);
            snap.Buffs.Add(new EntityBuffBlock { EntityId = deadGuid.ToString(), HasShield = true, Shield = 9 });
            Stage(snap);

            // No debe tirar aunque el buff apunte a una entidad no registrada.
            Assert.DoesNotThrow(() => _svc.TryBeginResume(_turnOrder, new List<Guid> { player }, player));
        }

        // ================================================================
        // Fase 4 — estado de boss
        // ================================================================

        [Test]
        public void CaptureAndRestore_BossState_RoundTrips()
        {
            _phase.CurrentBase = GamePhase.Combat;
            SetRoomCell(new Vector2Int(0, 0));
            var boss = Guid.NewGuid();
            _turnOrder.RestoreState(new[] { boss }, 0, 0);

            var comboLog = new ComboLogService();
            var diceBlock = new DiceBlockService();
            var threat = new ThreatenedAreaService();
            ServiceLocator.AddService<IComboLogService>(comboLog);
            ServiceLocator.AddService<IDiceBlockService>(diceBlock);
            ServiceLocator.AddService<IThreatenedAreaService>(threat);

            comboLog.Record("combo.trio");
            comboLog.Record("combo.poker"); // History: [poker, trio]
            diceBlock.Block(2);
            diceBlock.Block(4);
            threat.Mark(boss, new[] { new GridCoord(1, 1), new GridCoord(1, 2) }, 7, default);

            var snap = (CombatResumeSnapshot)_svc.CaptureState();
            Assert.AreEqual(new[] { "combo.poker", "combo.trio" }, snap.ComboHistory.ToArray());
            CollectionAssert.AreEquivalent(new[] { 2, 4 }, snap.BlockedDice);
            Assert.AreEqual(1, snap.Telegraphs.Count);
            Assert.AreEqual(boss.ToString(), snap.Telegraphs[0].SourceId);
            Assert.AreEqual(7, snap.Telegraphs[0].Damage);
            Assert.AreEqual(2, snap.Telegraphs[0].Tiles.Count);

            // Vaciar servicios y restaurar vía TryBeginResume.
            comboLog.Clear();
            diceBlock.Clear();
            threat.ClearAll();
            Stage(snap);
            Assert.IsTrue(_svc.TryBeginResume(_turnOrder, new List<Guid> { boss }, boss));

            Assert.AreEqual(new[] { "combo.poker", "combo.trio" }, comboLog.Last(2).ToArray());
            Assert.IsTrue(diceBlock.IsBlocked(2) && diceBlock.IsBlocked(4));
            Assert.IsTrue(threat.HasPending(boss));
            Assert.AreEqual(2, threat.GetPendingTiles(boss).Count);
        }

        [Test]
        public void Snapshot_WithBuffsAndBossState_RoundTripsThroughOdin()
        {
            var src = new CombatResumeSnapshot
            {
                Active = true,
                CurrentCell = new Vector2Int(0, 0),
                Order = new List<string> { "x" },
                ActiveEntityId = "x",
            };
            src.Buffs.Add(new EntityBuffBlock { EntityId = "x", HasShield = true, Shield = 3 });
            src.ComboHistory.Add("combo.poker");
            src.BlockedDice.Add(5);
            src.Telegraphs.Add(new TelegraphEntry
            {
                SourceId = "x",
                Damage = 4,
                Tiles = new List<GridCoord> { new GridCoord(2, 2) },
            });

            byte[] bytes = SerializationUtility.SerializeValue(src, DataFormat.JSON);
            var r = SerializationUtility.DeserializeValue<CombatResumeSnapshot>(bytes, DataFormat.JSON);

            Assert.AreEqual(1, r.Buffs.Count);
            Assert.AreEqual(3, r.Buffs[0].Shield);
            Assert.AreEqual(new[] { "combo.poker" }, r.ComboHistory.ToArray());
            Assert.AreEqual(new[] { 5 }, r.BlockedDice.ToArray());
            Assert.AreEqual(1, r.Telegraphs.Count);
            Assert.AreEqual(new GridCoord(2, 2), r.Telegraphs[0].Tiles[0]);
        }

        // ================================================================
        // Preservación al limpiar el player (teardown de EndRun) — #0028
        // ================================================================

        [Test]
        public void CaptureState_AfterPlayerCleared_PreservesLastActiveSnapshot()
        {
            _phase.CurrentBase = GamePhase.Combat;
            SetRoomCell(new Vector2Int(1, 1));
            var player = Guid.NewGuid();
            var enemy = Guid.NewGuid();
            _turnOrder.RestoreState(new[] { player, enemy }, 0, 3);

            var fakePlayer = new FakePlayerService { PlayerGuid = player };
            ServiceLocator.AddService<IPlayerService>(fakePlayer);

            // Capture 1: player válido → snapshot activo cacheado.
            var snap1 = (CombatResumeSnapshot)_svc.CaptureState();
            Assert.IsTrue(snap1.Active);
            Assert.AreEqual(player.ToString(), snap1.PlayerGuid);

            // Player limpiado (EndRun corre ClearPlayer antes del capture de OnRunEnd):
            // el capture NO debe descartar el combate — debe preservar el último válido.
            fakePlayer.PlayerGuid = Guid.Empty;
            var snap2 = (CombatResumeSnapshot)_svc.CaptureState();

            Assert.IsTrue(snap2.Active, "no debe perder el combate por el player limpiado");
            Assert.AreEqual(3, snap2.RoundIndex);
            Assert.AreEqual(2, snap2.Order.Count);
            Assert.AreEqual(player.ToString(), snap2.PlayerGuid, "preserva el guid del player");
        }

        // ================================================================
        // Fakes
        // ================================================================

        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public Rollgeon.Heroes.ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
#pragma warning disable CS0067 // eventos requeridos por la interfaz, no usados en el fake
            public event Action<Rollgeon.Heroes.ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
            public void SetPlayer(Rollgeon.Heroes.ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { }
            public void ClearPlayer() => PlayerGuid = Guid.Empty;
        }

        private sealed class FakePhase : IPhaseService
        {
            public GamePhase CurrentBase { get; set; } = GamePhase.Combat;
            public PhaseOverlay CurrentOverlay => PhaseOverlay.None;
            public void ReplacePhase(GamePhase next) => CurrentBase = next;
            public void PushOverlay(PhaseOverlay overlay) { }
            public void PopOverlay() { }
        }

        private sealed class FakeDungeon : IDungeonService
        {
            public RoomInstance Current;
            public RoomSO CurrentRoom => Current?.Template;
            public RoomInstance CurrentRoomInstance => Current;
            public DoorDirection? LastEntryDirection => null;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances()
                => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells()
                => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId)
            { neighborInstanceId = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection direction) => false;
            public bool EnterRoomByInstanceId(Guid instanceId) => false;
            public bool SetRoomState(Guid instanceId, RoomState state) => false;
            public void ResyncDoorVisuals(Guid instanceId) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }
    }
}
