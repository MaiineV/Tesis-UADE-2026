using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Editor.Tools.RoomEditor;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor.Tests
{
    [TestFixture]
    public class WallOccluderOpsTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private RoomLayout MakeLayout()
        {
            var go = new GameObject("Room");
            _created.Add(go);
            return go.AddComponent<RoomLayout>();
        }

        private GameObject MakeTile(RoomLayout layout, int x, int z, TileType type)
        {
            var go = new GameObject($"Tile_{x}_{z}_{type}");
            go.transform.SetParent(layout.transform);
            var marker = go.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(x, z);
            marker.Layer = 0;
            marker.Type = type;
            marker.IsBlocker = type == TileType.Wall;
            return go;
        }

        // -----------------------------------------------------------------
        // InferDirection — pure function, 8 octants
        // -----------------------------------------------------------------

        [TestCase(  0,  1, WallDirection.N)]
        [TestCase(  1,  1, WallDirection.NE)]
        [TestCase(  1,  0, WallDirection.E)]
        [TestCase(  1, -1, WallDirection.SE)]
        [TestCase(  0, -1, WallDirection.S)]
        [TestCase( -1, -1, WallDirection.SW)]
        [TestCase( -1,  0, WallDirection.W)]
        [TestCase( -1,  1, WallDirection.NW)]
        public void should_quantize_to_correct_octant_when_InferDirection_called(int dx, int dz, WallDirection expected)
        {
            // Arrange
            var center = new Vector3(0f, 0f, 0f);
            var cell = new Vector3Int(dx, 0, dz);

            // Act
            var actual = WallOccluderOps.InferDirection(cell, center);

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void should_return_north_when_InferDirection_called_with_cell_at_center()
        {
            // Arrange
            var center = new Vector3(3f, 0f, 5f);
            var cell = new Vector3Int(3, 0, 5);

            // Act
            var actual = WallOccluderOps.InferDirection(cell, center);

            // Assert
            Assert.AreEqual(WallDirection.N, actual);
        }

        [Test]
        public void should_use_offset_center_when_InferDirection_called_with_nonzero_center()
        {
            // Room centered at (5, 0, 5). Cell at (5, 0, 10) is north of center.
            // Arrange
            var center = new Vector3(5f, 0f, 5f);
            var cell = new Vector3Int(5, 0, 10);

            // Act
            var actual = WallOccluderOps.InferDirection(cell, center);

            // Assert
            Assert.AreEqual(WallDirection.N, actual);
        }

        // -----------------------------------------------------------------
        // ComputeRoomCenterCell
        // -----------------------------------------------------------------

        [Test]
        public void should_return_zero_when_ComputeRoomCenterCell_called_on_empty_room()
        {
            // Arrange
            var layout = MakeLayout();

            // Act
            var center = WallOccluderOps.ComputeRoomCenterCell(layout);

            // Assert
            Assert.AreEqual(Vector3.zero, center);
        }

        [Test]
        public void should_average_floor_cells_when_ComputeRoomCenterCell_called_with_floors_and_walls()
        {
            // Arrange
            // 3x3 floor in cells (0..2, 0..2) → expected center (1, _, 1)
            var layout = MakeLayout();
            for (int x = 0; x <= 2; x++)
                for (int z = 0; z <= 2; z++)
                    MakeTile(layout, x, z, TileType.Floor);
            // Walls scattered far away — must be ignored.
            MakeTile(layout, 100, 100, TileType.Wall);
            MakeTile(layout, -50, -50, TileType.Wall);

            // Act
            var center = WallOccluderOps.ComputeRoomCenterCell(layout);

            // Assert
            Assert.AreEqual(1f, center.x, 1e-4f);
            Assert.AreEqual(1f, center.z, 1e-4f);
        }

        [Test]
        public void should_average_all_markers_when_ComputeRoomCenterCell_called_with_no_floors()
        {
            // Arrange — only walls. Center should be the centroid of all walls.
            var layout = MakeLayout();
            MakeTile(layout, 0, 0, TileType.Wall);
            MakeTile(layout, 4, 0, TileType.Wall);
            MakeTile(layout, 0, 4, TileType.Wall);
            MakeTile(layout, 4, 4, TileType.Wall);

            // Act
            var center = WallOccluderOps.ComputeRoomCenterCell(layout);

            // Assert
            Assert.AreEqual(2f, center.x, 1e-4f);
            Assert.AreEqual(2f, center.z, 1e-4f);
        }

        // -----------------------------------------------------------------
        // EnsureOccluder
        // -----------------------------------------------------------------

        [Test]
        public void should_add_component_when_EnsureOccluder_called_on_tile_without_one()
        {
            // Arrange
            var layout = MakeLayout();
            MakeTile(layout, 1, 1, TileType.Floor);   // center hint
            var wall = MakeTile(layout, 1, 5, TileType.Wall);

            // Act
            var result = WallOccluderOps.EnsureOccluder(wall, layout, new Vector3Int(1, 0, 5));

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Added, result);
            var occ = wall.GetComponent<WallOccluder>();
            Assert.IsNotNull(occ);
            Assert.AreEqual(WallDirection.N, occ.Direction);
        }

        [Test]
        public void should_be_idempotent_when_EnsureOccluder_called_twice()
        {
            // Arrange
            var layout = MakeLayout();
            MakeTile(layout, 1, 1, TileType.Floor);
            var wall = MakeTile(layout, 1, 5, TileType.Wall);
            var cell = new Vector3Int(1, 0, 5);
            WallOccluderOps.EnsureOccluder(wall, layout, cell);

            // Act
            var second = WallOccluderOps.EnsureOccluder(wall, layout, cell);

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Skipped, second);
            Assert.AreEqual(1, wall.GetComponents<WallOccluder>().Length,
                "EnsureOccluder must not stack multiple WallOccluder components.");
        }

        [Test]
        public void should_update_direction_when_EnsureOccluder_called_on_stale_occluder()
        {
            // Arrange — wall preset to S, but its cell is north of center.
            var layout = MakeLayout();
            MakeTile(layout, 0, 0, TileType.Floor);
            var wall = MakeTile(layout, 0, 5, TileType.Wall);
            var occ = wall.AddComponent<WallOccluder>();
            occ.Direction = WallDirection.S;

            // Act
            var result = WallOccluderOps.EnsureOccluder(wall, layout, new Vector3Int(0, 0, 5));

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Updated, result);
            Assert.AreEqual(WallDirection.N, wall.GetComponent<WallOccluder>().Direction);
        }

        [Test]
        public void should_skip_when_EnsureOccluder_called_on_occluder_with_manual_override()
        {
            // Arrange — wall is north of center but designer locked it to E.
            var layout = MakeLayout();
            MakeTile(layout, 0, 0, TileType.Floor);
            var wall = MakeTile(layout, 0, 5, TileType.Wall);
            var occ = wall.AddComponent<WallOccluder>();
            occ.Direction = WallDirection.E;
            occ.ManualOverride = true;

            // Act
            var result = WallOccluderOps.EnsureOccluder(wall, layout, new Vector3Int(0, 0, 5));

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Skipped, result);
            Assert.AreEqual(WallDirection.E, wall.GetComponent<WallOccluder>().Direction,
                "ManualOverride must protect Direction from auto-bake.");
        }

        [Test]
        public void should_return_skipped_when_EnsureOccluder_called_with_null_tile()
        {
            // Arrange
            var layout = MakeLayout();

            // Act
            var result = WallOccluderOps.EnsureOccluder(null, layout, Vector3Int.zero);

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Skipped, result);
        }

        [Test]
        public void should_return_skipped_when_EnsureOccluder_called_with_null_room()
        {
            // Arrange
            var go = new GameObject("OrphanWall");
            _created.Add(go);

            // Act
            var result = WallOccluderOps.EnsureOccluder(go, null, Vector3Int.zero);

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Skipped, result);
        }
    }
}
