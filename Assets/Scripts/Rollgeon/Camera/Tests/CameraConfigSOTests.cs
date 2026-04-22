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
        public void DefaultOcclusionMap_CardinalFacings_HideOneWall()
        {
            var map = CameraConfigSO.DefaultOcclusionMap();
            Assert.AreEqual(1, map[CameraFacing.N].Count);
            Assert.AreEqual(WallDirection.S, map[CameraFacing.N][0]);

            Assert.AreEqual(1, map[CameraFacing.E].Count);
            Assert.AreEqual(WallDirection.W, map[CameraFacing.E][0]);

            Assert.AreEqual(1, map[CameraFacing.S].Count);
            Assert.AreEqual(WallDirection.N, map[CameraFacing.S][0]);

            Assert.AreEqual(1, map[CameraFacing.W].Count);
            Assert.AreEqual(WallDirection.E, map[CameraFacing.W][0]);
        }

        [Test]
        public void DefaultOcclusionMap_DiagonalFacings_HideTwoWalls()
        {
            var map = CameraConfigSO.DefaultOcclusionMap();
            Assert.AreEqual(2, map[CameraFacing.NE].Count);
            Assert.AreEqual(2, map[CameraFacing.SE].Count);
            Assert.AreEqual(2, map[CameraFacing.SW].Count);
            Assert.AreEqual(2, map[CameraFacing.NW].Count);
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
