using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Hover de una placa de la fila del pie del tooltip: abre la burbuja con el detalle
    /// del estado que la tarjeta muestra (nombre, regla, turnos restantes).
    /// </summary>
    /// <remarks>
    /// Necesita dos cables que pone el tooling: el <c>raycastTarget</c> de la placa (menú
    /// Rollgeon/Tooltips/7) y el <c>GraphicRaycaster</c> que <see cref="TooltipController"/>
    /// agrega al panel en runtime. El resto del panel sigue sin interceptar el mouse.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Tooltip Status Slot Hover")]
    [RequireComponent(typeof(TooltipCardView))]
    public sealed class TooltipStatusSlotHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private TooltipCardView _view;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_view == null) _view = GetComponent<TooltipCardView>();
            StatusHoverBubble.Show((RectTransform)transform, _view.CurrentState);
        }

        public void OnPointerExit(PointerEventData eventData) => StatusHoverBubble.Hide();

        // La columna recicla slots apagándolos, sin pointer-exit: la burbuja no puede quedar
        // colgada mostrando el estado de una placa que ya no está.
        private void OnDisable() => StatusHoverBubble.Hide();
    }
}
