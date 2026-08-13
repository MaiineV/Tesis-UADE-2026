using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.HUD.CharacterFrame
{
    /// <summary>
    /// Click izquierdo sobre el marco → alterna el pin de la ruleta. Vive en el GO del
    /// marco (no en el cluster) para que los clicks en los íconos revelados no pineen, y
    /// no es un <c>Button</c> a propósito: sus transitions pisarían la prioridad de
    /// sprites Normal/Hover/Pressed que maneja el controller.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Character Frame Pin Relay")]
    public class CharacterFramePinRelay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, Required] private CharacterFrameController _controller;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (_controller != null) _controller.TogglePin();
        }
    }
}
