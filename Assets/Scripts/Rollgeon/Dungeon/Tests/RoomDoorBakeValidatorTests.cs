using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.EditorTools;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Cubre <see cref="RoomDoorBakeValidator"/> (Fix#0013): cada puerta debe tener su
    /// tile-frente caminable en el NavGraph horneado, si no el cruce en Exploración no
    /// ofrece casilla. El validador solo avisa, no toca nada.
    /// </summary>
    [TestFixture]
    public class RoomDoorBakeValidatorTests
    {
        private readonly List<GameObject> _objects = new();

        // Sala en el origen, tileSize 1 → WorldToGrid = floor(world.xz). NavGraph.Rect(6,6)
        // expone nodos (0..5, 0..5).
        private RoomLayout CreateRoom(NavGraph graph)
        {
            var go = new GameObject("TestRoom");
            _objects.Add(go);
            var layout = go.AddComponent<RoomLayout>();
            layout.TileSize = 1f;
            layout.NavGraph = graph;
            return layout;
        }

        private DoorController CreateDoor(RoomLayout layout, string name, Vector3 worldPosition, DoorDirection dir)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.transform.SetParent(layout.transform, worldPositionStays: true);
            go.transform.position = worldPosition;
            var ctrl = go.AddComponent<DoorController>();
            ctrl.Direction = dir;
            return ctrl;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void Validate_DoorFrontIsWalkableFloor_NoWarnings()
        {
            // Arrange — puerta Sur en (3,0); tile-frente = (3,0)+(0,1) = (3,1) ∈ Rect(6,6).
            var layout = CreateRoom(NavGraph.Rect(6, 6));
            CreateDoor(layout, "DoorSouth", new Vector3(3.5f, 0f, 0.5f), DoorDirection.South);

            // Act
            var findings = RoomDoorBakeValidator.ValidateRoom(layout);

            // Assert
            Assert.AreEqual(0, findings.Count, string.Join(" | ", findings));
        }

        [Test]
        public void Validate_DoorFrontMissingFloor_ReturnsActionableWarning()
        {
            // Arrange — puerta Sur en (3,5); tile-frente = (3,6) fuera de Rect(6,6).
            var layout = CreateRoom(NavGraph.Rect(6, 6));
            CreateDoor(layout, "DoorSouth", new Vector3(3.5f, 0f, 5.5f), DoorDirection.South);

            // Act
            var findings = RoomDoorBakeValidator.ValidateRoom(layout);

            // Assert
            Assert.AreEqual(1, findings.Count);
            Assert.That(findings[0], Does.Contain("South"));
            Assert.That(findings[0], Does.Contain("(3,6)"));
        }

        [Test]
        public void Validate_EmptyNavGraph_FlaggedSeparately()
        {
            // Arrange — graph vacío: HasNode siempre true, no se puede validar tile-frente.
            var layout = CreateRoom(new NavGraph());
            CreateDoor(layout, "DoorSouth", new Vector3(3.5f, 0f, 5.5f), DoorDirection.South);

            // Act
            var findings = RoomDoorBakeValidator.ValidateRoom(layout);

            // Assert
            Assert.AreEqual(1, findings.Count);
            Assert.That(findings[0], Does.Contain("vacío"));
        }
    }
}
