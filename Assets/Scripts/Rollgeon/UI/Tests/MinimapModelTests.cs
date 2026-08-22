using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Modelo puro del minimapa: fog of war (visitadas + vecinas conectadas a una
    /// visitada — <see cref="RoomDiscovery"/>) y offsets relativos a la sala actual.
    /// </summary>
    [TestFixture]
    public class MinimapModelTests
    {
        private readonly List<ScriptableObject> _createdObjects = new List<ScriptableObject>();
        private Dictionary<Guid, RoomInstance> _rooms;

        [SetUp]
        public void SetUp()
        {
            _rooms = new Dictionary<Guid, RoomInstance>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _createdObjects)
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _createdObjects.Clear();
        }

        [Test]
        public void Build_CurrentRoom_IsCellZeroAndCurrent()
        {
            // Arrange
            var current = AddRoom(new Vector2Int(2, 3), RoomType.Start, visited: true);

            // Act
            var cells = MinimapModel.Build(_rooms, current);

            // Assert
            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(Vector2Int.zero, cells[0].Offset);
            Assert.IsTrue(cells[0].IsCurrent);
            Assert.IsTrue(cells[0].IsVisited);
        }

        [Test]
        public void Build_AdjacentConnectedToVisited_IsDiscoveredWithOffset()
        {
            // Arrange — actual visitada en (2,3); vecina Este sin visitar conectada.
            var current = AddRoom(new Vector2Int(2, 3), RoomType.Start, visited: true);
            var east = AddRoom(new Vector2Int(3, 3), RoomType.Combat, visited: false);
            Connect(east, DoorDirection.West, current);

            // Act
            var cells = MinimapModel.Build(_rooms, current);

            // Assert — offset East = (+1, 0), sin visitar.
            Assert.AreEqual(2, cells.Count);
            var eastCell = cells.Single(c => !c.IsCurrent);
            Assert.AreEqual(new Vector2Int(1, 0), eastCell.Offset);
            Assert.IsFalse(eastCell.IsVisited);
        }

        [Test]
        public void Build_UnconnectedUnvisitedRoom_IsHidden()
        {
            // Arrange — sala lejana sin conexión a nada visitado: fog of war la tapa.
            var current = AddRoom(new Vector2Int(0, 0), RoomType.Start, visited: true);
            AddRoom(new Vector2Int(4, 4), RoomType.Combat, visited: false);

            // Act
            var cells = MinimapModel.Build(_rooms, current);

            // Assert
            Assert.AreEqual(1, cells.Count);
            Assert.IsTrue(cells[0].IsCurrent);
        }

        [Test]
        public void Build_VisitedFarRoom_StaysVisibleWithRelativeOffset()
        {
            // Arrange — una sala ya visitada queda visible aunque no sea adyacente
            // a la actual (memoria del recorrido, como Isaac).
            var current = AddRoom(new Vector2Int(2, 2), RoomType.Combat, visited: true);
            AddRoom(new Vector2Int(0, 0), RoomType.Combat, visited: true);

            // Act
            var cells = MinimapModel.Build(_rooms, current);

            // Assert
            Assert.AreEqual(2, cells.Count);
            var far = cells.Single(c => !c.IsCurrent);
            Assert.AreEqual(new Vector2Int(-2, -2), far.Offset);
            Assert.IsTrue(far.IsVisited);
        }

        [Test]
        public void Build_RoomType_ComesFromTemplate_WithCombatFallback()
        {
            // Arrange — boss adyacente sin visitar (debe revelar su tipo, spec) y una
            // sala sin template (defensivo) que cae a Combat.
            var current = AddRoom(new Vector2Int(0, 0), RoomType.Start, visited: true);
            var boss = AddRoom(new Vector2Int(0, 1), RoomType.Boss, visited: false);
            Connect(boss, DoorDirection.South, current);
            var noTemplateId = Guid.NewGuid();
            _rooms[noTemplateId] = new RoomInstance
            {
                InstanceId = noTemplateId,
                GridCell = new Vector2Int(1, 0),
                Visited = true,
                Template = null,
            };

            // Act
            var cells = MinimapModel.Build(_rooms, current);

            // Assert
            Assert.AreEqual(RoomType.Boss, cells.Single(c => c.Offset == new Vector2Int(0, 1)).Type);
            Assert.AreEqual(RoomType.Combat, cells.Single(c => c.Offset == new Vector2Int(1, 0)).Type);
        }

        [Test]
        public void Build_UnknownCurrentId_ReturnsEmpty()
        {
            // Arrange
            AddRoom(new Vector2Int(0, 0), RoomType.Start, visited: true);

            // Act / Assert — dungeon sin generar o id viejo de otro piso: lista vacía.
            Assert.IsEmpty(MinimapModel.Build(_rooms, Guid.NewGuid()));
            Assert.IsEmpty(MinimapModel.Build(null, Guid.NewGuid()));
        }

        // ----- Helpers ---------------------------------------------------------

        private Guid AddRoom(Vector2Int cell, RoomType type, bool visited)
        {
            var template = ScriptableObject.CreateInstance<RoomSO>();
            template.Type = type;
            _createdObjects.Add(template);

            var id = Guid.NewGuid();
            _rooms[id] = new RoomInstance
            {
                InstanceId = id,
                GridCell = cell,
                Visited = visited,
                Template = template,
            };
            return id;
        }

        private void Connect(Guid from, DoorDirection dir, Guid to)
            => _rooms[from].Connections[dir] = to;
    }
}
