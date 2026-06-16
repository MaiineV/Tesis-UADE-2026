using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Editor.Tools.RoomEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Regresión de RC1 (Fix#0013): Auto-Populate Door Slots infería la dirección relativa
    /// al ORIGEN del prefab en vez de a <see cref="RoomLayout.LocalBounds"/>.center. Con
    /// salas cuyo origen no es el centro (pivot en la esquina), eso clasificaba casi todas
    /// las puertas como N/E. Estos tests fijan que respete bounds-center y que coincida con
    /// el path del Room Editor (binder).
    /// </summary>
    [TestFixture]
    public class RoomLayoutAutoPopulateTests
    {
        private readonly List<GameObject> _objects = new();

        private RoomLayout CreateLayout(Bounds localBounds)
        {
            var go = new GameObject("TestRoom");
            _objects.Add(go);
            var layout = go.AddComponent<RoomLayout>();
            layout.LocalBounds = localBounds;
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
        public void AutoPopulate_DoorSouthOfOffsetBoundsCenter_InfersSouth()
        {
            // Arrange — centro de la sala en z=5 (origen en la esquina). La puerta en z=2 está
            // al SUR del centro, aunque relativo al origen daría norte (el bug original).
            var layout = CreateLayout(new Bounds(new Vector3(0f, 0f, 5f), new Vector3(10f, 1f, 10f)));
            var controller = CreateController(layout, "DoorSouth", new Vector3(0f, 0f, 2f));

            // Act
            layout.AutoPopulateDoorSlots();

            // Assert
            Assert.AreEqual(DoorDirection.South, controller.Direction);
            Assert.IsNotNull(layout.GetDoorSlot(DoorDirection.South));
        }

        [Test]
        public void AutoPopulate_SetsAnchorToControllerTransform()
        {
            // Arrange
            var layout = CreateLayout(new Bounds(Vector3.zero, new Vector3(10f, 1f, 10f)));
            var controller = CreateController(layout, "DoorEast", new Vector3(4f, 0f, 0f));

            // Act
            layout.AutoPopulateDoorSlots();

            // Assert
            var slot = layout.GetDoorSlot(DoorDirection.East);
            Assert.IsNotNull(slot);
            Assert.AreSame(controller.transform, slot.Anchor);
            Assert.AreSame(controller.gameObject, slot.DoorRoot);
        }

        [Test]
        public void AutoPopulate_SetsSpawnPointIdFromDirection()
        {
            // Arrange
            var layout = CreateLayout(new Bounds(Vector3.zero, new Vector3(10f, 1f, 10f)));
            var controller = CreateController(layout, "DoorNorth", new Vector3(0f, 0f, 4f));

            // Act
            layout.AutoPopulateDoorSlots();

            // Assert
            Assert.AreEqual(DoorDirection.North, controller.Direction);
            Assert.AreEqual("door_N", controller.SpawnPointId);
        }

        [Test]
        public void AutoPopulate_AndBinder_AgreeOnDirectionAndAnchor()
        {
            // Arrange — mismo controller resuelto por los dos paths de autoría.
            var layout = CreateLayout(new Bounds(new Vector3(0f, 0f, 5f), new Vector3(10f, 1f, 10f)));
            var controller = CreateController(layout, "DoorWest", new Vector3(-4f, 0f, 5f));

            // Act
            layout.AutoPopulateDoorSlots();
            var binderDir = RoomEditorDoorBinder.InferDirection(layout, controller.transform.position);

            // Assert
            var slot = layout.GetDoorSlot(DoorDirection.West);
            Assert.AreEqual(DoorDirection.West, binderDir);
            Assert.IsNotNull(slot);
            Assert.AreEqual(binderDir, slot.Direction);
            Assert.AreSame(controller.transform, slot.Anchor);
        }
    }
}
