using System;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using UnityEngine;

namespace Rollgeon.Tutorial.UI
{
    /// <summary>
    /// Resuelve el anchor de un paso del tutorial a coordenadas de PANTALLA reales.
    /// <para>
    /// Gotcha pixel-art: la cámara renderiza a un RT chico, así que
    /// <c>WorldToScreenPoint</c> devuelve coords del RT — hay que re-escalar por
    /// <c>cam.pixelWidth/Height → Screen.width/Height</c> (mismo fix que
    /// <c>WorldTooltipTrigger.ResolveAnchorScreenPos</c> y
    /// <c>FloatingDamageSpawner.ResolveScreenPos</c>).
    /// </para>
    /// </summary>
    public static class TutorialAnchorResolver
    {
        /// <summary>
        /// Posición de pantalla del anchor. <c>false</c> = no resoluble este frame
        /// (entidad despawneada, rect destruido, anchor detrás de cámara) — el
        /// overlay cae al layout centrado sin recorte.
        /// </summary>
        public static bool TryResolve(TutorialStepDisplayRequest request, out Vector2 screenPos)
        {
            screenPos = default;
            switch (request.AnchorKind)
            {
                case TutorialAnchorKind.WorldPosition:
                    return TryWorldToScreen(request.WorldPosition, out screenPos);

                case TutorialAnchorKind.WorldEntity:
                    return TryResolveEntityWorldPos(request.EntityGuid, out var worldPos)
                           && TryWorldToScreen(worldPos, out screenPos);

                case TutorialAnchorKind.RectTransform:
                    return TryResolveRectGroup(request.UiTarget, request.UiTargetsExtra, out screenPos);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Radio del recorte para un grupo de RectTransforms del HUD: media diagonal
        /// del bounding box en pantalla + padding. Los anchors de mundo usan el
        /// default de settings.
        /// </summary>
        public static float ResolveUiCutoutRadius(RectTransform target, RectTransform[] extras, float paddingPx)
        {
            return TryResolveUiCutout(target, extras, paddingPx, out _, out _, out var radius)
                ? radius
                : paddingPx;
        }

        /// <summary>
        /// Recorte de un grupo de rects, en una sola pasada: centro, media caja (con
        /// padding) y radio del círculo circunscripto.
        /// </summary>
        /// <remarks>
        /// El recorte que dibuja el shader es circular, pero la flecha se ubica contra
        /// <paramref name="halfSize"/>: el radio lo manda la DIAGONAL, así que en un rect
        /// ancho y bajo desborda cientos de píxeles por el lado corto y la flecha
        /// terminaba flotando lejos del elemento que señala.
        /// </remarks>
        public static bool TryResolveUiCutout(
            RectTransform target, RectTransform[] extras, float paddingPx,
            out Vector2 center, out Vector2 halfSize, out float radius)
        {
            center = default;
            halfSize = default;
            radius = paddingPx;

            if (!TryGetGroupBounds(target, extras, out var min, out var max)) return false;

            center = (min + max) * 0.5f;
            halfSize = (max - min) * 0.5f + new Vector2(paddingPx, paddingPx);
            radius = Vector2.Distance(min, max) * 0.5f + paddingPx;
            return true;
        }

        private static bool TryWorldToScreen(Vector3 worldPos, out Vector2 screenPos)
        {
            screenPos = default;
            var cam = UnityEngine.Camera.main;
            if (cam == null) return false;

            var rtPos = cam.WorldToScreenPoint(worldPos);
            if (rtPos.z < 0f) return false; // detrás de la cámara

            float sx = cam.pixelWidth > 0 ? rtPos.x / cam.pixelWidth * Screen.width : rtPos.x;
            float sy = cam.pixelHeight > 0 ? rtPos.y / cam.pixelHeight * Screen.height : rtPos.y;
            screenPos = new Vector2(sx, sy);
            return true;
        }

        // Misma cadena que FloatingDamageSpawner: IEntityPositionResolver, fallback
        // IPawnRegistry (player y enemigos viven en registries distintos según quién
        // los spawneó — ambos apuntan al mismo Transform en runtime sano).
        private static bool TryResolveEntityWorldPos(Guid entityGuid, out Vector3 worldPos)
        {
            worldPos = default;
            if (entityGuid == Guid.Empty) return false;

            if (ServiceLocator.TryGetService<IEntityPositionResolver>(out var resolver) && resolver != null)
            {
                var pos = resolver.TryGetWorldPosition(entityGuid);
                if (pos.HasValue)
                {
                    worldPos = pos.Value;
                    return true;
                }
            }

            if (ServiceLocator.TryGetService<IPawnRegistry>(out var pawnRegistry) && pawnRegistry != null
                && pawnRegistry.TryGetTransform(entityGuid, out var pawnTransform) && pawnTransform != null)
            {
                worldPos = pawnTransform.position;
                return true;
            }

            return false;
        }

        // Centro del bounding box del grupo (target + extras). ScreenSpaceOverlay:
        // world == screen píxeles. (El HUD del proyecto es siempre overlay; si algún
        // canvas usara cámara habría que pasar por RectTransformUtility con esa cámara.)
        private static bool TryResolveRectGroup(
            RectTransform target, RectTransform[] extras, out Vector2 screenPos)
        {
            screenPos = default;
            if (!TryGetGroupBounds(target, extras, out var min, out var max)) return false;
            screenPos = (min + max) * 0.5f;
            return true;
        }

        // Bounding box (en píxeles de pantalla) del rect principal + los extras vivos.
        // false si NINGÚN rect es resoluble este frame.
        private static bool TryGetGroupBounds(
            RectTransform target, RectTransform[] extras, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = AccumulateRect(target, ref min, ref max);
            if (extras != null)
            {
                foreach (var extra in extras)
                    any |= AccumulateRect(extra, ref min, ref max);
            }
            return any;
        }

        private static bool AccumulateRect(RectTransform rect, ref Vector2 min, ref Vector2 max)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            min = Vector2.Min(min, Vector2.Min(corners[0], corners[2]));
            max = Vector2.Max(max, Vector2.Max(corners[0], corners[2]));
            return true;
        }
    }
}
