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

        // BUG-063: el prefab dejaba el TMP en blanco puro sobre el marco FrameAnim
        // (casi blanco) — el nombre del ítem era ilegible; solo el monto recibía tinte
        // vía rich-text. Marrón oscuro de la paleta (mismo del texto de tooltip sobre
        // pergamino). Default por initializer ⇒ el prefab existente no se toca.
        [SerializeField, Tooltip("Color del nombre del ítem. El monto conserva su tinte rich-text.")]
        private Color _labelColor = new Color(0.14f, 0.1f, 0.07f, 1f);

        public RectTransform Rect => (RectTransform)transform;

        public void Show(Sprite icon, string label, Sprite fallbackIcon)
        {
            if (_icon != null)
            {
                _icon.sprite = icon != null ? icon : fallbackIcon;
                _icon.enabled = _icon.sprite != null;
            }
            if (_label != null)
            {
                _label.text = label;
                _label.color = _labelColor;
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
