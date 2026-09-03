using Rollgeon.UI.HUD.Status;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// La burbuja de detalle de un ícono de la fila del pie del tooltip: nombre, regla y
    /// turnos restantes del estado (<see cref="StatusTooltipText.Build"/>). Una sola por
    /// proceso, creada por código — es información, no arte.
    /// </summary>
    /// <remarks>
    /// No reusa el panel de <see cref="TooltipController"/>: reemplazar el contenido del
    /// panel fijado con el mouse adentro dispara pointer-exit → restore → re-enter en loop.
    /// La burbuja es un objeto aparte con su propio canvas, un paso por encima del panel.
    /// </remarks>
    public sealed class StatusHoverBubble : MonoBehaviour
    {
        private const float TextWidth = 260f;
        private const float Gap = 6f;
        private const float FontSize = 24f;
        private static readonly Color Plate = new Color(0.10f, 0.07f, 0.05f, 0.95f);
        private static readonly Color Ink = new Color(0.94f, 0.90f, 0.82f);

        private static StatusHoverBubble s_instance;

        private TextMeshProUGUI _label;
        private RectTransform _rect;

        public static void Show(RectTransform anchor, in StatusIconState state)
        {
            if (anchor == null) return;

            string text = StatusTooltipText.Build(state);
            if (string.IsNullOrEmpty(text)) return;

            var bubble = Resolve(anchor);
            if (bubble == null) return;

            bubble._label.text = text;
            bubble.gameObject.SetActive(true);
            bubble.PlaceAbove(anchor);
        }

        public static void Hide()
        {
            if (s_instance != null) s_instance.gameObject.SetActive(false);
        }

        private static StatusHoverBubble Resolve(RectTransform anchor)
        {
            if (s_instance != null) return s_instance;

            var canvas = anchor.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("StatusHoverBubble", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.rootCanvas.transform, worldPositionStays: false);
            // Apoyada sobre el ancla: el pivot inferior-centro hace que crecer no la baje.
            rect.pivot = new Vector2(0.5f, 0f);

            var plate = go.AddComponent<Image>();
            plate.color = Plate;
            plate.raycastTarget = false;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = FontSize;
            label.color = Ink;
            label.raycastTarget = false;
            labelGo.AddComponent<LayoutElement>().preferredWidth = TextWidth;

            // Mismo mecanismo que el panel: overrideSorting + un paso más arriba, así la
            // burbuja nunca queda debajo de la caja que la disparó.
            var bubbleCanvas = go.AddComponent<Canvas>();
            bubbleCanvas.overrideSorting = true;
            bubbleCanvas.sortingOrder = TooltipController.OverlaySortingOrder + 1;

            s_instance = go.AddComponent<StatusHoverBubble>();
            s_instance._label = label;
            s_instance._rect = rect;
            return s_instance;
        }

        private void PlaceAbove(RectTransform anchor)
        {
            _rect.position = anchor.TransformPoint(
                new Vector3(anchor.rect.center.x, anchor.rect.yMax + Gap, 0f));

            // Clamp horizontal contra el canvas: la fila vive pegada al borde derecho de la
            // pantalla y una burbuja centrada se saldría.
            if (_rect.parent is not RectTransform canvasRect) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);

            var local = _rect.localPosition;
            float half = _rect.rect.width * 0.5f;
            float xMin = canvasRect.rect.xMin + half;
            float xMax = canvasRect.rect.xMax - half;
            if (xMin < xMax) local.x = Mathf.Clamp(local.x, xMin, xMax);
            _rect.localPosition = local;
        }
    }
}
