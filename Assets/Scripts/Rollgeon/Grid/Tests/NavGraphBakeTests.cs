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
