using System;
using Patterns;
using PrimeTween;
using Rollgeon.Combat.Rolls;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Vaso de generala del Pool de Rolls del jugador con label "actual/max"
    /// debajo. Heredera directa de la pila de chips (Feature#0050→#0053):
    /// mismo GUID de script, mismo prefab. Solo visible en combate — el pool no
    /// existe en exploración. Dueña del ESTADO (datos, gating, label, pose
    /// lógica); el movimiento vive en <see cref="RollCupJuice"/> — sin juice o
    /// fuera de Play, la pose se aplica instantánea.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Roll Cup View")]
    public class RollCupView : MonoBehaviour
    {
        private const string LogPrefix = "[RollCupView] ";

        [Title("Roll Cup — Widget refs")]
        [Required("Arrastrar el RectTransform del vaso (Image con VasoGenerala_0).")]
        [SerializeField]
        private RectTransform _cup;

        [Required("Arrastrar el TextMeshProUGUI debajo del vaso.")]
        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField, Optional, Tooltip("Coreografía del vaso (bob/shake/flip). Sin wiring, poses instantáneas.")]
        private RollCupJuice _juice;

        [Title("Feedback — sin rolls")]
        [SerializeField, Tooltip("Color del flash del número cuando una acción no se puede pagar. " +
                 "Default = #D1365A, el rojo de UI de la paleta.")]
        private Color _insufficientColor = new Color(0.820f, 0.212f, 0.353f, 1f);

        [SerializeField, Tooltip("Duración del flash rojo del número (ida y vuelta).")]
        private float _insufficientFlashDuration = 0.18f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Punch de escala del número al rechazar.")]
        private float _insufficientPunch = 0.3f;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private bool _hasData;

        // -1 = sin dato mostrado (primer fetch o reentrada a combate): el
        // clasificador devuelve None y el vaso toma la pose sin coreografía.
        private int _lastShownCurrent = -1;

        private Color _labelBaseColor = Color.white;
        private Vector3 _labelRestScale = Vector3.one;
        private Tween _insufficientFlash;
        private Tween _insufficientPunchTween;

        /// <summary>Pose lógica actual del vaso (true = boca abajo, sin rolls).</summary>
        public bool IsCupFaceDown { get; private set; }

        private void Awake()
        {
            if (_label != null)
            {
                _labelBaseColor = _label.color;
                _labelRestScale = _label.transform.localScale;
            }
        }

        /// <summary>
        /// Feedback de "no te alcanza": sacude el vaso y hace flashear el número en rojo.
        /// Lo dispara <see cref="PlayerActionButtonsView"/> cuando el jugador intenta usar
        /// una acción impagable — el vaso es la respuesta a "¿por qué no puedo?".
        /// No-op con reduced motion (el shake del juice ya se auto-gatea).
        /// </summary>
        public void PlayInsufficient()
        {
            if (_juice != null) _juice.PlayInsufficientShake();

            if (_label == null || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            if (_insufficientFlash.isAlive) return; // spam de rechazos: un flash por vez

            // Yoyo en vez de dos tweens encadenados: se auto-restaura al color base
            // aunque lo interrumpa un cambio de escena. Apply() reescribe el texto en
            // cada cambio del pool pero nunca el color — si el flash quedara a medias,
            // el número se quedaría rojo para siempre y nada lo repararía.
            _insufficientFlash = Tween.Color(_label, _insufficientColor,
                _insufficientFlashDuration,
                cycles: 2, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);

            if (_insufficientPunch <= 0f) return;
            // El Label del pool no tiene hijos, así que escalarlo es seguro.
            _insufficientPunchTween = Tween.PunchScale(_label.transform,
                strength: Vector3.one * _insufficientPunch,
                duration: _insufficientFlashDuration * 2f,
                useUnscaledTime: true);
        }

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            if (!_bound) Subscribe();
            FetchInitialState();
        }

        public void Unbind()
        {
            // No-op: ciclo de vida por OnEnable/OnDisable (patrón sub-views HUD).
        }

        private void OnEnable()
        {
            Subscribe();
            FetchInitialState();
        }

        private void OnDisable()
        {
            Unsubscribe();

            // Red de seguridad: si el flash queda a medias al apagarse el HUD, el número
            // volvería a encenderse rojo. Lo devolvemos a su color de autoría a mano.
            if (_insufficientFlash.isAlive) _insufficientFlash.Stop();
            if (_insufficientPunchTween.isAlive) _insufficientPunchTween.Stop();
            if (_label != null)
            {
                _label.color = _labelBaseColor;
                _label.transform.localScale = _labelRestScale;
            }
        }

        private void Subscribe()
        {
            if (_bound) return;
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandleRollsChanged);
            TypedEvent<InsufficientRollsPayload>.Subscribe(HandleInsufficientRolls);
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandleRollsChanged);
            TypedEvent<InsufficientRollsPayload>.Unsubscribe(HandleInsufficientRolls);
            _bound = false;
        }

        private void HandleInsufficientRolls(InsufficientRollsPayload payload)
        {
            // Antes de que el Bind resuelva, _playerGuid está vacío: ahí el único
            // jugador posible es el del payload, así que no filtramos de más.
            if (_playerGuid != Guid.Empty && payload.PlayerGuid != _playerGuid) return;
            PlayInsufficient();
        }

        /// <summary>
        /// Reintento del estado inicial: al arrancar la run el BindAll del HUD
        /// puede correr antes de que el jugador/servicio existan y el fetch
        /// falla silencioso — el vaso quedaba sin datos hasta el primer combate.
        /// Se reintenta por frame hasta la primera lectura exitosa.
        /// </summary>
        private void Update()
        {
            if (_hasData) return;

            if (_playerGuid == Guid.Empty
                && ServiceLocator.TryGetService<Rollgeon.Player.IPlayerService>(out var ps) && ps != null
                && ps.PlayerGuid != Guid.Empty)
            {
                _playerGuid = ps.PlayerGuid;
            }

            if (_playerGuid == Guid.Empty) return;
            FetchInitialState();
        }

        private void HandleRollsChanged(params object[] args)
        {
            if (args == null || args.Length < 3)
            {
                Debug.LogWarning(LogPrefix + "OnPlayerRollsChanged args malformed (len < 3).", this);
                return;
            }
            if (!(args[0] is Guid guid) || guid != _playerGuid) return;
            if (!(args[1] is int current) || !(args[2] is int max)) return;

            _hasData = true;
            Apply(current, max, animate: true);
        }

        private void FetchInitialState()
        {
            // Silencioso sin datos: el Update reintenta (loguear acá spamearía).
            if (_playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null) return;

            int max = rolls.GetMax(_playerGuid);
            if (max <= 0) return; // ruleset aún no inicializado

            _hasData = true;
            Apply(rolls.GetCurrent(_playerGuid), max, animate: false);
        }

        private void Apply(int current, int max, bool animate)
        {
            // El pool solo existe en combate: fuera de él el vaso y el número se
            // ocultan (el GO raíz queda activo para seguir escuchando eventos).
            bool inCombat = ServiceLocator.TryGetService<IRollPoolService>(out var rolls)
                            && rolls != null && rolls.IsCombatActive;
            if (_cup != null) _cup.gameObject.SetActive(inCombat);
            if (_label != null) _label.gameObject.SetActive(inCombat);
            if (!inCombat)
            {
                // Olvidar lo mostrado: reentrar a combate aplica pose directa y
                // nunca reproduce una transición espuria (ej. flip por el 0 del
                // OnCombatEnd anterior). El juice se frena — un bob sobre un
                // vaso oculto es puro desperdicio.
                _lastShownCurrent = -1;
                if (_juice != null && Application.isPlaying) _juice.StopAndRest();
                return;
            }

            // El número nunca miente: se actualiza antes de cualquier coreografía.
            if (_label != null) _label.text = ChipStackMath.FormatRollsLabel(current, max);

            bool faceDown = RollCupMath.IsFaceDown(current);
            var transition = animate
                ? RollCupMath.Classify(_lastShownCurrent, current)
                : RollCupTransition.None;
            _lastShownCurrent = current;
            IsCupFaceDown = faceDown;

            if (_juice != null && Application.isPlaying)
            {
                if (transition == RollCupTransition.None) _juice.SetPoseInstant(faceDown);
                else _juice.OnTransition(transition, faceDown);
            }
            else
            {
                ApplyPoseInstant(faceDown);
            }
        }

        /// <summary>Pose sin animación — camino de EditMode y de reduced wiring.</summary>
        private void ApplyPoseInstant(bool faceDown)
        {
            if (_cup == null) return;
            _cup.localEulerAngles = new Vector3(0f, 0f,
                faceDown ? RollCupMath.FaceDownZ : RollCupMath.UprightZ);
        }
    }
}
