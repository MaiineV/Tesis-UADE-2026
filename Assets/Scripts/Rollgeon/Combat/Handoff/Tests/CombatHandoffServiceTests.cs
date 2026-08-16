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

        private class SpyRerollBudgetService : IRerollBudgetService
        {
            public RerollBudget Current { get; private set; }
            public int StartBudgetCallCount { get; private set; }
            public int EndBudgetCallCount { get; private set; }

#pragma warning disable 67 // event declared by interface; spy never raises it
            public event Action<RerollStartedPayload> OnRerollStarted;
#pragma warning restore 67
            public event Action<RerollBudget> OnBudgetStarted;

            public void StartBudget(ActionDefinitionSO action)
            {
                StartBudgetCallCount++;
                if (Current != null)
                    throw new InvalidOperationException("budget already active");
                // RerollBudget setters are internal to the main assembly; the spy
                // only needs Current to be non-null for the regression test.
                Current = new RerollBudget();
                OnBudgetStarted?.Invoke(Current);
            }

            public void EndBudget()
            {
                EndBudgetCallCount++;
                Current = null;
            }

            public RerollQueryResult QueryExtraRoll(Guid playerGuid) => RerollQueryResult.Free();
            public bool TryExtraRoll(Guid playerGuid) => false;
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
        // Regression: combat-end cleanup (bug Fix#0001 — enemy dies mid-chain,
        // leftover reroll budget breaks the next combat with InvalidOperationException
        // on StartBudget).
        // -------------------------------------------------------------------

        [Test]
        public void OnCombatEnd_WithActiveRerollBudget_EndsTheBudget()
        {
            // Arrange: simulate the state at the moment the enemy dies mid-chain —
            // an active budget is open from the action that landed the killing blow.
            var spyBudget = new SpyRerollBudgetService();
            ServiceLocator.AddService<IRerollBudgetService>(spyBudget, ServiceScope.Global);

            var wrapper = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            wrapper.ActionId = "test.action";
            wrapper.FreeRollCount = 3;
            _createdObjects.Add(wrapper);
            spyBudget.StartBudget(wrapper);

            Assert.IsNotNull(spyBudget.Current, "pre-condition: budget must be open");

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);

            // Assert
            Assert.AreEqual(1, spyBudget.EndBudgetCallCount,
                "Combat end must end the active reroll budget so the next combat's " +
                "StartBudget does not throw InvalidOperationException.");
            Assert.IsNull(spyBudget.Current, "Current budget must be null after combat end");
        }

        [Test]
        public void OnCombatEnd_WithNoActiveBudget_DoesNotThrow()
        {
            var spyBudget = new SpyRerollBudgetService();
            ServiceLocator.AddService<IRerollBudgetService>(spyBudget, ServiceScope.Global);

            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory));
        }

        [Test]
        public void OnCombatEnd_NoBudgetServiceRegistered_DoesNotThrow()
        {
            // ServiceLocator stays empty for this service. Reset must tolerate that.
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory));
        }

        [Test]
        public void Dispose_UnsubscribesFromCombatEnd()
        {
            var spyBudget = new SpyRerollBudgetService();
            ServiceLocator.AddService<IRerollBudgetService>(spyBudget, ServiceScope.Global);

            var wrapper = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            wrapper.ActionId = "test.action";
            wrapper.FreeRollCount = 1;
            _createdObjects.Add(wrapper);
            spyBudget.StartBudget(wrapper);

            _service.Dispose();

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);

            Assert.AreEqual(0, spyBudget.EndBudgetCallCount,
                "After Dispose the OnCombatEnd handler must be unsubscribed.");
        }

        // -------------------------------------------------------------------
        // Regression PUL-016: el combate cierra con la entrada paga de una fase de
        // chain pendiente (el enemigo muere antes de que el player consuma todas las
        // fases). El reset apagaba el flag pero dejaba el prompt "… Roll (1E)" prendido,
        // y reaparecía en el combate siguiente encima del roll de ataque.
        // -------------------------------------------------------------------

        [Test]
        public void OnCombatEnd_WithChainPaidPromptVisible_HidesPrompt()
        {
            // Arrange — el prompt se prende SIN pasar por Show() a propósito: así no queda
            // suscripto al bus por su cuenta y el único que puede apagarlo es el reset del
            // servicio. Es lo que hace fallar este test contra el código viejo.
            var prompt = MakeHudWithChainPrompt();
            Assert.IsTrue(prompt.gameObject.activeSelf, "pre-condition: el prompt arranca visible");

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);

            // Assert
            Assert.IsFalse(prompt.gameObject.activeSelf,
                "El fin de combate debe apagar el prompt de roll pago; si no, se filtra al " +
                "combate siguiente porque desactivar el canvas no limpia el m_IsActive del hijo.");
        }

        [Test]
        public void OnCombatEnd_WithoutChainPromptWired_DoesNotThrow()
        {
            // Arrange — hud sin prompt wireado (el default del prefab).
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            _spyScreen.Current = hudGo.AddComponent<CombatHUDView>();

            // Act / Assert
            Assert.DoesNotThrow(() =>
                EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory));
        }

        /// <summary>
        /// CombatHUDView real con un ChainRollPromptView hijo ya visible, plantado como
        /// pantalla actual del screen manager. Devuelve el prompt.
        /// </summary>
        private ChainRollPromptView MakeHudWithChainPrompt()
        {
            var hudGo = new GameObject("CombatHUD");
            _createdObjects.Add(hudGo);
            var hud = hudGo.AddComponent<CombatHUDView>();

            var promptGo = new GameObject("ChainRollPrompt");
            promptGo.transform.SetParent(hudGo.transform);
            var prompt = promptGo.AddComponent<ChainRollPromptView>();

            SetPrivateField(hud, "_chainRollPrompt", prompt);
            _spyScreen.Current = hud;
            return prompt;
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
        // (bugs 2026-07-20: el shield roll debe consumir un roll sobrante, y
        // sin sobrantes la energía habilita la entrada paga en vez de cancelar)
        // -------------------------------------------------------------------

        [Test]
        public void ResolveChainPhaseEntry_ReturnsFree_WhenRollsRemain()
        {
            // Arrange / Act — los rolls sobrantes ganan, con o sin energía.
            var withEnergy = CombatHandoffService.ResolveChainPhaseEntry(
                remainingFreeRolls: 2, energy: 3, allowsEnergyReroll: true);
            var withoutEnergy = CombatHandoffService.ResolveChainPhaseEntry(
                remainingFreeRolls: 1, energy: 0, allowsEnergyReroll: false);

            // Assert
            Assert.AreEqual(CombatHandoffService.ChainPhaseEntry.Free, withEnergy);
            Assert.AreEqual(CombatHandoffService.ChainPhaseEntry.Free, withoutEnergy);
        }

        [Test]
        public void ResolveChainPhaseEntry_ReturnsPaid_WhenNoRollsButEnergyAndAllowed()
        {
            // Arrange / Act
            var entry = CombatHandoffService.ResolveChainPhaseEntry(
                remainingFreeRolls: 0, energy: 1, allowsEnergyReroll: true);

            // Assert
            Assert.AreEqual(CombatHandoffService.ChainPhaseEntry.Paid, entry);
        }

        [Test]
        public void ResolveChainPhaseEntry_ReturnsFinish_WhenNoRollsAndNoEnergy()
        {
            // Arrange / Act
            var entry = CombatHandoffService.ResolveChainPhaseEntry(
                remainingFreeRolls: 0, energy: 0, allowsEnergyReroll: true);

            // Assert
            Assert.AreEqual(CombatHandoffService.ChainPhaseEntry.Finish, entry);
        }

        [Test]
        public void ResolveChainPhaseEntry_ReturnsFinish_WhenNoRollsAndEnergyButRerollForbidden()
        {
            // Arrange / Act — la energía sola no habilita si el behavior prohíbe
            // energy-reroll (mismo gate que RerollBudgetService al cobrar).
            var entry = CombatHandoffService.ResolveChainPhaseEntry(
                remainingFreeRolls: 0, energy: 5, allowsEnergyReroll: false);

            // Assert
            Assert.AreEqual(CombatHandoffService.ChainPhaseEntry.Finish, entry);
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

        private sealed class CountingBudgetService : IRerollBudgetService
        {
            public int TryExtraRollCallCount { get; private set; }
            public RerollBudget Current => null;
#pragma warning disable 67
            public event Action<RerollStartedPayload> OnRerollStarted;
            public event Action<RerollBudget> OnBudgetStarted;
#pragma warning restore 67
            public void StartBudget(ActionDefinitionSO action) { }
            public void EndBudget() { }
            public RerollQueryResult QueryExtraRoll(Guid playerGuid) => RerollQueryResult.Free();
            public bool TryExtraRoll(Guid playerGuid) { TryExtraRollCallCount++; return true; }
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
        public void TryScheduleForcedFullHandReroll_ActiveHand_RerollsAllDiceWithoutConsumingBudget()
        {
            // Arrange
            var roller = ArmActiveHand();
            var budget = new CountingBudgetService();
            ServiceLocator.AddService<IRerollBudgetService>(budget);
            SetScheduler((d, cb) => cb());

            // Act
            var accepted = _service.TryScheduleForcedFullHandReroll(_stubPlayer.PlayerGuid);

            // Assert — mano completa re-tirada, gratis.
            Assert.IsTrue(accepted);
            Assert.AreEqual(1, roller.RerollCallCount);
            Assert.IsFalse(CombatHandoffService.AllDiceHeld(roller.LastKeep));
            foreach (var kept in roller.LastKeep)
                Assert.IsFalse(kept, "El forced reroll debe re-tirar TODOS los dados (keep all-false).");
            Assert.AreEqual(0, budget.TryExtraRollCallCount,
                "El reroll del Torpe no debe consumir budget ni energía.");
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
        public void EnergyReroll_ClassicMode_NothingSelected_RerollsWholeHandAndConsumesBudget()
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
            var budget = new CountingBudgetService();
            ServiceLocator.AddService<IRerollBudgetService>(budget);

            // Act — sin dice zone cableada GetCurrentKeep() es null = nada lockeado.
            hud.OnEnergyRerollRequested?.Invoke();

            // Assert — vuela toda la mano y el reroll SÍ consume budget.
            Assert.AreEqual(1, roller.RerollCallCount);
            foreach (var kept in roller.LastKeep)
                Assert.IsFalse(kept, "En clásico sin selección debe volar toda la mano.");
            Assert.AreEqual(1, budget.TryExtraRollCallCount);
        }

        [Test]
        public void EnergyReroll_ClassicMode_AllDiceSelected_BailsWithoutConsumingBudget()
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
            var budget = new CountingBudgetService();
            ServiceLocator.AddService<IRerollBudgetService>(budget);

            var zone = hudGo.AddComponent<Rollgeon.UI.HUD.DiceZoneView>();
            SetPrivateField(zone, "_heldStates", new[] { true, true, true });
            SetPrivateField(hud, "_diceZone", zone);

            // Act
            hud.OnEnergyRerollRequested?.Invoke();

            // Assert — guard defensivo: ni roller ni budget se tocan.
            Assert.AreEqual(0, roller.RerollCallCount);
            Assert.AreEqual(0, budget.TryExtraRollCallCount);
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
    }
}
