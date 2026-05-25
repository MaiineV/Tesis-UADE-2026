using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Editor.Tools.RoomEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class RoomEditorDoorBinderTests
    {
        private readonly List<GameObject> _objects = new();

        private RoomLayout CreateLayout()
        {
            var go = new GameObject("TestRoom");
            _objects.Add(go);
            var layout = go.AddComponent<RoomLayout>();
            layout.LocalBounds = new Bounds(Vector3.zero, new Vector3(10f, 1f, 10f));
            return layout;
        }

        private DoorController CreateController(RoomLayout layout, string name, Vector3 worldPosition)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.transform.SetParent(layout.transform, worldPositionStays: true);
            go.transform.position = worldPosition;
            return go.AddComponent<DoorController>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void InferDirection_NorthOfBoundsCenter_ReturnsNorth()
        {
            // Arrange
            var layout = CreateLayout();

            // Act
            var dir = RoomEditorDoorBinder.InferDirection(layout, new Vector3(0f, 0f, 4f));

            // Assert
            Assert.AreEqual(DoorDirection.North, dir);
        }

        [Test]
        public void InferDirection_OffsetBoundsCenter_StillReferencesBoundsCenter()
        {
            // Arrange — bounds.center shifted, so world (0,0,0) is south of the center.
            var layout = CreateLayout();
            layout.LocalBounds = new Bounds(new Vector3(0f, 0f, 5f), new Vector3(10f, 1f, 10f));

            // Act
            var dir = RoomEditorDoorBinder.InferDirection(layout, Vector3.zero);

            // Assert
            Assert.AreEqual(DoorDirection.South, dir);
        }

        [Test]
        public void BindOnPlace_SetsControllerDirectionAndSpawnPointId()
        {
            // Arrange
            var layout = CreateLayout();
            var controller = CreateController(layout, "DoorEast", new Vector3(4f, 0f, 0f));

            // Act
            var dir = RoomEditorDoorBinder.BindOnPlace(layout, controller, controller.transform.position);

            // Assert
            Assert.AreEqual(DoorDirection.East, dir);
            Assert.AreEqual(DoorDirection.East, controller.Direction);
            Assert.AreEqual("door_E", controller.SpawnPointId);
        }

        [Test]
        public void BindOnPlace_AddsSlotIfMissing()
        {
            // Arrange
            var layout = CreateLayout();
            var controller = CreateController(layout, "DoorNorth", new Vector3(0f, 0f, 4f));

            // Act
            RoomEditorDoorBinder.BindOnPlace(layout, controller, controller.transform.position);

            // Assert
            var slot = layout.GetDoorSlot(DoorDirection.North);
            Assert.IsNotNull(slot);
            Assert.AreSame(controller.gameObject, slot.DoorRoot);
            Assert.AreSame(controller.transform, slot.Anchor);
        }

        [Test]
        public void BindOnPlace_DuplicateDirection_DestroysOldDoorAndLogsWarning()
        {
            // Arrange
            var layout = CreateLayout();
            var first  = CreateController(layout, "FirstNorth",  new Vector3(0f, 0f, 4f));
            RoomEditorDoorBinder.BindOnPlace(layout, first, first.transform.position);
            var second = CreateController(layout, "SecondNorth", new Vector3(0f, 0f, 4f));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("replacing door at North"));

            // Act
            RoomEditorDoorBinder.BindOnPlace(layout, second, second.transform.position);

            // Assert
            Assert.IsTrue(first == null, "old door GameObject should have been destroyed");
            Assert.AreEqual(1, layout.DoorSlots.Count);
            Assert.AreSame(second.gameObject, layout.GetDoorSlot(DoorDirection.North).DoorRoot);
        }

        [Test]
        public void UpsertSlot_ExistingSlotPointingAtSameController_UpdatesAnchorWithoutDestroy()
        {
            // Arrange — slot is already pointing at the controller (e.g. direction edit re-binds).
            var layout = CreateLayout();
            var controller = CreateController(layout, "DoorWest", new Vector3(-4f, 0f, 0f));
            RoomEditorDoorBinder.BindOnPlace(layout, controller, controller.transform.position);

            // Act — call upsert directly, no warning expected since DoorRoot already matches.
            RoomEditorDoorBinder.UpsertSlot(layout, controller, DoorDirection.West);

            // Assert
            Assert.IsTrue(controller != null, "controller should not have been destroyed");
            Assert.AreEqual(1, layout.DoorSlots.Count);
        }

        [Test]
        public void RemoveSlot_MatchingDirection_RemovesAndReturnsTrue()
        {
            // Arrange
            var layout = CreateLayout();
            var controller = CreateController(layout, "DoorSouth", new Vector3(0f, 0f, -4f));
            RoomEditorDoorBinder.BindOnPlace(layout, controller, controller.transform.position);

            // Act
            var removed = RoomEditorDoorBinder.RemoveSlot(layout, DoorDirection.South);

            // Assert
            Assert.IsTrue(removed);
            Assert.IsNull(layout.GetDoorSlot(DoorDirection.South));
        }

        [Test]
        public void RemoveSlot_NoMatchingDirection_ReturnsFalse()
        {
            // Arrange
            var layout = CreateLayout();

            // Act
            var removed = RoomEditorDoorBinder.RemoveSlot(layout, DoorDirection.East);

            // Assert
            Assert.IsFalse(removed);
        }
    }
}
