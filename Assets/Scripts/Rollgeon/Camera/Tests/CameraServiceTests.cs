using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using UnityEngine;

namespace Rollgeon.GameCamera.Tests
{
    [TestFixture]
    public class CameraServiceTests
    {
        private GameObject _cameraGO;
        private CameraService _service;
        private CameraConfigSO _config;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _cameraGO = new GameObject("TestMainCamera", typeof(UnityEngine.Camera));
            _created.Add(_cameraGO);

            _config = ScriptableObject.CreateInstance<CameraConfigSO>();
            _created.Add(_config);

            _service = _cameraGO.AddComponent<CameraService>();
            _service.Initialize(_config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ------------------------------------------------------------------ //
        // Rotation                                                            //
        // ------------------------------------------------------------------ //

        [Test]
        public void RotateBy45_Clockwise_AdvancesOneStep()
        {
            var start = _service.CurrentFacing;
            _service.RotateBy45(clockwise: true);
            var expected = WrapFacing((int)start + 45);
            Assert.AreEqual(expected, _service.CurrentFacing);
        }

        [Test]
        public void RotateBy45_EightClockwiseSteps_ReturnsToStart()
        {
            var start = _service.CurrentFacing;
            for (int i = 0; i < 8; i++) _service.RotateBy45(clockwise: true);
            Assert.AreEqual(start, _service.CurrentFacing);
        }

        [Test]
        public void RotateBy45_CounterClockwiseFromN_WrapsToNW()
        {
            // Force to N first
            while (_service.CurrentFacing != CameraFacing.N) _service.RotateBy45(clockwise: true);
            _service.RotateBy45(clockwise: false);
            Assert.AreEqual(CameraFacing.NW, _service.CurrentFacing);
        }

        [Test]
        public void RotateBy45_FiresFacingChangedEvent()
        {
            CameraFacing? received = null;
            _service.FacingChanged += f => received = f;

            _service.RotateBy45(clockwise: true);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(_service.CurrentFacing, received.Value);
        }

        [Test]
        public void RotateBy45_WhenDisabled_IsNoop()
        {
            _config.EnableRotation = false;
            var start = _service.CurrentFacing;
            _service.RotateBy45(clockwise: true);
            Assert.AreEqual(start, _service.CurrentFacing);
        }

        [Test]
        public void AccumulateRotationDrag_TriggersStepAtThreshold()
        {
            var start = _service.CurrentFacing;
            _service.AccumulateRotationDrag(_config.DragPixelsPerStep);
            Assert.AreEqual(WrapFacing((int)start + 45), _service.CurrentFacing);
        }

        [Test]
        public void AccumulateRotationDrag_BelowThreshold_NoStep()
        {
            var start = _service.CurrentFacing;
            _service.AccumulateRotationDrag(_config.DragPixelsPerStep - 1f);
            Assert.AreEqual(start, _service.CurrentFacing);
        }

        // ------------------------------------------------------------------ //
        // Pan                                                                 //
        // ------------------------------------------------------------------ //

        [Test]
        public void PanBy_SetsIsPanningTrue()
        {
            Assert.IsFalse(_service.IsPanning);
            _service.PanBy(new Vector2(10f, 0f));
            Assert.IsTrue(_service.IsPanning);
        }

        [Test]
        public void PanBy_WhenDisabled_StaysNotPanning()
        {
            _config.EnablePan = false;
            _service.PanBy(new Vector2(10f, 0f));
            Assert.IsFalse(_service.IsPanning);
        }

        [Test]
        public void PanBy_ZeroDelta_Noop()
        {
            _service.PanBy(Vector2.zero);
            Assert.IsFalse(_service.IsPanning);
        }

        // ------------------------------------------------------------------ //
        // Zoom                                                                //
        // ------------------------------------------------------------------ //

        [Test]
        public void ZoomBy_ClampsToMax()
        {
            for (int i = 0; i < 50; i++) _service.ZoomBy(10f);
            // targetZoom is internal; proxy via floor view gate having fired or
            // by crossing FloorViewZoomThreshold — just assert no explosion and
            // the floor view has toggled on since the max is above threshold.
            Assert.IsTrue(_service.IsFloorView);
        }

        [Test]
        public void ZoomBy_WhenDisabled_DoesNotToggleFloorView()
        {
            _config.EnableZoom = false;
            for (int i = 0; i < 50; i++) _service.ZoomBy(10f);
            Assert.IsFalse(_service.IsFloorView);
        }

        [Test]
        public void ZoomBy_CrossingThreshold_FiresFloorViewEvent()
        {
            bool? received = null;
            _service.FloorViewToggled += v => received = v;

            // Fresh config starts mid-range; pump enough positive zoom to cross threshold
            for (int i = 0; i < 50; i++) _service.ZoomBy(10f);

            Assert.IsTrue(received.HasValue);
            Assert.IsTrue(received.Value);
        }

        // ------------------------------------------------------------------ //
        // Recenter / Follow                                                   //
        // ------------------------------------------------------------------ //

        [Test]
        public void SetFollowTarget_AssignsAndResetsPan()
        {
            var targetGO = new GameObject("target");
            _created.Add(targetGO);

            _service.PanBy(new Vector2(10f, 0f));
            Assert.IsTrue(_service.IsPanning);

            _service.SetFollowTarget(targetGO.transform);

            Assert.AreSame(targetGO.transform, _service.FollowTarget);
            Assert.IsFalse(_service.IsPanning);
        }

        [Test]
        public void SetFollowTarget_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.SetFollowTarget(null));
            Assert.IsNull(_service.FollowTarget);
        }

        [Test]
        public void RecenterOnPlayer_Instant_ClearsPanningFlag()
        {
            var targetGO = new GameObject("target");
            _created.Add(targetGO);
            _service.SetFollowTarget(targetGO.transform);

            _service.PanBy(new Vector2(20f, 20f));
            Assert.IsTrue(_service.IsPanning);

            _service.RecenterOnPlayer(instant: true);
            Assert.IsFalse(_service.IsPanning);
        }

        [Test]
        public void RecenterOnPlayer_FiresRecenteredEvent()
        {
            bool received = false;
            EventManager.Subscribe(EventName.OnCameraRecentered, _ => received = true);

            var targetGO = new GameObject("target");
            _created.Add(targetGO);
            _service.SetFollowTarget(targetGO.transform);
            _service.RecenterOnPlayer(instant: true);

            Assert.IsTrue(received);
        }

        // ------------------------------------------------------------------ //
        // Shake (TODO v8 scaffold)                                            //
        // ------------------------------------------------------------------ //

        [Test]
        public void Shake_WithZeroDuration_Noop()
        {
            Assert.DoesNotThrow(() => _service.Shake(0.5f, 0f));
        }

        [Test]
        public void Shake_WithZeroAmplitude_Noop()
        {
            Assert.DoesNotThrow(() => _service.Shake(0f, 0.5f));
        }

        // ------------------------------------------------------------------ //
        // Helpers                                                             //
        // ------------------------------------------------------------------ //

        private static CameraFacing WrapFacing(int degrees)
        {
            int d = ((degrees % 360) + 360) % 360;
            return (CameraFacing)d;
        }
    }
}
