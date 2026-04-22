using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    [TestFixture]
    public class RoomLayoutTests
    {
        private readonly List<GameObject> _objects = new();

        private RoomLayout CreateLayout()
        {
            var go = new GameObject("TestRoom");
            _objects.Add(go);
            return go.AddComponent<RoomLayout>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void GetDoorSlot_Found_ReturnsSlot()
        {
            var layout = CreateLayout();
            var n = new DoorSlotRef { Direction = DoorDirection.North };
            var e = new DoorSlotRef { Direction = DoorDirection.East };
            layout.DoorSlots.Add(n);
            layout.DoorSlots.Add(e);

            Assert.AreSame(n, layout.GetDoorSlot(DoorDirection.North));
            Assert.AreSame(e, layout.GetDoorSlot(DoorDirection.East));
        }

        [Test]
        public void GetDoorSlot_Missing_ReturnsNull()
        {
            var layout = CreateLayout();
            layout.DoorSlots.Add(new DoorSlotRef { Direction = DoorDirection.North });

            Assert.IsNull(layout.GetDoorSlot(DoorDirection.South));
            Assert.IsNull(layout.GetDoorSlot(DoorDirection.West));
        }

        [Test]
        public void GetDoorSlot_NullEntriesSkipped()
        {
            var layout = CreateLayout();
            layout.DoorSlots.Add(null);
            var s = new DoorSlotRef { Direction = DoorDirection.South };
            layout.DoorSlots.Add(s);

            Assert.AreSame(s, layout.GetDoorSlot(DoorDirection.South));
        }

        [Test]
        public void GetOrigin_NoGridOrigin_ReturnsTransformPosition()
        {
            var layout = CreateLayout();
            layout.transform.position = new Vector3(3f, 0f, 5f);

            Assert.AreEqual(new Vector3(3f, 0f, 5f), layout.GetOrigin());
        }

        [Test]
        public void GetOrigin_WithGridOrigin_ReturnsOriginPosition()
        {
            var layout = CreateLayout();
            var originGo = new GameObject("Origin");
            originGo.transform.SetParent(layout.transform);
            originGo.transform.localPosition = new Vector3(1f, 0f, 2f);
            layout.GridOrigin = originGo.transform;

            Assert.AreEqual(new Vector3(1f, 0f, 2f), layout.GetOrigin());
        }

        [Test]
        public void DoorSlots_DefaultsToEmpty()
        {
            var layout = CreateLayout();
            Assert.IsNotNull(layout.DoorSlots);
            Assert.AreEqual(0, layout.DoorSlots.Count);
        }

        [Test]
        public void LocalBounds_DefaultsToUnitCube()
        {
            var layout = CreateLayout();
            Assert.AreEqual(Vector3.one, layout.LocalBounds.size);
        }
    }
}