using System;
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

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        /// <summary>Configura el botón con texto + callback.</summary>
        public void Configure(string label, string subLabel, Action onClick)
        {
            if (_label != null) _label.text = label ?? string.Empty;
            if (_subLabel != null) _subLabel.text = subLabel ?? string.Empty;
            _onClick = onClick;
            SetSelected(false);
            SetInteractable(true);
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
