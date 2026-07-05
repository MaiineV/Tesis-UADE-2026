using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Dungeon;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Tutorial.Tests
{
    [TestFixture]
    public class TutorialFloorPlanSOTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        private RoomSO CreateRoom(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.Type = type;
            _createdObjects.Add(room);
            return room;
        }

        private TutorialFloorPlanSO CreatePlan(params (Vector2Int cell, RoomSO room)[] entries)
        {
            var so = ScriptableObject.CreateInstance<TutorialFloorPlanSO>();
            _createdObjects.Add(so);
            so.SetEntries(entries
                .Select(e => new TutorialFloorPlanSO.Entry { Cell = e.cell, Room = e.room })
                .ToList());
            return so;
        }

        [Test]
        public void ToPlan_ValidTutorialLayout_MapsCellsTypesAndAssignments()
        {
            var start = CreateRoom("start", RoomType.Start);
            var combat = CreateRoom("combat", RoomType.Combat);
            var shop = CreateRoom("shop", RoomType.Shop);
            var so = CreatePlan(
                (new Vector2Int(0, 0), start),
                (new Vector2Int(0, 1), combat),
                (new Vector2Int(-1, 1), shop));

            var plan = so.ToPlan();

            Assert.AreEqual(3, plan.Cells.Count);
            Assert.AreEqual(3, plan.TargetCount);
            Assert.AreSame(start, plan.Assignments[Vector2Int.zero]);
            Assert.AreEqual(RoomType.Shop, plan.Types[new Vector2Int(-1, 1)]);
            Assert.AreEqual(1, plan.ResolvedCounts[RoomType.Combat]);
        }

        [Test]
        public void ToPlan_EmptyEntries_Throws()
        {
            var so = CreatePlan();

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_NullRoom_Throws()
        {
            var so = CreatePlan((Vector2Int.zero, null));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_DuplicateCell_Throws()
        {
            var start = CreateRoom("start", RoomType.Start);
            var combat = CreateRoom("combat", RoomType.Combat);
            var so = CreatePlan(
                (Vector2Int.zero, start),
                (Vector2Int.zero, combat));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_StartNotAtOrigin_Throws()
        {
            var start = CreateRoom("start", RoomType.Start);
            var combat = CreateRoom("combat", RoomType.Combat);
            var so = CreatePlan(
                (new Vector2Int(0, 1), start),
                (Vector2Int.zero, combat));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_TwoStartRooms_Throws()
        {
            var startA = CreateRoom("start_a", RoomType.Start);
            var startB = CreateRoom("start_b", RoomType.Start);
            var so = CreatePlan(
                (Vector2Int.zero, startA),
                (new Vector2Int(0, 1), startB));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_NoStartRoom_Throws()
        {
            var combat = CreateRoom("combat", RoomType.Combat);
            var so = CreatePlan((Vector2Int.zero, combat));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_DisconnectedCell_Throws()
        {
            var start = CreateRoom("start", RoomType.Start);
            var combat = CreateRoom("combat", RoomType.Combat);
            var so = CreatePlan(
                (Vector2Int.zero, start),
                (new Vector2Int(5, 5), combat));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }

        [Test]
        public void ToPlan_DiagonalNeighbor_ThrowsBecauseNotFourAdjacent()
        {
            var start = CreateRoom("start", RoomType.Start);
            var combat = CreateRoom("combat", RoomType.Combat);
            var so = CreatePlan(
                (Vector2Int.zero, start),
                (new Vector2Int(1, 1), combat));

            Assert.Throws<InvalidOperationException>(() => so.ToPlan());
        }
    }
}
