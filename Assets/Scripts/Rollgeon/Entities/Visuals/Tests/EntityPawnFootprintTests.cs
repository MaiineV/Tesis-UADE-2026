using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Entities.Visuals.Tests
{
    /// <summary>Un pawn multi-celda se dibuja en el centro de su rectángulo; el 1×1 no cambia.</summary>
    [TestFixture]
    public class EntityPawnFootprintTests
    {
        const float PawnY = 0.1f;

        private readonly List<Object> _created = new List<Object>();
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5), new Vector3(10f, 0f, -4f), tileSize: 2f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        EntityPawn MakePawn()
        {
            var go = new GameObject("Pawn");
            _created.Add(go);
            return go.AddComponent<EntityPawn>();
        }

        static void AssertPos(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void SnapToGrid_1x1_PositionUnchanged()
        {
            var pawn = MakePawn();
            pawn.SnapToGrid(_grid, new GridCoord(1, 2));

            var expected = _grid.GridToWorld(new GridCoord(1, 2));
            expected.y += PawnY;
            AssertPos(expected, pawn.transform.position);
            Assert.AreEqual(Vector2Int.one, pawn.Footprint);
        }

        [Test]
        public void SnapToGrid_2x2_CenteredOnRect()
        {
            var pawn = MakePawn();
            pawn.SetFootprint(new Vector2Int(2, 2));
            pawn.SnapToGrid(_grid, new GridCoord(1, 1));

            // Centro del 2×2 = esquina compartida por las cuatro celdas = origen + (ancla + 1) · tile.
            var expected = _grid.GridOrigin + new Vector3(2f * 2f, PawnY, 2f * 2f);
            AssertPos(expected, pawn.transform.position);
        }

        [Test]
        public void SnapToGrid_2x1_HalfTileOnXOnly()
        {
            var pawn = MakePawn();
            pawn.SetFootprint(new Vector2Int(2, 1));
            pawn.SnapToGrid(_grid, new GridCoord(0, 0));

            var expected = _grid.GridToWorld(new GridCoord(0, 0)) + new Vector3(1f, PawnY, 0f);
            AssertPos(expected, pawn.transform.position);
        }

        [Test]
        public void SetFootprint_ClampsNonPositive()
        {
            var pawn = MakePawn();
            pawn.SetFootprint(new Vector2Int(0, -3));
            Assert.AreEqual(Vector2Int.one, pawn.Footprint);
        }
    }
}
