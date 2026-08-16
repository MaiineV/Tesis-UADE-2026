using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="RectPerimeterMath"/>: la parametrización por longitud de
    /// arco del contorno que reparte los dots del highlight de End Turn.
    /// </summary>
    [TestFixture]
    public class RectPerimeterMathTests
    {
        private static readonly Vector2 Size = new Vector2(200f, 50f); // perímetro 500

        private const float Delta = 1e-4f;

        [Test]
        public void Perimeter_Of200x50_Is500()
        {
            Assert.AreEqual(500f, RectPerimeterMath.Perimeter(Size), Delta);
        }

        [Test]
        public void Perimeter_NegativeComponents_ClampToZero()
        {
            Assert.AreEqual(400f, RectPerimeterMath.Perimeter(new Vector2(200f, -50f)), Delta,
                "una componente negativa cuenta como 0");
        }

        [Test]
        public void PointOnPerimeter_AtZero_IsTopLeftCorner()
        {
            AssertPoint(new Vector2(-100f, 25f), RectPerimeterMath.PointOnPerimeter(Size, 0f));
        }

        [Test]
        public void PointOnPerimeter_WalksCornersClockwise()
        {
            // top-right en t = w/P, bottom-right en t = (w+h)/P, bottom-left en (2w+h)/P.
            AssertPoint(new Vector2(100f, 25f), RectPerimeterMath.PointOnPerimeter(Size, 0.4f));
            AssertPoint(new Vector2(100f, -25f), RectPerimeterMath.PointOnPerimeter(Size, 0.5f));
            AssertPoint(new Vector2(-100f, -25f), RectPerimeterMath.PointOnPerimeter(Size, 0.9f));
        }

        [Test]
        public void PointOnPerimeter_MidTopEdge_IsCenteredHorizontally()
        {
            AssertPoint(new Vector2(0f, 25f), RectPerimeterMath.PointOnPerimeter(Size, 0.2f));
        }

        [Test]
        public void PointOnPerimeter_WrapsAboveOne()
        {
            AssertPoint(RectPerimeterMath.PointOnPerimeter(Size, 0.25f),
                RectPerimeterMath.PointOnPerimeter(Size, 1.25f));
        }

        [Test]
        public void PointOnPerimeter_WrapsBelowZero()
        {
            AssertPoint(RectPerimeterMath.PointOnPerimeter(Size, 0.25f),
                RectPerimeterMath.PointOnPerimeter(Size, -0.75f));
        }

        [Test]
        public void PointOnPerimeter_UniformSamples_AllLieOnBorder()
        {
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                var p = RectPerimeterMath.PointOnPerimeter(Size, i / (float)count);
                bool onVerticalEdge = Mathf.Abs(Mathf.Abs(p.x) - 100f) < Delta;
                bool onHorizontalEdge = Mathf.Abs(Mathf.Abs(p.y) - 25f) < Delta;
                Assert.IsTrue(onVerticalEdge || onHorizontalEdge,
                    $"la muestra {i}/{count} ({p}) debe caer sobre el borde del rect");
                Assert.LessOrEqual(Mathf.Abs(p.x), 100f + Delta);
                Assert.LessOrEqual(Mathf.Abs(p.y), 25f + Delta);
            }
        }

        [Test]
        public void PointOnPerimeter_DegenerateSize_ReturnsCenter()
        {
            AssertPoint(Vector2.zero, RectPerimeterMath.PointOnPerimeter(Vector2.zero, 0.37f));
        }

        private static void AssertPoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Delta, $"x — expected {expected}, got {actual}");
            Assert.AreEqual(expected.y, actual.y, Delta, $"y — expected {expected}, got {actual}");
        }
    }
}
