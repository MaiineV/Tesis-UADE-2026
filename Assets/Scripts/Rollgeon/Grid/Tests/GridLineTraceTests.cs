using System;
using System.Linq;
using NUnit.Framework;

namespace Rollgeon.Grid.Tests
{
    /// <summary>
    /// <see cref="GridLineTrace.Trace"/>: corte por pared, por ocupante, y por distancia máxima
    /// (Justa de Justicia / Garfio del rediseño de ítems activos).
    /// </summary>
    [TestFixture]
    public sealed class GridLineTraceTests
    {
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 1)); // x: 0..4, y: 0
        }

        [Test]
        public void Trace_NoObstacles_StopsAtMaxReached()
        {
            var result = GridLineTrace.Trace(_grid, new GridCoord(0, 0), Cardinal.East, 2);

            Assert.AreEqual(LineTraceStop.MaxReached, result.Stop);
            CollectionAssert.AreEqual(new[] { new GridCoord(1, 0), new GridCoord(2, 0) }, result.FreeCells);
            Assert.AreEqual(new GridCoord(2, 0), result.HitCoord);
            Assert.AreEqual(Guid.Empty, result.Occupant);
        }

        [Test]
        public void Trace_HitsRoomEdge_StopsAtWall()
        {
            var result = GridLineTrace.Trace(_grid, new GridCoord(3, 0), Cardinal.East, 5);

            Assert.AreEqual(LineTraceStop.Wall, result.Stop);
            CollectionAssert.AreEqual(new[] { new GridCoord(4, 0) }, result.FreeCells);
            Assert.AreEqual(new GridCoord(5, 0), result.HitCoord, "la celda fuera de bounds que cortó la línea");
        }

        [Test]
        public void Trace_HitsOccupant_StopsBeforeItWithOccupantGuid()
        {
            var occupant = Guid.NewGuid();
            _grid.Register(occupant, new GridCoord(2, 0));

            var result = GridLineTrace.Trace(_grid, new GridCoord(0, 0), Cardinal.East, 5);

            Assert.AreEqual(LineTraceStop.Occupant, result.Stop);
            CollectionAssert.AreEqual(new[] { new GridCoord(1, 0) }, result.FreeCells);
            Assert.AreEqual(new GridCoord(2, 0), result.HitCoord);
            Assert.AreEqual(occupant, result.Occupant);
        }

        [Test]
        public void Trace_IgnoredOccupant_PassesThrough()
        {
            var ignored = Guid.NewGuid();
            _grid.Register(ignored, new GridCoord(2, 0));

            var result = GridLineTrace.Trace(_grid, new GridCoord(0, 0), Cardinal.East, 4, ignore: ignored);

            Assert.AreEqual(LineTraceStop.MaxReached, result.Stop);
            Assert.AreEqual(4, result.FreeCells.Count);
            Assert.IsTrue(result.FreeCells.Contains(new GridCoord(2, 0)),
                "el ocupante ignorado no corta la línea");
        }

        [Test]
        public void Trace_ZeroMaxTiles_ReturnsEmptyMaxReached()
        {
            var result = GridLineTrace.Trace(_grid, new GridCoord(0, 0), Cardinal.East, 0);

            Assert.AreEqual(LineTraceStop.MaxReached, result.Stop);
            CollectionAssert.IsEmpty(result.FreeCells);
        }
    }
}
