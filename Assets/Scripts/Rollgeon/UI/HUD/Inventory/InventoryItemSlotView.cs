using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Inventory
{
    /// <summary>
    /// Una celda rombo del inventario: el fondo cambia según estado (con item / vacía,
    /// normal / hover — NewUI_5/6/8/11) y el ícono del item va centrado. El tooltip
    /// (nombre + descripción localizados) se arma on-demand en cada hover, así que no
    /// necesita suscribirse al cambio de idioma.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Inventory Item Slot View")]
    public class InventoryItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Title("Refs")]
        [SerializeField, Required] private Image _cellBg;
        [SerializeField, Required] private Image _icon;
        [SerializeField, Required] private UITooltipTrigger _tooltip;

        [Title("Sprites de estado")]
        [SerializeField] private Sprite _filledSprite;
        [SerializeField] private Sprite _emptySprite;
        [SerializeField] private Sprite _filledHoverSprite;
        [SerializeField] private Sprite _emptyHoverSprite;

        private bool _occupied;
        private bool _hovered;

        /// <summary>Estado actual — seam de test y debug.</summary>
        public bool IsOccupied => _occupied;

        /// <summary>Sprite que está mostrando el fondo — seam de test.</summary>
        public Sprite CurrentBgSprite => _cellBg != null ? _cellBg.sprite : null;

        /// <summary>
        /// Bindea un item, o <c>null</c> para dejar la celda vacía (rombo apagado, sin tooltip).
        /// </summary>
        public void Bind(ItemSO item)
        {
            _occupied = item != null;
            bool hasIcon = _occupied && item.Icon != null;

            if (_icon != null)
            {
                _icon.sprite = hasIcon ? item.Icon : null;
                _icon.enabled = hasIcon;
            }

            if (_tooltip != null)
            {
                // String vacío explícito en celdas vacías — con TextProvider null el
                // trigger cae en TooltipResolver.AutoResolve y puede mostrar otra cosa.
                _tooltip.TextProvider = _occupied ? () => BuildTooltip(item) : () => string.Empty;
            }

            RefreshBackground();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            RefreshBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            RefreshBackground();
        }

        // El pool del drawer apaga celdas al achicarse — sin esto una celda reusada
        // podría renacer con el sprite hover pegado.
        private void OnDisable()
        {
            _hovered = false;
            RefreshBackground();
        }

        private void RefreshBackground()
        {
            if (_cellBg == null) return;
            _cellBg.sprite = _occupied
                ? (_hovered ? _filledHoverSprite : _filledSprite)
                : (_hovered ? _emptyHoverSprite : _emptySprite);
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
