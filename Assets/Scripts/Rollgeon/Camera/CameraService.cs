using System;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Patterns;
using PrimeTween;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
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
        // Foco capturado cuando la cámara está en modo estático (FollowPlayer = false):
        // la posición del target al momento de anclar (spawn / entrada a sala). La cámara
        // se encuadra sobre este punto y no trackea al player aunque se mueva.
        private Vector3 _staticFocus;
        private bool _hasStaticFocus;
        // Pedido de reanclar el foco estático en el próximo LateUpdate. Se setea en el
        // recenter (entrada a sala) porque ahí el pawn puede no estar reposicionado aún;
        // LateUpdate corre después de los Updates/eventos, con el pawn ya en su spawn.
        private bool _pendingStaticReanchor;
        private bool _isPanning;
        private bool _isFloorView;
        private float _pendingDragPixels;
        private float _lastRotationStepTime = float.NegativeInfinity;
        // Pan suavizado: el input mueve _panTarget y _panOffset lo persigue con
        // SmoothDamp en LateUpdate (PanLerpSeconds). Recenter/anclajes setean ambos.
        private Vector3 _panTarget;
        private Vector3 _panVelocity;

        private Tween _rotationTween;
        private Tween _zoomTween;
        private Tween _recenterTween;

        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeElapsed;

        private EventManager.EventReceiver _onRoomEnteredHandler;

        static readonly int s_PixelPanOffset = Shader.PropertyToID("_PixelPanOffset");

        public CameraFacing CurrentFacing => _currentFacing;
        public float CurrentZoom => _currentZoom;
        public Transform FollowTarget => _followTarget;
        public bool IsPanning => _isPanning;
        public bool IsFloorView => _isFloorView;

        public event Action<CameraFacing> FacingChanged;
        public event Action<bool> FloorViewToggled;
        public event Action<float> ZoomChanged;

        [SerializeField]
        [Tooltip("Override opcional del CameraConfigSO. Si es null, el service lo resuelve " +
                 "desde el ServiceLocator (scope Global) en Awake.")]
        private CameraConfigSO _configOverride;

        /// <summary>
        /// Inicialización pública: deja la cámara operativa y la registra como
        /// <see cref="ICameraService"/> en <see cref="ServiceScope.Global"/>. Útil para
        /// tests y para bootstraps que quieran wirear la cámara manualmente sin pasar por
        /// <see cref="Awake"/>. <see cref="OnDestroy"/> la desregistra.
        /// </summary>
        public void Initialize(CameraConfigSO config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _camera = GetComponent<UnityEngine.Camera>();
            _rig = transform;

            _currentFacing = _config.StartingFacing;
            _currentZoom = Mathf.Clamp(_config.DefaultZoom, _config.ZoomMin, _config.ZoomMax);
            _targetZoom = _currentZoom;
            SetPanImmediate(HomeOffset);

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

            // Global, no Run: la cámara vive y muere con la SCENE, no con la run. En Run
            // scope se desregistraba sola — Unity corre todos los Awake antes de cualquier
            // Start, así que este registro quedaba hecho y acto seguido
            // GameplayBootstrapper.Start → StartRun → ClearScope(Run) lo borraba, dejando la
            // cámara viva pero irresoluble: sin SetFollowTarget y sin recenter al entrar a
            // una sala. OnDestroy la desregistra al descargar la scene.
            ServiceLocator.AddService<ICameraService>(this, ServiceScope.Global);
        }

        private void HandleRoomEntered(params object[] args) => RefreshWallOcclusion();

        /// <summary>
        /// Autowire para uso en la scene de gameplay: resuelve el
        /// <see cref="CameraConfigSO"/> (override o desde <see cref="ServiceLocator"/>) y
        /// delega en <see cref="Initialize"/>, que es quien registra el service
        /// (§17.E — "registrado al despertar"). Tests y bootstraps manuales pueden llamar
        /// <see cref="Initialize"/> primero — <c>Awake</c> detecta que ya hay config y no
        /// hace nada.
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
        }

        private void OnEnable() => MMCameraShakeEvent.Register(OnFeelCameraShake);

        private void OnDisable() => MMCameraShakeEvent.Unregister(OnFeelCameraShake);

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
            SetPanImmediate(HomeOffset);
            _isPanning = false;

            if (target != null)
            {
                // Capturamos el foco estático para el modo no-follow (en follow se ignora
                // y se usa la posición viva del target).
                _staticFocus = ResolveStaticFocus();
                _hasStaticFocus = true;
                if (_rig != null) PlaceRigAt(FocusAnchor + _panOffset);
            }
            else
            {
                _hasStaticFocus = false;
            }
        }

        // Foco para el modo estático: el CENTRO de la sala actual (no la posición del
        // player, que spawnea pegado a la puerta de entrada y dejaba la cámara
        // descentrada). X/Z del centro del bounding box de la sala, a la altura del piso
        // (Y del player). Fallback a la posición del player si no hay sala/layout.
        private Vector3 ResolveStaticFocus()
        {
            Vector3 fallback = _followTarget != null ? _followTarget.position : _staticFocus;

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null)
                return fallback;

            var prefab = dungeon.CurrentRoomInstance?.SpawnedPrefab;
            if (prefab == null) return fallback;

            var layout = prefab.GetComponent<RoomLayout>();
            if (layout == null) return fallback;

            var center = layout.transform.TransformPoint(layout.LocalBounds.center);
            float floorY = _followTarget != null ? _followTarget.position.y : center.y;
            return new Vector3(center.x, floorY, center.z);
        }

        // Offset "home" del foco: la posición/encuadre default configurado. La cámara
        // arranca acá y el recenter vuelve a este encuadre. (0,0,0) por default =
        // centrado exacto en el player (comportamiento previo).
        private Vector3 HomeOffset => _config != null ? _config.DefaultFocusOffset : Vector3.zero;

        private void LateUpdate()
        {
            if (_config == null || _rig == null) return;

            if (_pendingStaticReanchor && _followTarget != null)
            {
                _staticFocus = ResolveStaticFocus();
                _hasStaticFocus = true;
                _pendingStaticReanchor = false;
            }

            SmoothPanOffset();

            if (_followTarget != null || _hasStaticFocus)
            {
                // Follow → trackea la posición actual del player. Estático → usa el foco
                // anclado (FocusAnchor lo resuelve según FollowPlayer). El _panOffset suma
                // el encuadre default + el pan manual en ambos casos.
                PlaceRigAt(FocusAnchor + _panOffset);
            }
            else if (_isPanning)
            {
                PlaceRigAt(_panOffset);
            }

            // Pixel snap siempre al final, después de todo posicionamiento.
            if (_config.EnablePixelSnap)
                ApplyPixelSnap();

            // Después del snap: el shake es ruido deliberado, no queremos que el snap lo coma.
            ApplyShake();
        }

        // Punto base sobre el que se encuadra la cámara. En modo follow = posición actual
        // del player (trackea). En modo estático (FollowPlayer = false) = el foco capturado
        // al anclar, así la cámara se queda quieta aunque el player se mueva.
        private Vector3 FocusAnchor
        {
            get
            {
                bool staticMode = _config != null && !_config.FollowPlayer && _hasStaticFocus;
                if (staticMode) return _staticFocus;
                return _followTarget != null ? _followTarget.position : Vector3.zero;
            }
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
        /// <see cref="RotateBy45"/> cada <c>DragPixelsPerStep</c>, con un
        /// mínimo de <c>RotationStepCooldownSeconds</c> entre pasos.
        /// </summary>
        public void AccumulateRotationDrag(float deltaPixels)
        {
            if (_config == null || !_config.EnableRotation) return;
            _pendingDragPixels += deltaPixels;

            float step = _config.DragPixelsPerStep;
            float cooldown = _config.RotationStepCooldownSeconds;

            if (cooldown <= 0f)
            {
                while (_pendingDragPixels >= step)
                {
                    RotateBy45(clockwise: true);
                    _pendingDragPixels -= step;
                }
                while (_pendingDragPixels <= -step)
                {
                    RotateBy45(clockwise: false);
                    _pendingDragPixels += step;
                }
                return;
            }

            // Un flick violento no debe encolar una ráfaga de pasos diferidos:
            // lo acumulado nunca supera un paso.
            _pendingDragPixels = Mathf.Clamp(_pendingDragPixels, -step, step);

            if (Time.unscaledTime - _lastRotationStepTime < cooldown) return;

            if (_pendingDragPixels >= step)
            {
                RotateBy45(clockwise: true);
            }
            else if (_pendingDragPixels <= -step)
            {
                RotateBy45(clockwise: false);
            }
            else
            {
                return;
            }

            // Consumir TODO lo pendiente: el próximo paso exige un umbral entero
            // de movimiento fresco — sensibilidad constante durante el drag.
            _pendingDragPixels = 0f;
            _lastRotationStepTime = Time.unscaledTime;
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
            var yaw = GetYawForFacing(_currentFacing);
            var worldRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var worldForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            // Con cámara orto la altura visible son 2×zoom world units repartidas en
            // Screen.height pixeles: escalar por eso hace que el pan se sienta igual
            // en pantalla a cualquier zoom (PanSpeed 1 = el piso acompaña al cursor 1:1).
            float worldPerPixel = (_currentZoom * 2f) / Mathf.Max(1, Screen.height);
            var delta = (-screenDelta.x * worldRight - screenDelta.y * worldForward)
                        * (worldPerPixel * _config.PanSpeed);

            _panTarget += delta;

            if (_config.PanClampToFloorBounds)
            {
                _panTarget = ClampPanToFloor(_panTarget);
            }
        }

        /// <summary>
        /// Setea el pan sin suavizado: offset actual, target y velocity quedan
        /// alineados. Para anclajes/recenter donde el smoothing no debe pelear.
        /// </summary>
        private void SetPanImmediate(Vector3 offset)
        {
            _panOffset = offset;
            _panTarget = offset;
            _panVelocity = Vector3.zero;
        }

        // _panOffset persigue a _panTarget con SmoothDamp. Unscaled time: el pan es
        // input de UI de cámara y no debe congelarse durante hit-stops/timescale.
        private void SmoothPanOffset()
        {
            if (_panOffset == _panTarget) return;

            if (_config.PanLerpSeconds <= 0f)
            {
                SetPanImmediate(_panTarget);
                return;
            }

            _panOffset = Vector3.SmoothDamp(
                _panOffset, _panTarget, ref _panVelocity,
                _config.PanLerpSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        private Vector3 ClampPanToFloor(Vector3 offset)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return offset;

            var bounds = dungeon.GetFloorBounds();
            if (bounds.size == Vector3.zero) return offset;

            var basePos = FocusAnchor;
            var focus = basePos + offset;
            var clamped = new Vector3(
                Mathf.Clamp(focus.x, bounds.min.x, bounds.max.x),
                focus.y,
                Mathf.Clamp(focus.z, bounds.min.z, bounds.max.z));
            return clamped - basePos;
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

            // BUG-068: el tutorial gatea su paso de cámara con este evento — tiene que
            // disparar apenas el target quedó fijado, no esperar a que el tween termine.
            ZoomChanged?.Invoke(_targetZoom);
            EventManager.Trigger(EventName.OnCameraZoomChanged, _targetZoom, previousTarget);

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
                SetPanImmediate(HomeOffset);
                _isPanning = false;
                return;
            }

            if (_recenterTween.isAlive) _recenterTween.Stop();

            // "Recentrar" = volver al encuadre default configurado (HomeOffset). Con
            // DefaultFocusOffset en (0,0,0) esto es centrar exacto en el player.
            var homeOffset = HomeOffset;

            // Cámara estática (no-follow): reanclar el foco a la posición actual del player
            // (típicamente el spawn de la sala) y quedarse ahí. Sin tween de pan — en
            // estático el pan no se trackea frame a frame, así que el snap directo alcanza.
            if (_config != null && !_config.FollowPlayer)
            {
                // Reanclar diferido: capturamos la posición del player en el próximo
                // LateUpdate, cuando ya fue reposicionado al spawn de la sala nueva.
                _pendingStaticReanchor = true;
                SetPanImmediate(homeOffset);
                _isPanning = false;
                EventManager.Trigger(EventName.OnCameraRecentered, instant);
                return;
            }

            if (instant || _config == null || _config.RecenterTweenSeconds <= 0f)
            {
                SetPanImmediate(homeOffset);
                _isPanning = false;
            }
            else
            {
                var startOffset = _panOffset;
                _recenterTween = Tween.Custom(
                    startValue: 0f,
                    endValue: 1f,
                    duration: _config.RecenterTweenSeconds,
                    // El tween manda: target y velocity se mantienen alineados para
                    // que el SmoothDamp del LateUpdate no pelee contra el recenter.
                    onValueChange: t => SetPanImmediate(
                        Vector3.LerpUnclamped(startOffset, homeOffset, t)),
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
        // Shake (§17.E.10)                                                    //
        // ------------------------------------------------------------------ //

        public void Shake(float amplitude, float durationSeconds)
        {
            if (_config == null || _rig == null) return;
            if (amplitude <= 0f || durationSeconds <= 0f) return;

            // El shake más fuerte gana: un shake chico no debe pisar uno grande en curso.
            float remaining = _shakeDuration - _shakeElapsed;
            if (remaining > 0f && amplitude < _shakeAmplitude) return;

            _shakeAmplitude = amplitude;
            _shakeDuration = durationSeconds;
            _shakeElapsed = 0f;
        }

        /// <summary>
        /// Offset de shake, aplicado DESPUÉS de <c>PlaceRigAt</c>. Un tween sobre el
        /// transform no sirve: <see cref="LateUpdate"/> reescribe <c>_rig.position</c>
        /// todos los frames y se comería el shake — por eso el offset se suma acá al final.
        /// </summary>
        private void ApplyShake()
        {
            if (_shakeDuration <= 0f || _shakeElapsed >= _shakeDuration) return;

            // Unscaled: el shake va de la mano con el freeze frame (timeScale ~0) del
            // impacto. Con deltaTime escalado, el shake se congelaría junto con el juego.
            _shakeElapsed += Time.unscaledDeltaTime;

            float decay = 1f - Mathf.Clamp01(_shakeElapsed / _shakeDuration);
            if (decay <= 0f) { _shakeDuration = 0f; return; }

            _rig.position += UnityEngine.Random.insideUnitSphere * (_shakeAmplitude * decay);
        }

        // Puente con Feel: cualquier MMF_CameraShake del proyecto entra por acá en vez de
        // por un MMCameraShaker. La cámara la maneja este service (LateUpdate reescribe la
        // posición), así que un shaker propio de MM sobre este mismo transform no se vería.
        private void OnFeelCameraShake(
            float duration, float amplitude, float frequency,
            float amplitudeX, float amplitudeY, float amplitudeZ,
            bool infinite = false, MMChannelData channelData = null, bool useUnscaledTime = false)
        {
            Shake(amplitude, duration);
        }
    }
}
