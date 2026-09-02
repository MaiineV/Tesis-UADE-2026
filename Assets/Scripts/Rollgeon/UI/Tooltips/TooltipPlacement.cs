using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>Cómo se posiciona el panel de tooltip al mostrarse.</summary>
    public enum TooltipPlacementMode
    {
        /// <summary>Ancla al elemento + offset global, corrido lo mínimo para entrar en pantalla.</summary>
        AutoFit = 0,

        /// <summary>Anchor de UI configurado + offset. Igual se clampea a pantalla.</summary>
        Fixed = 1,

        /// <summary>Cuelga del costado para no tapar lo que se mira; si no entra, del otro lado.</summary>
        Beside = 2,

        /// <summary>Fijo en la esquina superior derecha: una posición estable que el ojo aprende.</summary>
        ScreenTopRight = 3,
    }

    /// <summary>
    /// <c>Below</c> cuelga el panel entero DEBAJO del punto — para anclas en los pies del
    /// pawn, donde crecer hacia arriba tapa el modelo.
    /// </summary>
    public enum TooltipVerticalSide
    {
        Above = 0,
        Below = 1,
    }

    /// <summary>Matemática pura del anclaje vertical — testeable sin canvas.</summary>
    public static class TooltipVerticalPlacement
    {
        /// <summary>
        /// Dónde cae el pivot (inferior-centro). Below: el tope del panel queda offset.y por
        /// debajo del anclaje.
        /// </summary>
        public static Vector2 ComputeAnchorTarget(Vector2 screenPos, Vector2 offset,
            float panelScreenHeight, TooltipVerticalSide side)
        {
            if (side == TooltipVerticalSide.Below)
                return new Vector2(screenPos.x, screenPos.y - offset.y - panelScreenHeight);
            return screenPos + offset;
        }
    }

    /// <summary>Config de posicionamiento por-trigger, compartida por los dos triggers.</summary>
    [Serializable]
    public class TooltipPlacementSettings
    {
        [Tooltip("AutoFit sigue al elemento; Fixed usa un anchor de UI + offset.")]
        public TooltipPlacementMode Mode = TooltipPlacementMode.AutoFit;

        [ShowIf(nameof(IsFixed))]
        [Tooltip("Anchor de UI. Null = el rect del propio trigger.")]
        public RectTransform FixedAnchor;

        [ShowIf(nameof(IsFixed))]
        [Tooltip("Offset en píxeles de referencia del canvas: se escala con la resolución.")]
        public Vector2 FixedOffset;

        private bool IsFixed => Mode == TooltipPlacementMode.Fixed;

        /// <summary>
        /// Anchor (o <paramref name="fallbackAnchor"/>) + offset escalado por el scaleFactor:
        /// resolución-independiente.
        /// </summary>
        public Vector2 ResolveFixedScreenPos(RectTransform fallbackAnchor)
        {
            var anchor = FixedAnchor != null ? FixedAnchor : fallbackAnchor;
            if (anchor == null) return FixedOffset;

            var canvas = anchor.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            return ScreenPosOf(anchor, canvas) + FixedOffset * scale;
        }

        /// <summary>
        /// Borde SUPERIOR del rect, centrado en X: anclado al centro, el panel quedaba montado
        /// sobre el propio elemento. Fixed NO pasa por acá.
        /// </summary>
        public static Vector2 ScreenPosOf(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            return ScreenTopEdgeOf(rect, rect.GetComponentInParent<Canvas>());
        }

        private static Vector2 ScreenTopEdgeOf(RectTransform rect, Canvas canvas)
        {
            if (rect == null) return Vector2.zero;
            var r = rect.rect;
            Vector3 worldTopEdge = rect.TransformPoint(new Vector3(r.center.x, r.yMax, 0f));
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return worldTopEdge;
            return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldTopEdge);
        }

        // Centro real del rect: el punto de partida de Fixed, autorado a mano, no se mueve.
        private static Vector2 ScreenPosOf(RectTransform rect, Canvas canvas)
        {
            if (rect == null) return Vector2.zero;
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return worldCenter;
            return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldCenter);
        }
    }
}
