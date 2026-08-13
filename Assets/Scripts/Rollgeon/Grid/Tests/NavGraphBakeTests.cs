using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    [TestFixture]
    public sealed class NavGraphBakeTests
    {
        [Test]
        public void Bake_NullRoot_ReturnsEmptyGraph()
        {
            var graph = NavGraphBaker.Bake(null, new NavGraphBakeSettings());
            Assert.IsTrue(graph.IsEmpty);
        }

        [Test]
        public void Bake_NullSettings_ReturnsEmptyGraph()
        {
            var root = new GameObject("Root");
            try
            {
                var graph = NavGraphBaker.Bake(root, null);
                Assert.IsTrue(graph.IsEmpty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bake_NoRenderers_ReturnsEmptyGraph()
        {
            var root = new GameObject("Root");
            try
            {
                var graph = NavGraphBaker.Bake(root, new NavGraphBakeSettings { TileSize = 1f });
                Assert.IsTrue(graph.IsEmpty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bake_FourAdjacentTiles_CreatesFullyConnectedGraph()
        {
            var root = new GameObject("Root");
            try
            {
                // Create 4 cubes in a 2x2 grid at Y=0
                CreateCube(root, new Vector3(0, 0, 0));
                CreateCube(root, new Vector3(1, 0, 0));
                CreateCube(root, new Vector3(0, 0, 1));
                CreateCube(root, new Vector3(1, 0, 1));

                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                Assert.AreEqual(4, graph.NodeCount);
                // 4 pairs of adjacent tiles = 4 bidirectional = 8 directed edges
                Assert.AreEqual(8, graph.Edges.Count);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bake_HeightDifferenceBeyondThreshold_NoEdge()
        {
            var root = new GameObject("Root");
            try
            {
                CreateCube(root, new Vector3(0, 0, 0));
                CreateCube(root, new Vector3(1, 2, 0)); // height diff = 2

                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                Assert.AreEqual(2, graph.NodeCount);
                Assert.AreEqual(0, graph.Edges.Count);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bake_HeightWithinThreshold_HasEdge()
        {
            var root = new GameObject("Root");
            try
            {
                CreateCube(root, new Vector3(0, 0, 0));
                CreateCube(root, new Vector3(1, 0.3f, 0)); // height diff = 0.3 < 0.5

                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                Assert.AreEqual(2, graph.NodeCount);
                Assert.AreEqual(2, graph.Edges.Count); // bidirectional
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bake_DiagonalTiles_NoEdge()
        {
            var root = new GameObject("Root");
            try
            {
                CreateCube(root, new Vector3(0, 0, 0));
                CreateCube(root, new Vector3(1, 0, 1)); // diagonal, Manhattan=2

                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                Assert.AreEqual(2, graph.NodeCount);
                Assert.AreEqual(0, graph.Edges.Count);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // -------------------------------------------------------------
        // BUG-012 — el bloqueo sale del Footprint autorado, no del modelo
        // -------------------------------------------------------------

        [Test]
        public void Bake_BlockerModelOverhangsNeighbors_BlocksOnlyFootprintCell()
        {
            var root = new GameObject("Root");
            try
            {
                // Arrange: dos floors finos adyacentes + un blocker de 1 celda
                // en (1,0) cuyo mesh (escala 3) invade la celda vecina (0,0).
                CreateFloorTile(root, 0, 0);
                CreateFloorTile(root, 1, 0);
                CreateBlockerProp(root, x: 1, z: 0, meshScale: new Vector3(3f, 1f, 3f));

                // Act
                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                // Assert: solo la celda del footprint pierde su nodo. Con el
                // bug (bounds del renderer) el mesh también mataba (0,0).
                Assert.AreEqual(1, graph.NodeCount,
                    "El blocker de 1 celda debe matar solo su propio nodo.");
                Assert.AreEqual(new GridCoord(0, 0), graph.Nodes[0].Coord,
                    "El nodo sobreviviente debe ser la celda vecina (0,0).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bake_BlockerModelOverhangsPath_DoesNotBlockEdgeOutsideFootprint()
        {
            var root = new GameObject("Root");
            try
            {
                // Arrange: floors caminables en (0,0)-(1,0); blocker en (3,0)
                // con mesh ancho (escala x 4.2) que invade el segmento entre
                // los dos floors sin tocar sus celdas de footprint.
                CreateFloorTile(root, 0, 0);
                CreateFloorTile(root, 1, 0);
                CreateBlockerProp(root, x: 3, z: 0, meshScale: new Vector3(4.2f, 1f, 1f));

                // Act
                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                // Assert: con el bug, el renderer ancho bloqueaba el edge
                // (0,0)↔(1,0) pese a que el footprint vive en la celda (3,0).
                Assert.AreEqual(2, graph.NodeCount);
                Assert.AreEqual(2, graph.Edges.Count,
                    "El edge entre floors fuera del footprint debe sobrevivir.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // -------------------------------------------------------------
        // Prop multi-celda con PIVOT DESFASADO del footprint. El bloqueo debe
        // salir del espacio de celdas autorado (Coord + FootprintOffset), no
        // del transform: si sale del pivot, las celdas bloqueadas se corren.
        // -------------------------------------------------------------

        [Test]
        public void Bake_BlockerWithOffsetPivot_BlocksAuthoredFootprintCells_NotPivotCells()
        {
            var root = new GameObject("Root");
            try
            {
                // Arrange: strip de 4 floors (X=0..3) en la fila Z=1, ubicados
                // en el centro de su celda (convención del editor: celda N =
                // [N, N+1]). Un blocker 2x1 autorado en las celdas (1,1)-(2,1),
                // pero con el transform corrido +0.7 en X respecto del centro
                // de su footprint (pivot desfasado del mesh, como la ruleta).
                CreateFloorCell(root, 0, 1);
                CreateFloorCell(root, 1, 1);
                CreateFloorCell(root, 2, 1);
                CreateFloorCell(root, 3, 1);
                CreateOffsetPivotBlocker(
                    root,
                    coord: new GridCoord(1, 1),
                    footprint: new Vector3Int(2, 1, 1),
                    localPos: new Vector3(2.7f, 0.5f, 1.5f)); // footprint center X=2.0

                // Act
                var settings = new NavGraphBakeSettings { TileSize = 1f, HeightThreshold = 0.5f };
                var graph = NavGraphBaker.Bake(root, settings);

                // Assert: se bloquean EXACTAMENTE las celdas autoradas (1,1) y
                // (2,1); sobreviven (0,1) y (3,1). Con el bloqueo desde el pivot
                // corrido se bloqueaban (2,1) y (3,1) — celdas desfasadas.
                var coords = new HashSet<GridCoord>();
                foreach (var n in graph.Nodes) coords.Add(n.Coord);

                Assert.AreEqual(2, graph.NodeCount,
                    "Un blocker 2x1 debe matar exactamente 2 nodos de floor.");
                Assert.IsTrue(coords.Contains(new GridCoord(0, 1)), "(0,1) debe sobrevivir.");
                Assert.IsTrue(coords.Contains(new GridCoord(3, 1)), "(3,1) NO debe bloquearse (pivot drift).");
                Assert.IsFalse(coords.Contains(new GridCoord(1, 1)), "(1,1) debe bloquearse.");
                Assert.IsFalse(coords.Contains(new GridCoord(2, 1)), "(2,1) debe bloquearse.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Floor tile ubicado en el CENTRO de su celda con la convención del
        // editor (celda N = [N, N+1], centro N+0.5), para que los renderer
        // bounds coincidan con el rectángulo de la celda.
        private static void CreateFloorCell(GameObject parent, int x, int z)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, worldPositionStays: false);
            cube.transform.localPosition = new Vector3(x + 0.5f, 0f, z + 0.5f);
            cube.transform.localScale = new Vector3(1f, 0.1f, 1f);

            var marker = cube.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(x, z);
            marker.Type = TileType.Floor;
            marker.IsBlocker = false;
        }

        // Blocker cuyo transform NO está en el centro de su footprint (pivot
        // desfasado). El footprint autorado (Coord/Footprint/Offset) es la
        // fuente de verdad; el transform solo aporta el rango Y del renderer.
        private static void CreateOffsetPivotBlocker(
            GameObject parent, GridCoord coord, Vector3Int footprint, Vector3 localPos)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, worldPositionStays: false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = new Vector3(footprint.x, 1f, footprint.z);

            var marker = cube.AddComponent<TileMarker>();
            marker.Coord = coord;
            marker.Type = TileType.Decoration;
            marker.IsBlocker = true;
            marker.Footprint = footprint;
            marker.FootprintOffset = Vector3Int.zero;
        }

        private static void CreateCube(GameObject parent, Vector3 localPos)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, worldPositionStays: false);
            cube.transform.localPosition = localPos;
        }

        // Floor tile fino (slab) con TileMarker — el patrón que deja la
        // RoomEditor tool: marker centrado en su celda, coord autorada.
        private static void CreateFloorTile(GameObject parent, int x, int z)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, worldPositionStays: false);
            cube.transform.localPosition = new Vector3(x, 0f, z);
            cube.transform.localScale = new Vector3(1f, 0.1f, 1f);

            var marker = cube.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(x, z);
            marker.Type = TileType.Floor;
            marker.IsBlocker = false;
        }

        // Prop blocker de 1 celda cuyo mesh puede sobresalir del footprint
        // (la situación de BUG-012).
        private static void CreateBlockerProp(GameObject parent, int x, int z, Vector3 meshScale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, worldPositionStays: false);
            cube.transform.localPosition = new Vector3(x, 0.5f, z);
            cube.transform.localScale = meshScale;

            var marker = cube.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(x, z);
            marker.Type = TileType.Decoration;
            marker.IsBlocker = true;
            marker.Footprint = Vector3Int.one;
            marker.FootprintOffset = Vector3Int.zero;
        }
    }
}
