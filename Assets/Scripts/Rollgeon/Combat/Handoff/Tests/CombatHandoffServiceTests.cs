using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.FSM;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Entities;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.Combat.Handoff.Tests
{
    [TestFixture]
    public class CombatHandoffServiceTests
    {
        private StubDungeonService _stubDungeon;
        private StubPlayerService _stubPlayer;
        private SpyEnemySpawnResolver _spyResolver;
        private SpyEnemyAIHandler _spyAI;
        private SpyScreenManager _spyScreen;
        private SpyCombatStarter _spyCombat;
        private CombatHandoffService _service;
        private bool _savedKeepSelected;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        // -------------------------------------------------------------------
        // Stubs / Spies
        // -------------------------------------------------------------------

        private class StubDungeonService : IDungeonService
        {
            public RoomSO CurrentRoom { get; set; }
            public RoomInstance CurrentRoomInstance { get; set; }

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() =>
                new Dictionary<Guid, RoomInstance>();

            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() =>
                new Dictionary<Guid, FloorShell>();

            public bool CanEnterRoomByDoor(Rollgeon.Dungeon.Components.DoorDirection dir, out Guid id)
            {
                id = Guid.Empty;
                return false;
            }

            public Rollgeon.Dungeon.Components.DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(Rollgeon.Dungeon.Components.DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public bool SetRoomState(Guid id, Rollgeon.Dungeon.RoomState state) => false;
            public void ResyncDoorVisuals(Guid id) { }

            public UnityEngine.Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        private class SpyEnemySpawnResolver : IEnemySpawnResolver
        {
            public int ResolveCallCount { get; private set; }
            public RoomSO LastRoom { get; private set; }
            public RoomInstance LastInstance { get; private set; }
            public List<(Guid id, EnemyDataSO data)> ReturnValue { get; set; } = new();

            public List<(Guid id, EnemyDataSO data)> Resolve(RoomInstance instance, System.Random rng)
            {
                ResolveCallCount++;
                LastInstance = instance;
                LastRoom = instance?.Template;
                return ReturnValue;
            }
        }

        private class SpyEnemyAIHandler : IEnemyAIHandler
        {
            public int HandleCallCount { get; private set; }
            public Guid LastEnemyId { get; private set; }

            public void HandleEnemyTurn(Guid enemyId)
            {
                HandleCallCount++;
                LastEnemyId = enemyId;
            }
        }

        private class SpyScreenManager : IScreenManager
        {
            // set público: los tests que ejercitan paths que resuelven el hud desde el
            // screen manager (ej. el reset de fase de combate) necesitan plantarlo.
            public IBaseScreen Current { get; set; }
            public int PushByStringIdCallCount { get; private set; }
            public string LastScreenId { get; private set; }
            public IScreenPayload LastPayload { get; private set; }

            public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PushByStringId(string screenId, IScreenPayload payload = null)
            {
                PushByStringIdCallCount++;
                LastScreenId = screenId;
                LastPayload = payload;
            }
            public void PopCurrent() { }
            public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen { }
            public void PopOverlay() { }
            public void RegisterScreen(IBaseScreen screen) { }
            public void UnregisterScreen(IBaseScreen screen) { }
        }

        private class SpyCombatStarter : ICombatStarter
        {
            public int StartCombatCallCount { get; private set; }
            public Guid LastPlayerId { get; private set; }
            public IReadOnlyList<Guid> LastParticipants { get; private set; }
            public Guid LastRoomInstanceId { get; private set; }
            public Action<Guid> LastEnemyActionHandler { get; private set; }

            public void StartCombat(
                Guid playerId,
                IReadOnlyList<Guid> participants,
                Guid roomInstanceId,
                Action<Guid> enemyActionHandler)
            {
                StartCombatCallCount++;
                LastPlayerId = playerId;
                LastParticipants = participants;
                LastRoomInstanceId = roomInstanceId;
                LastEnemyActionHandler = enemyActionHandler;
            }
        }

        private class StubPlayerCombatActions : IPlayerCombatActions
        {
            public void SendPlayerAction() { }
            public void EndPlayerTurn() { }
        }

        private sealed class CountingRollPool : Rollgeon.Combat.Rolls.IRollPoolService
        {
            public int CurrentRolls = 99;
            public int TrySpendCallCount { get; private set; }

            public bool IsCombatActive => true;
            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count)
            {
                if (count > CurrentRolls) return false;
                TrySpendCallCount++;
                CurrentRolls -= count;
                return true;
            }
            public int Drain(Guid entityId, int amount)
            {
                int drained = Math.Min(amount, CurrentRolls);
                CurrentRolls -= drained;
                return drained;
            }
            public void AddRolls(Guid entityId, int amount) => CurrentRolls += amount;
            public int GetCurrent(Guid entityId) => CurrentRolls;
            public int GetMax(Guid entityId) => 15;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) => CurrentRolls = value;
        }

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            // KeepFromSelection depende del modo persistido en PlayerPrefs: pin al
            // default (invertido) para que la suite no dependa de la pref del dev,
            // y restore en TearDown para no pisarla.
            _savedKeepSelected = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected;
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = false;

            _stubDungeon = new StubDungeonService();
            _stubPlayer = new StubPlayerService();
            _spyResolver = new SpyEnemySpawnResolver();
            _spyAI = new SpyEnemyAIHandler();
            _spyScreen = new SpyScreenManager();
            _spyCombat = new SpyCombatStarter();

            _service = new CombatHandoffService(
                _stubDungeon, _stubPlayer, _spyResolver,
                _spyAI, _spyScreen, _spyCombat, new StubPlayerCombatActions());
        }

        [TearDown]
        public void TearDown()
        {
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = _savedKeepSelected;
            _service?.Dispose();

            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private RoomSO CreateRoom(RoomType type, string id = "test_room")
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        private void SetCurrentRoom(RoomSO room)
        {
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomInstance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
            };
        }

        private EnemyDataSO CreateEnemy(string name)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            _createdObjects.Add(enemy);
            return enemy;
        }

        private void TriggerCombat(Guid roomInstanceId, string roomId, RoomType roomType)
        {
            EventManager.Trigger(EventName.OnCombatTriggered, roomInstanceId, roomId, roomType);
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void OnCombatTriggered_CallsResolverWithCurrentRoom()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            var enemy = CreateEnemy("Goblin");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (Guid.NewGuid(), enemy) };

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(1, _spyResolver.ResolveCallCount);
            Assert.AreSame(room, _spyResolver.LastRoom);
        }

        [Test]
        public void OnCombatTriggered_PushesCombatHUDScreen()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(1, _spyScreen.PushByStringIdCallCount);
            Assert.AreEqual("CombatHUD", _spyScreen.LastScreenId);
        }

        [Test]
        public void OnCombatTriggered_CallsStartCombat()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            var roomInstanceId = Guid.NewGuid();
            TriggerCombat(roomInstanceId, "test_room", RoomType.Combat);

            Assert.AreEqual(1, _spyCombat.StartCombatCallCount);
            Assert.AreEqual(roomInstanceId, _spyCombat.LastRoomInstanceId);
        }

        [Test]
        public void OnCombatTriggered_ParticipantsIncludePlayer()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            var enemyId = Guid.NewGuid();
            var enemy = CreateEnemy("Goblin");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (enemyId, enemy) };

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.IsNotNull(_spyCombat.LastParticipants);
            Assert.IsTrue(
                ((List<Guid>)_spyCombat.LastParticipants).Contains(_stubPlayer.PlayerGuid),
                "Participants must include the player Guid");
        }

        [Test]
        public void OnCombatTriggered_ParticipantsIncludeEnemies()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            var enemyId = Guid.NewGuid();
            var enemy = CreateEnemy("Goblin");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (enemyId, enemy) };

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.IsTrue(
                ((List<Guid>)_spyCombat.LastParticipants).Contains(enemyId),
                "Participants must include enemy Guids");
        }

        [Test]
        public void OnCombatTriggered_BossRoom_SpawnsOneEnemy()
        {
            var room = CreateRoom(RoomType.Boss);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "boss_room", RoomType.Boss);

            Assert.AreEqual(RoomType.Boss, _spyResolver.LastInstance?.Template?.Type,
                "Boss rooms should pass the boss instance to the resolver.");
        }

        [Test]
        public void OnCombatTriggered_CombatRoom_PassesCombatInstance()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "combat_room", RoomType.Combat);

            Assert.AreEqual(RoomType.Combat, _spyResolver.LastInstance?.Template?.Type,
                "Combat rooms should pass the combat instance to the resolver.");
        }

        [Test]
        public void OnCombatTriggered_PassesEnemyAIHandlerToStartCombat()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.IsNotNull(_spyCombat.LastEnemyActionHandler);
            // Invoke to verify it routes to the AI handler.
            var testId = Guid.NewGuid();
            _spyCombat.LastEnemyActionHandler(testId);
            Assert.AreEqual(1, _spyAI.HandleCallCount);
            Assert.AreEqual(testId, _spyAI.LastEnemyId);
        }

        [Test]
        public void OnCombatTriggered_BossBarSubscriberThrows_StartCombatStillRuns()
        {
            // Arrange — BUG-078: TypedEvent.Raise no aísla excepciones; un subscriber
            // roto de la barra de boss (ej. BossBarView en frame 1 del resume) mataba
            // el resto del handoff dejando boss visible + HUD pusheado + FSM nula.
            var room = CreateRoom(RoomType.Boss);
            SetCurrentRoom(room);
            var enemy = CreateEnemy("Boss");
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>
                { (Guid.NewGuid(), enemy) };
            Action<BossEncounterStartedPayload> throwingListener =
                _ => throw new InvalidOperationException("boss bar rota");
            TypedEvent<BossEncounterStartedPayload>.Subscribe(throwingListener);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("BossEncounterStarted"));

            try
            {
                // Act
                TriggerCombat(Guid.NewGuid(), "boss_room", RoomType.Boss);

                // Assert — el combate arranca igual: la barra es cosmética.
                Assert.AreEqual(1, _spyCombat.StartCombatCallCount,
                    "StartCombat debe correr aunque el subscriber del boss bar lance.");
            }
            finally
            {
                TypedEvent<BossEncounterStartedPayload>.Clear();
            }
        }

        [Test]
        public void Dispose_UnsubscribesFromEvent()
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();

            _service.Dispose();

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(0, _spyResolver.ResolveCallCount,
                "After Dispose, resolver should not be called");
        }

        [Test]
        public void OnCombatTriggered_NullCurrentRoom_DoesNotCallStartCombat()
        {
            _stubDungeon.CurrentRoom = null;

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            Assert.AreEqual(0, _spyCombat.StartCombatCallCount,
                "StartCombat should not be called when current room is null");
        }

        // -------------------------------------------------------------------
        // Combat-end cleanup: el reset del estado de fase debe tolerar un
        // ServiceLocator vacio y un Dispose previo (Feature#0050: el budget
        // por accion murio; el pool se resetea solo via sus propios eventos).
        // -------------------------------------------------------------------

        [Test]
        public void OnCombatEnd_NoServicesRegistered_DoesNotThrow()
        {
            // ServiceLocator stays empty. Reset must tolerate that.
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory));
        }

        [Test]
        public void Dispose_ThenCombatEnd_DoesNotThrow()
        {
            _service.Dispose();

            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory));
        }

        [Test]
        public void OnCombatEnd_WithBareHudWired_DoesNotThrow()
        {
            // Arrange — hud sin sub-views wireadas (el default del prefab).
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            _spyScreen.Current = hudGo.AddComponent<CombatHUDView>();

            // Act / Assert
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        // -------------------------------------------------------------------
        // ResolveChainPhaseEntry — entrada a la fase siguiente del chain
        // (Feature#0050: alcanza con 1 roll en el pool; la distincion free/paid
        // murio con el sistema de energia)
        // -------------------------------------------------------------------

        [Test]
        public void ResolveChainPhaseEntry_ReturnsTrue_WhenPoolHasRolls()
        {
            Assert.IsTrue(CombatHandoffService.ResolveChainPhaseEntry(rollsAvailable: 1));
            Assert.IsTrue(CombatHandoffService.ResolveChainPhaseEntry(rollsAvailable: 5));
        }

        [Test]
        public void ResolveChainPhaseEntry_ReturnsFalse_WhenPoolIsEmpty()
        {
            Assert.IsFalse(CombatHandoffService.ResolveChainPhaseEntry(rollsAvailable: 0));
        }

        // -------------------------------------------------------------------
        // BUG-030 (Torpe): TryScheduleForcedFullHandReroll — relanzamiento
        // completo gratuito de la mano activa, diferido por scheduler.
        // -------------------------------------------------------------------

        private sealed class SpyDiceRoller : IDiceRoller
        {
            public int RerollCallCount { get; private set; }
            public bool[] LastKeep { get; private set; }

            public int[] RollAll(DiceBagSO bag) => new int[bag.Dice.Count];

            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
            {
                RerollCallCount++;
                LastKeep = (bool[])keep.Clone();
                return (int[])previousResult.Clone();
            }
        }

        private sealed class FakeDiceBlockService : Rollgeon.Combat.DiceBlock.IDiceBlockService
        {
            private readonly Dictionary<int, string> _blocked = new Dictionary<int, string>();
            public void Block(int index, string label = null) => _blocked[index] = label;
            public void Unblock(int index) => _blocked.Remove(index);
            public bool IsBlocked(int index) => _blocked.ContainsKey(index);
            public string LabelOf(int index) => _blocked.TryGetValue(index, out var l) ? l : null;
            public IReadOnlyCollection<int> BlockedIndices => _blocked.Keys;
            public void Clear() => _blocked.Clear();
        }

        // Planta una mano activa (behavior con tirada + faces reveladas) por
        // reflexión — el estado que TryScheduleForcedFullHandReroll valida.
        private SpyDiceRoller ArmActiveHand(int diceCount = 3)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>();
            for (int i = 0; i < diceCount; i++) bag.Dice.Add(DiceType.D6);
            _createdObjects.Add(bag);
            _stubPlayer.DiceBag = bag;

            var roller = new SpyDiceRoller();
            ServiceLocator.AddService<IDiceRoller>(roller);

            SetPrivateField(_service, "_selectedBehavior",
                new HeroActionBehavior { ActionName = "TestAttack", NeedsDiceRoll = true });
            SetPrivateField(_service, "_lastFaces", new[] { 1, 2, 3 });
            return roller;
        }

        private void SetScheduler(Action<float, Action> scheduler)
        {
            var prop = typeof(CombatHandoffService).GetProperty("ForcedRerollScheduler",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(prop, "ForcedRerollScheduler seam no encontrado.");
            prop.SetValue(_service, scheduler);
        }

        [Test]
        public void TryScheduleForcedFullHandReroll_NoActiveHand_ReturnsFalse()
        {
            // Arrange — servicio recién construido, sin behavior ni tirada.
            SetScheduler((d, cb) => cb());

            // Act / Assert
            Assert.IsFalse(_service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid));
        }

        [Test]
        public void TryScheduleForcedFullHandReroll_ActiveHand_RerollsAllDiceWithoutConsumingRolls()
        {
            // Arrange
            var roller = ArmActiveHand();
            var pool = new CountingRollPool();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(pool);
            SetScheduler((d, cb) => cb());

            // Act
            var accepted = _service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid);

            // Assert — mano completa re-tirada, gratis.
            Assert.IsTrue(accepted);
            Assert.AreEqual(1, roller.RerollCallCount);
            Assert.IsFalse(CombatHandoffService.AllDiceHeld(roller.LastKeep));
            foreach (var kept in roller.LastKeep)
                Assert.IsFalse(kept, "El forced reroll debe re-tirar TODOS los dados (keep all-false).");
            Assert.AreEqual(0, pool.TrySpendCallCount,
                "El reroll del Torpe no debe consumir rolls del pool.");
        }

        [Test]
        public void TryScheduleForcedFullHandReroll_WhilePending_ReturnsFalse()
        {
            // Arrange — scheduler que retiene el callback: el pending queda abierto.
            ArmActiveHand();
            SetScheduler((d, cb) => { });

            Assert.IsTrue(_service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid));

            // Act / Assert
            Assert.IsFalse(_service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid));
        }

        [Test]
        public void ForcedReroll_PreservesBlockedDiceFaces()
        {
            // Arrange — Boss 1: el dado bloqueado no puede re-tirarse ni por el Torpe.
            var roller = ArmActiveHand();
            var blocks = new FakeDiceBlockService();
            blocks.Block(0);
            ServiceLocator.AddService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(blocks);
            SetScheduler((d, cb) => cb());

            // Act
            _service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid);

            // Assert
            Assert.IsTrue(roller.LastKeep[0], "El dado bloqueado debe conservarse.");
            Assert.IsFalse(roller.LastKeep[1]);
            Assert.IsFalse(roller.LastKeep[2]);
        }

        [Test]
        public void CombatEnd_CancelsPendingForcedReroll()
        {
            // Arrange — el callback del scheduler llega DESPUÉS del fin de combate.
            var roller = ArmActiveHand();
            Action captured = null;
            SetScheduler((d, cb) => captured = cb);
            Assert.IsTrue(_service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid));

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);
            captured?.Invoke();

            // Assert
            Assert.AreEqual(0, roller.RerollCallCount,
                "El callback tardío no debe re-tirar sobre un combate cerrado.");
        }

        [Test]
        public void ForcedRerollPending_BlocksConfirm_UntilRerollRuns()
        {
            // Arrange — wiring real del HUD para obtener el delegate de Confirm.
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            var hud = hudGo.AddComponent<CombatHUDView>();
            _spyScreen.Current = hud;
            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            // La mano activa se planta DESPUÉS del wiring (que resetea el estado).
            var roller = ArmActiveHand();
            int rollResolvedCount = 0;
            EventManager.EventReceiver onResolved = args => rollResolvedCount++;
            EventManager.Subscribe(EventName.OnRollResolved, onResolved);

            Action captured = null;
            SetScheduler((d, cb) => captured = cb);
            Assert.IsTrue(_service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid));

            // Act 1 — Confirm durante la ventana pending: no debe resolver la mano.
            hud.OnConfirmRequested?.Invoke();
            Assert.AreEqual(0, rollResolvedCount,
                "Confirm no debe ejecutar mientras el forced reroll está pendiente.");

            // Act 2 — corre el reroll y el Confirm vuelve a funcionar.
            captured?.Invoke();
            Assert.AreEqual(1, roller.RerollCallCount);
            hud.OnConfirmRequested?.Invoke();

            // Assert
            Assert.AreEqual(1, rollResolvedCount,
                "Tras el reroll forzado, Confirm debe resolver la mano normalmente.");
            EventManager.UnSubscribe(EventName.OnRollResolved, onResolved);
        }

        // -------------------------------------------------------------------
        // QoL: reroll invertido (KeepFromSelection) — la máscara de la UI marca
        // los dados SELECCIONADOS para re-tirar; el roller recibe el complemento.
        // -------------------------------------------------------------------

        [Test]
        public void KeepFromSelection_RerollsSelectedAndKeepsUnselected()
        {
            // Arrange + Act
            var keep = CombatHandoffService.KeepFromSelection(
                new[] { true, false, true, false, false }, 5);

            // Assert — keep = complemento exacto de la selección.
            CollectionAssert.AreEqual(new[] { false, true, false, true, true }, keep);
        }

        [Test]
        public void KeepFromSelection_NullSelection_KeepsAllDice()
        {
            // Arrange + Act — sin selección, nada vuela (el guard AllDiceHeld bailea).
            var keep = CombatHandoffService.KeepFromSelection(null, 3);

            // Assert
            CollectionAssert.AreEqual(new[] { true, true, true }, keep);
        }

        [Test]
        public void KeepFromSelection_ShortMask_KeepsDiceWithoutSelectionState()
        {
            // Arrange + Act — un dado sin estado de selección no debe volar.
            var keep = CombatHandoffService.KeepFromSelection(new[] { true }, 3);

            // Assert
            CollectionAssert.AreEqual(new[] { false, true, true }, keep);
        }

        // -------------------------------------------------------------------
        // Modo clásico (RerollSelectionPrefs.KeepSelected): la selección marca
        // los dados que SE QUEDAN; vuelan los no seleccionados.
        // -------------------------------------------------------------------

        [Test]
        public void KeepFromSelection_ClassicMode_KeepsSelectedAndRerollsUnselected()
        {
            // Arrange
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;

            // Act
            var keep = CombatHandoffService.KeepFromSelection(
                new[] { true, false, true, false, false }, 5);

            // Assert — keep = la selección tal cual.
            CollectionAssert.AreEqual(new[] { true, false, true, false, false }, keep);
        }

        [Test]
        public void KeepFromSelection_ClassicMode_NullSelection_RerollsAllDice()
        {
            // Arrange — sin selección nada está lockeado: vuela toda la mano.
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;

            // Act
            var keep = CombatHandoffService.KeepFromSelection(null, 3);

            // Assert
            CollectionAssert.AreEqual(new[] { false, false, false }, keep);
        }

        [Test]
        public void KeepFromSelection_ClassicMode_ShortMask_RerollsDiceWithoutSelectionState()
        {
            // Arrange — un dado sin estado de selección no está lockeado: vuela.
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;

            // Act
            var keep = CombatHandoffService.KeepFromSelection(new[] { true }, 3);

            // Assert
            CollectionAssert.AreEqual(new[] { true, false, false }, keep);
        }

        [Test]
        public void PoolReroll_ClassicMode_NothingSelected_RerollsWholeHandAndSpendsOneRoll()
        {
            // Arrange — wiring real del HUD para obtener el delegate del reroll.
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            var hud = hudGo.AddComponent<CombatHUDView>();
            _spyScreen.Current = hud;
            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            var roller = ArmActiveHand();
            var pool = new CountingRollPool();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(pool);

            // Act — sin dice zone cableada GetCurrentKeep() es null = nada lockeado.
            hud.OnEnergyRerollRequested?.Invoke();

            // Assert — vuela toda la mano y el reroll SÍ consume 1 roll del pool.
            Assert.AreEqual(1, roller.RerollCallCount);
            foreach (var kept in roller.LastKeep)
                Assert.IsFalse(kept, "En clásico sin selección debe volar toda la mano.");
            Assert.AreEqual(1, pool.TrySpendCallCount);
        }

        [Test]
        public void PoolReroll_ClassicMode_AllDiceSelected_BailsWithoutConsumingRolls()
        {
            // Arrange — todos lockeados ⇒ keep all-true ⇒ nada que re-tirar.
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            var hud = hudGo.AddComponent<CombatHUDView>();
            _spyScreen.Current = hud;
            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);

            var roller = ArmActiveHand();
            var pool = new CountingRollPool();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(pool);

            var zone = hudGo.AddComponent<Rollgeon.UI.HUD.DiceZoneView>();
            SetPrivateField(zone, "_heldStates", new[] { true, true, true });
            SetPrivateField(hud, "_diceZone", zone);

            // Act
            hud.OnEnergyRerollRequested?.Invoke();

            // Assert — guard defensivo: ni roller ni pool se tocan.
            Assert.AreEqual(0, roller.RerollCallCount);
            Assert.AreEqual(0, pool.TrySpendCallCount);
        }

        // -------------------------------------------------------------------
        // QoL: cancel por click derecho
        // -------------------------------------------------------------------

        [Test]
        public void TryCancelFromRightClick_WithoutSelectionInProgress_ReturnsFalse()
        {
            // Arrange — HUD de combate activo pero sin ninguna selección abierta.
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            _spyScreen.Current = hudGo.AddComponent<CombatHUDView>();

            // Act / Assert — el router cae al deselect-all de dados.
            Assert.IsFalse(_service.HasCancellableSelection);
            Assert.IsFalse(_service.TryCancelFromRightClick());
        }

        [Test]
        public void TryCancelFromRightClick_WithoutCombatHud_ReturnsFalse()
        {
            // Arrange — selección pendiente pero la pantalla actual no es el HUD de
            // combate (pausa, transición): el cancel global no debe operar a ciegas.
            SetPrivateField(_service, "_awaitingPlayerSelection", true);
            _spyScreen.Current = null;

            // Act / Assert
            Assert.IsFalse(_service.TryCancelFromRightClick());
        }

        [Test]
        public void TryCancelFromRightClick_AwaitingPlayerSelection_CancelsAndFreesState()
        {
            // Arrange — Movement esperando su tile. Sin ISelectionController registrado,
            // CancelPlayerSelection usa la limpieza defensiva (libera estado y emite
            // OnBehaviorExecuted) — alcanza para verificar el contrato del cancel.
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            _spyScreen.Current = hudGo.AddComponent<CombatHUDView>();
            SetPrivateField(_service, "_awaitingPlayerSelection", true);
            SetPrivateField(_service, "_selectedBehavior",
                new HeroActionBehavior { ActionName = "Movement", NeedsDiceRoll = false });
            Assert.IsTrue(_service.HasCancellableSelection, "pre-condition");

            int behaviorExecutedCount = 0;
            EventManager.EventReceiver onExecuted = args => behaviorExecutedCount++;
            EventManager.Subscribe(EventName.OnBehaviorExecuted, onExecuted);

            // Act
            var cancelled = _service.TryCancelFromRightClick();

            // Assert — canceló, liberó el estado y avisó a la UI para destrabarse.
            Assert.IsTrue(cancelled);
            Assert.IsFalse(_service.HasCancellableSelection);
            Assert.AreEqual(1, behaviorExecutedCount,
                "El cancel debe emitir OnBehaviorExecuted para liberar los slots de la UI.");
            EventManager.UnSubscribe(EventName.OnBehaviorExecuted, onExecuted);
        }

        // -------------------------------------------------------------------
        // §6.6 Dado de Movimiento — seleccionar Movement tira su dado propio
        // (separado de la build), cobra 1 roll al tirar y deja la acción prepaga.
        // -------------------------------------------------------------------

        private sealed class FakeMovementDie : Rollgeon.Movement.Die.IMovementDieService
        {
            public int NextFace = 2;
            public int RollCount;
            public int ClearCount;
            public bool HasActive;
            public Guid ActiveGuid;
            public Action<int> PendingReveal;
            public bool DeferReveal;

            public DiceType CurrentType => DiceType.D4;
            public int LastFace => NextFace;
            public void SetTypeOverride(DiceType? type) { }
            public void SetPresenter(Rollgeon.Movement.Die.IMovementDiePresenter presenter) { }
#pragma warning disable 67
            public event Action<Guid, int> OnRolled;
            public event Action OnCleared;
#pragma warning restore 67

            public void Roll(Guid playerGuid, Action<int> onRevealed)
            {
                RollCount++;
                void Reveal()
                {
                    HasActive = true;
                    ActiveGuid = playerGuid;
                    onRevealed(NextFace);
                }
                if (DeferReveal) PendingReveal = _ => Reveal();
                else Reveal();
            }

            public bool TryGetActiveRange(Guid playerGuid, out int range)
            {
                range = HasActive && playerGuid == ActiveGuid ? NextFace : 0;
                return HasActive && playerGuid == ActiveGuid;
            }

            public void ClearActiveRange()
            {
                ClearCount++;
                HasActive = false;
                PendingReveal = null;
            }
        }

        // Efecto de movimiento con selección BeforeRoll sobre celdas vacías alcanzables
        // por camino — la misma forma que EffMove con RangeFromMovementDie.
        private sealed class FakeMovementDieMoveEffect : Rollgeon.Effects.IEffect
        {
            public string GetEffectName() => "FakeMove";
            public Rollgeon.Effects.Selection.SelectionSettings GetSelection()
                => new Rollgeon.Effects.Selection.SelectionSettings
                {
                    SlotState = Rollgeon.Effects.Selection.SlotState.Empty,
                    Timing = Rollgeon.Effects.Selection.SelectionTiming.BeforeRoll,
                    Range = 4,
                    RangeMode = Rollgeon.Effects.Selection.RangeMode.PathReachable,
                    RangeFromMovementDie = true,
                    AutoAccept = true,
                };
            public bool HasSelectionRequirement() => true;
            public bool RequiresSelectionAt(Rollgeon.Effects.Selection.SelectionTiming timing)
                => timing == Rollgeon.Effects.Selection.SelectionTiming.BeforeRoll;
            public bool ValidateSelection(Rollgeon.Effects.Selection.TargetSelectionResult result,
                Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }
            public bool Apply(Rollgeon.Effects.EffectContext context) => true;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target);
        }

        /// <summary>
        /// Arma un combate real con un héroe cuyo slot Movement usa el dado (índice 0 de
        /// GetBehaviorsForPhase(Combat)), una grilla <paramref name="gridSize"/>² con el
        /// jugador en (0,0) y el pool de rolls contable.
        /// </summary>
        private (CombatHUDView hud, CountingRollPool pool) ArmMovementDieCombat(int gridSize)
        {
            var room = CreateRoom(RoomType.Combat);
            SetCurrentRoom(room);
            _spyResolver.ReturnValue = new List<(Guid, EnemyDataSO)>();
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            var hud = hudGo.AddComponent<CombatHUDView>();
            _spyScreen.Current = hud;

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _createdObjects.Add(hero);
            hero.PhaseBehaviors = new List<HeroActionBehavior>
            {
                new HeroActionBehavior
                {
                    ActionName = "Movement",
                    IsBaseBehavior = true,
                    Slot = HeroBehaviorSlot.Movement,
                    NeedsDiceRoll = false,
                    Effects = new List<Rollgeon.Effects.EffectData>
                    {
                        new Rollgeon.Effects.EffectData
                        {
                            Effects = new List<Rollgeon.Effects.IEffect> { new FakeMovementDieMoveEffect() },
                        },
                    },
                },
            };
            _stubPlayer.CurrentHero = hero;

            var grid = new Rollgeon.Grid.GridManager();
            grid.LoadRoom(Rollgeon.Grid.NavGraph.Rect(gridSize, gridSize));
            grid.Register(_stubPlayer.PlayerGuid, new Rollgeon.Grid.GridCoord(0, 0));
            ServiceLocator.AddService<Rollgeon.Grid.IGridManager>(grid);
            ServiceLocator.AddService<Rollgeon.Movement.IMovementService>(
                new Rollgeon.Movement.MovementService(grid));

            var pool = new CountingRollPool();
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(pool);

            TriggerCombat(Guid.NewGuid(), "test_room", RoomType.Combat);
            return (hud, pool);
        }

        [Test]
        public void MovementDie_SelectMovement_RollsOwnDieSpendsOneRollAndConfirmsPrepaid()
        {
            // Arrange
            var (hud, pool) = ArmMovementDieCombat(gridSize: 4);
            var die = new FakeMovementDie { NextFace = 2 };
            ServiceLocator.AddService<Rollgeon.Movement.Die.IMovementDieService>(die);

            bool prepaidAtConfirm = false;
            Rollgeon.Combat.Rolls.RollActionKind resolvedKind = Rollgeon.Combat.Rolls.RollActionKind.Unknown;
            EventManager.EventReceiver onResolved = args =>
            {
                // DoConfirm emite OnRollResolved antes de limpiar el estado: acá el flag ya
                // tiene que estar marcado para que RollsPrepaid=true llegue al sub-FSM.
                prepaidAtConfirm = (bool)GetPrivateField(_service, "_movementRollPrepaid");
                resolvedKind = (Rollgeon.Combat.Rolls.RollActionKind)args[2];
            };
            EventManager.Subscribe(EventName.OnRollResolved, onResolved);
            int rollStarted = 0;
            EventManager.EventReceiver onStarted = _ => rollStarted++;
            EventManager.Subscribe(EventName.OnRollStarted, onStarted);

            // Act
            hud.OnBehaviorSelected?.Invoke(0);

            // Assert
            Assert.AreEqual(1, die.RollCount, "Movement debe tirar SU dado, no la build.");
            Assert.AreEqual(1, pool.TrySpendCallCount, "Exactamente 1 roll, cobrado al tirar.");
            Assert.AreEqual(1, rollStarted);
            Assert.IsTrue(prepaidAtConfirm, "DoConfirm debe correr con el roll prepago.");
            Assert.AreEqual(Rollgeon.Combat.Rolls.RollActionKind.Movement, resolvedKind);
            Assert.IsFalse((bool)GetPrivateField(_service, "_movementRollPrepaid"),
                "Al terminar la acción el flag se limpia.");
            Assert.GreaterOrEqual(die.ClearCount, 1, "Al terminar la acción se descarta el rango activo.");

            EventManager.UnSubscribe(EventName.OnRollResolved, onResolved);
            EventManager.UnSubscribe(EventName.OnRollStarted, onStarted);
        }

        [Test]
        public void MovementDie_NoReachableTileAtReveal_ReleasesSelectionWithoutRefund()
        {
            // Arrange — el gate pre-selección ya rechaza un Movement sin destino, así que el
            // caso real es asíncrono: la selección pasó, el dado anima, y al revelar ya no hay
            // adónde ir (el tablero cambió). Se simula quitando la grilla antes del reveal.
            var (hud, pool) = ArmMovementDieCombat(gridSize: 4);
            var die = new FakeMovementDie { NextFace = 1, DeferReveal = true };
            ServiceLocator.AddService<Rollgeon.Movement.Die.IMovementDieService>(die);
            int executed = 0;
            EventManager.EventReceiver onExecuted = _ => executed++;
            EventManager.Subscribe(EventName.OnBehaviorExecuted, onExecuted);
            hud.OnBehaviorSelected?.Invoke(0);
            Assert.AreEqual(1, pool.TrySpendCallCount, "pre-condition: cobró al tirar");
            ServiceLocator.RemoveService<Rollgeon.Grid.IGridManager>();

            // Act
            die.PendingReveal(0);

            // Assert — pagó por la tirada que vio; la UI se libera; nada quedó armado.
            Assert.AreEqual(1, pool.TrySpendCallCount, "Sin reembolso.");
            Assert.AreEqual(98, pool.CurrentRolls);
            Assert.AreEqual(1, executed, "OnBehaviorExecuted libera el slot en la UI.");
            Assert.IsNull(GetPrivateField(_service, "_selectedBehavior"));
            Assert.IsFalse(die.HasActive, "El rango activo se descarta.");
            Assert.IsFalse((bool)GetPrivateField(_service, "_movementRollPrepaid"));

            EventManager.UnSubscribe(EventName.OnBehaviorExecuted, onExecuted);
        }

        [Test]
        public void MovementDie_PoolEmpty_DoesNotRollAndReleasesSelection()
        {
            // Arrange
            var (hud, pool) = ArmMovementDieCombat(gridSize: 4);
            pool.CurrentRolls = 0;
            var die = new FakeMovementDie();
            ServiceLocator.AddService<Rollgeon.Movement.Die.IMovementDieService>(die);

            // Act
            hud.OnBehaviorSelected?.Invoke(0);

            // Assert
            Assert.AreEqual(0, die.RollCount, "Sin roll en el pool no se tira el dado.");
            Assert.IsNull(GetPrivateField(_service, "_selectedBehavior"));
        }

        [Test]
        public void MovementDie_DeferredRevealAfterCombatEnd_IsIgnored()
        {
            // Arrange — el HUD anima el dado; el combate termina antes del reveal.
            var (hud, pool) = ArmMovementDieCombat(gridSize: 4);
            var die = new FakeMovementDie { DeferReveal = true };
            ServiceLocator.AddService<Rollgeon.Movement.Die.IMovementDieService>(die);
            hud.OnBehaviorSelected?.Invoke(0);
            Assert.AreEqual(1, pool.TrySpendCallCount, "pre-condition: cobró al tirar");
            var pending = die.PendingReveal;
            Assert.IsNotNull(pending, "pre-condition: reveal diferido");
            int resolved = 0;
            EventManager.EventReceiver onResolved = _ => resolved++;
            EventManager.Subscribe(EventName.OnRollResolved, onResolved);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);
            pending(0); // el presenter termina tarde

            // Assert — el reset de combate soltó _selectedBehavior: el callback no confirma nada.
            Assert.AreEqual(0, resolved);
            Assert.GreaterOrEqual(die.ClearCount, 1, "El fin de combate descarta el dado.");

            EventManager.UnSubscribe(EventName.OnRollResolved, onResolved);
        }

        [Test]
        public void MovementDie_AfterRoll_RightClickCancelsWithoutRefund()
        {
            // Arrange — Movement con el dado ya tirado (roll pagado) esperando su tile.
            // §6.6 revertido: la selección se puede soltar; el roll pagado se pierde.
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            var hud = hudGo.AddComponent<CombatHUDView>();
            _spyScreen.Current = hud;
            var behavior = new HeroActionBehavior { ActionName = "Movement", NeedsDiceRoll = false };
            behavior.SetBehaviorValue(Rollgeon.Entities.Behaviors.BehaviorValueKey.FloatingDamage,
                new Rollgeon.Entities.Behaviors.FloatBehaviorValue { Value = 7f });
            SetPrivateField(_service, "_awaitingPlayerSelection", true);
            SetPrivateField(_service, "_movementRollPrepaid", true);
            SetPrivateField(_service, "_selectedBehavior", behavior);
            int executed = 0;
            EventManager.EventReceiver onExecuted = _ => executed++;
            EventManager.Subscribe(EventName.OnBehaviorExecuted, onExecuted);

            // Act / Assert — el click derecho cancela y libera todo el estado.
            Assert.IsTrue(_service.HasCancellableSelection);
            Assert.IsTrue(_service.TryCancelFromRightClick());
            Assert.IsFalse((bool)GetPrivateField(_service, "_awaitingPlayerSelection"));
            Assert.IsFalse((bool)GetPrivateField(_service, "_movementRollPrepaid"));
            Assert.IsNull(GetPrivateField(_service, "_selectedBehavior"));
            Assert.AreEqual(1, executed, "OnBehaviorExecuted libera el slot en la UI.");
            // El path cancelado no pasa por Execute(): los stored values del behavior
            // (compartido toda la run) se limpian en el cancel.
            Assert.IsFalse(behavior.TryGetBehaviorValues<Rollgeon.Entities.Behaviors.FloatBehaviorValue>(
                Rollgeon.Entities.Behaviors.BehaviorValueKey.FloatingDamage, out _));

            EventManager.UnSubscribe(EventName.OnBehaviorExecuted, onExecuted);
        }

        [Test]
        public void MovementDie_AfterRoll_SlotReclickCancelsCommittedSelection()
        {
            // Arrange — combate real con callbacks del HUD bindeados; se simula el
            // estado comprometido (dado tirado, roll prepago) sobre el behavior REAL
            // del slot 0, así el re-click resuelve sameAction.
            var (hud, pool) = ArmMovementDieCombat(gridSize: 4);
            var movementBehavior = _stubPlayer.CurrentHero.PhaseBehaviors[0];
            SetPrivateField(_service, "_awaitingPlayerSelection", true);
            SetPrivateField(_service, "_movementRollPrepaid", true);
            SetPrivateField(_service, "_selectedBehavior", movementBehavior);

            // Act — el guard viejo (§6.6) ignoraba este click con el roll prepago.
            hud.OnBehaviorSelected?.Invoke(0);

            // Assert — la selección comprometida se soltó sin cobrar nada nuevo.
            Assert.IsFalse((bool)GetPrivateField(_service, "_awaitingPlayerSelection"));
            Assert.IsFalse((bool)GetPrivateField(_service, "_movementRollPrepaid"));
            Assert.IsNull(GetPrivateField(_service, "_selectedBehavior"));
            Assert.AreEqual(0, pool.TrySpendCallCount, "Cancelar no cobra rolls.");
        }

        [Test]
        public void MovementDie_LegacyMovement_StillCancellableByRightClick()
        {
            // Arrange — sin dado (cobro-al-ejecutar): el cancel sigue siendo gratis.
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            _spyScreen.Current = hudGo.AddComponent<CombatHUDView>();
            SetPrivateField(_service, "_awaitingPlayerSelection", true);
            SetPrivateField(_service, "_movementRollPrepaid", false);
            SetPrivateField(_service, "_selectedBehavior",
                new HeroActionBehavior { ActionName = "Movement", NeedsDiceRoll = false });

            // Act / Assert
            Assert.IsTrue(_service.HasCancellableSelection);
            Assert.IsTrue(_service.TryCancelFromRightClick());
            Assert.IsFalse((bool)GetPrivateField(_service, "_awaitingPlayerSelection"));
        }

        [Test]
        public void MovementDie_ServiceNotRegistered_LegacyPathDoesNotChargeOnSelect()
        {
            // Arrange — sin IMovementDieService: Movement sigue cobrando al ejecutar (BUG-013).
            var (hud, pool) = ArmMovementDieCombat(gridSize: 4);

            // Act
            hud.OnBehaviorSelected?.Invoke(0);

            // Assert — el path legacy no cobra al seleccionar.
            Assert.AreEqual(0, pool.TrySpendCallCount);
            Assert.IsFalse((bool)GetPrivateField(_service, "_movementRollPrepaid"));
        }
    }
}
