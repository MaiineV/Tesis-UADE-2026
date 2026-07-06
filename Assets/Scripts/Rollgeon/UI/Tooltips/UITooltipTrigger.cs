using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Trigger de tooltip para elementos de Canvas UI. Cuando el cursor entra al rect,
    /// muestra el tooltip <b>anclado a la posición del elemento</b> (no al cursor) —
    /// usando el centro de su <see cref="RectTransform"/>.
    /// El posicionamiento es configurable por trigger via <see cref="TooltipPlacementSettings"/>:
    /// AutoFit re-posiciona para entrar completo en pantalla; Fixed ancla a un
    /// RectTransform configurado + offset X/Y.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Tooltips/UI Tooltip Trigger")]
    public sealed class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// Provider que arma el texto on-demand cada vez que el cursor entra al elemento.
        /// Si <c>null</c> o devuelve vacío, no se muestra el tooltip al hover.
        /// </summary>
        public Func<string> TextProvider;

        [SerializeField] private TooltipPlacementSettings _placement = new TooltipPlacementSettings();

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
            TooltipController.Instance.Show(text, ResolvePlacementScreenPos(), _ownerId, _placement.Mode);
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

        private RectTransform SelfRect => _rect != null ? _rect : transform as RectTransform;

        // Punto-pantalla final según el modo: AutoFit ancla al propio elemento (el
        // controller suma su offset global y clampea); Fixed ancla al RectTransform
        // configurado + offset X/Y exactos.
        private Vector2 ResolvePlacementScreenPos()
        {
            if (_placement.Mode == TooltipPlacementMode.Fixed)
            {
                var anchor = _placement.FixedAnchor != null ? _placement.FixedAnchor : SelfRect;
                var canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                return ScreenPosOf(anchor) + _placement.FixedOffset * scale;
            }

            return ScreenPosOf(SelfRect);
        }

        // Punto-pantalla de un rect de UI. Para Canvas Screen Space Overlay,
        // RectTransform.position ya está en screen-space (las x/y coinciden con pixels).
        // Para Camera/World, convertimos con la cámara del canvas.
        private static Vector2 ScreenPosOf(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;

            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return rect.position;

            return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.position);
        }

#if UNITY_EDITOR
        [Title("Preview (solo editor)")]
        [TextArea(2, 5)]
        [Tooltip("Texto de ejemplo usado por el botón de preview — elegí uno del largo " +
                 "real esperado para ver cuánto espacio ocupa el panel.")]
        [SerializeField] private string _previewText =
            "<b>Acción</b>\nCosto: 2 de energía\nDaño: ATQ (3) + puntaje del combo";

        [Button("Mostrar preview en Game view")]
        private void ShowEditorPreview()
        {
            var controller = FindFirstObjectByType<TooltipController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogWarning("[UITooltipTrigger] No hay TooltipController en la escena.", this);
                return;
            }
            controller.EditorPreview(_previewText, ResolvePlacementScreenPos(), _placement.Mode);
        }

        [Button("Ocultar preview")]
        private void HideEditorPreview()
        {
            var controller = FindFirstObjectByType<TooltipController>(FindObjectsInactive.Include);
            if (controller != null) controller.EditorPreviewHide();
        }
#endif
    }
}
