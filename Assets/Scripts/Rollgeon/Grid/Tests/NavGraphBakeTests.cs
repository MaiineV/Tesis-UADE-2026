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

        private static void CreateCube(GameObject parent, Vector3 localPos)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform, worldPositionStays: false);
            cube.transform.localPosition = localPos;
        }
    }
}
