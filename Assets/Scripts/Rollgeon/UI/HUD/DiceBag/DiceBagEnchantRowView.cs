using System;
using Rollgeon.Localization;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Una fila del acordeón de encantamientos del drawer: header clickeable
    /// "Nombre - Tipo" (tipo coloreado por categoría) y un panel de descripción
    /// que se expande debajo. El drawer garantiza que haya a lo sumo una abierta.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Bag Enchant Row View")]
    public class DiceBagEnchantRowView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private TextMeshProUGUI _headerLabel;
        [SerializeField, Required] private Button _headerButton;
        [SerializeField, Required] private GameObject _descriptionPanel;
        [SerializeField, Required] private TextMeshProUGUI _descriptionLabel;

        private Action _onClick;

        /// <summary>Estado actual — seam de test y del acordeón del drawer.</summary>
        public bool IsExpanded => _descriptionPanel != null && _descriptionPanel.activeSelf;

        private void Awake()
        {
            if (_headerButton != null) _headerButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_headerButton != null) _headerButton.onClick.RemoveListener(HandleClick);
            _onClick = null;
        }

        private void HandleClick() => _onClick?.Invoke();

        public void Bind(EnchantmentSO enchantment, Action onClick)
        {
            _onClick = onClick;

            if (_headerLabel != null) _headerLabel.text = BuildHeader(enchantment);
            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = enchantment != null
                    ? LocalizedContent.Description(enchantment.UpgradeId, enchantment.Description ?? string.Empty)
                    : string.Empty;
            }

            SetExpanded(false);
        }

        public void SetExpanded(bool expanded)
        {
            if (_descriptionPanel != null) _descriptionPanel.SetActive(expanded);
        }

        /// <summary>
        /// "Nombre - <color>Tipo</color>". Sin categoría (None) el segmento del tipo
        /// se omite — la auditoría de assets no debería dejar que pase.
        /// </summary>
        public static string BuildHeader(EnchantmentSO enchantment)
        {
            if (enchantment == null) return string.Empty;

            string name = LocalizedContent.Name(enchantment.UpgradeId,
                !string.IsNullOrEmpty(enchantment.DisplayName) ? enchantment.DisplayName : enchantment.UpgradeId);

            string categoryKey = DiceBagTextKeys.CategoryKey(enchantment.Category);
            if (categoryKey == null) return name;

            string label = LocalizedContent.Ui(categoryKey, enchantment.Category.ToString());
            string hex = EnchantmentPalette.CategoryHex(enchantment.Category);
            return $"{name} - <color=#{hex}>{label}</color>";
        }
    }
}
