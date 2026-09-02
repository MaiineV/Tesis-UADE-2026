using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Trigger de tooltip para elementos de Canvas UI: ancla al elemento, no al cursor.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Tooltips/UI Tooltip Trigger")]
    public sealed class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Texto por hover. Null al primer uso = auto-resolve de <see cref="IHasTooltipInfo"/>.</summary>
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

        private Vector2 ResolvePlacementScreenPos()
        {
            return _placement.Mode == TooltipPlacementMode.Fixed
                ? _placement.ResolveFixedScreenPos(SelfRect)
                : TooltipPlacementSettings.ScreenPosOf(SelfRect);
        }

#if UNITY_EDITOR
        [Title("Preview (solo editor)")]
        [TextArea(2, 5)]
        [Tooltip("Texto de ejemplo del botón de preview.")]
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
