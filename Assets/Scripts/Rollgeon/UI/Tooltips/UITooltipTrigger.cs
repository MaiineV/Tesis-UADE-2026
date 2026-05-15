using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Trigger de tooltip para elementos de Canvas UI. Cuando el cursor entra al rect,
    /// muestra el tooltip <b>anclado a la posición del elemento</b> (no al cursor) —
    /// usando el centro de su <see cref="RectTransform"/>.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Tooltips/UI Tooltip Trigger")]
    public sealed class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// Provider que arma el texto on-demand cada vez que el cursor entra al elemento.
        /// Si <c>null</c> o devuelve vacío, no se muestra el tooltip al hover.
        /// </summary>
        public Func<string> TextProvider;

        private RectTransform _rect;
        private int _ownerId;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _ownerId = GetInstanceID();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TextProvider == null) TextProvider = TooltipResolver.AutoResolve(this);
            if (TextProvider == null || TooltipController.Instance == null) return;
            string text = TextProvider();
            if (string.IsNullOrEmpty(text)) return;
            TooltipController.Instance.Show(text, GetAnchorScreenPos(eventData), _ownerId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipController.Instance == null) return;
            TooltipController.Instance.Hide(_ownerId);
        }

        private void OnDisable()
        {
            if (TooltipController.Instance != null) TooltipController.Instance.Hide(_ownerId);
        }

        // Punto-pantalla del elemento UI. Para Canvas Screen Space Overlay, RectTransform.position
        // ya está en screen-space (las x/y coinciden con pixels). Para Camera/World, convertimos.
        private Vector2 GetAnchorScreenPos(PointerEventData eventData)
        {
            if (_rect == null) return eventData.position;

            var canvas = _rect.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return _rect.position;

            var cam = canvas.worldCamera != null ? canvas.worldCamera : eventData.pressEventCamera;
            return RectTransformUtility.WorldToScreenPoint(cam, _rect.position);
        }
    }
}
