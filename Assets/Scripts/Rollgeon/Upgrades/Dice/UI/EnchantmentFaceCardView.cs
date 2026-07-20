using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Mini-card de una cara en la sección "Caras actuales" del altar: el sprite
    /// base del dado seleccionado con el número de la cara centrado. Mismo
    /// lenguaje visual que las dice cards — escala a cualquier cara (un D20
    /// muestra 17 igual que un D4 muestra 3), a diferencia de los pips.
    /// </summary>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Enchantment Face Card View")]
    public sealed class EnchantmentFaceCardView : MonoBehaviour
    {
        [Title("Face Card — Widget refs")]
        [Required, SerializeField] private Image _icon;
        [Required, SerializeField] private TextMeshProUGUI _numberLabel;

        public void Set(Sprite diceSprite, int face)
        {
            if (_icon != null)
            {
                _icon.sprite = diceSprite;
                _icon.enabled = diceSprite != null;
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
            }
            if (_numberLabel != null)
            {
                _numberLabel.text = face.ToString();
                _numberLabel.raycastTarget = false;
            }
        }
    }
}
