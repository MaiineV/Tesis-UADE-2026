using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Inventory
{
    /// <summary>
    /// Una celda del grid de inventario: la casilla, el sprite base y el ícono del item.
    /// El tooltip (nombre + descripción localizados) se arma on-demand en cada hover,
    /// así que no necesita suscribirse al cambio de idioma.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Inventory Item Slot View")]
    public class InventoryItemSlotView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private Image _cellBg;
        [SerializeField, Required] private Image _base;
        [SerializeField, Required] private Image _icon;
        [SerializeField, Required] private UITooltipTrigger _tooltip;

        /// <summary>
        /// Bindea un item, o <c>null</c> para dejar la celda vacía (base visible, sin tooltip).
        /// </summary>
        public void Bind(ItemSO item)
        {
            bool occupied = item != null;
            bool hasIcon = occupied && item.Icon != null;

            if (_icon != null)
            {
                _icon.sprite = hasIcon ? item.Icon : null;
                _icon.enabled = hasIcon;
            }

            // La base es SOLO el fallback de un item sin sprite propio — una celda vacía
            // muestra el cuadradito pelado, sin dibujo adentro.
            if (_base != null) _base.enabled = occupied && !hasIcon;

            if (_tooltip != null)
            {
                // String vacío explícito en celdas vacías — con TextProvider null el
                // trigger cae en TooltipResolver.AutoResolve y puede mostrar otra cosa.
                _tooltip.TextProvider = occupied ? () => BuildTooltip(item) : () => string.Empty;
            }
        }

        private static string BuildTooltip(ItemSO item)
        {
            string fallbackName = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : item.ItemId;
            string name = LocalizedContent.Name(item.ItemId, fallbackName);
            string body = LocalizedContent.Description(item.ItemId, item.Description ?? string.Empty);
            return string.IsNullOrEmpty(body) ? $"<b>{name}</b>" : $"<b>{name}</b>\n{body}";
        }
    }
}
