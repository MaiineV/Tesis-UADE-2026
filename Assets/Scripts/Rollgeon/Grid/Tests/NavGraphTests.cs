using System.Linq;
using NUnit.Framework;

namespace Rollgeon.Grid.Tests
{
    [TestFixture]
    public class NavGraphTests
    {
        [Test]
        public void Empty_IsEmpty()
        {
            var g = new NavGraph();
            Assert.IsTrue(g.IsEmpty);
            Assert.AreEqual(0, g.NodeCount);
        }

        [Test]
        public void Empty_HasNodeReturnsTrue()
        {
            var g = new NavGraph();
            Assert.IsTrue(g.HasNode(new GridCoord(5, 5)));
        }

        [Test]
        public void Empty_GetNeighborsReturnsFourNeighbors()
        {
            var g = new NavGraph();
            var origin = new GridCoord(1, 1);
            var neighbors = g.GetNeighbors(origin).ToList();
            Assert.AreEqual(4, neighbors.Count);
            var targets = neighbors.Select(e => e.To).ToList();
            CollectionAssert.AreEquivalent(origin.Neighbors4().ToList(), targets);
        }

        [Test]
        public void Empty_InBoundsReturnsTrue()
        {
            var g = new NavGraph();
            Assert.IsTrue(g.InBounds(new GridCoord(100, 200)));
        }

        [Test]
        public void AddNode_IncreasesNodeCount()
        {
            var g = new NavGraph();
            g.AddNode(new NavNode(new GridCoord(0, 0)));
            Assert.AreEqual(1, g.NodeCount);
            Assert.IsFalse(g.IsEmpty);
        }

        [Test]
        public void AddNode_DuplicateIsIgnored()
        {
            var g = new NavGraph();
            g.AddNode(new NavNode(new GridCoord(0, 0)));
            g.AddNode(new NavNode(new GridCoord(0, 0), 5f));
            Assert.AreEqual(1, g.NodeCount);
        }

