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
    /// De qué lado del punto de anclaje se cuelga el panel (BUG-075). <c>Above</c> es el
    /// comportamiento histórico (pivot inferior-centro ⇒ crece hacia arriba). <c>Below</c>
    /// coloca el panel entero DEBAJO del punto — para tooltips de mundo anclados al
    /// origen del pawn (los pies), donde crecer hacia arriba tapaba el modelo.
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
        /// Punto-pantalla del BORDE SUPERIOR de un rect de UI, centrado en X (no el pivot
        /// — <c>rect.position</c> es el pivot, y triggers con pivot (0,0) o (0.5,0)
        /// (PocionSlot, botones de combate) quedaban con el tooltip descolgado hacia un
        /// costado). Usado por <see cref="Tooltips.UITooltipTrigger"/> en modo AutoFit —
        /// el único caller. Para Canvas Screen Space Overlay, el world point ya está en
        /// screen-space; para Camera/World se proyecta con la cámara del canvas.
        /// </summary>
        /// <remarks>
        /// BUG-041: antes anclaba al CENTRO del rect. Con el offset chico del
        /// <c>TooltipController</c> (12px por default) el borde inferior del panel
        /// quedaba DENTRO del elemento — el tooltip de curación tapado por el propio
        /// botón/cursor que lo disparó. Ancorar al borde superior deja el panel entero
        /// por encima del elemento antes de sumar el offset del controller.
        /// <para>
        /// El modo Fixed (<see cref="TooltipPlacementSettings.ResolveFixedScreenPos"/>) NO
        /// pasa por este método — sigue centrando su propio anchor, así que los triggers
        /// Fixed (offset autorado a mano, ej. chips de combate) no cambian.
        /// </para>
        /// </remarks>
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

        // Centro real del rect (no el pivot) — sigue siendo lo que usa el modo Fixed vía
        // ResolveFixedScreenPos: ahí el offset lo autora el diseñador a mano, así que el
        // punto de partida no debe moverse con el fix de AutoFit (BUG-041).
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
