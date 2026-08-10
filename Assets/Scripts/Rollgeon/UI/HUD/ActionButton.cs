using System;
using PrimeTween;
using Rollgeon.Heroes;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Wrapper de un <see cref="UnityEngine.UI.Button"/> de la HUD de combate que
    /// expone un mini state machine (<see cref="ActionButtonState"/>) para responder
    /// a Selected (pressed), Used, Locked, Available, Unaffordable. No escucha eventos
    /// del bus — recibe transiciones via <see cref="SetState"/> desde
    /// <see cref="PlayerActionButtonsView"/>.
    /// </summary>
    /// <remarks>
    /// Implementa <see cref="IPointerDownHandler"/> para poder reaccionar al intento de
    /// usar una accion impagable. El <see cref="Button"/> queda no-interactable en ese
    /// estado, pero eso no impide recibir el evento: uGUI filtra los handlers por
    /// <c>isActiveAndEnabled</c>, nunca por <c>interactable</c>, y ejecuta el functor
    /// sobre TODOS los componentes del GameObject que implementan la interfaz.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Button")]
    public class ActionButton : MonoBehaviour, IPointerDownHandler
    {
        // ======================================================================
        // Serialized fields
        // ======================================================================

        [Title("Wiring")]
        [Required]
        [SerializeField]
        private Button _button;

        [InfoBox("El slot define la behavior del hero que este boton invoca y se " +
                 "usa para resolver costo y precondiciones.")]
        [SerializeField]
        private HeroBehaviorSlot _slot;

        [InfoBox("Opcional. Label TMP que muestra el costo de energia.")]
        [SerializeField]
        private TextMeshProUGUI _costLabel;

        [Title("Cost label format")]
        [SerializeField, Tooltip("Format string para el costo. Default '{0}'. Ej: '{0}E', '-{0}'.")]
        private string _costLabelFormat = "{0}";

        [SerializeField, Tooltip("Texto cuando el costo es 0. Vacio = ocultar label.")]
        private string _zeroCostText = "";

        [Title("Visual — base")]
        [SerializeField]
        private Color _baseColor = Color.white;

        [Title("Visual — Selected (pressed)")]
        [SerializeField, Range(1f, 1.3f)]
        private float _selectedScale = 1.08f;

        [SerializeField]
        private Color _glowColor = new Color(1f, 0.95f, 0.4f, 1f);

        [SerializeField, Tooltip("Velocidad del pulse del glow alpha cuando Selected.")]
        private float _glowSpeed = 3f;

        [SerializeField, Tooltip("Alpha minimo y maximo del pulse.")]
        private Vector2 _glowAlphaRange = new Vector2(0.35f, 1f);

        [SerializeField, Tooltip("Distancia del Outline component (px).")]
        private Vector2 _outlineDistance = new Vector2(3f, -3f);

        [Title("Visual — Used")]
        [SerializeField, Range(0f, 1f), Tooltip("Multiplicador aplicado al color base cuando Used.")]
        private float _usedColorMultiplier = 0.45f;

        [Title("Visual — Unaffordable (no alcanza la energia)")]
        [SerializeField, Tooltip("Rojo del costo y del outline cuando no alcanza la energia. " +
                 "Default = #D1365A, el rojo de UI de la paleta.")]
        private Color _unaffordableColor = new Color(0.820f, 0.212f, 0.353f, 1f);

        [SerializeField, Tooltip("Duracion del shake del chip al intentar usarlo sin energia.")]
        private float _rejectShakeDuration = 0.28f;

        [SerializeField, Tooltip("Amplitud horizontal del shake de rechazo (px). Mantener chico: " +
                 "el chip comparte GameObject con el tooltip y el preview de rango, y un " +
                 "desplazamiento grande genera pointer exit/enter espurios bajo el cursor.")]
        private float _rejectShakeStrength = 6f;

        [SerializeField, Tooltip("Frecuencia del shake de rechazo (Hz).")]
        private float _rejectShakeFrequency = 22f;


        [Title("Activación")]
        [SerializeField, Tooltip("Si false, el click NO invoca OnClicked — el botón se activa " +
                 "solo por drag-and-drop (CNF-002 v2). ActionDragController lo apaga al " +
                 "attachear su ActionDragHandle; default true para usos sin drag.")]
        private bool _clickActivates = true;

        // ======================================================================
        // Internal state
        // ======================================================================

        [ShowInInspector, ReadOnly]
        private ActionButtonState _state = ActionButtonState.Locked;

        private Image _image;
        private Outline _outline;
        private Vector3 _baseScale;

        private Color _costLabelBaseColor = Color.white;
        private Tween _rejectShake;

        // Ortogonal al estado a proposito. Un chip puede estar Used o Locked por otra
        // razon Y ADEMAS ser impagable; con el rojo colgado del estado Unaffordable
        // (que es excluyente con los otros) esos casos nunca se pintaban. El estado
        // dice que podes hacer; esto dice si te alcanza la plata.
        private bool _affordable = true;

        // ======================================================================
        // Public API
        // ======================================================================

        public HeroBehaviorSlot Slot => _slot;
        public int SlotIndex => (int)_slot;
        public Button Button => _button;
        public ActionButtonState State => _state;

        /// <summary>Disparado cuando el boton es clickeado, independientemente del
        /// estado interactable (Unity gatekeepea Locked/Used a nivel Button).
        /// Delegate plano (no event) por simetria con
        /// <see cref="PlayerActionButtonsView.OnBehaviorSelected"/> — el view setea
        /// a null en OnDestroy para evitar leaks de lambdas con captura.</summary>
        public Action OnClicked;

        /// <summary>Disparado cuando el jugador intenta usar la accion sin energia
        /// suficiente (pointer down sobre un chip <see cref="ActionButtonState.Unaffordable"/>,
        /// o su hotkey). Lo consume <see cref="PlayerActionButtonsView"/> para sacudir
        /// tambien la pila de energia. Delegate plano por simetria con
        /// <see cref="OnClicked"/> — el view lo limpia en OnDestroy.</summary>
        public Action OnRejected;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
            if (_costLabel != null) _costLabelBaseColor = _costLabel.color;

            if (_button == null) _button = GetComponent<Button>();
            if (_button == null)
            {
                Debug.LogWarning($"[ActionButton] No Button wireado en {name} — el slot no respondera a clicks.");
                return;
            }

            _image = _button.targetGraphic as Image;
            _baseScale = transform.localScale;

            _outline = _button.GetComponent<Outline>();
            if (_outline == null)
            {
                _outline = _button.gameObject.AddComponent<Outline>();
            }
            _outline.effectColor = _glowColor;
            _outline.effectDistance = _outlineDistance;
            _outline.enabled = false;

            ApplyBaseColor();

            _button.onClick.AddListener(HandleClick);
            ApplyVisual();
        }

        private void OnDisable()
        {
            // Un shake en vuelo cuando el HUD se apaga dejaria el chip corrido y a
            // PrimeTween tweeneando un target destruido en el teardown de escena.
            if (_rejectShake.isAlive) _rejectShake.Complete();
        }

        private void OnDestroy()
        {
            if (_rejectShake.isAlive) _rejectShake.Stop();
            OnRejected = null;
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);
        }

        private void Update()
        {
            // Glow pulse mientras estamos Selected. Sin tweener externo — Mathf.PingPong
            // sobre unscaledTime alcanza para un efecto leve.
            if (_state != ActionButtonState.Selected || _outline == null) return;

            float t = Mathf.PingPong(Time.unscaledTime * _glowSpeed, 1f);
            float alpha = Mathf.Lerp(_glowAlphaRange.x, _glowAlphaRange.y, t);
            var c = _glowColor;
            c.a = alpha;
            _outline.effectColor = c;
        }

        // ======================================================================
        // State API
        // ======================================================================

        public void SetState(ActionButtonState state)
        {
            if (_state == state) return;
            _state = state;
            ApplyVisual();
        }

        /// <summary>
        /// Si al jugador le alcanza la energia para esta accion. Independiente de
        /// <see cref="SetState"/>: un chip Used o Locked por otra razon igual pinta el
        /// costo en rojo si no lo podes pagar, y responde al intento con el shake.
        /// </summary>
        public void SetAffordable(bool affordable)
        {
            if (_affordable == affordable) return;
            _affordable = affordable;
            ApplyCostLabelColor();
        }

        public bool IsAffordable => _affordable;

        private void ApplyVisual()
        {
            if (_button == null) return;

            ApplyCostLabelColor();

            switch (_state)
            {
                // Funcionalmente identico a Locked (no arranca drag, no responde al
                // hotkey); lo unico que cambia es que dice POR QUE.
                case ActionButtonState.Unaffordable:
                    _button.interactable = false;
                    ApplyBaseColor();
                    transform.localScale = _baseScale;
                    if (_outline != null)
                    {
                        _outline.effectColor = _unaffordableColor;
                        _outline.enabled = true;
                    }
                    break;

                case ActionButtonState.Locked:
                    _button.interactable = false;
                    ApplyBaseColor();
                    transform.localScale = _baseScale;
                    if (_outline != null) _outline.enabled = false;
                    break;

                case ActionButtonState.Available:
                    _button.interactable = true;
                    ApplyBaseColor();
                    transform.localScale = _baseScale;
                    if (_outline != null) _outline.enabled = false;
                    break;

                case ActionButtonState.Selected:
                    _button.interactable = true;
                    ApplyBaseColor();
                    transform.localScale = _baseScale * _selectedScale;
                    if (_outline != null)
                    {
                        _outline.effectColor = _glowColor;
                        _outline.enabled = true;
                    }
                    break;

                case ActionButtonState.Used:
                    _button.interactable = false;
                    if (_image != null) _image.color = _baseColor * _usedColorMultiplier;
                    transform.localScale = _baseScale;
                    if (_outline != null) _outline.enabled = false;
                    break;
            }
        }

        private void ApplyBaseColor()
        {
            if (_image != null) _image.color = _baseColor;
        }

        // Unaffordable sigue pintando rojo aunque nadie haya llamado SetAffordable —
        // el estado ya implica que la unica traba es la energia.
        private void ApplyCostLabelColor()
        {
            if (_costLabel == null) return;
            bool unaffordable = !_affordable || _state == ActionButtonState.Unaffordable;
            _costLabel.color = unaffordable ? _unaffordableColor : _costLabelBaseColor;
        }

        // ======================================================================
        // Cost label
        // ======================================================================

        public void RefreshCostLabel(HeroActionBehavior behavior)
        {
            if (_costLabel == null) return;
            // Sin behavior dejamos el label como esta en vez de vaciarlo: RefreshCostLabels
            // corre en el Bind, y si el hero todavia no resolvio, vaciarlo borraba el
            // numero para siempre (nada vuelve a llamarlo en todo el combate).
            if (behavior == null) return;
            RefreshCostLabel(behavior.EnergyCost);
        }

        public void RefreshCostLabel(int cost)
        {
            if (_costLabel == null) return;
            _costLabel.text = cost <= 0
                ? _zeroCostText
                : string.Format(_costLabelFormat, cost);
        }

        // ======================================================================
        // Click handler
        // ======================================================================

        /// <summary>Habilita/deshabilita la activación por click. El drag (que invoca
        /// <see cref="OnClicked"/> directo vía el dispatcher) no pasa por este gate.</summary>
        public void SetClickActivation(bool enabled) => _clickActivates = enabled;

        private void HandleClick()
        {
            if (!_clickActivates) return;
            OnClicked?.Invoke();
        }

        // ======================================================================
        // Rechazo por energia insuficiente
        // ======================================================================

        /// <summary>
        /// Pointer down sobre el chip. Solo nos interesa el caso impagable: el resto de
        /// los estados los maneja el Button (Available) o el drag handle. Usamos down y
        /// no click porque responde antes del umbral de drag — el jugador que "tira" del
        /// chip para usarlo tiene que sentir el rechazo igual.
        /// </summary>
        /// <remarks>
        /// Gatea por <see cref="_affordable"/> y no por el estado: un chip que ademas de
        /// impagable esta Locked por otra razon (Heal a vida llena) igual tiene que
        /// contestar algo. Antes salia por Unaffordable, que es excluyente, y esos casos
        /// se sentian como un boton muerto.
        /// </remarks>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_affordable && _state != ActionButtonState.Unaffordable) return;
            PlayRejectFeedback();
        }

        /// <summary>
        /// Sacude el chip y avisa (via <see cref="OnRejected"/>) para que la pila de
        /// energia responda tambien. No-op si ya hay un shake en vuelo — el jugador
        /// puede martillar el chip y no queremos un temblor perpetuo.
        /// </summary>
        /// <remarks>
        /// Solo sacude el chip entero, no el label del costo por separado: el GameObject
        /// <c>Cost</c> del prefab tiene colgado el marco de 100x100 del chip (mal
        /// parenteado), asi que escalarlo pulsaria el marco entero descentrado. El chip
        /// se mueve como una pieza y se lee mejor.
        /// </remarks>
        public void PlayRejectFeedback()
        {
            if (_rejectShake.isAlive) return;

            OnRejected?.Invoke();

            if (!Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            // Shake* de PrimeTween restaura el valor inicial al completar, asi que no
            // cacheamos reposo ni usamos StopAll: CombatHudZoneFlow reparentea y tweenea
            // estos mismos chips y un StopAll le mataria su tween a mitad de viaje.
            _rejectShake = Tween.ShakeLocalPosition(transform,
                strength: new Vector3(_rejectShakeStrength, 0f, 0f),
                duration: _rejectShakeDuration,
                frequency: _rejectShakeFrequency,
                useUnscaledTime: true);
        }
    }
}
