using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Una celda del reel gacha: fondo con sprite por rareza + ícono de ítem
    /// o variante de oro (ícono + monto). Sin tooltip — las celdas scrollean.
    /// Layout: el padre la posiciona con <see cref="ChestReelMath.CellCenterX"/>;
    /// nunca agregar LayoutGroup a la tira.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Chest Reveal/Chest Reel Cell View")]
    public sealed class ChestReelCellView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField] private Image _cellBg;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _goldIcon;
        [SerializeField] private TMP_Text _goldAmountLabel;

        [Title("Skin")]
        [Tooltip("Fondo por rareza, indexado por ItemRarity (0=Common … 3=Legendary).")]
        [SerializeField] private Sprite[] _rarityBackgrounds;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Fondo de la celda — el juice lo pulsa durante WaitDismiss.</summary>
        public Graphic BackgroundGraphic => _cellBg;

        public void Bind(ChestReelCellData data)
        {
            if (_cellBg != null && _rarityBackgrounds != null && _rarityBackgrounds.Length > 0)
            {
                int index = Mathf.Clamp((int)data.Rarity, 0, _rarityBackgrounds.Length - 1);
                _cellBg.sprite = _rarityBackgrounds[index];
            }

            bool hasItemIcon = !data.IsGold && data.Item != null && data.Item.Icon != null;
            if (_icon != null)
            {
                _icon.sprite = hasItemIcon ? data.Item.Icon : null;
                _icon.enabled = hasItemIcon;
            }

            if (_goldIcon != null) _goldIcon.enabled = data.IsGold;
            if (_goldAmountLabel != null)
            {
                _goldAmountLabel.gameObject.SetActive(data.IsGold);
                if (data.IsGold)
                {
                    string format = LocalizedContent.Ui(ChestRevealTextKeys.GoldAmount, "+{0}");
                    _goldAmountLabel.text = string.Format(format, data.GoldAmount);
                }
            }
        }
    }
}
