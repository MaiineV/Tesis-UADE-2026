using Patterns;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.HUD.DragDrop
{
    /// <summary>
    /// Hace arrastrable un <see cref="ActionButton"/>. Espejo de Bot-Game <c>CardViewUI</c>:
    /// implementa las interfaces de drag de UGUI y delega la mecánica al
    /// <see cref="ActionDragController"/> central.
    /// </summary>
    /// <remarks>
    /// Sólo arranca el drag si el botón está <see cref="ActionButtonState.Available"/> (gate
    /// load-bearing: en Locked/Selected/Used hay otra selección/roll en curso y arrancar un
    /// drag re-entraría en el flujo de combate). El click legacy sigue funcionando: UGUI marca
    /// <c>eligibleForClick=false</c> cuando hubo un drag real (&gt; <c>pixelDragThreshold</c>),
    /// así que un tap corto dispara <c>Button.onClick</c> (select) y un drag dispara el drop.
    /// </remarks>
    [RequireComponent(typeof(ActionButton))]
    [AddComponentMenu("Rollgeon/UI/HUD/Action Drag Handle")]
    public sealed class ActionDragHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ActionButton _button;
        private ActionDragController _controller;
        private bool _dragging;

        private void Awake()
        {
            _button = GetComponent<ActionButton>();
        }

        private ActionDragController ResolveController()
        {
            // FindFirstObjectByType una vez; se cachea. Si el controller fue destruido
            // (reload de escena) el fake-null de Unity dispara un re-resolve.
            if (_controller != null) return _controller;

            if (ServiceLocator.TryGetService<ActionDragController>(out var svc) && svc != null)
                _controller = svc;
            else
                _controller = Object.FindFirstObjectByType<ActionDragController>();

            return _controller;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (_button == null) { Debug.Log("[DragDrop] OnBeginDrag: _button null — handle sin ActionButton."); return; }

            Debug.Log($"[DragDrop] OnBeginDrag slot={_button.Slot} state={_button.State} canBegin={ActionDragPolicy.CanBeginDrag(_button.State)}");
            if (!ActionDragPolicy.CanBeginDrag(_button.State)) return;

            var controller = ResolveController();
            if (controller == null) { Debug.LogWarning("[DragDrop] OnBeginDrag: no se resolvió ActionDragController."); return; }

            _dragging = controller.BeginDrag(_button, eventData);
            Debug.Log($"[DragDrop] BeginDrag devolvió dragging={_dragging}");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _controller?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            _controller?.EndDrag(eventData);
        }
    }
}
