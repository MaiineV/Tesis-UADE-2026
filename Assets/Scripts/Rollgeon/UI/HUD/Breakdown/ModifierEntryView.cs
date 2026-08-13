using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>Una entrada del cascade de modificadores globales: icono + "+X" / "×X".</summary>
    public sealed class ModifierEntryView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _label;

        public RectTransform Rect => (RectTransform)transform;

        public void Show(Sprite icon, string label, Sprite fallbackIcon)
        {
            if (_icon != null)
            {
                _icon.sprite = icon != null ? icon : fallbackIcon;
                _icon.enabled = _icon.sprite != null;
            }
            if (_label != null) _label.text = label;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
