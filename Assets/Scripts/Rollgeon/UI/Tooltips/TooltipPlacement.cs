using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>Cómo se posiciona el panel de tooltip al mostrarse.</summary>
    public enum TooltipPlacementMode
    {
        /// <summary>
        /// Ancla al elemento + offset global del controller, y después se re-posiciona
        /// lo mínimo necesario para que el panel entre COMPLETO en el canvas.
        /// </summary>
        AutoFit = 0,

        /// <summary>
        /// Posición fija relativa a un RectTransform configurado + offset X/Y. No suma
        /// el offset global de AutoFit, pero SÍ se clampea al canvas (red de seguridad
        /// de borde — la posición configurada por diseño sigue siendo la base).
        /// </summary>
        Fixed = 1,
    }

    /// <summary>
    /// Config de posicionamiento por-trigger, editable en Inspector. La comparten
    /// <see cref="UITooltipTrigger"/> y <see cref="WorldTooltipTrigger"/>.
    /// </summary>
    [Serializable]
    public class TooltipPlacementSettings
    {
        [Tooltip("AutoFit: sigue al elemento y se re-posiciona para entrar completo en " +
                 "pantalla. Fixed: posición fija relativa a un objeto de UI + offset X/Y.")]
        public TooltipPlacementMode Mode = TooltipPlacementMode.AutoFit;

        [ShowIf(nameof(IsFixed))]
        [Tooltip("Objeto de UI respecto al cual se posiciona el tooltip. " +
                 "Null = el RectTransform del propio trigger.")]
        public RectTransform FixedAnchor;

        [ShowIf(nameof(IsFixed))]
        [Tooltip("Offset X/Y relativo al anchor, en píxeles de REFERENCIA del canvas " +
                 "(ej. 1920x1080 del CanvasScaler). Se multiplica por canvas.scaleFactor, " +
                 "así que la posición relativa se mantiene en cualquier resolución.")]
        public Vector2 FixedOffset;

        private bool IsFixed => Mode == TooltipPlacementMode.Fixed;

        /// <summary>
        /// Punto-pantalla final del modo Fixed: anchor configurado (o
        /// <paramref name="fallbackAnchor"/>) + offset escalado por el scaleFactor del
        /// canvas del anchor. Como el anchor se mueve con el layout del canvas y el
        /// offset escala con la resolución, el resultado es resolución-independiente
        /// (el offset se interpreta en píxeles de referencia del CanvasScaler).
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
        /// Punto-pantalla del CENTRO de un rect de UI (no su pivot — <c>rect.position</c>
        /// es el pivot, y triggers con pivot (0,0) o (0.5,0) (PocionSlot, botones de
        /// combate) quedaban con el tooltip descolgado hacia un costado). Para Canvas
        /// Screen Space Overlay, el world point ya está en screen-space; para Camera/World
        /// se proyecta con la cámara del canvas.
        /// </summary>
        public static Vector2 ScreenPosOf(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            return ScreenPosOf(rect, rect.GetComponentInParent<Canvas>());
        }

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
