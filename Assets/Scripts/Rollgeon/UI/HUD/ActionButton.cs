using System;
using Rollgeon.Heroes;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Wrapper de un <see cref="UnityEngine.UI.Button"/> de la HUD de combate que
    /// expone un mini state machine (<see cref="ActionButtonState"/>) para responder
    /// a Selected (pressed), Used, Locked, Available. No escucha eventos del bus —
    /// recibe transiciones via <see cref="SetState"/> desde
    /// <see cref="PlayerActionButtonsView"/>.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Button")]
    public class ActionButton : MonoBehaviour
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

        // ======================================================================
        // Internal state
        // ======================================================================

        [ShowInInspector, ReadOnly]
        private ActionButtonState _state = ActionButtonState.Locked;

        private Image _image;
        private Outline _outline;
        private Vector3 _baseScale;

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

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
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

        private void OnDestroy()
        {
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

        private void ApplyVisual()
        {
            if (_button == null) return;

            switch (_state)
            {
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

        // ======================================================================
        // Cost label
        // ======================================================================

        public void RefreshCostLabel(HeroActionBehavior behavior)
        {
            if (_costLabel == null) return;
            if (behavior == null) { _costLabel.text = string.Empty; return; }

            _costLabel.text = behavior.EnergyCost <= 0
                ? _zeroCostText
                : string.Format(_costLabelFormat, behavior.EnergyCost);
        }

        // ======================================================================
        // Click handler
        // ======================================================================

        private void HandleClick()
        {
            OnClicked?.Invoke();
        }
    }
}
