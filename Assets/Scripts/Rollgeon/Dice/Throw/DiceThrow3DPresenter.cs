using System;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Presenter 3D de los dados arrojables (CNF-008): cubos con Rigidbody en una
    /// bandeja física fuera del gameplay, renderizados por una cámara overlay. Mismo
    /// input que el 2D (hold agarra con atracción, click derecho cancela, flick
    /// arroja), pero acá la física es REAL: la cara superior leída al asentarse ES el
    /// resultado que entra al pipeline (salvo tiradas riggeadas y dados no-d6, que
    /// conservan la cara del roller — ver <see cref="DiceThrowService.CompleteReveal"/>).
    /// </summary>
    /// <remarks>
    /// Los dados agarrados persiguen un punto en el "carry plane" (plano horizontal a
    /// <see cref="DiceThrowSettingsSO.CarryHeight"/> sobre el piso de la bandeja) via
    /// velocidad directa — sin kinematics, así el flick hereda el momentum natural.
    /// Dados de canto: nudge acotado y, tras el timeout, snap-rotate a la cara más
    /// cercana — la resolución nunca cuelga. El reroll en 3D usa el botón (el
    /// grab-to-reroll de slots es 2D-only por ahora).
    /// </remarks>
    [AddComponentMenu("Rollgeon/Dice/Dice Throw 3D Presenter")]
    public sealed class DiceThrow3DPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab del dado físico (Assets/Prefabs/Dice/DiceThrow3D_Die.prefab).")]
        private DiceThrow3DDie _diePrefab;

        [SerializeField, Tooltip("Cámara overlay que renderiza SOLO la bandeja (layer DiceTray).")]
        private Camera _trayCamera;

        [SerializeField, Tooltip("Centro del piso de la bandeja — origen de spawns y alineado.")]
        private Transform _trayCenter;

        [SerializeField, Optional, Tooltip("Zona de dados del HUD. Null = se resuelve por Find al abrir sesión.")]
        private DiceZoneView _diceZone;

        // ---- Hooks para la capa de juice (DiceThrowJuice) — payload por índice ----

        /// <summary>Un dado entró al agarre.</summary>
        public event Action<int> DieGrabbed;

        /// <summary>Flick soltó dados al vuelo (cantidad de la volea).</summary>
        public event Action<int> DiceThrown;

        /// <summary>(índice, magnitud del impulso) — colisión física en vuelo.</summary>
        public event Action<int, float> DieImpact;

        /// <summary>(índice, cara, orden de settle en la volea) — para pitch ramps.</summary>
        public event Action<int, int, int> DieSettled;

        /// <summary>Empujoncito a un dado de canto.</summary>
        public event Action<int> DieNudged;

        /// <summary>Hay dados en la mano — para el rattle.</summary>
        public bool IsCarryingDice
        {
            get
            {
                if (_service == null || !_service.IsBusy) return false;
                foreach (var die in _dice.Values)
                    if (_service.GetDieState(die.Index) == DieThrowState.Grabbed) return true;
                return false;
            }
        }

        /// <summary>Velocidad del carry proyectada a px de pantalla — densidad del rattle (escala común con el 2D).</summary>
        public float SmoothedCursorSpeedScreen => ScreenFlickSpeed();

        private sealed class Die3D
        {
            public int Index;
            public DiceThrow3DDie View;
            public DiceThrowDieJuice Juice; // opcional en el prefab — llamadas directas
            public Vector3 SpotPos;      // spot "sin tirar"
            public Vector3 RestPos;      // última pose quieta
            public Quaternion RestRot;
            public float SettleHeld;
            public float FlightTime;
            public int Nudges;
            public bool Reported;        // ya notificó su settle al servicio
            public bool Returning;
            public Tween Tween;
        }

        private IDiceThrowService _service;
        private DiceThrowSettingsSO _cfg;
        private DiceThrowSettingsSO _fallbackCfg;
        private readonly DiceThrowInputScope _inputScope = new DiceThrowInputScope();
        private readonly Dictionary<int, Die3D> _dice = new Dictionary<int, Die3D>();

        private Vector3 _carryPoint;
        private Vector3 _carryVel;
        private bool _hasLastCarry;
        private bool _lmbWasPressed;
        private bool _aligning;
        private int _settleOrder;

        private void OnEnable()
        {
            // La cámara de la bandeja arranca apagada aunque en la escena esté
            // enabled — solo se prende mientras hay una sesión de throw activa.
            SyncTrayCamera(false);
        }

        private void OnDisable()
        {
            if (_service != null)
            {
                UnhookService(_service);
                _service.DetachPresenter(this);
                _service = null;
            }
            TeardownVisuals();
        }

        private void Update()
        {
            SyncServiceBinding();
            if (_service == null || !_service.IsBusy || _dice.Count == 0) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            EnsureCfg();
            TickCarryPoint(dt);
            if (!_aligning) TickInput();
            TickDice(dt);
            if (_aligning) TickAlignCompletion();
        }

        // Solo toma el rol de presenter cuando el modo es 3D — el presenter 2D y este
        // coexisten en la escena; el servicio acepta un attach por vez, así que cada
        // uno se attachea únicamente en su modo.
        private void SyncServiceBinding()
        {
            ServiceLocator.TryGetService<IDiceThrowService>(out var svc);
            bool wantsThis = svc != null && svc.Mode == DiceThrowMode.ThreeD;

            if (ReferenceEquals(svc, _service) && wantsThis) return;
            if (_service == null && !wantsThis) return;

            if (_service != null && (!ReferenceEquals(svc, _service) || !wantsThis))
            {
                UnhookService(_service);
                _service.DetachPresenter(this);
                TeardownVisuals();
                _service = null;
            }

            if (!wantsThis) return;

            _service = svc;
            _service.OnSessionStarted += HandleSessionStarted;
            _service.OnSessionAborted += HandleSessionAborted;
            _service.OnPhaseChanged += HandlePhaseChanged;
            _service.AttachPresenter(this);
        }

        private void UnhookService(IDiceThrowService svc)
        {
            svc.OnSessionStarted -= HandleSessionStarted;
            svc.OnSessionAborted -= HandleSessionAborted;
            svc.OnPhaseChanged -= HandlePhaseChanged;
        }

        // La cámara overlay stackeada en la Main Camera renderiza la bandeja siempre
        // que esté enabled — sin este gate el HUD 3D (piso de la bandeja) queda
        // visible fuera de las sesiones de throw.
        private void SyncTrayCamera(bool active)
        {
            if (_trayCamera != null && _trayCamera.enabled != active)
                _trayCamera.enabled = active;
        }

        // ---- Session ----------------------------------------------------------

        private void HandleSessionStarted()
        {
            EnsureCfg();

            // Chains: la sesión nueva puede abrir con el outro del confirm anterior
            // todavía volando en el HUD — completarlo YA (mismo guard que el 2D).
            ResolveZone();
            _diceZone?.NotifyThrowSessionStarted();

            if (_diePrefab == null || _trayCamera == null || _trayCenter == null)
            {
                Debug.LogWarning("[DiceThrow3DPresenter] Wiring incompleto (prefab/cámara/bandeja) — " +
                                 "la sesión seguirá sin visual 3D.", this);
                return;
            }

            SyncTrayCamera(true);

            var mask = _service.ThrownMask;
            int total = 0;
            for (int i = 0; i < mask.Count; i++) if (mask[i]) total++;

            float halfExtent = DieHalfExtent();
            float spacing = halfExtent * 3f;
            int spot = 0;

            for (int i = 0; i < mask.Count; i++)
            {
                if (!mask[i]) continue;
                var pos = _trayCenter.position
                          + new Vector3((spot - (total - 1) * 0.5f) * spacing, halfExtent, -spacing);
                var view = Instantiate(_diePrefab, pos, Random.rotationUniform);
                view.SnapUpright();
                view.Body.isKinematic = true; // quieto en el spot hasta que lo agarren

                int idx = i;
                view.Impacted += impulse => HandleDieImpact(idx, impulse);

                _dice[i] = new Die3D
                {
                    Index = i,
                    View = view,
                    Juice = view.GetComponent<DiceThrowDieJuice>(),
                    SpotPos = pos,
                    RestPos = pos,
                    RestRot = view.transform.rotation,
                };
                spot++;
            }

            _hasLastCarry = false;
            _carryVel = Vector3.zero;
            _aligning = false;
            _settleOrder = 0;
            _inputScope.Acquire();
        }

        private void HandleSessionAborted() => TeardownVisuals();

        private void HandlePhaseChanged(DiceThrowPhase phase)
        {
            if (phase == DiceThrowPhase.Settled) StartAlign();
        }

        // ---- Input ------------------------------------------------------------

        // Punto del cursor proyectado al carry plane (plano horizontal sobre la bandeja).
        private void TickCarryPoint(float dt)
        {
            var mouse = Mouse.current;
            if (mouse == null || _trayCamera == null || _trayCenter == null) return;

            var ray = _trayCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, _trayCenter.position + Vector3.up * _cfg.CarryHeight);
            if (!plane.Raycast(ray, out float enter)) return;

            var point = ray.GetPoint(enter);
            if (_hasLastCarry)
            {
                var inst = (point - _carryPoint) / dt;
                float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(_cfg.VelocitySmoothTau, 0.0001f));
                _carryVel = Vector3.Lerp(_carryVel, inst, alpha);
            }
            _carryPoint = point;
            _hasLastCarry = true;
        }

        private void TickInput()
        {
            var mouse = Mouse.current;
            if (mouse == null || !_hasLastCarry) return;

            bool lmb = mouse.leftButton.isPressed;

            if (lmb)
            {
                float grabRadius = DieHalfExtent() * 3f;
                float r2 = grabRadius * grabRadius;
                foreach (var die in _dice.Values)
                {
                    var state = _service.GetDieState(die.Index);
                    if (state != DieThrowState.NotThrown && state != DieThrowState.Settled) continue;

                    var flat = die.View.transform.position; flat.y = _carryPoint.y;
                    if ((flat - _carryPoint).sqrMagnitude > r2) continue;
                    if (!_service.TryGrab(die.Index)) continue;

                    if (die.Tween.isAlive) die.Tween.Stop();
                    die.Returning = false;
                    die.View.Body.isKinematic = false;
                    die.View.Body.useGravity = false; // flota mientras está agarrado
                    die.View.EmitImpacts = false;     // carried se chocan entre sí — sin clatter
                    die.Juice?.PlayPickup();
                    DieGrabbed?.Invoke(die.Index);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame)
                CancelGrabAndReturn();

            if (!lmb && _lmbWasPressed && AnyGrabbed())
            {
                // El umbral de flick es consistente con el 2D (velocidad de pantalla).
                if (ScreenFlickSpeed() >= _cfg.FlickMinSpeed) ThrowGrabbed();
                else CancelGrabAndReturn();
            }
            _lmbWasPressed = lmb;
        }

        private float ScreenFlickSpeed()
        {
            // La velocidad del carry point (mundo) proyectada de vuelta a px de pantalla
            // aproximando por el tamaño del frustum — suficiente para el umbral.
            if (_trayCamera == null) return 0f;
            float worldPerPixel = WorldUnitsPerPixel();
            return worldPerPixel > 0f ? _carryVel.magnitude / worldPerPixel : 0f;
        }

        private float WorldUnitsPerPixel()
        {
            float dist = Vector3.Distance(_trayCamera.transform.position, _trayCenter.position);
            float frustumH = 2f * dist * Mathf.Tan(_trayCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return frustumH / Mathf.Max(1, _trayCamera.pixelHeight);
        }

        private bool AnyGrabbed()
        {
            foreach (var die in _dice.Values)
                if (_service.GetDieState(die.Index) == DieThrowState.Grabbed) return true;
            return false;
        }

        private void ThrowGrabbed()
        {
            int thrown = 0;
            foreach (var idx in _service.ReleaseGrabbed())
            {
                if (!_dice.TryGetValue(idx, out var die)) continue;
                var body = die.View.Body;
                body.useGravity = true;
                body.linearVelocity = _carryVel * _cfg.FlickWorldGain * Random.Range(0.9f, 1.1f)
                                      + Vector3.up * 0.5f;
                body.angularVelocity = Random.onUnitSphere * (_cfg.Tumble * Mathf.Max(1f, _carryVel.magnitude));
                die.FlightTime = 0f;
                die.SettleHeld = 0f;
                die.Nudges = 0;
                die.Reported = false;
                die.View.EmitImpacts = true; // en vuelo los golpes SÍ suenan
                thrown++;
            }
            if (thrown > 0)
            {
                _settleOrder = 0;
                DiceThrown?.Invoke(thrown);
            }
        }

        private void CancelGrabAndReturn()
        {
            foreach (var idx in _service.CancelGrab())
            {
                if (!_dice.TryGetValue(idx, out var die)) continue;

                var body = die.View.Body;
                body.isKinematic = true;
                body.useGravity = true;

                bool settled = _service.GetDieState(idx) == DieThrowState.Settled;
                var targetPos = settled ? die.RestPos : die.SpotPos;
                var targetRot = settled ? die.RestRot : die.View.transform.rotation;

                die.Returning = true;
                if (_cfg.ReturnSeconds <= 0f)
                {
                    die.View.transform.SetPositionAndRotation(targetPos, targetRot);
                    die.Returning = false;
                }
                else
                {
                    die.Tween = Tween.Position(die.View.transform, targetPos, _cfg.ReturnSeconds, Ease.OutCubic);
                    Tween.Rotation(die.View.transform, targetRot, _cfg.ReturnSeconds, Ease.OutCubic);
                }
            }
        }

        // ---- Simulación / settle -------------------------------------------------

        private void TickDice(float dt)
        {
            foreach (var die in _dice.Values)
            {
                if (die.Returning)
                {
                    if (!die.Tween.isAlive) die.Returning = false;
                    continue;
                }

                switch (_service.GetDieState(die.Index))
                {
                    case DieThrowState.Grabbed:
                    {
                        // Persecución por velocidad (no kinematic): el flick hereda momentum.
                        var target = _carryPoint + OrbitOffset(die.Index);
                        var body = die.View.Body;
                        var toTarget = target - die.View.transform.position;
                        body.linearVelocity = Vector3.ClampMagnitude(toTarget * _cfg.PullStrength, 30f);
                        body.angularVelocity = Vector3.ClampMagnitude(
                            body.angularVelocity + Random.onUnitSphere * (0.5f * dt * 60f), 6f);
                        break;
                    }
                    case DieThrowState.Flying:
                    {
                        die.FlightTime += dt;
                        if (die.Reported) break;

                        bool still = die.View.IsPhysicallyStill;
                        bool held = DiceThrow2DMath.SettleTick(
                            still ? 0f : float.MaxValue, 1f, 0.2f, dt, ref die.SettleHeld);

                        if (held)
                        {
                            int face = die.View.ReadTopFace(out float dot);
                            if (DiceFaceReader.IsCocked(dot, _cfg.CockedDotThreshold))
                            {
                                if (die.Nudges < _cfg.MaxNudges)
                                {
                                    die.Nudges++;
                                    die.SettleHeld = 0f;
                                    die.View.Nudge(_cfg.NudgeImpulse);
                                    DieNudged?.Invoke(die.Index);
                                    break;
                                }
                                die.View.SnapUpright();
                                face = die.View.ReadTopFace(out _);
                            }
                            SettleDie(die, face);
                        }
                        else if (die.FlightTime >= _cfg.MaxSettleSeconds)
                        {
                            // Failsafe: nunca colgar la resolución por un dado inquieto.
                            die.View.SnapUpright();
                            SettleDie(die, die.View.ReadTopFace(out _));
                        }
                        break;
                    }
                }
            }
        }

        private void SettleDie(Die3D die, int face)
        {
            die.Reported = true;
            die.View.Body.isKinematic = true;
            die.View.EmitImpacts = false;
            die.RestPos = die.View.transform.position;
            die.RestRot = die.View.transform.rotation;
            die.Juice?.PlaySettle(face);
            _service.NotifyDieSettled(die.Index, face);
            DieSettled?.Invoke(die.Index, face, _settleOrder++);
        }

        // Colisiones físicas en vuelo: squash per-die (reemplaza al camera-punch —
        // el RT pixel-art de 320x180 cuantizaría cualquier shake de cámara) + relay
        // por índice para el clatter de la capa de zona.
        private void HandleDieImpact(int index, float impulse)
        {
            if (impulse < _cfg.TrayImpactMinImpulse) return;
            if (_dice.TryGetValue(index, out var die))
                die.Juice?.PlayBounce(
                    0.5f + 0.5f * DiceThrowFeelMath.Intensity01(impulse, _cfg.TrayImpactRefImpulse));
            DieImpact?.Invoke(index, impulse);
        }

        // ---- Alineado -------------------------------------------------------------

        private void StartAlign()
        {
            _aligning = true;
            float halfExtent = DieHalfExtent();
            float spacing = halfExtent * 3f;

            int order = 0, total = _dice.Count;
            foreach (var die in _dice.Values)
            {
                if (die.Tween.isAlive) die.Tween.Stop();
                die.Returning = false;
                die.View.Body.isKinematic = true;

                var target = _trayCenter.position
                             + new Vector3((order - (total - 1) * 0.5f) * spacing, halfExtent, 0f);
                if (_cfg.AlignSeconds <= 0f)
                    die.View.transform.position = target;
                else
                    die.Tween = Tween.Position(die.View.transform, target, _cfg.AlignSeconds,
                        Ease.InOutCubic, startDelay: order * _cfg.AlignStagger);
                order++;
            }
        }

        private void TickAlignCompletion()
        {
            foreach (var die in _dice.Values)
                if (die.Tween.isAlive) return;

            _aligning = false;
            _service.CompleteReveal();
            TeardownVisuals();
        }

        // ---- Helpers ----------------------------------------------------------------

        private void TeardownVisuals()
        {
            foreach (var die in _dice.Values)
            {
                if (die.Tween.isAlive) die.Tween.Stop();
                if (die.View != null) Destroy(die.View.gameObject);
            }
            _dice.Clear();
            _aligning = false;
            _inputScope.Release();
            SyncTrayCamera(false);
        }

        private float DieHalfExtent()
        {
            if (_diePrefab == null) return 0.5f;
            return _diePrefab.transform.localScale.x * 0.5f;
        }

        private Vector3 OrbitOffset(int index)
        {
            float angle = index * Mathf.PI * 2f / 5f;
            float radius = DieHalfExtent() * 2.2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        private void ResolveZone()
        {
            if (_diceZone == null)
                _diceZone = FindFirstObjectByType<DiceZoneView>(FindObjectsInactive.Include);
        }

        private void EnsureCfg()
        {
            _cfg = _service != null && _service.Settings != null ? _service.Settings : FallbackCfg();
        }

        private DiceThrowSettingsSO FallbackCfg()
        {
            if (_fallbackCfg == null)
                _fallbackCfg = ScriptableObject.CreateInstance<DiceThrowSettingsSO>();
            return _fallbackCfg;
        }
    }
}
