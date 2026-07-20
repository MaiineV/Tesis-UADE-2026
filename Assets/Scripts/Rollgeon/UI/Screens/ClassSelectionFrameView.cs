using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Marco del ícono de una entrada de clase en la selección: swapea el sprite
    /// del borde según selección (UI-Sheet-sheet_7 seleccionado, _8 deseleccionado).
    /// Lo invoca <see cref="ClassSelectionScreen"/> vía el indicador de selección
    /// (el componente vive en el mismo botón que el underline).
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Screens/Class Selection Frame View")]
    public class ClassSelectionFrameView : MonoBehaviour
    {
        [SerializeField, Required, Tooltip("Image del borde del ícono de la clase.")]
        private Image _frame;

        [SerializeField, Tooltip("UI-Sheet-sheet_7 — borde con la clase seleccionada.")]
        private Sprite _selectedSprite;

        [SerializeField, Tooltip("UI-Sheet-sheet_8 — borde con la clase deseleccionada.")]
        private Sprite _deselectedSprite;

        public void SetSelected(bool selected)
        {
            if (_frame == null) return;
            var sprite = selected ? _selectedSprite : _deselectedSprite;
            if (sprite != null) _frame.sprite = sprite;
        }
    }
}
