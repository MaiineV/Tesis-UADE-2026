using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Exploration;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="RoomNavigationView"/> (UI#0011d).
    /// Verifies Bind/Unbind lifecycle, label refresh, button wiring,
    /// and event-driven UI updates.
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
            public int CurrentRoomIndex { get; set; }
            public int RoomCount { get; set; }
            public bool IsLastRoom { get; set; }
            public int NextRoomCallCount { get; private set; }

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }

            public bool NextRoom()
            {
                NextRoomCallCount++;
                return true;
            }

            public IReadOnlyList<RoomSO> GetFloorRooms() => Array.Empty<RoomSO>();
        }

        private class StubExplorationController : IExplorationController
        {
            public bool IsExploring { get; set; } = true;
            public int AdvanceRoomCallCount { get; private set; }

            public void BeginExploration() { }

            public bool AdvanceRoom()
            {
                AdvanceRoomCallCount++;
                return true;
            }
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

            // Labels
            _roomNameLabel = CreateChildTMP("RoomNameLabel");
            _roomProgressLabel = CreateChildTMP("RoomProgressLabel");
            _roomTypeLabel = CreateChildTMP("RoomTypeLabel");

            // Buttons
            _proceedButton = CreateChildButton("ProceedButton");
            _pauseButton = CreateChildButton("PauseButton");

            // Wire via reflection
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
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 8;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.AreEqual("Gran Salon", _roomNameLabel.text);
            Assert.AreEqual("Room 1/8", _roomProgressLabel.text);
            Assert.AreEqual("Start", _roomTypeLabel.text);
        }

        [Test]
        public void Bind_WithNoDungeonService_DegradesGracefully()
        {
            // No services registered — should not throw, labels show fallback.
            Assert.DoesNotThrow(() => _view.Bind(Guid.NewGuid()));

            Assert.AreEqual("???", _roomNameLabel.text);
            Assert.AreEqual("Room ?/?", _roomProgressLabel.text);
            Assert.AreEqual("", _roomTypeLabel.text);
        }

        [Test]
        public void Bind_WithNoExplorationController_DegradesGracefully()
        {
            var room = CreateRoom("room_01", "Room A", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 4;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            // No IExplorationController registered.

            Assert.DoesNotThrow(() => _view.Bind(Guid.NewGuid()));

            // Proceed should be disabled because _exploration is null.
            Assert.IsFalse(_proceedButton.interactable);
        }

        [Test]
        public void RefreshRoomInfo_UpdatesLabelsFromDungeon()
        {
            var room = CreateRoom("shop_01", "Bazar Magico", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 3;
            _stubDungeon.RoomCount = 10;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            // Change room and refresh
            var room2 = CreateRoom("potion_01", "Sala de Pociones", RoomType.Potion);
            _stubDungeon.CurrentRoom = room2;
            _stubDungeon.CurrentRoomIndex = 4;

            _view.RefreshRoomInfo();

            Assert.AreEqual("Sala de Pociones", _roomNameLabel.text);
            Assert.AreEqual("Room 5/10", _roomProgressLabel.text);
            Assert.AreEqual("Potion", _roomTypeLabel.text);
        }

        [Test]
        public void RefreshRoomInfo_ProgressFormat()
        {
            var room = CreateRoom("start_0", "Start", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 8;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.AreEqual("Room 1/8", _roomProgressLabel.text,
                "Progress label must follow 'Room {index+1}/{count}' format.");
        }

        [Test]
        public void ProceedButton_CallsAdvanceRoom()
        {
            var room = CreateRoom("shop_01", "Shop", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 1;
            _stubDungeon.RoomCount = 5;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            _proceedButton.onClick.Invoke();

            Assert.AreEqual(1, _stubExploration.AdvanceRoomCallCount,
                "Clicking proceed should call AdvanceRoom once.");
        }

        [Test]
        public void ProceedButton_DisabledWhenNotExploring()
        {
            var room = CreateRoom("shop_01", "Shop", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 4;
            _stubExploration.IsExploring = false;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());

            Assert.IsFalse(_proceedButton.interactable,
                "Proceed must be disabled when IsExploring is false.");
        }

        [Test]
        public void OnRoomEntered_RefreshesUI()
        {
            var room = CreateRoom("start_0", "Start", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 5;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            Assert.AreEqual("Start", _roomNameLabel.text);

            // Simulate advancing
            var room2 = CreateRoom("shop_01", "Tienda", RoomType.Shop);
            _stubDungeon.CurrentRoom = room2;
            _stubDungeon.CurrentRoomIndex = 1;

            EventManager.Trigger(EventName.OnRoomEntered);

            Assert.AreEqual("Tienda", _roomNameLabel.text);
            Assert.AreEqual("Room 2/5", _roomProgressLabel.text);
        }

        [Test]
        public void OnCombatTriggered_DisablesProceed()
        {
            var room = CreateRoom("combat_01", "Arena", RoomType.Shop);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 4;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            Assert.IsTrue(_proceedButton.interactable, "Should be interactable before combat.");

            EventManager.Trigger(EventName.OnCombatTriggered);

            Assert.IsFalse(_proceedButton.interactable,
                "Proceed must be disabled when combat is triggered.");
        }

        [Test]
        public void Unbind_UnsubscribesEvents()
        {
            var room = CreateRoom("start_0", "Start", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 5;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            _view.Unbind();

            // Change data and trigger event — labels should NOT update.
            var room2 = CreateRoom("shop_01", "Tienda", RoomType.Shop);
            _stubDungeon.CurrentRoom = room2;
            _stubDungeon.CurrentRoomIndex = 1;

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
        public void OnFloorCleared_UpdatesProgressLabelAndDisablesProceed()
        {
            var room = CreateRoom("start_0", "Start", RoomType.Start);
            _stubDungeon.CurrentRoom = room;
            _stubDungeon.CurrentRoomIndex = 0;
            _stubDungeon.RoomCount = 5;

            ServiceLocator.AddService<IDungeonService>(_stubDungeon, ServiceScope.Run);
            ServiceLocator.AddService<IExplorationController>(_stubExploration, ServiceScope.Run);

            _view.Bind(Guid.NewGuid());
            Assert.IsTrue(_proceedButton.interactable, "Should be interactable before floor cleared.");

            EventManager.Trigger(EventName.OnFloorCleared);

            Assert.AreEqual("Floor Cleared!", _roomProgressLabel.text,
                "Progress label must show 'Floor Cleared!' after OnFloorCleared.");
            Assert.IsFalse(_proceedButton.interactable,
                "Proceed must be disabled after floor cleared.");
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

        private static T GetPrivate<T>(object target, string fieldName) where T : class
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
            return field.GetValue(target) as T;
        }
    }
}