        [Test]
        public void AddEdge_CreatesDirectedEdge()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));
            g.AddEdge(new NavEdge(a, b, 2f));

            Assert.IsTrue(g.HasEdge(a, b));
            Assert.IsFalse(g.HasEdge(b, a));
        }

        [Test]
        public void AddBidirectionalEdge_CreatesBothDirections()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));
            g.AddBidirectionalEdge(a, b, 3f);

            Assert.IsTrue(g.HasEdge(a, b));
            Assert.IsTrue(g.HasEdge(b, a));
        }

        [Test]
        public void GetNeighbors_ReturnsEdgesFromNode()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            var c = new GridCoord(0, 1);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));
            g.AddNode(new NavNode(c));
            g.AddBidirectionalEdge(a, b);
            g.AddBidirectionalEdge(a, c);

            var neighbors = g.GetNeighbors(a).ToList();
            Assert.AreEqual(2, neighbors.Count);
            var targets = neighbors.Select(e => e.To).ToList();
            Assert.Contains(b, targets);
            Assert.Contains(c, targets);
        }

        [Test]
        public void RemoveEdge_RemovesDirectedEdge()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));
            g.AddBidirectionalEdge(a, b);
            g.RemoveEdge(a, b);

            Assert.IsFalse(g.HasEdge(a, b));
            Assert.IsTrue(g.HasEdge(b, a));
        }

        [Test]
        public void RemoveBidirectionalEdge_RemovesBothDirections()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));
            g.AddBidirectionalEdge(a, b);
            g.RemoveBidirectionalEdge(a, b);

            Assert.IsFalse(g.HasEdge(a, b));
            Assert.IsFalse(g.HasEdge(b, a));
        }

        [Test]
        public void FromSnapshot_WalkableTilesBecomeNodes()
        {
            // 2x2 con (1,0) bloqueado
            var walkable = new[] { true, false, true, true };
            var snap = new GridSnapshot(2, 2, walkable);
            var g = NavGraph.FromSnapshot(snap);

            Assert.AreEqual(3, g.NodeCount);
            Assert.IsTrue(g.HasNode(new GridCoord(0, 0)));
            Assert.IsFalse(g.HasNode(new GridCoord(1, 0)));
            Assert.IsTrue(g.HasNode(new GridCoord(0, 1)));
            Assert.IsTrue(g.HasNode(new GridCoord(1, 1)));
        }

        [Test]
        public void FromSnapshot_AdjacentWalkableTilesGetEdges()
        {
            var snap = GridSnapshot.Rect(2, 1);
            var g = NavGraph.FromSnapshot(snap);

            Assert.IsTrue(g.HasEdge(new GridCoord(0, 0), new GridCoord(1, 0)));
            Assert.IsTrue(g.HasEdge(new GridCoord(1, 0), new GridCoord(0, 0)));
        }

        [Test]
        public void FromSnapshot_BlockedTilesExcludedFromEdges()
        {
            // (1,0) bloqueado — no debería haber edge (0,0)->(1,0)
            var walkable = new[] { true, false, true, true };
            var snap = new GridSnapshot(2, 2, walkable);
            var g = NavGraph.FromSnapshot(snap);

            Assert.IsFalse(g.HasEdge(new GridCoord(0, 0), new GridCoord(1, 0)));
        }

        [Test]
        public void Rect_ProducesFullyConnectedGrid()
        {
            var g = NavGraph.Rect(3, 2);
            Assert.AreEqual(6, g.NodeCount);
            // Interior node (1,0) debería tener 3 vecinos (no 4 porque borde inferior)
            var neighbors = g.GetNeighbors(new GridCoord(1, 0)).ToList();
            Assert.AreEqual(3, neighbors.Count);
        }

        [Test]
        public void WidthHeight_ReflectsMaxCoords()
        {
            var g = NavGraph.Rect(4, 3);
            Assert.AreEqual(4, g.Width);
            Assert.AreEqual(3, g.Height);
        }

        [Test]
        public void WidthHeight_EmptyReturnsZero()
        {
            var g = new NavGraph();
            Assert.AreEqual(0, g.Width);
            Assert.AreEqual(0, g.Height);
        }

        [Test]
        public void GetEdgeCost_ReturnsCorrectCost()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));
            g.AddEdge(new NavEdge(a, b, 2.5f));

            Assert.AreEqual(2.5f, g.GetEdgeCost(a, b), 0.001f);
        }

        [Test]
        public void GetEdgeCost_ReturnsInfinityForMissingEdge()
        {
            var g = new NavGraph();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            g.AddNode(new NavNode(a));
            g.AddNode(new NavNode(b));

            Assert.AreEqual(float.PositiveInfinity, g.GetEdgeCost(a, b));
        }

        [Test]
        public void InBounds_NonEmptyGraph_MatchesHasNode()
        {
            var g = NavGraph.Rect(2, 2);
            Assert.IsTrue(g.InBounds(new GridCoord(0, 0)));
            Assert.IsTrue(g.InBounds(new GridCoord(1, 1)));
            Assert.IsFalse(g.InBounds(new GridCoord(2, 0)));
            Assert.IsFalse(g.InBounds(new GridCoord(-1, 0)));
        }

        [Test]
        public void Clear_ResetsGraph()
        {
            var g = NavGraph.Rect(3, 3);
            Assert.IsFalse(g.IsEmpty);
            g.Clear();
            Assert.IsTrue(g.IsEmpty);
            Assert.AreEqual(0, g.NodeCount);
        }

        [Test]
        public void TryGetNode_ReturnsNodeData()
        {
            var g = new NavGraph();
            g.AddNode(new NavNode(new GridCoord(2, 3), 1.5f));

            Assert.IsTrue(g.TryGetNode(new GridCoord(2, 3), out var node));
            Assert.AreEqual(1.5f, node.Height, 0.001f);
        }

        [Test]
        public void AllCoords_EnumeratesAllNodeCoords()
        {
            var g = NavGraph.Rect(2, 2);
            var coords = g.AllCoords().ToList();
            Assert.AreEqual(4, coords.Count);
            Assert.Contains(new GridCoord(0, 0), coords);
            Assert.Contains(new GridCoord(1, 1), coords);
        }
    }
}
