using System;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Presenter 2D de los dados arrojables (CNF-008): dados-UI sobre un layer del
    /// canvas que se agarran manteniendo click izquierdo (atracción spring al cursor),
    /// se cancelan con click derecho (vuelven a su lugar previo), se arrojan con un
    /// flick del mouse, rebotan en los bordes de pantalla y, al asentarse todos, se
    /// alinean en los slots del <see cref="DiceZoneView"/> revelando el resultado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La física es COSMÉTICA: el resultado ya fue precalculado por el
    /// <see cref="IDiceThrowService"/> al abrir la sesión — cada dado muestra su cara
    /// (<see cref="IDiceThrowService.PeekPendingFace"/>) recién al frenar. El servicio
    /// es la única fuente de verdad de estados; este componente solo lee input y anima.
    /// </para>
    /// <para>
    /// <b>Grab-to-reroll</b>: sin sesión activa, agarrar dados ASENTADOS de los slots
    /// (no holdeados, no bloqueados) arma un reroll — al arrojarlos se pide el reroll
    /// al flujo dueño (<see cref="IDiceThrowService.GrabRerollHandler"/>) con
    /// keep = los dados NO agarrados, y los ghosts continúan el vuelo sin corte.
    /// Click derecho (o soltar sin velocidad) devuelve todo a su lugar sin cobrar.
    /// </para>
    /// <para>
    /// <b>Cero UI creada por código</b>: el dado volador es el prefab
    /// <see cref="DiceThrowDieView"/> y el layer es un RectTransform autorado en la
    /// escena — tamaño y estética se editan en el prefab/inspector.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Dice/Dice Throw 2D Presenter")]
    public sealed class DiceThrow2DPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Zona de dados del HUD. Null = se resuelve por Find al arrancar la sesión.")]
        private DiceZoneView _diceZone;

        [SerializeField, Tooltip("Prefab del dado volador (Assets/Prefabs/UI/DiceThrowDie.prefab).")]
        private DiceThrowDieView _diePrefab;

        [SerializeField, Tooltip("Layer full-rect (CanvasGroup sin raycast) donde vuelan los dados. " +
                 "Autorado en la escena bajo el Canvas raíz.")]
        private RectTransform _layer;

        [SerializeField, Tooltip("Partículas de impacto (bordes, choques entre dados, settle). " +
                 "Opcional — sin wiring, sin partículas.")]
        private DiceThrowImpactBurst _impactBurst;

        // ---- Hooks para la capa de juice (DiceThrowJuice) ---------------------
        // Payload plano por índice, NUNCA la view: el reveal destruye los ghosts el
        // mismo frame (TickAlignCompletion) — una view en el evento sería dangling
        // para cualquier consumidor que difiera (MMF, tweens, rate-limits).

        /// <summary>Un dado entró al agarre (sesión o arming de reroll).</summary>
        public event Action<int> DieGrabbed;

        /// <summary>Flick soltó dados al vuelo (cantidad lanzada en la volea).</summary>
        public event Action<int> DiceThrown;

        /// <summary>(índice, velocidad px/s antes del impacto) — rebote contra un borde
        /// o choque contra otro dado (velocidad relativa de aproximación).</summary>
        public event Action<int, float> DieBounced;

        /// <summary>(índice, cara, orden de settle en la volea) — para pitch ramps.</summary>
        public event Action<int, int, int> DieSettled;

        /// <summary>Hay dados en la mano ahora (agarre de sesión o arming de reroll) — para el rattle.</summary>
        public bool IsCarryingDice
        {
            get
            {
                if (_armed.Count > 0) return true;
                if (_service == null || !_service.IsBusy) return false;
                foreach (var die in _dice.Values)
                    if (_service.GetDieState(die.Index) == DieThrowState.Grabbed) return true;
                return false;
            }
        }

        /// <summary>Velocidad suavizada del cursor en px del layer — densidad del rattle.</summary>
        public Vector2 SmoothedCursorVelocity => _mouseVel;

        private sealed class DieVisual
        {
            public int Index;
            public DiceThrowDieView View;
            public DiceThrowDieJuice Juice; // opcional en el prefab — llamadas directas (lifetime seguro)
            public Vector2 Vel;
            public Vector2 RestPos;    // último lugar quieto (spot inicial o settle)
            public Vector2 SpotPos;    // spot "sin tirar" / posición del slot de origen
            public float SettleHeld;
            public float FlightTime;
            public float FaceCycleAt;
            public float AngularVel;   // rotación cosmética del sprite en vuelo (grados/s)
            public bool FlyingNow;     // cache por-frame para el pase de colisiones
            public bool Returning;     // tween de vuelta en curso — no simular
            public Tween Tween;
            public Tween RotTween;     // enderezado al settle
            public Tween ScaleTween;   // drop-in de spawn
        }

        private IDiceThrowService _service;
        private DiceThrowSettingsSO _cfg;
        private DiceThrowSettingsSO _fallbackCfg;
        private readonly DiceThrowInputScope _inputScope = new DiceThrowInputScope();

        // Sesión activa (dados del servicio).
        private readonly Dictionary<int, DieVisual> _dice = new Dictionary<int, DieVisual>();

        // Arming de grab-to-reroll (pre-sesión): ghosts agarrados de los slots.
        private readonly Dictionary<int, DieVisual> _armed = new Dictionary<int, DieVisual>();
        private bool _transferPending; // el flick del arming abrió la sesión — adoptar ghosts
        private Vector2 _transferVel;

        private Vector2 _cursorLocal;
        private Vector2 _mouseVel;
        private bool _hasLastCursor;
        private bool _lmbWasPressed;
        private bool _aligning;
        private int _settleOrder;

        // Click vs drag del arming: el press recién arma con intención de drag.
        private Vector2 _armPressPos;
        private float _armPressAt;
        private bool _armDragIntent;

        private static bool ReducedMotion => Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion;

        // ---- Lifecycle -------------------------------------------------------

        private void OnDisable()
        {
            if (_service != null)
            {
                UnhookService(_service);
                _service.DetachPresenter(this); // aborta si había sesión
                _service = null;
            }
            CancelArmingInstant();
            TeardownVisuals();
        }

        private void Update()
        {
            SyncServiceBinding();
            if (_service == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool sessionActive = _service.IsBusy && _dice.Count > 0;
            // Con el outro del confirm en curso los slots están volando al centro o
            // desvaneciéndose — armar un grab ahí agarraría dados en pleno teardown.
            bool outroPending = Rollgeon.UI.HUD.DiceAnim.DiceOutroGate.OutroPending;
            bool canArm = _service.Mode == DiceThrowMode.TwoD && _service.CanGrabReroll && !outroPending;

            if (!sessionActive && !canArm && _armed.Count == 0) return;

            EnsureCfg();
            if (!EnsureLayer()) return;
            TickCursor(dt);

            if (sessionActive)
            {
                if (!_aligning) TickSessionInput();
                TickSessionSimulation(dt);
                if (_aligning) TickAlignCompletion();
            }
            else if (canArm || _armed.Count > 0)
            {
                if (!canArm)
                {
                    // El confirm agarró un arming a medias: el tween de vuelta apuntaría
                    // a un slot que el outro está animando/limpiando — cancel seco.
                    if (outroPending) CancelArmingInstant();
                    else CancelArmingReturn();
                    return;
                }
                TickArmingInput();
                TickArmingSimulation(dt);
            }
        }

        // El servicio es Run-scoped: se re-crea por run. Re-resolver por frame y
        // re-attachear cuando cambia la instancia (mismo patrón de polling que usa
        // TileClickHandler con ISelectionController). En modo 3D este presenter se
        // desengancha — el DiceThrow3DPresenter toma el rol.
        private void SyncServiceBinding()
        {
            ServiceLocator.TryGetService<IDiceThrowService>(out var svc);
            bool wantsThis = svc != null && svc.Mode != DiceThrowMode.ThreeD;

            if (ReferenceEquals(svc, _service) && wantsThis) return;
            if (_service == null && !wantsThis) return;

            if (_service != null)
            {
                UnhookService(_service);
                _service.DetachPresenter(this);
                CancelArmingInstant();
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

        // ---- Session events ----------------------------------------------------

        private void HandleSessionStarted()
        {
            EnsureCfg();
            ResolveZone();
            if (!EnsureLayer()) return;

            // Chains: la sesión nueva puede abrir con el outro del confirm anterior
            // todavía volando — completarlo YA, antes de esconder slots.
            _diceZone?.NotifyThrowSessionStarted();

            // Sesión abierta desde afuera (ej. roll de la próxima fase del chain) con
            // un arming a medias: cancelarlo antes de arrancar limpio.
            if (_armed.Count > 0 && !_transferPending) CancelArmingInstant();

            // Los slots que el arming ocultó se reactivan un instante para que
            // HideSlotsForThrow los trackee (y RestoreSlotsAfterThrow funcione en abort).
            if (_transferPending) SetArmedSlotsActive(true);

            _diceZone?.HideSlotsForThrow(_service.ThrownMask);

            var mask = _service.ThrownMask;
            var spotCenter = AnchorToLayerLocal(_diceZone != null ? _diceZone.GetRollArea() : null);
            float spacing = (_diePrefab != null ? _diePrefab.HalfSize * 2f : 56f) + 12f;

            int spot = 0, total = 0;
            for (int i = 0; i < mask.Count; i++) if (mask[i]) total++;

            for (int i = 0; i < mask.Count; i++)
            {
                if (!mask[i]) continue;

                if (_transferPending && _armed.TryGetValue(i, out var ghost))
                {
                    // El ghost del arming pasa a ser el dado de la sesión — continúa
                    // desde su posición actual, sin corte visual.
                    _dice[i] = ghost;
                    _armed.Remove(i);
                }
                else
                {
                    var pos = spotCenter + new Vector2((spot - (total - 1) * 0.5f) * spacing, 0f);
                    var die = CreateDie(i, pos);
                    _dice[i] = die;

                    // Drop-in staggered — solo dados nuevos (los ghosts adoptados del
                    // arming ya están visibles y a escala).
                    if (!ReducedMotion && _cfg.SpawnDropSeconds > 0f)
                    {
                        die.View.Rect.localScale = Vector3.zero;
                        die.ScaleTween = Tween.Scale(die.View.Rect, 1f, _cfg.SpawnDropSeconds,
                            Ease.OutBack, startDelay: spot * _cfg.SpawnDropStagger);
                    }
                }
                spot++;
            }

            if (_transferPending)
            {
                // Reanudar el vuelo: agarrar y soltar en el servicio con la velocidad
                // del flick capturada durante el arming.
                _transferPending = false;
                foreach (var die in _dice.Values) _service.TryGrab(die.Index);
                int thrown = 0;
                foreach (var idx in _service.ReleaseGrabbed())
                {
                    if (!_dice.TryGetValue(idx, out var die)) continue;
                    die.Vel = _transferVel * _cfg.ThrowGain * Random.Range(0.9f, 1.1f);
                    die.AngularVel = FlickSpin(_transferVel);
                    die.FlightTime = 0f;
                    die.SettleHeld = 0f;
                    thrown++;
                }
                if (thrown > 0) DiceThrown?.Invoke(thrown);
                CancelArmingInstant(); // limpia ghosts armados que no entraron (no debería haber)
            }

            _mouseVel = Vector2.zero;
            _hasLastCursor = false;
            _aligning = false;
            _settleOrder = 0;
            _inputScope.Acquire();
        }

        private void HandleSessionAborted()
        {
            _diceZone?.RestoreSlotsAfterThrow();
            TeardownVisuals();
        }

        private void HandlePhaseChanged(DiceThrowPhase phase)
        {
            if (phase == DiceThrowPhase.Settled) StartAlign();
        }

        // ---- Cursor --------------------------------------------------------------

        private void TickCursor(float dt)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var local = ScreenToLayerLocal(mouse.position.ReadValue());
            if (_hasLastCursor)
            {
                var inst = (local - _cursorLocal) / dt;
                _mouseVel = DiceThrow2DMath.SmoothVelocity(_mouseVel, inst, _cfg.VelocitySmoothTau, dt);
            }
            _cursorLocal = local;
            _hasLastCursor = true;
        }

        // ---- Sesión: input + simulación -------------------------------------------

        private void TickSessionInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool lmb = mouse.leftButton.isPressed;

            // Grab acumulativo: con el botón apretado, todo dado agarrable que entre
            // en el radio del cursor queda agarrado (efecto agujero negro).
            if (lmb)
            {
                float r2 = _cfg.GrabRadius * _cfg.GrabRadius;
                foreach (var die in _dice.Values)
                {
                    var state = _service.GetDieState(die.Index);
                    if (state != DieThrowState.NotThrown && state != DieThrowState.Settled) continue;
                    if ((die.View.Rect.anchoredPosition - _cursorLocal).sqrMagnitude > r2) continue;
                    if (!_service.TryGrab(die.Index)) continue;
                    if (die.Tween.isAlive) die.Tween.Stop();
                    if (die.RotTween.isAlive) die.RotTween.Stop();
                    if (die.ScaleTween.isAlive) die.ScaleTween.Stop();
                    // Agarrar endereza y normaliza (por si venía de un drop-in/upright a medias).
                    die.View.Rect.localRotation = Quaternion.identity;
                    die.View.Rect.localScale = Vector3.one;
                    die.Returning = false;
                    die.Vel = Vector2.zero;
                    die.Juice?.PlayPickup();
                    DieGrabbed?.Invoke(die.Index);
                }
            }

            // Click derecho = cancelar el agarre (los dados vuelven a su lugar previo).
            if (mouse.rightButton.wasPressedThisFrame)
                CancelSessionGrab();

            // Soltar con velocidad = flick (arrojar). Soltar suave = cancel.
            if (!lmb && _lmbWasPressed && AnySessionGrabbed())
            {
                if (_mouseVel.magnitude >= _cfg.FlickMinSpeed) ThrowSessionGrabbed();
                else CancelSessionGrab();
            }
            _lmbWasPressed = lmb;
        }

        private bool AnySessionGrabbed()
        {
            foreach (var die in _dice.Values)
                if (_service.GetDieState(die.Index) == DieThrowState.Grabbed) return true;
            return false;
        }

        private void ThrowSessionGrabbed()
        {
            int thrown = 0;
            foreach (var idx in _service.ReleaseGrabbed())
            {
                if (!_dice.TryGetValue(idx, out var die)) continue;
                die.Vel = _mouseVel * _cfg.ThrowGain * Random.Range(0.9f, 1.1f);
                die.AngularVel = FlickSpin(_mouseVel);
                die.FlightTime = 0f;
                die.SettleHeld = 0f;
                thrown++;
            }
            if (thrown > 0)
            {
                _settleOrder = 0;
                DiceThrown?.Invoke(thrown);
            }
        }

        private void CancelSessionGrab()
        {
            foreach (var idx in _service.CancelGrab())
            {
                if (!_dice.TryGetValue(idx, out var die)) continue;
                die.Vel = Vector2.zero;
                var target = _service.GetDieState(idx) == DieThrowState.Settled ? die.RestPos : die.SpotPos;
                TweenBack(die, target);
            }
        }

        private void TickSessionSimulation(float dt)
        {
            float halfSize = _dice.Count > 0 && _diePrefab != null ? _diePrefab.HalfSize : 28f;
            var rect = _layer.rect;

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
                        var target = _cursorLocal + OrbitOffset(die.Index);
                        die.View.Rect.anchoredPosition = DiceThrow2DMath.SpringStep(
                            die.View.Rect.anchoredPosition, ref die.Vel, target,
                            _cfg.SpringStiffness, _cfg.SpringDamping, dt);
                        break;
                    }
                    case DieThrowState.Flying:
                    {
                        var pos = DiceThrow2DMath.FlightStep(die.View.Rect.anchoredPosition, ref die.Vel, _cfg.FlightDrag, dt);
                        var vel = die.Vel;
                        float preBounceSpeed = vel.magnitude;
                        bool bounced = DiceThrow2DMath.BounceInRect(
                            ref pos, ref vel, rect, halfSize, _cfg.Restitution, out var bounceNormal);
                        die.Vel = vel;
                        die.View.Rect.anchoredPosition = pos;
                        die.FlightTime += dt;
                        if (bounced)
                        {
                            float intensity = DiceThrowFeelMath.Intensity01(preBounceSpeed, _cfg.FlickMinSpeed);
                            die.Juice?.PlayBounce(0.5f + 0.5f * intensity);
                            _impactBurst?.Burst(pos - bounceNormal * halfSize, bounceNormal, intensity);
                            DieBounced?.Invoke(die.Index, preBounceSpeed);
                            die.AngularVel *= _cfg.Restitution; // el impacto come spin
                        }

                        // Rotación cosmética del sprite mientras vuela.
                        if (die.AngularVel != 0f)
                        {
                            die.View.Rect.Rotate(0f, 0f, die.AngularVel * dt);
                            die.AngularVel = DiceThrowFeelMath.SpinDecayStep(
                                die.AngularVel, _cfg.FlightSpinDecay, dt);
                        }

                        // Caras "rodando" mientras vuela — puro teatro.
                        if (Time.unscaledTime >= die.FaceCycleAt)
                        {
                            die.View.ShowFace(Random.Range(1, 7));
                            die.FaceCycleAt = Time.unscaledTime + 0.06f;
                        }

                        bool settled = DiceThrow2DMath.SettleTick(
                            die.Vel.magnitude, _cfg.SettleSpeedEps, _cfg.SettleHoldSeconds, dt, ref die.SettleHeld);
                        if (settled || die.FlightTime >= _cfg.MaxFlightSeconds)
                        {
                            die.Vel = Vector2.zero;
                            die.AngularVel = 0f;
                            die.RestPos = die.View.Rect.anchoredPosition;
                            SettleUpright(die);
                            int face = _service.PeekPendingFace(die.Index);
                            die.View.ShowFace(face);
                            die.Juice?.PlaySettle(face);
                            _impactBurst?.Burst(die.RestPos + Vector2.down * (halfSize * 0.6f),
                                Vector2.up, 0.25f);
                            _service.NotifyDieSettled(die.Index);
                            DieSettled?.Invoke(die.Index, face, _settleOrder++);
                        }
                        break;
                    }
                }
            }

            // El empuje corre después de integrar todos — durante el align los tweens
            // son dueños de las posiciones y separar acá pelearía con ellos.
            if (!_aligning) ResolveDieCollisions(rect, halfSize);
        }

        // ---- Empuje entre dados (círculos de igual radio) --------------------------

        private readonly List<DieVisual> _collidersScratch = new List<DieVisual>();

        // Los que vuelan intercambian momento; los quietos (asentados o esperando en
        // su spot) reciben un empujón posicional acotado y el que vuela rebota.
        // Grabbed/Returning quedan afuera: el spring del cursor y los tweens de
        // retorno son dueños de esas posiciones.
        private void ResolveDieCollisions(Rect rect, float halfSize)
        {
            if (!_cfg.DiceCollide || _dice.Count < 2) return;

            _collidersScratch.Clear();
            foreach (var die in _dice.Values)
            {
                if (die.Returning) continue;
                var state = _service.GetDieState(die.Index);
                if (state != DieThrowState.Flying && state != DieThrowState.Settled &&
                    state != DieThrowState.NotThrown) continue;
                die.FlyingNow = state == DieThrowState.Flying;
                _collidersScratch.Add(die);
            }

            float radius = halfSize * _cfg.DieCollisionRadiusScale;
            for (int i = 0; i < _collidersScratch.Count; i++)
            {
                for (int j = i + 1; j < _collidersScratch.Count; j++)
                {
                    var a = _collidersScratch[i];
                    var b = _collidersScratch[j];
                    var posA = a.View.Rect.anchoredPosition;
                    var posB = b.View.Rect.anchoredPosition;
                    float approach = 0f;

                    if (a.FlyingNow && b.FlyingNow)
                    {
                        approach = DiceThrow2DMath.ResolveDiePair(ref posA, ref a.Vel,
                            ref posB, ref b.Vel, radius, _cfg.DieCollisionRestitution);
                    }
                    else if (a.FlyingNow || b.FlyingNow)
                    {
                        var fly = a.FlyingNow ? a : b;
                        var still = a.FlyingNow ? b : a;
                        var posFly = fly.View.Rect.anchoredPosition;
                        var posStill = still.View.Rect.anchoredPosition;
                        approach = DiceThrow2DMath.ResolveDieStatic(ref posFly, ref fly.Vel,
                            ref posStill, radius, _cfg.DieCollisionRestitution,
                            shovePerSpeed: 0.04f, maxShove: halfSize);
                        if (approach > 0f)
                        {
                            // El empujón no lo saca de la mesa; un asentado re-ancla su
                            // reposo (uno sin tirar conserva su spot como casa).
                            posStill = ClampToRect(posStill, rect, halfSize);
                            if (!still.FlyingNow &&
                                _service.GetDieState(still.Index) == DieThrowState.Settled)
                                still.RestPos = posStill;
                        }
                        if (a.FlyingNow) { posA = posFly; posB = posStill; }
                        else { posA = posStill; posB = posFly; }
                    }
                    else if (DiceThrow2DMath.SeparateOverlap(ref posA, ref posB, radius))
                    {
                        // Dos quietos superpuestos (settle apretado): separar sin juice.
                        posA = ClampToRect(posA, rect, halfSize);
                        posB = ClampToRect(posB, rect, halfSize);
                        if (_service.GetDieState(a.Index) == DieThrowState.Settled) a.RestPos = posA;
                        if (_service.GetDieState(b.Index) == DieThrowState.Settled) b.RestPos = posB;
                    }

                    a.View.Rect.anchoredPosition = posA;
                    b.View.Rect.anchoredPosition = posB;

                    if (approach >= _cfg.DieCollisionJuiceMinSpeed)
                    {
                        float intensity = DiceThrowFeelMath.Intensity01(approach, _cfg.FlickMinSpeed);
                        a.Juice?.PlayBounce(0.5f + 0.5f * intensity);
                        b.Juice?.PlayBounce(0.5f + 0.5f * intensity);
                        DieBounced?.Invoke(a.Index, approach); // clack coalescido en la capa de zona
                        _impactBurst?.Burst((posA + posB) * 0.5f, (posA - posB).normalized, intensity);
                    }
                }
            }
        }

        private static Vector2 ClampToRect(Vector2 pos, Rect rect, float halfSize)
        {
            pos.x = Mathf.Clamp(pos.x, rect.xMin + halfSize, rect.xMax - halfSize);
            pos.y = Mathf.Clamp(pos.y, rect.yMin + halfSize, rect.yMax - halfSize);
            return pos;
        }

        // ---- Grab-to-reroll (arming pre-sesión) --------------------------------------

        private void TickArmingInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool lmb = mouse.leftButton.isPressed;

            // Click vs drag: armar recién con intención de drag (moverse más que el
            // slop o sostener más que la ventana). Un click seco no toca el slot y su
            // Button dispara el toggle de hold — armar en el press lo desactivaba a
            // mitad del click y la selección nunca llegaba (bug "no puedo marcar").
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _armPressPos = _cursorLocal;
                _armPressAt = Time.unscaledTime;
                _armDragIntent = false;
            }
            if (lmb && !_armDragIntent)
                _armDragIntent = DiceThrow2DMath.DragIntent(_armPressPos, _cursorLocal,
                    Time.unscaledTime - _armPressAt, _cfg.GrabDragSlopPixels, _cfg.GrabClickSeconds);

            if (lmb && _armDragIntent) TryArmSlotsUnderCursor();

            if (mouse.rightButton.wasPressedThisFrame && _armed.Count > 0)
                CancelArmingReturn();

            if (!lmb && _lmbWasPressed && _armed.Count > 0)
            {
                if (_mouseVel.magnitude >= _cfg.FlickMinSpeed) FlickArmedReroll();
                else CancelArmingReturn();
            }
            _lmbWasPressed = lmb;
        }

        // Agarra dados ASENTADOS desde los slots del HUD: activos (con cara), no
        // holdeados (holdear = "este lo conservo") y no bloqueados por el boss.
        private void TryArmSlotsUnderCursor()
        {
            ResolveZone();
            if (_diceZone == null || _diePrefab == null) return;

            var slots = _diceZone.GetDiceSlots();
            if (slots == null || slots.Count == 0) return;

            var held = _diceZone.GetHeldStates();
            ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var blocks);

            float r2 = _cfg.GrabRadius * _cfg.GrabRadius;
            for (int i = 0; i < slots.Count; i++)
            {
                if (_armed.ContainsKey(i) || slots[i] == null) continue;

                var slotView = slots[i].GetComponent<DiceSlotView>();
                if (slotView == null || !slotView.gameObject.activeSelf) continue;
                if (slotView.CurrentFace <= 0) continue;
                if (held != null && i < held.Length && held[i]) continue;
                if (blocks != null && blocks.IsBlocked(i)) continue;

                var slotPos = AnchorToLayerLocal(slots[i]);
                if ((slotPos - _cursorLocal).sqrMagnitude > r2) continue;

                var ghost = CreateDie(i, slotPos);
                ghost.View.ShowFace(slotView.CurrentFace);
                ghost.SpotPos = slotPos;
                slotView.gameObject.SetActive(false);
                _armed[i] = ghost;
                ghost.Juice?.PlayPickup();
                DieGrabbed?.Invoke(i);
            }
        }

        // Flick del arming: pedir el reroll al flujo dueño con keep = no agarrados.
        // Si arranca, la sesión se abre SINCRÓNICAMENTE dentro del handler y
        // HandleSessionStarted adopta los ghosts (por _transferPending).
        private void FlickArmedReroll()
        {
            var handler = _service.GrabRerollHandler;
            if (handler == null) { CancelArmingReturn(); return; }

            int count = _diceZone != null ? _diceZone.GetDiceSlots().Count : 5;
            var keep = new bool[count];
            for (int i = 0; i < count; i++) keep[i] = !_armed.ContainsKey(i);

            _transferPending = true;
            _transferVel = _mouseVel;

            bool started = handler(keep);
            _transferPending = false;

            // Sin budget/energía (o guard del flujo): devolver todo a su lugar.
            if (!started && _armed.Count > 0) CancelArmingReturn();
        }

        private void TickArmingSimulation(float dt)
        {
            foreach (var die in _armed.Values)
            {
                if (die.Returning) continue; // el tween maneja la vuelta

                var target = _cursorLocal + OrbitOffset(die.Index);
                die.View.Rect.anchoredPosition = DiceThrow2DMath.SpringStep(
                    die.View.Rect.anchoredPosition, ref die.Vel, target,
                    _cfg.SpringStiffness, _cfg.SpringDamping, dt);
            }

            // Ghosts cuyo tween de vuelta terminó: restaurar slot y limpiar.
            _armingReturnDone.Clear();
            foreach (var kv in _armed)
            {
                if (kv.Value.Returning && !kv.Value.Tween.isAlive)
                    _armingReturnDone.Add(kv.Key);
            }
            foreach (var idx in _armingReturnDone) FinishArmedReturn(idx);
        }

        private readonly List<int> _armingReturnDone = new List<int>();

        private void CancelArmingReturn()
        {
            foreach (var die in _armed.Values)
            {
                if (die.Returning) continue;
                die.Vel = Vector2.zero;
                if (_cfg.ReturnSeconds <= 0f)
                {
                    die.View.Rect.anchoredPosition = die.SpotPos;
                    die.Returning = true; // FinishArmedReturn lo levanta el próximo tick
                }
                else
                {
                    die.Returning = true;
                    die.Tween = Tween.UIAnchoredPosition(die.View.Rect, die.SpotPos, _cfg.ReturnSeconds, Ease.OutCubic);
                }
            }
        }

        private void FinishArmedReturn(int index)
        {
            if (!_armed.TryGetValue(index, out var die)) return;
            RestoreSlot(index);
            if (die.View != null) Destroy(die.View.gameObject);
            _armed.Remove(index);
        }

        private void CancelArmingInstant()
        {
            foreach (var kv in _armed)
            {
                if (kv.Value.Tween.isAlive) kv.Value.Tween.Stop();
                RestoreSlot(kv.Key);
                if (kv.Value.View != null) Destroy(kv.Value.View.gameObject);
            }
            _armed.Clear();
            _transferPending = false;
        }

        private void RestoreSlot(int index)
        {
            var slots = _diceZone != null ? _diceZone.GetDiceSlots() : null;
            if (slots == null || index >= slots.Count || slots[index] == null) return;
            // Un ClearAll diferido (outro del confirm) pudo limpiar el slot mientras
            // estaba armado: reactivarlo mostraría un slot vacío hasta el próximo roll.
            var view = slots[index].GetComponent<DiceSlotView>();
            if (view != null && view.CurrentFace <= 0) return;
            slots[index].gameObject.SetActive(true);
        }

        private void SetArmedSlotsActive(bool active)
        {
            foreach (var idx in _armed.Keys)
            {
                var slots = _diceZone != null ? _diceZone.GetDiceSlots() : null;
                if (slots == null || idx >= slots.Count || slots[idx] == null) continue;
                slots[idx].gameObject.SetActive(active);
            }
        }

        // ---- Alineado final -------------------------------------------------------

        private void StartAlign()
        {
            _aligning = true;
            var slots = _diceZone != null ? _diceZone.GetDiceSlots() : null;

            int order = 0;
            foreach (var die in _dice.Values)
            {
                if (die.Tween.isAlive) die.Tween.Stop();
                die.Returning = false;

                Vector2 target = slots != null && die.Index < slots.Count && slots[die.Index] != null
                    ? AnchorToLayerLocal(slots[die.Index])
                    : die.RestPos;

                if (_cfg.AlignSeconds <= 0f)
                    die.View.Rect.anchoredPosition = target;
                else
                    die.Tween = Tween.UIAnchoredPosition(die.View.Rect, target, _cfg.AlignSeconds,
                        Ease.InOutCubic, startDelay: order * _cfg.AlignStagger);
                order++;
            }
        }

        private void TickAlignCompletion()
        {
            foreach (var die in _dice.Values)
                if (die.Tween.isAlive) return;

            // Reveal primero (los slots reales se encienden debajo de los ghosts,
            // exactamente en la misma posición) y recién después el teardown.
            _aligning = false;
            _service.CompleteReveal();
            TeardownVisuals();
        }

        // ---- Visuales ---------------------------------------------------------------

        private DieVisual CreateDie(int index, Vector2 pos)
        {
            var view = Instantiate(_diePrefab, _layer);
            view.Rect.anchoredPosition = pos;
            view.ShowText("?");

            return new DieVisual
            {
                Index = index,
                View = view,
                Juice = view.GetComponent<DiceThrowDieJuice>(),
                RestPos = pos,
                SpotPos = pos,
            };
        }

        private void TweenBack(DieVisual die, Vector2 target)
        {
            if (_cfg.ReturnSeconds <= 0f)
            {
                die.View.Rect.anchoredPosition = target;
                die.Returning = false;
                return;
            }
            die.Returning = true;
            die.Tween = Tween.UIAnchoredPosition(die.View.Rect, target, _cfg.ReturnSeconds, Ease.OutCubic);
        }

        private void TeardownVisuals()
        {
            foreach (var die in _dice.Values)
            {
                if (die.Tween.isAlive) die.Tween.Stop();
                if (die.RotTween.isAlive) die.RotTween.Stop();
                if (die.ScaleTween.isAlive) die.ScaleTween.Stop();
                if (die.View != null) Destroy(die.View.gameObject);
            }
            _dice.Clear();
            _aligning = false;
            _inputScope.Release();
        }

        // ---- Espacios / helpers ------------------------------------------------------

        private void EnsureCfg()
        {
            _cfg = _service != null && _service.Settings != null ? _service.Settings : FallbackCfg();
        }

        private void ResolveZone()
        {
            if (_diceZone == null)
                _diceZone = FindFirstObjectByType<DiceZoneView>(FindObjectsInactive.Include);
        }

        private bool EnsureLayer()
        {
            if (_layer != null)
            {
                _layer.SetAsLastSibling(); // los dados vuelan por encima del resto del HUD
                return true;
            }
            Debug.LogWarning("[DiceThrow2DPresenter] _layer sin asignar — autorar 'DiceThrowLayer' " +
                             "bajo el Canvas y arrastrarlo al inspector.", this);
            enabled = false;
            return false;
        }

        private Vector2 ScreenToLayerLocal(Vector2 screen)
        {
            var canvas = _layer.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, cam, out var local);
            return local;
        }

        private Vector2 AnchorToLayerLocal(RectTransform anchor)
        {
            if (anchor == null || _layer == null) return Vector2.zero;
            var world = anchor.TransformPoint(Vector3.zero);
            return (Vector2)_layer.InverseTransformPoint(world);
        }

        // Velocidad angular cosmética que el flick imprime al sprite (0 con reduced motion).
        private float FlickSpin(Vector2 flickVel)
        {
            if (ReducedMotion) return 0f;
            return DiceThrowFeelMath.FlickAngularVelocity(
                       flickVel, _cfg.FlightSpinDegreesPerUnit, _cfg.FlightSpinMaxDegreesPerSecond)
                   * Random.Range(0.8f, 1.2f);
        }

        // Endereza el ghost al asentarse — el "clac" visual, y deja el align invisible
        // (el slot real debajo está siempre a 0°).
        private void SettleUpright(DieVisual die)
        {
            if (die.RotTween.isAlive) die.RotTween.Stop();
            if (_cfg.SettleUprightSeconds <= 0f || ReducedMotion)
            {
                die.View.Rect.localRotation = Quaternion.identity;
                return;
            }
            die.RotTween = Tween.LocalRotation(die.View.Rect, Quaternion.identity,
                _cfg.SettleUprightSeconds, Ease.OutBack);
        }

        // Offsets en anillo para que los dados agarrados orbiten el cursor sin apilarse.
        private Vector2 OrbitOffset(int index)
        {
            float angle = index * Mathf.PI * 2f / 5f;
            float radius = (_diePrefab != null ? _diePrefab.HalfSize * 2f : 56f) * 0.65f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private DiceThrowSettingsSO FallbackCfg()
        {
            if (_fallbackCfg == null)
                _fallbackCfg = ScriptableObject.CreateInstance<DiceThrowSettingsSO>();
            return _fallbackCfg;
        }
    }
}
