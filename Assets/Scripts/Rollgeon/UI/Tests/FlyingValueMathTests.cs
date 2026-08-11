using NUnit.Framework;
using Rollgeon.UI.HUD.Breakdown;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>Tests de la curva de vuelo de <see cref="FlyingValueView"/>.</summary>
    [TestFixture]
    public class FlyingValueMathTests
    {
        [Test]
        public void EvaluateBezier_Endpoints_MatchP0AndP2()
        {
            var p0 = new Vector2(-100f, 0f);
            var p1 = new Vector2(0f, 80f);
            var p2 = new Vector2(200f, 40f);

            Assert.AreEqual(p0, FlyingValueView.EvaluateBezier(p0, p1, p2, 0f));
            Assert.AreEqual(p2, FlyingValueView.EvaluateBezier(p0, p1, p2, 1f));
        }

        [Test]
        public void EvaluateBezier_Midpoint_IsPulledTowardControlPoint()
        {
            var p0 = new Vector2(0f, 0f);
            var p1 = new Vector2(50f, 100f);
            var p2 = new Vector2(100f, 0f);

            var mid = FlyingValueView.EvaluateBezier(p0, p1, p2, 0.5f);

            // B(0.5) = 0.25·p0 + 0.5·p1 + 0.25·p2 = (50, 50)
            Assert.AreEqual(50f, mid.x, 0.001f);
            Assert.AreEqual(50f, mid.y, 0.001f);
        }
    }
}
