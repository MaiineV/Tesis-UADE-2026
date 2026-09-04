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

        [Test]
        public void AccumulateRotationDrag_FastFlick_FiresSingleStep()
        {
            // Arrange — un flick violento acumula muchos umbrales en un solo evento.
            var start = _service.CurrentFacing;

            // Act
            _service.AccumulateRotationDrag(_config.DragPixelsPerStep * 5f);

            // Assert — con cooldown activo dispara UN paso, no una ráfaga (el
            // temblor del bug original).
            Assert.AreEqual(WrapFacing((int)start + 45), _service.CurrentFacing);
        }

        [Test]
        public void AccumulateRotationDrag_SecondCallDuringCooldown_NoSecondStep()
        {
            // Arrange
            var start = _service.CurrentFacing;

            // Act — dos umbrales completos en el mismo instante (elapsed = 0 < cooldown).
            _service.AccumulateRotationDrag(_config.DragPixelsPerStep);
            _service.AccumulateRotationDrag(_config.DragPixelsPerStep);

            // Assert
            Assert.AreEqual(WrapFacing((int)start + 45), _service.CurrentFacing);
        }

        [Test]
        public void AccumulateRotationDrag_ZeroCooldown_FiresAllAccumulatedSteps()
        {
            // Arrange — cooldown 0 = comportamiento legacy (todos los pasos acumulados).
            _config.RotationStepCooldownSeconds = 0f;
            var start = _service.CurrentFacing;

            // Act
            _service.AccumulateRotationDrag(_config.DragPixelsPerStep * 3f);

            // Assert
            Assert.AreEqual(WrapFacing((int)start + 135), _service.CurrentFacing);
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

        // BUG-068: el tutorial gatea su paso de cámara con ZoomChanged — necesita
        // distinguir "el jugador hizo zoom de verdad" de "scrolleó pero ya estaba clampeado".

        [Test]
        public void ZoomBy_WithChange_FiresZoomChangedEvent()
        {
            // Arrange
            float? received = null;
            var beforeZoom = _service.CurrentZoom;
            _service.ZoomChanged += z => received = z;

            // Act — config fresca arranca a mitad de rango (ZoomMin=6, ZoomMax=22,
            // DefaultZoom=9): un paso positivo no clampea.
            _service.ZoomBy(1f);

            // Assert
            Assert.IsTrue(received.HasValue,
                "ZoomChanged debe dispararse cuando el clamp movió _targetZoom.");
            Assert.AreNotEqual(beforeZoom, received.Value);
        }

        [Test]
        public void ZoomBy_ClampedWithNoChange_DoesNotFireZoomChangedEvent()
        {
            // Arrange — empuja el zoom hasta ZoomMax (clampeado).
            for (int i = 0; i < 50; i++) _service.ZoomBy(10f);
            bool fired = false;
            _service.ZoomChanged += _ => fired = true;

            // Act — ya está en el máximo; un scroll positivo más no mueve _targetZoom.
            _service.ZoomBy(10f);

            // Assert
            Assert.IsFalse(fired,
                "Sin cambio real en _targetZoom (clampeado), ZoomChanged no debe disparar.");
        }

        [Test]
        public void ZoomBy_WhenDisabled_DoesNotFireZoomChangedEvent()
        {
            // Arrange
            _config.EnableZoom = false;
            bool fired = false;
            _service.ZoomChanged += _ => fired = true;

            // Act
            _service.ZoomBy(1f);

            // Assert
            Assert.IsFalse(fired);
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
            // Arrange — default StartingFacing = NE ⇒ OcclusionMap[NE] = { W, SW, S }
            // (opuesto al facing + diagonales: walls del lado de la cámara).
            // El fixture sólo tiene N/E/S/W walls, así que W y S deben ocultarse,
            // N y E quedan visibles.
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

            // Assert — StartingFacing = NE ⇒ ocultar S y W (lado opuesto al facing).
            Assert.IsFalse(nWall.IsHidden);
            Assert.IsFalse(eWall.IsHidden);
            Assert.IsTrue (sWall.IsHidden);
            Assert.IsTrue (wWall.IsHidden);
        }

        [Test]
        public void RotateBy45_FromN_HidesSouthWall()
        {
            // Arrange — facing N ⇒ OcclusionMap[N] = { SW, S, SE }. En el fixture
            // sólo hay N/E/S/W, así que sólo la S wall queda oculta.
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);
            while (_service.CurrentFacing != CameraFacing.N) _service.RotateBy45(clockwise: true);

            // Assert
            Assert.AreEqual(CameraFacing.N, _service.CurrentFacing);
            Assert.IsFalse(nWall.IsHidden, "N wall visible when facing N.");
            Assert.IsFalse(eWall.IsHidden, "E wall visible when facing N.");
            Assert.IsTrue (sWall.IsHidden, "S wall hidden when facing N.");
            Assert.IsFalse(wWall.IsHidden, "W wall visible when facing N.");
        }

        [Test]
        public void RotateBy45_FromSeToS_SwitchesHiddenWallFromWestNorthToNorthOnly()
        {
            // Arrange — facing SE ⇒ OcclusionMap[SE] = { W, NW, N }.
            RegisterFakeDungeonWithOccluders(
                out var nWall, out var eWall, out var sWall, out var wWall);
            while (_service.CurrentFacing != CameraFacing.SE) _service.RotateBy45(clockwise: true);

            Assert.IsTrue(nWall.IsHidden && wWall.IsHidden, "Pre-condition: N+W hidden at SE.");

            // Act — rotate clockwise once → S.
            _service.RotateBy45(clockwise: true);

            // Assert — OcclusionMap[S] = { NW, N, NE }. Del fixture sólo N aplica.
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

        // ------------------------------------------------------------------ //
        // Registro en el ServiceLocator                                       //
        // ------------------------------------------------------------------ //

        // Regresión: la cámara se registraba en ServiceScope.Run. Unity corre TODOS los
        // Awake antes de cualquier Start, así que el registro se hacía y acto seguido
        // GameplayBootstrapper.Start → StartRun → ClearScope(Run) lo borraba. Nadie lo
        // volvía a registrar (Awake no corre dos veces) y tanto el SetFollowTarget del
        // bootstrapper como el recenter del RoomGridLoader dejaban de resolver el service:
        // la cámara no se centraba ni al arrancar ni al cambiar de sala.

        [Test]
        public void Initialize_RegistersItselfAsCameraService()
        {
            // El SetUp ya llamó Initialize sobre _service.
            Assert.IsTrue(ServiceLocator.TryGetService<ICameraService>(out var registered));
            Assert.AreSame(_service, registered);
        }

        [Test]
        public void Initialize_RegistersOutsideRunScope_SurvivesClearScopeRun()
        {
            ServiceLocator.ClearScope(ServiceScope.Run);

            Assert.IsTrue(ServiceLocator.TryGetService<ICameraService>(out var registered),
                "La cámara vive con la scene, no con la run. Si ClearScope(Run) la borra, " +
                "StartRun la desregistra y se pierde el recenter al entrar a una sala.");
            Assert.AreSame(_service, registered);
        }

        // El desregistro en OnDestroy (lo que hace seguro el scope Global cuando se
        // descarga la scene) no se cubre acá: EditMode no dispara los callbacks de
        // lifecycle de Unity — ni Awake ni OnDestroy — así que el test pasaría por
        // motivos equivocados. Verificado en playtest.

        // El otro extremo de esta regresión — que StartRun no desregistre una cámara ya
        // inicializada — se cubre en RunBootstrapperTests, el fixture que ya tiene armado
        // el entorno de arranque de run.

        // ------------------------------------------------------------------ //
        // Paneo entre salas (Feature#0086)                                    //
        // ------------------------------------------------------------------ //

        // Sin Play Mode el reanclado toma el camino snap: el foco salta al centro nuevo
        // y OnCameraRoomPanFinished se emite en el acto. El tween real se valida en smoke.

        private Transform ArrangeStaticCameraWithTargetAt(Vector3 position)
        {
            _config.FollowPlayer = false;
            ServiceLocator.AddService<IDungeonService>(new FakeDungeonService(), ServiceScope.Run);

            var targetGO = new GameObject("target");
            _created.Add(targetGO);
            targetGO.transform.position = position;
            _service.SetFollowTarget(targetGO.transform);
            return targetGO.transform;
        }

        [Test]
        public void ApplyPendingReanchor_AfterRoomCrossed_MovesFocusAndFiresRoomPanFinished()
        {
            // Arrange
            var target = ArrangeStaticCameraWithTargetAt(Vector3.zero);
            Assume.That(_service.StaticFocus, Is.EqualTo(Vector3.zero));
            int finished = 0;
            EventManager.Subscribe(EventName.OnCameraRoomPanFinished, _ => finished++);

            target.position = new Vector3(10f, 0f, 10f);
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "next");
            EventManager.Trigger(EventName.OnRoomCrossed, Guid.NewGuid(), Guid.NewGuid());
            _service.RecenterOnPlayer(instant: true);

            // Act
            _service.ApplyPendingReanchor();

            // Assert
            Assert.AreEqual(new Vector3(10f, 0f, 10f), _service.StaticFocus);
            Assert.AreEqual(1, finished, "El aterrizaje del foco avisa una sola vez.");
            Assert.IsFalse(_service.IsRoomPanning, "Fuera de Play Mode no hay tween.");
        }

        [Test]
        public void ApplyPendingReanchor_WithoutRoomCrossed_MovesFocusSilently()
        {
            // Arrange — primera sala del piso / resume: reanclado sin cruce.
            var target = ArrangeStaticCameraWithTargetAt(Vector3.zero);
            bool finished = false;
            EventManager.Subscribe(EventName.OnCameraRoomPanFinished, _ => finished = true);

            target.position = new Vector3(4f, 0f, -4f);
            _service.RecenterOnPlayer(instant: true);

            // Act
            _service.ApplyPendingReanchor();

            // Assert
            Assert.AreEqual(new Vector3(4f, 0f, -4f), _service.StaticFocus);
            Assert.IsFalse(finished, "Sin cruce no hay sala saliente que liberar.");
        }

        [Test]
        public void ApplyPendingReanchor_WithoutPendingRequest_IsNoop()
        {
            // Arrange
            ArrangeStaticCameraWithTargetAt(new Vector3(1f, 0f, 1f));
            bool finished = false;
            EventManager.Subscribe(EventName.OnCameraRoomPanFinished, _ => finished = true);
            EventManager.Trigger(EventName.OnRoomCrossed, Guid.NewGuid(), Guid.NewGuid());

            // Act — nadie pidió reanclar (ni RoomGridLoader ni recenter).
            _service.ApplyPendingReanchor();

            // Assert
            Assert.AreEqual(new Vector3(1f, 0f, 1f), _service.StaticFocus);
            Assert.IsFalse(finished);
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
            public bool SetRoomState(Guid id, RoomState state) => false;
            public void ResyncDoorVisuals(Guid id) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Occluders;
        }
    }
}
