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

        /// <summary>
        /// Cuelga del costado del elemento y hacia abajo, en vez de crecer por encima. Para lo
        /// que se mira mientras se lo señala: un panel centrado sobre un enemigo lo tapa justo
        /// cuando lo estás leyendo. Si de ese lado no entra, cuelga del otro.
        /// </summary>
        Beside = 2,

        /// <summary>
        /// Panel fijo en la esquina superior derecha del canvas, ignorando el punto-pantalla
        /// del trigger. Para el panel de combate: una posición estable que el ojo aprende,
        /// en vez de un panel que salta con cada enemigo hovereado.
        /// </summary>
        ScreenTopRight = 3,
    }

    /// <summary>
    /// De qué lado del punto de anclaje se cuelga el panel. <c>Above</c> crece hacia arriba
    /// (pivot inferior-centro); <c>Below</c> coloca el panel entero DEBAJO del punto — para
    /// anclas en los pies del pawn, donde crecer hacia arriba tapa el modelo.
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
        /// Punto-pantalla donde debe caer el PIVOT del panel (inferior-centro) para que
        /// quede del lado pedido del anclaje. <c>Above</c>: pivot en anchor + offset (el
        /// panel crece hacia arriba desde ahí). <c>Below</c>: el TOPE del panel queda a
        /// <paramref name="offset"/>.y por debajo del anclaje ⇒ pivot en
        /// anchor − offset − altura del panel.
        /// </summary>
        public static Vector2 ComputeAnchorTarget(Vector2 screenPos, Vector2 offset,
            float panelScreenHeight, TooltipVerticalSide side)
        {
            if (side == TooltipVerticalSide.Below)
                return new Vector2(screenPos.x, screenPos.y - offset.y - panelScreenHeight);
            return screenPos + offset;
        }
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
        /// Punto-pantalla del BORDE SUPERIOR de un rect de UI, centrado en X. Al borde y no
        /// al centro: con el offset chico del controller, anclar al centro deja el panel
        /// montado sobre el propio elemento que lo disparó. El modo Fixed NO pasa por acá —
        /// centra su propio anchor y el offset lo autora el diseñador a mano.
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

        // Centro real del rect (no el pivot): el punto de partida del modo Fixed, cuyo
        // offset está autorado a mano y no debe moverse.
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
