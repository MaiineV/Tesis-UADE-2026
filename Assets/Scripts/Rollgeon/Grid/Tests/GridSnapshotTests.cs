using System;
using System.Linq;
using NUnit.Framework;

namespace Rollgeon.Grid.Tests
{
    [TestFixture]
    public class GridSnapshotTests
    {
        [Test]
        public void Empty_IsEmptyAndWalkableEverywhere()
        {
            var s = GridSnapshot.Empty;
            Assert.IsTrue(s.IsEmpty);
            Assert.AreEqual(0, s.Width);
            Assert.AreEqual(0, s.Height);
            // IsWalkable en empty es true (treat as unbounded walkable).
            Assert.IsTrue(s.IsWalkable(new GridCoord(100, 100)));
        }

        [Test]
        public void Rect_ProducesAllWalkable()
        {
            var s = GridSnapshot.Rect(3, 2);
            Assert.IsFalse(s.IsEmpty);
            Assert.AreEqual(3, s.Width);
            Assert.AreEqual(2, s.Height);
            foreach (var c in s.AllCoords())
                Assert.IsTrue(s.IsWalkable(c), $"Rect debería ser walkable en {c}");
        }

        [Test]
        public void InBounds_ReflectsWidthHeight()
        {
            var s = GridSnapshot.Rect(2, 2);
            Assert.IsTrue(s.InBounds(new GridCoord(0, 0)));
            Assert.IsTrue(s.InBounds(new GridCoord(1, 1)));
            Assert.IsFalse(s.InBounds(new GridCoord(-1, 0)));
            Assert.IsFalse(s.InBounds(new GridCoord(0, -1)));
            Assert.IsFalse(s.InBounds(new GridCoord(2, 0)));
            Assert.IsFalse(s.InBounds(new GridCoord(0, 2)));
        }

        [Test]
        public void IsWalkable_ReturnsFalseOutOfBoundsWhenNotEmpty()
        {
            var s = GridSnapshot.Rect(2, 2);
            Assert.IsFalse(s.IsWalkable(new GridCoord(2, 0)));
            Assert.IsFalse(s.IsWalkable(new GridCoord(-1, 0)));
        }

        [Test]
        public void CustomWalkable_HonorsWalkableArray()
        {
            //   (0,1) (1,1)
            //   (0,0) (1,0)
            // blocked: (1,0)
            var walkable = new[] { true, false, true, true };
            var s = new GridSnapshot(2, 2, walkable);
            Assert.IsTrue(s.IsWalkable(new GridCoord(0, 0)));
            Assert.IsFalse(s.IsWalkable(new GridCoord(1, 0)));
            Assert.IsTrue(s.IsWalkable(new GridCoord(0, 1)));
            Assert.IsTrue(s.IsWalkable(new GridCoord(1, 1)));
        }

        [Test]
        public void Ctor_ThrowsIfWalkableLengthMismatches()
        {
            Assert.Throws<ArgumentException>(() => new GridSnapshot(2, 2, new bool[3]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridSnapshot(-1, 2, new bool[0]));
        }

        [Test]
        public void AllCoords_EnumeratesWidthTimesHeightTiles()
        {
            var s = GridSnapshot.Rect(4, 3);
            var all = s.AllCoords().ToList();
            Assert.AreEqual(12, all.Count);
            Assert.Contains(new GridCoord(0, 0), all);
            Assert.Contains(new GridCoord(3, 2), all);
        }
    }
}
