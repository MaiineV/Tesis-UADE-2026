using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.GameCamera.Tests
{
    [TestFixture]
    public class CameraConfigSOTests
    {
        [Test]
        public void DefaultOcclusionMap_HasEightEntries()
        {
            var map = CameraConfigSO.DefaultOcclusionMap();
            Assert.AreEqual(8, map.Count);
            foreach (CameraFacing facing in System.Enum.GetValues(typeof(CameraFacing)))
            {
                Assert.IsTrue(map.ContainsKey(facing), $"missing entry for {facing}");
            }
        }

        [Test]
        public void DefaultOcclusionMap_AllFacings_HideThreeWalls()
        {
            // Each facing should hide a 90° arc: the same-name wall + its two
            // compass-adjacent neighbors. Single-wall hides leave corners
            // visible blocking the player from view.
            var map = CameraConfigSO.DefaultOcclusionMap();
            foreach (CameraFacing facing in System.Enum.GetValues(typeof(CameraFacing)))
            {
                Assert.AreEqual(3, map[facing].Count, $"facing {facing} should hide 3 walls");
            }
        }

        [Test]
        public void DefaultOcclusionMap_CardinalFacings_HideOppositePlusTwoDiagonals()
        {
            // Each cardinal facing hides the OPPOSITE cardinal + its two flanking
            // diagonals (the walls on the camera's side = closest to camera).
            var map = CameraConfigSO.DefaultOcclusionMap();

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.SW, WallDirection.S, WallDirection.SE },
                map[CameraFacing.N]);

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.SW, WallDirection.W, WallDirection.NW },
                map[CameraFacing.E]);

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.NW, WallDirection.N, WallDirection.NE },
                map[CameraFacing.S]);

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.NE, WallDirection.E, WallDirection.SE },
                map[CameraFacing.W]);
        }

        [Test]
        public void DefaultOcclusionMap_DiagonalFacings_HideOppositePlusTwoCardinals()
        {
            var map = CameraConfigSO.DefaultOcclusionMap();

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.W, WallDirection.SW, WallDirection.S },
                map[CameraFacing.NE]);

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.W, WallDirection.NW, WallDirection.N },
                map[CameraFacing.SE]);

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.N, WallDirection.NE, WallDirection.E },
                map[CameraFacing.SW]);

            CollectionAssert.AreEquivalent(
                new[] { WallDirection.E, WallDirection.SE, WallDirection.S },
                map[CameraFacing.NW]);
        }

        [Test]
        public void FreshInstance_HasExpectedDefaults()
        {
            var config = ScriptableObject.CreateInstance<CameraConfigSO>();
            try
            {
                Assert.AreEqual(CameraFacing.NE, config.StartingFacing);
                Assert.IsTrue(config.EnableRotation);
                Assert.IsTrue(config.EnablePan);
                Assert.IsTrue(config.EnableZoom);
                Assert.IsTrue(config.EnableWallOcclusion);
                Assert.IsTrue(config.EnableFloorView);
                Assert.Greater(config.ZoomMax, config.ZoomMin);
                Assert.GreaterOrEqual(config.FloorViewZoomThreshold, config.ZoomMin);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void FacingEnum_ValuesAreDegrees()
        {
            Assert.AreEqual(0, (int)CameraFacing.N);
            Assert.AreEqual(45, (int)CameraFacing.NE);
            Assert.AreEqual(90, (int)CameraFacing.E);
            Assert.AreEqual(135, (int)CameraFacing.SE);
            Assert.AreEqual(180, (int)CameraFacing.S);
            Assert.AreEqual(225, (int)CameraFacing.SW);
            Assert.AreEqual(270, (int)CameraFacing.W);
            Assert.AreEqual(315, (int)CameraFacing.NW);
        }
    }
}
