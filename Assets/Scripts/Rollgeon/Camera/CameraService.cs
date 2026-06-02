using System;
using Patterns;
using PrimeTween;
using Rollgeon.Dungeon;
using UnityEngine;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Implementación MonoBehaviour de <see cref="ICameraService"/>. Vive en el
    /// <c>Main Camera</c> de <c>02_Gameplay.unity</c>, inicializada por
    /// <see cref="CameraServiceBootstrap"/> con un <see cref="CameraConfigSO"/>.
    /// TECHNICAL.md §17.E.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    [AddComponentMenu("Rollgeon/Camera/Camera Service")]
    public sealed class CameraService : MonoBehaviour, ICameraService
    {
        private CameraConfigSO _config;
        private UnityEngine.Camera _camera;
        private Transform _rig;

        private Transform _followTarget;
        private CameraFacing _currentFacing;
        private float _targetZoom;
        private float _currentZoom;
        private Vector3 _panOffset;
        private bool _isPanning;
        private bool _isFloorView;
        private float _pendingDragPixels;

        private Tween _rotationTween;
        private Tween _zoomTween;
        private Tween _recenterTween;
        private Tween _shakeTween;

        private EventManager.EventReceiver _onRoomEnteredHandler;

        static readonly int s_PixelPanOffset = Shader.PropertyToID("_PixelPanOffset");

        public CameraFacing CurrentFacing => _currentFacing;
        public float CurrentZoom => _currentZoom;
        public Transform FollowTarget => _followTarget;
        public bool IsPanning => _isPanning;
        public bool IsFloorView => _isFloorView;

        public event Action<CameraFacing> FacingChanged;
        public event Action<bool> FloorViewToggled;

        [SerializeField]
        [Tooltip("Override opcional del CameraConfigSO. Si es null, el service lo resuelve " +
                 "desde el ServiceLocator (scope Global) en Awake.")]
        private CameraConfigSO _configOverride;

        /// <summary>
        /// Inicialización pública. Útil para tests y para bootstraps que quieran
        /// wirear la cámara manualmente sin pasar por <see cref="Awake"/>.
        /// </summary>
        public void Initialize(CameraConfigSO config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _camera = GetComponent<UnityEngine.Camera>();
            _rig = transform;

            _currentFacing = _config.StartingFacing;
            _currentZoom = Mathf.Clamp(_config.DefaultZoom, _config.ZoomMin, _config.ZoomMax);
            _targetZoom = _currentZoom;

            ApplyInitialPose();
            ApplyZoomImmediate(_currentZoom);

            // Suscripción a OnRoomEntered: cuando aparece una room nueva, sus walls
            // recién entonces existen — hay que aplicarles el facing actual o quedan
            // todas visibles hasta que el usuario rote la cámara.
            if (_onRoomEnteredHandler == null)
            {
                _onRoomEnteredHandler = HandleRoomEntered;
                EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
            }

            RefreshWallOcclusion();
        }

        private void HandleRoomEntered(params object[] args) => RefreshWallOcclusion();

        /// <summary>
        /// Autowire para uso en la scene de gameplay: resuelve el
        /// <see cref="CameraConfigSO"/> (override o desde <see cref="ServiceLocator"/>),
        /// se inicializa y se registra como <see cref="ICameraService"/> en
        /// <see cref="ServiceScope.Run"/> (§17.E — "registrado al despertar").
        /// Tests y bootstraps manuales pueden llamar <see cref="Initialize"/>
        /// primero — <c>Awake</c> detecta que ya hay config y no hace nada.
        /// </summary>
        private void Awake()
        {
            if (_config != null) return;

            var config = _configOverride;
            if (config == null)
            {
                ServiceLocator.TryGetService(out config);
            }
            if (config == null) return;  // inerte hasta que algo llame Initialize()

            Initialize(config);
            ServiceLocator.AddService<ICameraService>(this, ServiceScope.Run);
        }

        private void OnDestroy()
        {
            if (_onRoomEnteredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
                _onRoomEnteredHandler = null;
            }

            if (ServiceLocator.TryGetService<ICameraService>(out var current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.RemoveService<ICameraService>();
            }
        }

        /// <summary>
        /// Devuelve el yaw efectivo para un <see cref="CameraFacing"/> dado.
        /// Cardinales (N/E/S/W) = múltiplo exacto de 90°; sin offset.
        /// Diagonales (NE/SE/SW/NW) = base + <see cref="CameraConfigSO.DiagonalYawOffset"/>.
        /// </summary>
        private float GetYawForFacing(CameraFacing facing)
        {
            float baseYaw = (float)facing;
            bool isDiagonal = ((int)facing % 90) != 0;
            return isDiagonal && _config != null
                ? baseYaw + _config.DiagonalYawOffset
                : baseYaw;
        }

        private void ApplyInitialPose()
        {
            if (_rig == null) return;
            _rig.rotation = Quaternion.Euler(_config.PitchDegrees, GetYawForFacing(_currentFacing), 0f);
        }

        // ------------------------------------------------------------------ //
        // Follow target                                                      //
        // ------------------------------------------------------------------ //

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            _panOffset = Vector3.zero;
            _isPanning = false;

            if (target != null && _rig != null)
            {
                PlaceRigAt(target.position);
            }
        }

        private void LateUpdate()
        {
            if (_config == null || _rig == null) return;

            if (_followTarget != null)
            {
                PlaceRigAt(_followTarget.position + _panOffset);
            }
            else if (_isPanning)
            {
                PlaceRigAt(_panOffset);
            }

            // Pixel snap siempre al final, después de todo posicionamiento.
            if (_config.EnablePixelSnap)
                ApplyPixelSnap();
        }

        // ------------------------------------------------------------------ //
        // Pixel Snap                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Snappea la posición del rig a la grilla de texels del RenderTexture.
        /// Proyecta la posición world sobre los ejes screen-plane de la cámara
        /// via dot product (no InverseTransformPoint — evita drift por origen).
        /// El error de redondeo se empuja como _PixelPanOffset al SharpUpscale
        /// shader para compensar en UV y recuperar movimiento perfectamente suave.
        /// </summary>
        private void ApplyPixelSnap()
        {
            if (_camera == null || !_camera.orthographic) return;
            if (_config.PixelRenderHeight <= 0) return;

            float texelSize = (_camera.orthographicSize * 2f) / _config.PixelRenderHeight;
            if (texelSize <= 1e-5f) return;

            Vector3 pos   = _rig.position;
            Vector3 right = _camera.transform.right;
            Vector3 up    = _camera.transform.up;

            float projRight    = Vector3.Dot(pos, right);
            float projUp       = Vector3.Dot(pos, up);
            float snappedRight = Mathf.Round(projRight / texelSize) * texelSize;
            float snappedUp    = Mathf.Round(projUp    / texelSize) * texelSize;
            float errRight     = snappedRight - projRight;
            float errUp        = snappedUp    - projUp;

            _rig.position = pos + right * errRight + up * errUp;

            float uvErrX = errRight / (_camera.orthographicSize * 2f * _camera.aspect);
            float uvErrY = errUp    / (_camera.orthographicSize * 2f);
            Shader.SetGlobalVector(s_PixelPanOffset, new Vector4(uvErrX, uvErrY, 0f, 0f));
        }

        /// <summary>
        /// Reposiciona el rig detrás del <paramref name="focus"/> usando la
        /// rotación actual del rig (que puede estar mid-tween). Invariante
        /// §17.E.5: no tocamos la rotación acá — sólo la posición.
        /// </summary>
        private void PlaceRigAt(Vector3 focus)
        {
            var offset = _rig.rotation * Vector3.forward * _config.DistanceFromTarget;
            _rig.position = focus - offset;
        }

        // ------------------------------------------------------------------ //
        // Rotation                                                            //
        // ------------------------------------------------------------------ //

        public void RotateBy45(bool clockwise)
        {
            if (_config == null || !_config.EnableRotation) return;

            int step = clockwise ? 1 : -1;
            int nextYaw = ((int)_currentFacing + step * (int)_config.RotationStepDegrees + 360) % 360;
            _currentFacing = (CameraFacing)nextYaw;

            if (_rotationTween.isAlive) _rotationTween.Stop();

            var targetRot = Quaternion.Euler(_config.PitchDegrees, GetYawForFacing(_currentFacing), 0f);

            if (_config.RotationTweenSeconds <= 0f)
            {
                _rig.rotation = targetRot;
            }
            else
            {
                _rotationTween = Tween.Rotation(
                    _rig,
                    targetRot,
                    _config.RotationTweenSeconds,
                    _config.RotationEase);
            }

            FacingChanged?.Invoke(_currentFacing);
            EventManager.Trigger(EventName.OnCameraFacingChanged, _currentFacing);
            RefreshWallOcclusion();
        }

        /// <summary>
        /// Drag accumulator (§17.E.4). Sumar pixeles de delta; dispara
        /// <see cref="RotateBy45"/> cada <c>DragPixelsPerStep</c>.
        /// </summary>
        public void AccumulateRotationDrag(float deltaPixels)
        {
            if (_config == null || !_config.EnableRotation) return;
            _pendingDragPixels += deltaPixels;

            while (_pendingDragPixels >= _config.DragPixelsPerStep)
            {
                RotateBy45(clockwise: true);
                _pendingDragPixels -= _config.DragPixelsPerStep;
            }
            while (_pendingDragPixels <= -_config.DragPixelsPerStep)
            {
                RotateBy45(clockwise: false);
                _pendingDragPixels += _config.DragPixelsPerStep;
            }
        }

        /// <summary>Resetea el accumulator de drag (al soltar el modifier).</summary>
        public void ResetRotationDrag() => _pendingDragPixels = 0f;

        // ------------------------------------------------------------------ //
        // Pan                                                                 //
        // ------------------------------------------------------------------ //

        public void PanBy(Vector2 screenDelta)
        {
            if (_config == null || !_config.EnablePan) return;
            if (screenDelta == Vector2.zero) return;

            _isPanning = true;

            // Convertir delta de pantalla a world, relativo a los ejes del rig.
            // Mouse delta "arrastrar a la derecha" debe mover la cámara a la izquierda
            // del punto focal (pan natural).
            var yaw = (float)_currentFacing;
            var worldRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var worldForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            var delta = (-screenDelta.x * worldRight - screenDelta.y * worldForward)
                        * (_config.PanSpeed * Time.deltaTime);

            _panOffset += delta;

            if (_config.PanClampToFloorBounds)
            {
                _panOffset = ClampPanToFloor(_panOffset);
            }
        }

        private Vector3 ClampPanToFloor(Vector3 offset)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return offset;

            var bounds = dungeon.GetFloorBounds();
            if (bounds.size == Vector3.zero) return offset;

            var focus = (_followTarget != null ? _followTarget.position : Vector3.zero) + offset;
            var clamped = new Vector3(
                Mathf.Clamp(focus.x, bounds.min.x, bounds.max.x),
                focus.y,
                Mathf.Clamp(focus.z, bounds.min.z, bounds.max.z));
            return clamped - (_followTarget != null ? _followTarget.position : Vector3.zero);
        }

        // ------------------------------------------------------------------ //
        // Zoom                                                                //
        // ------------------------------------------------------------------ //

        public void ZoomBy(float scrollDelta)
        {
            if (_config == null || !_config.EnableZoom) return;
            if (Mathf.Approximately(scrollDelta, 0f)) return;

            var previousTarget = _targetZoom;
            _targetZoom = Mathf.Clamp(
                _targetZoom + scrollDelta * _config.ZoomStep,
                _config.ZoomMin,
                _config.ZoomMax);

            if (Mathf.Approximately(previousTarget, _targetZoom)) return;

            if (_zoomTween.isAlive) _zoomTween.Stop();

            if (_config.ZoomTweenSeconds <= 0f)
            {
                ApplyZoomImmediate(_targetZoom);
            }
            else
            {
                var startZoom = _currentZoom;
                var endZoom = _targetZoom;
                _zoomTween = Tween.Custom(
                    startValue: startZoom,
                    endValue: endZoom,
                    duration: _config.ZoomTweenSeconds,
                    onValueChange: v => ApplyZoomImmediate(v),
                    ease: _config.ZoomEase);
            }

            EvaluateFloorViewGate();
        }

        private void ApplyZoomImmediate(float zoom)
        {
            _currentZoom = zoom;
            if (_camera == null) return;

            if (_config.IsOrthographic)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = zoom;
            }
            else
            {
                // perspectiva: modula la distancia del rig al target
                // re-aplicado en LateUpdate via DistanceFromTarget — acá sólo guardamos el zoom
            }
        }

        private void EvaluateFloorViewGate()
        {
            if (_config == null || !_config.EnableFloorView) return;

            bool shouldBeFloorView = _targetZoom >= _config.FloorViewZoomThreshold;
            if (shouldBeFloorView == _isFloorView) return;

            _isFloorView = shouldBeFloorView;
            FloorViewToggled?.Invoke(_isFloorView);
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, _isFloorView);
        }

        // ------------------------------------------------------------------ //
        // Recenter                                                            //
        // ------------------------------------------------------------------ //

        public void RecenterOnPlayer(bool instant = false)
        {
            if (_followTarget == null)
            {
                _panOffset = Vector3.zero;
                _isPanning = false;
                return;
            }

            if (_recenterTween.isAlive) _recenterTween.Stop();

            if (instant || _config == null || _config.RecenterTweenSeconds <= 0f)
            {
                _panOffset = Vector3.zero;
                _isPanning = false;
            }
            else
            {
                var startOffset = _panOffset;
                _recenterTween = Tween.Custom(
                    startValue: 0f,
                    endValue: 1f,
                    duration: _config.RecenterTweenSeconds,
                    onValueChange: t => _panOffset = Vector3.LerpUnclamped(startOffset, Vector3.zero, t),
                    ease: _config.RecenterEase);
                _isPanning = false;
            }

            EventManager.Trigger(EventName.OnCameraRecentered, instant);
        }

        // ------------------------------------------------------------------ //
        // Wall occlusion                                                      //
        // ------------------------------------------------------------------ //

        private void RefreshWallOcclusion()
        {
            if (_config == null || !_config.EnableWallOcclusion) return;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return;

            var occluders = dungeon.GetCurrentRoomOccluders();
            if (occluders == null) return;

            _config.OcclusionMap.TryGetValue(_currentFacing, out var hiddenDirs);

            foreach (var occ in occluders)
            {
                if (occ == null) continue;
                bool hide = hiddenDirs != null && hiddenDirs.Contains(occ.Direction);
                occ.SetHidden(hide, _config.WallFadeSeconds);
            }
        }

        // ------------------------------------------------------------------ //
        // Shake (§17.E.10 — TODO v8 scaffold)                                //
        // ------------------------------------------------------------------ //

        public void Shake(float amplitude, float durationSeconds)
        {
            if (_config == null || _rig == null) return;
            if (amplitude <= 0f || durationSeconds <= 0f) return;

            if (_shakeTween.isAlive) _shakeTween.Stop();

            _shakeTween = Tween.ShakeLocalPosition(
                _rig,
                strength: Vector3.one * amplitude,
                duration: durationSeconds);
        }
    }
}
