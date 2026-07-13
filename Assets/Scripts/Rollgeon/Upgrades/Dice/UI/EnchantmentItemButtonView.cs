using System;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
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
    /// </remarks>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Enchantment Item Button View")]
    public sealed class EnchantmentItemButtonView : MonoBehaviour
    {
        [Title("Item Button — Widget refs")]
        [Required]
        [SerializeField] private Button _button;

        [Required]
        [SerializeField] private TextMeshProUGUI _label;

        [Tooltip("Sublabel opcional — usado para mostrar 'X/Y cupos' o el nombre del encantamiento aplicado.")]
        [SerializeField] private TextMeshProUGUI _subLabel;

        [Tooltip("Highlight visual opcional. Activado cuando el botón está 'selected' en el flow.")]
        [SerializeField] private GameObject _selectedHighlight;

        private Action _onClick;
        private UITooltipTrigger _tooltipTrigger;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
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
            SetSelected(false);
            SetInteractable(true);
            ConfigureTooltip(tooltipProvider);
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

        public void SetSelected(bool selected)
        {
            if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
        }

        private void HandleClicked() => _onClick?.Invoke();
    }
}
