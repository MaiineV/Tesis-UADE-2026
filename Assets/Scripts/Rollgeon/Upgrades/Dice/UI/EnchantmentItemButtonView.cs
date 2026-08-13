using System;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Sub-view genérica que representa un botón clickeable con label + sublabel.
    /// Usado por <see cref="EnchantmentAltarView"/> para los botones de dado y los
    /// botones de slot (mismo shape: button + label + sublabel + selected highlight).
    /// </summary>
    /// <remarks>
    /// Pattern alineado con <c>ComboRowView</c>: prefab instanciable que el view
    /// principal clona para cada entry del bag. Configurable via
    /// <see cref="Configure"/>; el caller pasa el callback de click.
    /// La variante "dice card" agrega un icono de dado con el número de caras
    /// centrado (<see cref="SetDiceIcon"/>) — esos refs son opcionales y la slot
    /// card simplemente no los tiene.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Enchantment Item Button View")]
    public sealed class EnchantmentItemButtonView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Alpha del sublabel en estado "Vacío" — tono opaco pedido por diseño.</summary>
        private const float MutedAlpha = 0.55f;

        [Title("Item Button — Widget refs")]
        [Required]
        [SerializeField] private Button _button;

        [Required]
        [SerializeField] private TextMeshProUGUI _label;

        [Tooltip("Sublabel opcional — usado para mostrar 'X/Y cupos' o el nombre del encantamiento aplicado.")]
        [SerializeField] private TextMeshProUGUI _subLabel;

        [Tooltip("Highlight visual opcional. Activado cuando el botón está 'selected' en el flow.")]
        [SerializeField] private GameObject _selectedHighlight;

        [Title("Dice card (opcional — solo la variante de dado)")]
        [Tooltip("Sprite del dado, asignado en runtime via SetDiceIcon.")]
        [SerializeField, Optional] private Image _icon;

        [Tooltip("Número de caras centrado sobre el icono.")]
        [SerializeField, Optional] private TextMeshProUGUI _iconNumberLabel;

        [Title("Juice (opcional)")]
        [SerializeField, Optional] private EnchantmentAltarUiSettingsSO _juiceSettings;

        private Action _onClick;
        private UITooltipTrigger _tooltipTrigger;
        private bool _selected;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCaptured;
        private Color _subLabelBaseColor = Color.white;
        private bool _subLabelColorCaptured;

        private void Awake()
        {
            CaptureBaseScale();
            if (_button != null) _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        private void OnDisable()
        {
            // Nada de juice sobrevive al disable — patrón ChipStackView: matar
            // tweens y volver a la pose de reposo.
            Tween.StopAll(onTarget: transform);
            if (_baseScaleCaptured) transform.localScale = _baseScale;
        }

        private void CaptureBaseScale()
        {
            if (_baseScaleCaptured) return;
            _baseScale = transform.localScale;
            _baseScaleCaptured = true;
        }

        /// <summary>
        /// Configura el botón con texto + callback. <paramref name="tooltipProvider"/>
        /// es opcional (CNF-011) — hover sobre el botón muestra la descripción del
        /// encantamiento/dado. Botones son reusados desde un pool entre populates, así
        /// que sin provider hay que limpiar el tooltip de una config anterior en vez de
        /// dejarlo colgado (ver <see cref="ConfigureTooltip"/>).
        /// </summary>
        public void Configure(string label, string subLabel, Action onClick, Func<string> tooltipProvider = null)
        {
            if (_label != null) _label.text = label ?? string.Empty;
            if (_subLabel != null) _subLabel.text = subLabel ?? string.Empty;
            _onClick = onClick;
            SetMuted(false);
            SetSelected(false);
            SetInteractable(true);
            ConfigureTooltip(tooltipProvider);
        }

        /// <summary>
        /// Variante dice card: sprite del dado + número de caras centrado. No-op en
        /// prefabs sin icono (la slot card comparte esta clase).
        /// </summary>
        public void SetDiceIcon(Sprite sprite, string centeredNumber)
        {
            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
            }
            if (_iconNumberLabel != null)
            {
                _iconNumberLabel.text = centeredNumber ?? string.Empty;
                _iconNumberLabel.raycastTarget = false;
            }
        }

        /// <summary>
        /// Tono opaco del sublabel para el estado "Vacío" de un cupo — mismo texto,
        /// menos presencia. Restaurado en cada <see cref="Configure"/>.
        /// </summary>
        public void SetMuted(bool muted)
        {
            if (_subLabel == null) return;
            if (!_subLabelColorCaptured)
            {
                _subLabelBaseColor = _subLabel.color;
                _subLabelColorCaptured = true;
            }

            var c = _subLabelBaseColor;
            _subLabel.color = muted ? new Color(c.r, c.g, c.b, c.a * MutedAlpha) : c;
        }

        public void SetSelected(bool selected)
        {
            bool wasSelected = _selected;
            _selected = selected;
            if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);

            // Punch solo en la transición a seleccionado — los refresh que re-marcan
            // la misma card (repopulate tras un apply) no deben re-animar.
            if (selected && !wasSelected) PlaySelectPunch();
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
        }

        /// <summary>Punch de escala — usado por la view como feedback extra al encantar.</summary>
        public void PlaySelectPunch()
        {
            if (_juiceSettings == null || !Application.isPlaying || DiceUiMotionPrefs.ReducedMotion) return;

            CaptureBaseScale();
            Tween.StopAll(onTarget: transform);
            transform.localScale = _baseScale;
            Tween.PunchScale(transform, Vector3.one * _juiceSettings.SelectPunchScale,
                _juiceSettings.SelectPunchDuration, useUnscaledTime: true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanHoverJuice()) return;
            Tween.StopAll(onTarget: transform);
            Tween.Scale(transform, _baseScale * _juiceSettings.HoverScale,
                _juiceSettings.HoverDuration, Ease.OutQuad, useUnscaledTime: true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!CanHoverJuice()) return;
            Tween.StopAll(onTarget: transform);
            Tween.Scale(transform, _baseScale, _juiceSettings.HoverDuration,
                Ease.OutQuad, useUnscaledTime: true);
        }

        private bool CanHoverJuice()
        {
            return _juiceSettings != null
                   && Application.isPlaying
                   && !DiceUiMotionPrefs.ReducedMotion
                   && (_button == null || _button.interactable);
        }

        private void ConfigureTooltip(Func<string> tooltipProvider)
        {
            if (tooltipProvider != null)
            {
                if (_tooltipTrigger == null) _tooltipTrigger = GetComponent<UITooltipTrigger>();
                if (_tooltipTrigger == null) _tooltipTrigger = gameObject.AddComponent<UITooltipTrigger>();
                _tooltipTrigger.TextProvider = tooltipProvider;
                return;
            }

            // Sin provider — si el trigger ya existe (botón reusado que antes SÍ tenía
            // tooltip), vaciar el texto en vez de dejar TextProvider null: null hace que
            // UITooltipTrigger caiga a TooltipResolver.AutoResolve, que puede resolver un
            // tooltip de otro componente del mismo GameObject.
            if (_tooltipTrigger == null) _tooltipTrigger = GetComponent<UITooltipTrigger>();
            if (_tooltipTrigger != null) _tooltipTrigger.TextProvider = () => string.Empty;
        }

        private void HandleClicked() => _onClick?.Invoke();
    }
}
