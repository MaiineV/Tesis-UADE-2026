using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Exploration;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests para <see cref="RoomNavigationView"/>. Post-§13.6 el
    /// widget ya no tiene botón Proceed (la transición la dispara el player
    /// cruzando puertas). Solo refresca labels ante eventos de room/combat.
    /// </summary>
    [TestFixture]
    public class RoomNavigationViewTests
    {
        // -------------------------------------------------------------------
        // Stubs
        // -------------------------------------------------------------------

        private class StubDungeonService : IDungeonService
        {
            public RoomSO CurrentRoom { get; set; }
            public RoomInstance CurrentRoomInstance { get; set; }
            public Dictionary<Guid, RoomInstance> Instances = new();

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => Instances;
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();

            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id)
            {
                id = Guid.Empty;
                return false;
            }

            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;

            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private class StubExplorationController : IExplorationController
        {
            public bool IsExploring { get; set; } = true;
            public void BeginExploration() { }
            public void ResumeAfterCombat() { }
        }

        // -------------------------------------------------------------------
        // Fields
        // -------------------------------------------------------------------

        private GameObject _viewGO;
        private RoomNavigationView _view;
        private TextMeshProUGUI _roomNameLabel;
        private TextMeshProUGUI _roomProgressLabel;
        private TextMeshProUGUI _roomTypeLabel;
        private Button _proceedButton;
        private Button _pauseButton;
        private StubDungeonService _stubDungeon;
        private StubExplorationController _stubExploration;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        // -------------------------------------------------------------------
        // Setup / Teardown
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _stubDungeon = new StubDungeonService();
            _stubExploration = new StubExplorationController();

            _viewGO = new GameObject("RoomNavigationView");
            _viewGO.SetActive(false);
            _view = _viewGO.AddComponent<RoomNavigationView>();

            _roomNameLabel = CreateChildTMP("RoomNameLabel");
            _roomProgressLabel = CreateChildTMP("RoomProgressLabel");
            _roomTypeLabel = CreateChildTMP("RoomTypeLabel");

            _proceedButton = CreateChildButton("ProceedButton");
            _pauseButton = CreateChildButton("PauseButton");

            AssignPrivate(_view, "_roomNameLabel", _roomNameLabel);
            AssignPrivate(_view, "_roomProgressLabel", _roomProgressLabel);
            AssignPrivate(_view, "_roomTypeLabel", _roomTypeLabel);
            AssignPrivate(_view, "_proceedButton", _proceedButton);
            AssignPrivate(_view, "_pauseButton", _pauseButton);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();

            if (_viewGO != null) UnityEngine.Object.DestroyImmediate(_viewGO);

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void Bind_ResolvesServicesAndRefreshesLabels()
        {
            var room = CreateRoom("hall_01", "Gran Salon", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            SeedInstances(cleared: 1, total: 8);

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.AreEqual("Gran Salon", _roomNameLabel.text);
            Assert.AreEqual("Rooms 1/8", _roomProgressLabel.text);
            Assert.AreEqual("Start", _roomTypeLabel.text);
        }

        [Test]
        public void Bind_WithNoDungeonService_DegradesGracefully()
        {
            Assert.DoesNotThrow(() => _view.Bind(Guid.NewGuid()));

            Assert.AreEqual("???", _roomNameLabel.text);
            Assert.AreEqual("Rooms ?/?", _roomProgressLabel.text);
            Assert.AreEqual("", _roomTypeLabel.text);
        }

        [Test]
        public void RefreshRoomInfo_UpdatesLabelsFromDungeon()
        {
            var room = CreateRoom("shop_01", "Bazar Magico", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            SeedInstances(cleared: 3, total: 10);

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            var room2 = CreateRoom("potion_01", "Sala de Pociones", RoomType.Potion);
            _stubDungeon.CurrentRoom = room2;
            SeedInstances(cleared: 4, total: 10);

            _view.RefreshRoomInfo();

            Assert.AreEqual("Sala de Pociones", _roomNameLabel.text);
            Assert.AreEqual("Rooms 4/10", _roomProgressLabel.text);
            Assert.AreEqual("Potion", _roomTypeLabel.text);
        }

        [Test]
        public void ProceedButton_AlwaysDisabled_AfterBind()
        {
            var room = CreateRoom("shop_01", "Shop", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            SeedInstances(cleared: 1, total: 5);

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.IsFalse(_proceedButton.interactable,
                "Proceed fue deprecado — siempre deshabilitado tras §13.6.");
        }

        [Test]
        public void OnRoomEntered_RefreshesUI()
        {
            var room = CreateRoom("start_0", "Start", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            SeedInstances(cleared: 1, total: 5);

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            Assert.AreEqual("Start", _roomNameLabel.text);

            var room2 = CreateRoom("shop_01", "Tienda", RoomType.Shop);
            _stubDungeon.CurrentRoom = room2;
            SeedInstances(cleared: 2, total: 5);

            EventManager.Trigger(EventName.OnRoomEntered);

            Assert.AreEqual("Tienda", _roomNameLabel.text);
            Assert.AreEqual("Rooms 2/5", _roomProgressLabel.text);
        }

        [Test]
        public void OnRoomCleared_RefreshesProgressLabel()
        {
            var room = CreateRoom("combat_01", "Arena", RoomType.Combat);
            _stubDungeon.CurrentRoom = room;
            SeedInstances(cleared: 0, total: 4);

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            Assert.AreEqual("Rooms 0/4", _roomProgressLabel.text);

            SeedInstances(cleared: 1, total: 4);
            EventManager.Trigger(EventName.OnRoomCleared);

            Assert.AreEqual("Rooms 1/4", _roomProgressLabel.text);
        }

        [Test]
        public void Unbind_UnsubscribesEvents()
        {
            var room = CreateRoom("start_0", "Start", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            SeedInstances(cleared: 1, total: 5);

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            _view.Unbind();

            var room2 = CreateRoom("shop_01", "Tienda", RoomType.Shop);
            _stubDungeon.CurrentRoom = room2;

            EventManager.Trigger(EventName.OnRoomEntered);

            Assert.AreEqual("Start", _roomNameLabel.text,
                "After Unbind, OnRoomEntered must not refresh labels.");
        }

        [Test]
        public void Unbind_Idempotent()
        {
            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.DoesNotThrow(() =>
            {
                _view.Unbind();
                _view.Unbind();
            }, "Calling Unbind twice must not throw.");
        }

        [Test]
        public void PauseButton_DoesNotCrash()
        {
            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.DoesNotThrow(() => _pauseButton.onClick.Invoke(),
                "Clicking pause must not throw (stub behavior).");
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void SeedInstances(int cleared, int total)
        {
            _stubDungeon.Instances.Clear();
            for (int i = 0; i < total; i++)
            {
                var ri = new RoomInstance
                {
                    InstanceId = Guid.NewGuid(),
                    State = i < cleared ? RoomState.Cleared : RoomState.Uncleared,
                };
                _stubDungeon.Instances[ri.InstanceId] = ri;
            }
        }

        private RoomSO CreateRoom(string id, string displayName, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = displayName;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        private TextMeshProUGUI CreateChildTMP(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_viewGO.transform, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private Button CreateChildButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_viewGO.transform, false);
            return go.AddComponent<Button>();
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
