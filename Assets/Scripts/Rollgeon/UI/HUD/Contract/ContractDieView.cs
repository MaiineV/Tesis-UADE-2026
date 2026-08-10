using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Un dado de la mano de ejemplo: la cara, y el marco detrás cuando ese dado es de los
    /// que forman el combo. Los que no lo forman se dibujan opacos.
    /// </summary>
    /// <remarks>
    /// El marco va DETRÁS de la cara (menor sibling index) y es un poco más grande, así el
    /// borde asoma alrededor sin taparla — es el mismo truco que el frame de los slots de
    /// turno.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Die View")]
    public class ContractDieView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private Image _face;
        [SerializeField, Required] private Image _frame;

        public void Show(Sprite face, Sprite frame, bool highlighted, float dimmedAlpha)
        {
            if (_face != null)
            {
                _face.sprite = face;
                _face.enabled = face != null;

                // Opacar en vez de teñir: el arte del dado ya tiene su color y un tint
                // multiplicativo lo ensuciaría distinto según la cara.
                var color = _face.color;
                color.a = highlighted ? 1f : Mathf.Clamp01(dimmedAlpha);
                _face.color = color;
            }

            if (_frame == null) return;
            _frame.sprite = frame;
            _frame.enabled = highlighted && frame != null;
        }
    }
}
