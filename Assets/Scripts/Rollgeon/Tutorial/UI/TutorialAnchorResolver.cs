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
                    return TryResolveRect(request.UiTarget, out screenPos);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Radio del recorte para un RectTransform del HUD: media diagonal del rect
        /// en pantalla + padding. Los anchors de mundo usan el default de settings.
        /// </summary>
        public static float ResolveUiCutoutRadius(RectTransform target, float paddingPx)
        {
            if (target == null) return paddingPx;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            // Canvas ScreenSpaceOverlay: los world corners YA están en píxeles de pantalla.
            float halfDiagonal = Vector2.Distance(corners[0], corners[2]) * 0.5f;
            return halfDiagonal + paddingPx;
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

        private static bool TryResolveRect(RectTransform target, out Vector2 screenPos)
        {
            screenPos = default;
            if (target == null || !target.gameObject.activeInHierarchy) return false;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            // ScreenSpaceOverlay: world == screen píxeles. (El HUD del proyecto es
            // siempre overlay; si algún canvas usara cámara habría que pasar por
            // RectTransformUtility.WorldToScreenPoint con esa cámara.)
            screenPos = (corners[0] + corners[2]) * 0.5f;
            return true;
        }
    }
}
