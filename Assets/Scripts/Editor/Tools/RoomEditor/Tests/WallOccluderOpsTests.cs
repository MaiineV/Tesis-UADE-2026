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
            // EnsureOccluder usa Undo.AddComponent/RecordObject → los tiles del test
            // quedan referenciados en el Undo stack global y un Ctrl+Z posterior los
            // resucita huérfanos en la escena abierta (aparecían "Tile_1_5_Wall"
            // fantasma en 00_Bootstrap). Limpiar el stack corta la resurrección.
            UnityEditor.Undo.ClearAll();
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

        // -----------------------------------------------------------------
        // InferDirection — continuous overload (props off the grid)
        // -----------------------------------------------------------------

        [TestCase(-0.3f,  5.7f, WallDirection.N)]
        [TestCase( 5.3f, -0.2f, WallDirection.E)]
        [TestCase(-5.3f,  0.2f, WallDirection.W)]
        [TestCase(-4.9f, -2.1f, WallDirection.SW)]
        public void should_quantize_unaligned_position_when_InferDirection_called_with_vector3(float dx, float dz, WallDirection expected)
        {
            // Arrange
            var center = Vector3.zero;
            var cellPos = new Vector3(dx, 0f, dz);

            // Act
            var actual = WallOccluderOps.InferDirection(cellPos, center);

            // Assert
            Assert.AreEqual(expected, actual);
        }

        // -----------------------------------------------------------------
        // ResolvePropDirection — nearest wall wins, octant as fallback
        // -----------------------------------------------------------------

        [Test]
        public void should_copy_nearest_wall_direction_when_ResolvePropDirection_called_with_walls()
        {
            // Arrange — prop at the lower half of the W wall: by octant it would be
            // SW, but the wall tile right next to it was baked W.
            var walls = new List<WallOccluderOps.WallRef>
            {
                new(new Vector3(-6f, 0f, -3f), WallDirection.W),
                new(new Vector3(-6f, 0f, -6f), WallDirection.SW),
                new(new Vector3( 0f, 0f,  6f), WallDirection.N),
            };
            var prop = new Vector3(-5.5f, 0f, -2.9f);

            // Act
            var actual = WallOccluderOps.ResolvePropDirection(prop, walls, Vector3.zero);

            // Assert
            Assert.AreEqual(WallDirection.W, actual);
        }

        [Test]
        public void should_fall_back_to_octant_when_ResolvePropDirection_called_without_walls()
        {
            // Arrange
            var prop = new Vector3(0.2f, 0f, 6f);

            // Act
            var actual = WallOccluderOps.ResolvePropDirection(prop, new List<WallOccluderOps.WallRef>(), Vector3.zero);

            // Assert
            Assert.AreEqual(WallDirection.N, actual);
        }

        // -----------------------------------------------------------------
        // EnsureProp — occluders that live on props (torches, signs)
        // -----------------------------------------------------------------

        /// <summary>Prop suelto bajo la sala (sin TileMarker) con occluder propio, en world position.</summary>
        private WallOccluder MakeProp(RoomLayout layout, float worldX, float worldZ, WallDirection preset)
        {
            var go = new GameObject($"Prop_{worldX}_{worldZ}");
            go.transform.SetParent(layout.transform);
            go.transform.position = new Vector3(worldX, 2f, worldZ);
            var occ = go.AddComponent<WallOccluder>();
            occ.Direction = preset;
            return occ;
        }

        private GameObject MakeWall(RoomLayout layout, int x, int z, WallDirection direction)
        {
            var wall = MakeTile(layout, x, z, TileType.Wall);
            wall.AddComponent<WallOccluder>().Direction = direction;
            return wall;
        }

        [Test]
        public void should_copy_nearest_wall_when_EnsureProp_called_on_stale_prop()
        {
            // Arrange — 3x3 floor at cells (0..2, 0..2). Torch preset to NE (prefab
            // default) hangs in front of the north wall tile at cell (1, 4).
            var layout = MakeLayout();
            for (int x = 0; x <= 2; x++)
                for (int z = 0; z <= 2; z++)
                    MakeTile(layout, x, z, TileType.Floor);
            MakeWall(layout, 1, 4, WallDirection.N);
            MakeWall(layout, -2, 1, WallDirection.W);
            var torch = MakeProp(layout, 1.2f, 4.1f, WallDirection.NE);

            // Act
            var result = WallOccluderOps.EnsureProp(torch, layout,
                WallOccluderOps.CollectWallRefs(layout), WallOccluderOps.ComputeRoomCenterCell(layout));

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Updated, result);
            Assert.AreEqual(WallDirection.N, torch.Direction);
        }

        [Test]
        public void should_skip_when_EnsureProp_called_on_prop_with_manual_override()
        {
            // Arrange — prop next to the N wall but locked to E by the designer.
            var layout = MakeLayout();
            MakeTile(layout, 0, 0, TileType.Floor);
            MakeWall(layout, 0, 5, WallDirection.N);
            var torch = MakeProp(layout, 0.5f, 5.2f, WallDirection.E);
            torch.ManualOverride = true;

            // Act
            var result = WallOccluderOps.EnsureProp(torch, layout,
                WallOccluderOps.CollectWallRefs(layout), WallOccluderOps.ComputeRoomCenterCell(layout));

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Skipped, result);
            Assert.AreEqual(WallDirection.E, torch.Direction);
        }

        [Test]
        public void should_respect_grid_origin_when_EnsureProp_called_on_offset_room()
        {
            // Arrange — GridOrigin displaced to world (10, 0, 10). Walls at cells
            // (0, 5) → N and (5, 0) → E. A prop at world (10.5, _, 15.8) is in front
            // of the N wall; measured against world zero it would look far NE/E.
            var layout = MakeLayout();
            var originGo = new GameObject("GridOrigin");
            _created.Add(originGo);
            originGo.transform.position = new Vector3(10f, 0f, 10f);
            layout.GridOrigin = originGo.transform;
            MakeTile(layout, 0, 0, TileType.Floor);
            MakeWall(layout, 0, 5, WallDirection.N);
            MakeWall(layout, 5, 0, WallDirection.E);
            var torch = MakeProp(layout, 10.5f, 15.8f, WallDirection.NE);

            // Act
            var result = WallOccluderOps.EnsureProp(torch, layout,
                WallOccluderOps.CollectWallRefs(layout), WallOccluderOps.ComputeRoomCenterCell(layout));

            // Assert
            Assert.AreEqual(WallOccluderOps.BakeResult.Updated, result);
            Assert.AreEqual(WallDirection.N, torch.Direction);
        }

        // -----------------------------------------------------------------
        // BakeRoom — walls + props, doors untouched
        // -----------------------------------------------------------------

        [Test]
        public void should_bake_walls_and_props_but_not_doors_when_BakeRoom_called()
        {
            // Arrange — floor at (1,1); walls N (1,5) and W (-3,1) without occluder;
            // torch preset NE next to the W wall; door tile south with its own occluder.
            var layout = MakeLayout();
            MakeTile(layout, 1, 1, TileType.Floor);
            var north = MakeTile(layout, 1, 5, TileType.Wall);
            var west = MakeTile(layout, -3, 1, TileType.Wall);
            var torch = MakeProp(layout, -2.4f, 1.6f, WallDirection.NE);
            var door = MakeTile(layout, 1, -5, TileType.Door);
            var doorOcc = door.AddComponent<WallOccluder>();
            doorOcc.Direction = WallDirection.E;

            // Act
            var summary = WallOccluderOps.BakeRoom(layout);

            // Assert
            Assert.AreEqual(2, summary.WallsAdded);
            Assert.AreEqual(WallDirection.N, north.GetComponent<WallOccluder>().Direction);
            Assert.AreEqual(WallDirection.W, west.GetComponent<WallOccluder>().Direction);
            Assert.AreEqual(1, summary.PropsUpdated);
            Assert.AreEqual(WallDirection.W, torch.Direction,
                "Props must copy the direction of the wall they hang on, baked in the same pass.");
            Assert.AreEqual(WallDirection.E, doorOcc.Direction,
                "Door occluders are driven by DoorController, BakeRoom must leave them alone.");
        }

        [Test]
        public void should_leave_walls_untouched_when_BakeProps_called()
        {
            // Arrange — W wall hand-baked as SW (borderline octant); torch next to it.
            var layout = MakeLayout();
            MakeTile(layout, 1, 1, TileType.Floor);
            var west = MakeWall(layout, -3, 1, WallDirection.SW);
            var torch = MakeProp(layout, -2.4f, 1.6f, WallDirection.NE);
            var summary = new WallOccluderOps.BakeSummary();

            // Act
            WallOccluderOps.BakeProps(layout, ref summary);

            // Assert
            Assert.AreEqual(WallDirection.SW, west.GetComponent<WallOccluder>().Direction,
                "BakeProps must read walls as-is, never re-infer them.");
            Assert.AreEqual(WallDirection.SW, torch.Direction);
            Assert.AreEqual(1, summary.PropsUpdated);
            Assert.AreEqual(0, summary.WallsAdded + summary.WallsUpdated);
        }

        [Test]
        public void should_be_idempotent_when_BakeRoom_called_twice()
        {
            // Arrange
            var layout = MakeLayout();
            MakeTile(layout, 1, 1, TileType.Floor);
            MakeTile(layout, 1, 5, TileType.Wall);
            MakeProp(layout, 1.5f, 6f, WallDirection.NE);
            WallOccluderOps.BakeRoom(layout);

            // Act
            var second = WallOccluderOps.BakeRoom(layout);

            // Assert
            Assert.AreEqual(0, second.WallsAdded + second.WallsUpdated + second.PropsUpdated);
            Assert.AreEqual(1, second.WallsSkipped);
            Assert.AreEqual(1, second.PropsSkipped);
        }
    }
}
