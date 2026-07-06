using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    /// La física es COSMÉTICA: el resultado ya fue precalculado por el
    /// <see cref="IDiceThrowService"/> al abrir la sesión — cada dado muestra su cara
    /// (<see cref="IDiceThrowService.PeekPendingFace"/>) recién al frenar. Los visuales
    /// viven en un layer full-rect sin raycast (patrón del drag ghost) así que no
    /// interfieren con la UI real. El servicio es la única fuente de verdad de estados;
    /// este componente solo lee input y anima.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Dice/Dice Throw 2D Presenter")]
    public sealed class DiceThrow2DPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Zona de dados del HUD. Null = se resuelve por Find al arrancar la sesión.")]
        private DiceZoneView _diceZone;

        [SerializeField, Tooltip("Tamaño del dado volador (px de canvas).")]
        private Vector2 _dieSize = new Vector2(56f, 56f);

        private sealed class DieVisual
        {
            public int Index;
            public RectTransform Rt;
            public TextMeshProUGUI Label;
            public Vector2 Vel;
            public Vector2 RestPos;    // último lugar quieto (spot inicial o settle)
            public Vector2 SpotPos;    // spot "sin tirar" en la rollArea
            public float SettleHeld;
            public float FlightTime;
            public float FaceCycleAt;
            public bool Returning;     // tween de vuelta en curso (cancel) — no simular
            public Tween Tween;
        }

        private IDiceThrowService _service;
        private DiceThrowSettingsSO _cfg;
        private DiceThrowSettingsSO _fallbackCfg;
        private readonly DiceThrowInputScope _inputScope = new DiceThrowInputScope();
        private readonly Dictionary<int, DieVisual> _dice = new Dictionary<int, DieVisual>();

        private RectTransform _layer;
        private Canvas _canvas;
        private Vector2 _cursorLocal;
        private Vector2 _mouseVel;
        private bool _hasLastCursor;
        private bool _lmbWasPressed;
        private bool _aligning;

        // ---- Lifecycle -------------------------------------------------------

        private void OnDisable()
        {
            if (_service != null)
            {
                UnhookService(_service);
                _service.DetachPresenter(this); // aborta si había sesión
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

            TickCursor(dt);
            if (!_aligning) TickInput();
            TickSimulation(dt);
            if (_aligning) TickAlignCompletion();
        }

        // El servicio es Run-scoped: se re-crea por run. Re-resolver por frame y
        // re-attachear cuando cambia la instancia (mismo patrón de polling que usa
        // TileClickHandler con ISelectionController).
        private void SyncServiceBinding()
        {
            ServiceLocator.TryGetService<IDiceThrowService>(out var svc);
            if (ReferenceEquals(svc, _service)) return;

            if (_service != null)
            {
                UnhookService(_service);
                _service.DetachPresenter(this);
                TeardownVisuals();
            }

            _service = svc;
            if (_service == null) return;

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
            _cfg = _service.Settings != null ? _service.Settings : FallbackCfg();

            ResolveZone();
            EnsureLayer();
            if (_layer == null) return;

            _diceZone?.HideSlotsForThrow(_service.ThrownMask);

            var mask = _service.ThrownMask;
            var spotCenter = AnchorToLayerLocal(_diceZone != null ? _diceZone.GetRollArea() : null);

            int spot = 0, total = 0;
            for (int i = 0; i < mask.Count; i++) if (mask[i]) total++;

            for (int i = 0; i < mask.Count; i++)
            {
                if (!mask[i]) continue;
                var pos = spotCenter + new Vector2((spot - (total - 1) * 0.5f) * (_dieSize.x + 12f), 0f);
                _dice[i] = CreateDie(i, pos);
                spot++;
            }

            _mouseVel = Vector2.zero;
            _hasLastCursor = false;
            _aligning = false;
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

        // ---- Input -------------------------------------------------------------

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

        private void TickInput()
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
                    if ((die.Rt.anchoredPosition - _cursorLocal).sqrMagnitude > r2) continue;
                    if (!_service.TryGrab(die.Index)) continue;
                    if (die.Tween.isAlive) die.Tween.Stop();
                    die.Returning = false;
                    die.Vel = Vector2.zero;
                }
            }

            // Click derecho = cancelar el agarre (los dados vuelven a su lugar previo).
            if (mouse.rightButton.wasPressedThisFrame)
                CancelGrabAndReturn();

            // Soltar con velocidad = flick (arrojar). Soltar suave = cancel.
            if (!lmb && _lmbWasPressed && AnyGrabbed())
            {
                if (_mouseVel.magnitude >= _cfg.FlickMinSpeed) ThrowGrabbed();
                else CancelGrabAndReturn();
            }
            _lmbWasPressed = lmb;
        }

        private bool AnyGrabbed()
        {
            foreach (var die in _dice.Values)
                if (_service.GetDieState(die.Index) == DieThrowState.Grabbed) return true;
            return false;
        }

        private void ThrowGrabbed()
        {
            foreach (var idx in _service.ReleaseGrabbed())
            {
                if (!_dice.TryGetValue(idx, out var die)) continue;
                die.Vel = _mouseVel * _cfg.ThrowGain * Random.Range(0.9f, 1.1f);
                die.FlightTime = 0f;
                die.SettleHeld = 0f;
            }
        }

        private void CancelGrabAndReturn()
        {
            foreach (var idx in _service.CancelGrab())
            {
                if (!_dice.TryGetValue(idx, out var die)) continue;
                die.Vel = Vector2.zero;
                die.Returning = true;
                var target = _service.GetDieState(idx) == DieThrowState.Settled ? die.RestPos : die.SpotPos;
                if (_cfg.ReturnSeconds <= 0f)
                {
                    die.Rt.anchoredPosition = target;
                    die.Returning = false;
                }
                else
                {
                    die.Tween = Tween.UIAnchoredPosition(die.Rt, target, _cfg.ReturnSeconds, Ease.OutCubic);
                }
            }
        }

        // ---- Simulación ----------------------------------------------------------

        private void TickSimulation(float dt)
        {
            float halfSize = Mathf.Max(_dieSize.x, _dieSize.y) * 0.5f;
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
                        die.Rt.anchoredPosition = DiceThrow2DMath.SpringStep(
                            die.Rt.anchoredPosition, ref die.Vel, target,
                            _cfg.SpringStiffness, _cfg.SpringDamping, dt);
                        break;
                    }
                    case DieThrowState.Flying:
                    {
                        var pos = DiceThrow2DMath.FlightStep(die.Rt.anchoredPosition, ref die.Vel, _cfg.FlightDrag, dt);
                        var vel = die.Vel;
                        DiceThrow2DMath.BounceInRect(ref pos, ref vel, rect, halfSize, _cfg.Restitution);
                        die.Vel = vel;
                        die.Rt.anchoredPosition = pos;
                        die.FlightTime += dt;

                        // Caras "rodando" mientras vuela — puro teatro.
                        if (Time.unscaledTime >= die.FaceCycleAt)
                        {
                            die.Label.text = Random.Range(1, 7).ToString();
                            die.FaceCycleAt = Time.unscaledTime + 0.06f;
                        }

                        bool settled = DiceThrow2DMath.SettleTick(
                            die.Vel.magnitude, _cfg.SettleSpeedEps, _cfg.SettleHoldSeconds, dt, ref die.SettleHeld);
                        if (settled || die.FlightTime >= _cfg.MaxFlightSeconds)
                        {
                            die.Vel = Vector2.zero;
                            die.RestPos = die.Rt.anchoredPosition;
                            die.Label.text = _service.PeekPendingFace(die.Index).ToString();
                            _service.NotifyDieSettled(die.Index);
                        }
                        break;
                    }
                }
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
                    die.Rt.anchoredPosition = target;
                else
                    die.Tween = Tween.UIAnchoredPosition(die.Rt, target, _cfg.AlignSeconds,
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
            var go = new GameObject($"ThrowDie_{index}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_layer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = _dieSize;
            rt.anchoredPosition = pos;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.13f, 0.13f, 0.16f, 0.95f);
            bg.raycastTarget = false;

            var labelGo = new GameObject("Face", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "?";
            label.fontSize = _dieSize.y * 0.55f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            return new DieVisual
            {
                Index = index,
                Rt = rt,
                Label = label,
                RestPos = pos,
                SpotPos = pos,
            };
        }

        private void TeardownVisuals()
        {
            foreach (var die in _dice.Values)
            {
                if (die.Tween.isAlive) die.Tween.Stop();
                if (die.Rt != null) Destroy(die.Rt.gameObject);
            }
            _dice.Clear();
            _aligning = false;
            _inputScope.Release();
        }

        // ---- Espacios / helpers ------------------------------------------------------

        private void ResolveZone()
        {
            if (_diceZone == null)
                _diceZone = FindFirstObjectByType<DiceZoneView>(FindObjectsInactive.Include);
        }

        // Layer full-rect sin raycast bajo el canvas raíz — patrón del drag ghost.
        private void EnsureLayer()
        {
            if (_layer != null) return;

            _canvas = _diceZone != null
                ? _diceZone.GetComponentInParent<Canvas>()?.rootCanvas
                : GetComponentInParent<Canvas>()?.rootCanvas;
            if (_canvas == null)
            {
                Debug.LogWarning("[DiceThrow2DPresenter] Sin canvas — no puedo crear el layer.", this);
                return;
            }

            var go = new GameObject("DiceThrowLayer", typeof(RectTransform), typeof(CanvasGroup));
            _layer = (RectTransform)go.transform;
            _layer.SetParent(_canvas.transform, false);
            _layer.anchorMin = Vector2.zero;
            _layer.anchorMax = Vector2.one;
            _layer.offsetMin = _layer.offsetMax = Vector2.zero;
            _layer.SetAsLastSibling();
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        private Vector2 ScreenToLayerLocal(Vector2 screen)
        {
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
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

        // Offsets en anillo para que los dados agarrados orbiten el cursor sin apilarse.
        private Vector2 OrbitOffset(int index)
        {
            float angle = index * Mathf.PI * 2f / 5f;
            float radius = _dieSize.x * 0.65f;
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
