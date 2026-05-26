using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using UnityEngine;

namespace Rollgeon.GameCamera.Tests
{
    [TestFixture]
    public class CameraServiceTests
    {
        private GameObject _cameraGO;
        private CameraService _service;
        private CameraConfigSO _config;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

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
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
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
        // Wall occlusion                                                      //
        // ------------------------------------------------------------------ //

        [Test]
        public void Initialize_WithRegisteredDungeon_HidesWallsForStartingFacing()
        {
            // Arrange — default StartingFacing = NE ⇒ OcclusionMap[NE] = { S, W }.
            // En este test el service ya fue Initialize'd por [SetUp] ANTES de que
            // exista el fake dungeon (igual que en runtime cuando el dungeon no está
            // listo todavía). El refresh debe disparar al re-inicializar.
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);

            // Act — re-Initialize fuerza un RefreshWallOcclusion ahora que el dungeon existe.
            _service.Initialize(_config);

            // Assert
            Assert.AreEqual(CameraFacing.NE, _service.CurrentFacing);
            Assert.IsFalse(nWall.IsHidden, "N wall must remain visible when facing NE.");
            Assert.IsFalse(eWall.IsHidden, "E wall must remain visible when facing NE.");
            Assert.IsTrue (sWall.IsHidden, "S wall must hide when facing NE (OcclusionMap[NE]).");
            Assert.IsTrue (wWall.IsHidden, "W wall must hide when facing NE (OcclusionMap[NE]).");
        }

        [Test]
        public void OnRoomEntered_RefreshesOccluderState()
        {
            // Arrange — el service se inicializó sin dungeon en [SetUp]; ahora aparece
            // una room nueva y dispara OnRoomEntered. El service debe reaccionar.
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);

            // Act
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "test_room");

            // Assert — StartingFacing = NE ⇒ ocultar S y W.
            Assert.IsFalse(nWall.IsHidden);
            Assert.IsFalse(eWall.IsHidden);
            Assert.IsTrue (sWall.IsHidden);
            Assert.IsTrue (wWall.IsHidden);
        }

        [Test]
        public void RotateBy45_FromN_HidesOnlySouthWall()
        {
            // Arrange — forzar facing a N para cubrir el caso del usuario:
            // "si estoy en 0 grados, la pared S debería esconderse".
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);
            while (_service.CurrentFacing != CameraFacing.N) _service.RotateBy45(clockwise: true);

            // Sanity: rotar a N debió aplicar el occlusion map de N (= { S }).
            // Act — assertion directa: walls reflect facing == N.
            // Assert
            Assert.AreEqual(CameraFacing.N, _service.CurrentFacing);
            Assert.IsFalse(nWall.IsHidden, "N wall visible when facing N.");
            Assert.IsFalse(eWall.IsHidden, "E wall visible when facing N.");
            Assert.IsTrue (sWall.IsHidden, "S wall hidden when facing N.");
            Assert.IsFalse(wWall.IsHidden, "W wall visible when facing N.");
        }

        [Test]
        public void RotateBy45_FromSeToS_SwitchesHiddenWallFromWestNorthToNorth()
        {
            // Arrange — facing SE ⇒ OcclusionMap[SE] = { W, N }.
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);
            while (_service.CurrentFacing != CameraFacing.SE) _service.RotateBy45(clockwise: true);

            Assert.IsTrue(nWall.IsHidden && wWall.IsHidden, "Pre-condition: N+W hidden at SE.");

            // Act — rotate clockwise once → S.
            _service.RotateBy45(clockwise: true);

            // Assert — OcclusionMap[S] = { N } only.
            Assert.AreEqual(CameraFacing.S, _service.CurrentFacing);
            Assert.IsTrue (nWall.IsHidden, "N wall stays hidden when facing S.");
            Assert.IsFalse(eWall.IsHidden);
            Assert.IsFalse(sWall.IsHidden);
            Assert.IsFalse(wWall.IsHidden, "W wall should reveal after rotating SE → S.");
        }

        [Test]
        public void RefreshWallOcclusion_WhenDisabled_DoesNotMutateOccluders()
        {
            // Arrange
            _config.EnableWallOcclusion = false;
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);

            // Act
            _service.Initialize(_config);

            // Assert — todos siguen visibles aunque el facing default (NE) querría ocultar S/W.
            Assert.IsFalse(nWall.IsHidden);
            Assert.IsFalse(eWall.IsHidden);
            Assert.IsFalse(sWall.IsHidden);
            Assert.IsFalse(wWall.IsHidden);
        }

        // ------------------------------------------------------------------ //
        // Helpers                                                             //
        // ------------------------------------------------------------------ //

        private static CameraFacing WrapFacing(int degrees)
        {
            int d = ((degrees % 360) + 360) % 360;
            return (CameraFacing)d;
        }

        private FakeDungeonService RegisterFakeDungeonWithOccluders(
            out WallOccluder n, out WallOccluder e, out WallOccluder s, out WallOccluder w)
        {
            n = CreateOccluder(WallDirection.N);
            e = CreateOccluder(WallDirection.E);
            s = CreateOccluder(WallDirection.S);
            w = CreateOccluder(WallDirection.W);

            var fake = new FakeDungeonService
            {
                Occluders = new[] { n, e, s, w }
            };
            ServiceLocator.AddService<IDungeonService>(fake, ServiceScope.Run);
            return fake;
        }

        private WallOccluder CreateOccluder(WallDirection dir)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(go);
            go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            var occ = go.AddComponent<WallOccluder>();
            occ.Direction = dir;
            return occ;
        }

        // -----------------------------------------------------------------
        // Stubs
        // -----------------------------------------------------------------

        private sealed class FakeDungeonService : IDungeonService
        {
            public WallOccluder[] Occluders = Array.Empty<WallOccluder>();

            public RoomSO CurrentRoom => null;
            public RoomInstance CurrentRoomInstance => null;
            public DoorDirection? LastEntryDirection => null;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() =>
                new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() =>
                new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id)
            {
                id = Guid.Empty;
                return false;
            }
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Occluders;
        }
    }
}
